#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductLocalizationTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private readonly List<Font> createdFonts = new List<Font>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
            }
            createdObjects.Clear();
            for (int index = createdFonts.Count - 1; index >= 0; index--)
            {
                if (createdFonts[index] != null)
                    UnityEngine.Object.DestroyImmediate(createdFonts[index]);
            }
            createdFonts.Clear();
        }

        [Test]
        public void CatalogHasStableUniqueKeysAndPairedEnglishJapaneseTemplates()
        {
            Assert.That(ProductTextCatalog.TryValidate(out string error), Is.True, error);
            Assert.That(ProductTextCatalog.All, Is.Not.Empty);
            Assert.That(ProductTextCatalog.Keys,
                Is.EqualTo(ProductTextCatalog.Keys.OrderBy(
                    key => key, StringComparer.Ordinal).ToArray()));
            Assert.That(ProductTextCatalog.Keys.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(ProductTextCatalog.All.Count));

            IProductText english = ProductTextCatalog.English;
            IProductText japanese = ProductTextCatalog.ForLocale(
                ProductTextCatalog.JapaneseLocale);
            Assert.That(english.RequestedLocale,
                Is.EqualTo(ProductTextCatalog.EnglishLocale));
            Assert.That(japanese.EffectiveLocale,
                Is.EqualTo(ProductTextCatalog.JapaneseLocale));

            foreach (ProductTextEntry entry in ProductTextCatalog.All)
            {
                object[] arguments = Enumerable.Range(0, entry.ArgumentCount)
                    .Select(index => (object)("ARG" + index)).ToArray();
                Assert.That(entry.PlaceholderIndexes,
                    Is.EqualTo(entry.PlaceholderIndexes.OrderBy(index => index).ToArray()),
                    entry.Key);
                string englishValue = english.Get(entry.Key, arguments);
                Assert.That(englishValue, Is.Not.Empty, entry.Key);
                Assert.That(englishValue.All(character => character <= 0x7f), Is.True,
                    entry.Key + " must keep an ASCII last-resort rendering path.");
                Assert.That(japanese.Get(entry.Key, arguments), Is.Not.Empty, entry.Key);
                Assert.That(englishValue, Is.Not.EqualTo(entry.Key),
                    entry.Key);
            }
        }

        [Test]
        public void EntryRejectsUnstableKeysPlaceholderDriftAndUnsupportedArguments()
        {
            Assert.Throws<ArgumentException>(() =>
                new ProductTextEntry("Bad Key", "Valid", "有効"));
            Assert.Throws<ArgumentException>(() =>
                new ProductTextEntry("test.placeholder", "{0} {1}", "{0}"));
            Assert.Throws<ArgumentException>(() =>
                new ProductTextEntry("test.repeated", "{0} {0}", "{0}"));
            Assert.Throws<ArgumentException>(() =>
                new ProductTextEntry("test.gap", "{1}", "{1}"));
            Assert.Throws<KeyNotFoundException>(() =>
                ProductTextCatalog.English.Get("test.unknown"));
            Assert.Throws<ArgumentException>(() =>
                ProductTextCatalog.English.Get("result.score_you", new object()));
        }

        [Test]
        public void FormattingUsesInvariantCultureForTypedArguments()
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");

                Assert.That(ProductTextCatalog.English.Get("result.score_you", 12.5m),
                    Is.EqualTo("You: 12.5"));
                Assert.That(ProductTextCatalog.English.Get(
                    "settings.master_volume_value", 80), Is.EqualTo("Master 80%"));
                Assert.That(ProductTextCatalog.English.Get("library.slot_option",
                    new DateTime(2026, 8, 16, 9, 5, 0, DateTimeKind.Utc), "abc12345"),
                    Is.EqualTo("2026-08-16 09:05 UTC  -  abc12345"));
                foreach (string key in new[]
                {
                    "settings.error_save_failed",
                    "settings.error_defaults_failed",
                    "settings.error_input_apply_failed",
                    "settings.error_rebind_start_failed"
                })
                {
                    Assert.That(ProductTextCatalog.Entry(key).ArgumentCount, Is.Zero, key);
                    Assert.That(ProductTextCatalog.English.Get(key), Is.Not.Empty, key);
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        [Test]
        public void TextElementKeepsStableContractAndScalesFromImmutableBase()
        {
            Font font = CreateFont();
            ProductTextElement staticElement = CreateTextElement(
                "Static", "common.apply", ProductTextContentMode.Static, 20);

            staticElement.Apply(ProductTextCatalog.English, font, 125);
            Assert.That(staticElement.Target.text, Is.EqualTo("Apply"));
            Assert.That(staticElement.Target.fontSize, Is.EqualTo(25));
            staticElement.Apply(ProductTextCatalog.ForLocale(
                ProductTextCatalog.JapaneseLocale), font, 150);
            Assert.That(staticElement.Target.text, Is.EqualTo("適用"));
            Assert.That(staticElement.Target.fontSize, Is.EqualTo(30),
                "150% must be calculated from the immutable 20 point base, not 25.");
            Assert.That(staticElement.BaseFontSize, Is.EqualTo(20));
            Assert.Throws<InvalidOperationException>(() =>
                staticElement.Configure(ProductTextContentMode.Static, "common.cancel", 20));
            Assert.Throws<InvalidOperationException>(() =>
                staticElement.Configure(ProductTextContentMode.Static, "common.apply", 21));

            ProductTextElement dynamicElement = CreateTextElement(
                "Dynamic", "match.status", ProductTextContentMode.Dynamic, 18);
            dynamicElement.Target.text = "Runtime-owned value";
            dynamicElement.Apply(ProductTextCatalog.ForLocale(
                ProductTextCatalog.JapaneseLocale), font, 100);
            Assert.That(dynamicElement.Target.text, Is.EqualTo("Runtime-owned value"));

            ProductTextElement neutralElement = CreateTextElement(
                "Neutral", "card.symbol", ProductTextContentMode.LocaleNeutral, 16);
            neutralElement.Target.text = "8H";
            neutralElement.Apply(ProductTextCatalog.English, font, 125);
            Assert.That(neutralElement.Target.text, Is.EqualTo("8H"));
            Assert.That(neutralElement.Target.fontSize, Is.EqualTo(20));
        }

        [Test]
        public void ControllerUsesJapanesePriorityAndUpdatesInactiveElements()
        {
            Assert.That(ProductLocalizationController.JapaneseFontCandidates,
                Is.EqualTo(new[] { "Yu Gothic UI", "Meiryo UI", "Yu Gothic", "Meiryo" }));
            Font fallback = CreateFont();
            GameObject uiRoot = Create("UiRoot", typeof(RectTransform));
            ProductTextElement element = CreateTextElement(
                "InactiveLabel", "common.apply", ProductTextContentMode.Static, 20);
            element.transform.SetParent(uiRoot.transform, false);
            element.gameObject.SetActive(false);
            ProductLocalizationController controller = CreateController(uiRoot, fallback);
            var host = new RecordingFontHost(fallback,
                new[] { "Meiryo UI", "Yu Gothic UI", "Segoe UI" },
                new[] { "Meiryo UI", "Segoe UI" });
            controller.SetFontHost(host);

            ProductSettings requested = ProductSettings.CreateDefaults("ja-JP")
                .WithTextScalePercent(125);
            controller.Apply(requested);

            Assert.That(controller.RequestedLocale,
                Is.EqualTo(ProductTextCatalog.JapaneseLocale));
            Assert.That(controller.EffectiveLocale,
                Is.EqualTo(ProductTextCatalog.JapaneseLocale));
            Assert.That(controller.EffectiveFont, Is.SameAs(fallback));
            Assert.That(controller.HasCompleteGlyphCoverage, Is.True);
            Assert.That(controller.LastWarning, Is.Null);
            Assert.That(host.CreatedNames,
                Is.EqualTo(new[] { "Yu Gothic UI", "Meiryo UI" }));
            Assert.That(host.ProbedCharacters[0],
                Does.Contain(ProductTextCatalog.English.Get("common.apply")[0].ToString()));
            Assert.That(host.ProbedCharacters[0],
                Does.Contain(ProductTextCatalog.ForLocale(
                    ProductTextCatalog.JapaneseLocale).Get("common.apply")[0].ToString()));
            AssertFeedbackSymbolsWereProbed(host.ProbedCharacters[0]);
            Assert.That(element.Target.text, Is.EqualTo("適用"));
            Assert.That(element.Target.fontSize, Is.EqualTo(25));
        }

        [Test]
        public void MissingJapaneseGlyphsFallBackToEnglishWithoutChangingRequest()
        {
            Font fallback = CreateFont();
            GameObject uiRoot = Create("UiRoot", typeof(RectTransform));
            ProductTextElement element = CreateTextElement(
                "InactiveLabel", "common.apply", ProductTextContentMode.Static, 20);
            element.transform.SetParent(uiRoot.transform, false);
            element.gameObject.SetActive(false);
            ProductLocalizationController controller = CreateController(uiRoot, fallback);
            var host = new RecordingFontHost(fallback,
                ProductLocalizationController.JapaneseFontCandidates
                    .Concat(new[] { "Segoe UI" }).ToArray(),
                new[] { "Segoe UI" });
            controller.SetFontHost(host);

            ProductSettings requested = ProductSettings.CreateDefaults("ja-JP")
                .WithTextScalePercent(150);
            Assert.DoesNotThrow(() => controller.Apply(requested));

            Assert.That(requested.Locale, Is.EqualTo(ProductTextCatalog.JapaneseLocale),
                "Presentation fallback must not rewrite persisted settings.");
            Assert.That(controller.RequestedLocale,
                Is.EqualTo(ProductTextCatalog.JapaneseLocale));
            Assert.That(controller.EffectiveLocale,
                Is.EqualTo(ProductTextCatalog.EnglishLocale));
            Assert.That(controller.HasCompleteGlyphCoverage, Is.True);
            Assert.That(controller.LastWarning,
                Is.EqualTo(ProductTextCatalog.English.Get(
                    "warning.font.japanese_fallback")));
            Assert.That(element.Target.text, Is.EqualTo("Apply"));
            Assert.That(element.Target.fontSize, Is.EqualTo(30));

            controller.Apply(requested.WithTextScalePercent(100));
            Assert.That(element.Target.fontSize, Is.EqualTo(20),
                "Repeated application must always scale from the base size.");
        }

        [Test]
        public void MissingOsFontCandidatesUseConfiguredEnglishFallbackWithoutThrowing()
        {
            Font fallback = CreateFont();
            GameObject uiRoot = Create("UiRoot", typeof(RectTransform));
            ProductLocalizationController controller = CreateController(uiRoot, fallback);
            var host = new RecordingFontHost(fallback, Array.Empty<string>(),
                Array.Empty<string>());
            controller.SetFontHost(host);

            Assert.DoesNotThrow(() => controller.Apply(
                ProductSettings.CreateDefaults("ja-JP")));

            Assert.That(controller.RequestedLocale,
                Is.EqualTo(ProductTextCatalog.JapaneseLocale));
            Assert.That(controller.EffectiveLocale,
                Is.EqualTo(ProductTextCatalog.EnglishLocale));
            Assert.That(controller.EffectiveFont, Is.SameAs(fallback));
            Assert.That(controller.HasCompleteGlyphCoverage, Is.False);
            Assert.That(controller.LastWarning, Is.Not.Empty);
            Assert.That(host.CreatedNames, Is.Empty);
            Assert.That(host.ProbedCharacters, Has.Count.EqualTo(1));
            Assert.That(host.ProbedFonts[0], Is.SameAs(fallback));
            foreach (char character in ProductTextCatalog.RequiredCharacters(
                ProductTextCatalog.EnglishLocale))
                Assert.That(host.ProbedCharacters[0], Does.Contain(character.ToString()));
            AssertFeedbackSymbolsWereProbed(host.ProbedCharacters[0]);
        }

        [Test]
        public void CompleteConfiguredFallbackIsProbedAndAcceptedWithoutWarning()
        {
            Font fallback = CreateFont();
            GameObject uiRoot = Create("UiRoot", typeof(RectTransform));
            ProductLocalizationController controller = CreateController(uiRoot, fallback);
            var host = new RecordingFontHost(fallback, Array.Empty<string>(),
                Array.Empty<string>(), fallbackSupported: true);
            controller.SetFontHost(host);

            Assert.DoesNotThrow(() => controller.Apply(
                ProductSettings.CreateDefaults("en-US")));

            Assert.That(controller.RequestedLocale,
                Is.EqualTo(ProductTextCatalog.EnglishLocale));
            Assert.That(controller.EffectiveLocale,
                Is.EqualTo(ProductTextCatalog.EnglishLocale));
            Assert.That(controller.EffectiveFont, Is.SameAs(fallback));
            Assert.That(controller.HasCompleteGlyphCoverage, Is.True);
            Assert.That(controller.LastWarning, Is.Null);
            Assert.That(host.CreatedNames, Is.Empty);
            Assert.That(host.ProbedCharacters, Has.Count.EqualTo(1));
            Assert.That(host.ProbedFonts[0], Is.SameAs(fallback));
            foreach (char character in ProductTextCatalog.RequiredCharacters(
                ProductTextCatalog.EnglishLocale))
                Assert.That(host.ProbedCharacters[0], Does.Contain(character.ToString()));
            AssertFeedbackSymbolsWereProbed(host.ProbedCharacters[0]);
        }

        [Test]
        public void IncompleteConfiguredFallbackReportsAsciiWarningWithoutThrowing()
        {
            Font fallback = CreateFont();
            GameObject uiRoot = Create("UiRoot", typeof(RectTransform));
            ProductLocalizationController controller = CreateController(uiRoot, fallback);
            var host = new RecordingFontHost(fallback, Array.Empty<string>(),
                Array.Empty<string>(), fallbackSupported: false);
            controller.SetFontHost(host);

            Assert.DoesNotThrow(() => controller.Apply(
                ProductSettings.CreateDefaults("en-US")));

            Assert.That(controller.RequestedLocale,
                Is.EqualTo(ProductTextCatalog.EnglishLocale));
            Assert.That(controller.EffectiveLocale,
                Is.EqualTo(ProductTextCatalog.EnglishLocale));
            Assert.That(controller.EffectiveFont, Is.SameAs(fallback));
            Assert.That(controller.HasCompleteGlyphCoverage, Is.False);
            Assert.That(controller.LastWarning,
                Is.EqualTo(ProductTextCatalog.English.Get(
                    "warning.font.english_incomplete")));
            Assert.That(controller.LastWarning!.All(character => character <= 0x7f), Is.True,
                "The last-resort warning must remain renderable by an ASCII-only font.");
            Assert.That(host.CreatedNames, Is.Empty);
            Assert.That(host.ProbedCharacters, Has.Count.EqualTo(1));
            Assert.That(host.ProbedFonts[0], Is.SameAs(fallback));
            foreach (char character in ProductTextCatalog.RequiredCharacters(
                ProductTextCatalog.EnglishLocale))
                Assert.That(host.ProbedCharacters[0], Does.Contain(character.ToString()));
            AssertFeedbackSymbolsWereProbed(host.ProbedCharacters[0]);
        }

        private static void AssertFeedbackSymbolsWereProbed(string probedCharacters)
        {
            foreach (ProductFeedbackPresentation feedback in ProductPresentationCatalog.All)
            foreach (char character in feedback.Symbol)
                Assert.That(probedCharacters, Does.Contain(character.ToString()),
                    feedback.Kind.ToString());
        }

        private ProductLocalizationController CreateController(GameObject uiRoot, Font font)
        {
            GameObject controllerObject = Create("LocalizationController");
            ProductLocalizationController controller =
                controllerObject.AddComponent<ProductLocalizationController>();
            controller.Configure(uiRoot.transform, font);
            return controller;
        }

        private ProductTextElement CreateTextElement(string name, string key,
            ProductTextContentMode mode, int baseFontSize)
        {
            GameObject root = Create(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            Text target = root.GetComponent<Text>();
            target.fontSize = baseFontSize;
            ProductTextElement element = root.AddComponent<ProductTextElement>();
            element.Configure(target, mode, key, baseFontSize);
            return element;
        }

        private Font CreateFont()
        {
            var font = new Font();
            createdFonts.Add(font);
            return font;
        }

        private GameObject Create(string name, params Type[] components)
        {
            var root = new GameObject(name, components);
            createdObjects.Add(root);
            return root;
        }

        private sealed class RecordingFontHost : IProductFontHost
        {
            private readonly Font font;
            private readonly IReadOnlyList<string> installed;
            private readonly HashSet<string> supported;
            private readonly bool fallbackSupported;
            private string currentName = string.Empty;

            public RecordingFontHost(Font configuredFont, IEnumerable<string> installedNames,
                IEnumerable<string> supportedNames, bool fallbackSupported = false)
            {
                font = configuredFont;
                installed = installedNames.ToArray();
                supported = new HashSet<string>(supportedNames,
                    StringComparer.OrdinalIgnoreCase);
                this.fallbackSupported = fallbackSupported;
            }

            public List<string> CreatedNames { get; } = new List<string>();
            public List<string> ProbedCharacters { get; } = new List<string>();
            public List<Font> ProbedFonts { get; } = new List<Font>();

            public IReadOnlyList<string> GetInstalledFontNames() => installed;

            public Font CreateDynamicFont(string fontName, int fontSize)
            {
                currentName = fontName;
                CreatedNames.Add(fontName);
                return font;
            }

            public bool HasCharacters(Font configuredFont, string characters, int fontSize)
            {
                ProbedFonts.Add(configuredFont);
                ProbedCharacters.Add(characters);
                return string.IsNullOrEmpty(currentName)
                    ? fallbackSupported
                    : supported.Contains(currentName);
            }
        }
    }
}
