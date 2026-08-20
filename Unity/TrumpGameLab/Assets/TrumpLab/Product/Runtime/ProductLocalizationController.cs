#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TrumpLab.Product
{
    [DisallowMultipleComponent]
    public sealed class ProductLocalizationController : MonoBehaviour, IProductText,
        IProductSettingsApplier
    {
        public const int ProbeFontSize = 32;

        public static IReadOnlyList<string> JapaneseFontCandidates { get; } =
            Array.AsReadOnly(new[] { "Yu Gothic UI", "Meiryo UI", "Yu Gothic", "Meiryo" });
        public static IReadOnlyList<string> EnglishFontCandidates { get; } =
            Array.AsReadOnly(new[] { "Segoe UI", "Arial" });

        [SerializeField] private Transform? uiRoot;
        [SerializeField] private Font? fallbackFont;

        private readonly Dictionary<string, Font> fontCache =
            new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Font> ownedFonts = new HashSet<Font>();
        private IProductFontHost? fontHost;
        private Font? effectiveFont;
        private string selectedForRequestedLocale = string.Empty;

        public Transform UiRoot => uiRoot != null ? uiRoot :
            throw new InvalidOperationException("Product localization UI root is not configured.");
        public Font FallbackFont => fallbackFont != null ? fallbackFont :
            throw new InvalidOperationException("Product localization fallback font is not configured.");
        public string RequestedLocale { get; private set; } = ProductTextCatalog.EnglishLocale;
        public string EffectiveLocale { get; private set; } = ProductTextCatalog.EnglishLocale;
        public Font EffectiveFont => effectiveFont != null ? effectiveFont : FallbackFont;
        public bool HasCompleteGlyphCoverage { get; private set; }
        public string? LastWarning { get; private set; }
        public IProductText Text => this;

        public void Configure(Transform configuredUiRoot, Font configuredFallbackFont)
        {
            uiRoot = configuredUiRoot ?? throw new ArgumentNullException(nameof(configuredUiRoot));
            fallbackFont = configuredFallbackFont ??
                throw new ArgumentNullException(nameof(configuredFallbackFont));
            effectiveFont = configuredFallbackFont;
            selectedForRequestedLocale = string.Empty;
            HasCompleteGlyphCoverage = false;
        }

        public void SetFontHost(IProductFontHost configuredHost)
        {
            fontHost = configuredHost ?? throw new ArgumentNullException(nameof(configuredHost));
            selectedForRequestedLocale = string.Empty;
            fontCache.Clear();
            ownedFonts.Clear();
            effectiveFont = fallbackFont;
            HasCompleteGlyphCoverage = false;
        }

        public void Apply(ProductSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            ValidateConfiguration();
            RequestedLocale = settings.Locale;
            if (!string.Equals(selectedForRequestedLocale, RequestedLocale,
                    StringComparison.Ordinal))
            {
                SelectEffectiveFontAndLocale();
                selectedForRequestedLocale = RequestedLocale;
            }

            foreach (ProductTextElement element in
                UiRoot.GetComponentsInChildren<ProductTextElement>(includeInactive: true))
                element.Apply(this, EffectiveFont, settings.TextScalePercent);
        }

        public string Get(string key, params object[] args) =>
            ProductTextCatalog.Entry(key).Format(
                EffectiveLocale, args ?? Array.Empty<object>());

        private void Awake()
        {
            fontHost ??= new UnityProductFontHost();
            if (fallbackFont != null) effectiveFont = fallbackFont;
        }

        private void OnDestroy()
        {
            foreach (Font font in ownedFonts)
            {
                if (font == null || font == fallbackFont) continue;
                if (Application.isPlaying) Destroy(font);
                else DestroyImmediate(font);
            }
            ownedFonts.Clear();
            fontCache.Clear();
        }

        private void SelectEffectiveFontAndLocale()
        {
            LastWarning = null;
            HasCompleteGlyphCoverage = false;
            if (RequestedLocale == ProductTextCatalog.JapaneseLocale &&
                TrySelectFont(JapaneseFontCandidates,
                    RequiredCharacters(includeJapanese: true),
                    out Font? japaneseFont))
            {
                EffectiveLocale = ProductTextCatalog.JapaneseLocale;
                effectiveFont = japaneseFont;
                HasCompleteGlyphCoverage = true;
                return;
            }

            EffectiveLocale = ProductTextCatalog.EnglishLocale;
            string englishCharacters = RequiredCharacters(includeJapanese: false);
            if (!TrySelectFont(EnglishFontCandidates,
                    englishCharacters,
                    out Font? englishFont))
            {
                englishFont = FallbackFont;
                HasCompleteGlyphCoverage = HasCharacters(
                    englishFont, englishCharacters);
            }
            else
            {
                HasCompleteGlyphCoverage = true;
            }
            effectiveFont = englishFont;
            if (RequestedLocale == ProductTextCatalog.JapaneseLocale)
                LastWarning = ProductTextCatalog.English.Get(
                    "warning.font.japanese_fallback");
            else if (!HasCompleteGlyphCoverage)
                LastWarning = ProductTextCatalog.English.Get(
                    "warning.font.english_incomplete");
        }

        private bool HasCharacters(Font font, string characters)
        {
            try
            {
                IProductFontHost host = fontHost ??= new UnityProductFontHost();
                return host.HasCharacters(font, characters, ProbeFontSize);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string RequiredCharacters(bool includeJapanese)
        {
            string catalogCharacters = includeJapanese
                ? ProductTextCatalog.RequiredCharactersForAllLocales()
                : ProductTextCatalog.RequiredCharacters(ProductTextCatalog.EnglishLocale);
            var characters = new SortedSet<char>(catalogCharacters);
            foreach (ProductFeedbackPresentation feedback in ProductPresentationCatalog.All)
                characters.UnionWith(feedback.Symbol);
            return new string(characters.ToArray());
        }

        private bool TrySelectFont(IReadOnlyList<string> candidates, string characters,
            out Font? selected)
        {
            selected = null;
            IProductFontHost host = fontHost ??= new UnityProductFontHost();
            IReadOnlyList<string> installed;
            try
            {
                installed = host.GetInstalledFontNames() ?? Array.Empty<string>();
            }
            catch (Exception)
            {
                return false;
            }
            var available = new HashSet<string>(installed, StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in candidates)
            {
                if (!available.Contains(candidate)) continue;
                Font? font;
                try
                {
                    if (!fontCache.TryGetValue(candidate, out font) || font == null)
                    {
                        font = host.CreateDynamicFont(candidate, ProbeFontSize);
                        if (font != null)
                        {
                            fontCache[candidate] = font;
                            if (font != fallbackFont) ownedFonts.Add(font);
                        }
                    }
                    if (font != null && host.HasCharacters(font, characters, ProbeFontSize))
                    {
                        selected = font;
                        return true;
                    }
                }
                catch (Exception)
                {
                    // A missing or unusable OS font is a recoverable localization condition.
                }
            }
            return false;
        }

        private void ValidateConfiguration()
        {
            if (uiRoot == null || fallbackFont == null)
                throw new InvalidOperationException(
                    "Product localization controller is not configured.");
            fontHost ??= new UnityProductFontHost();
        }
    }
}
