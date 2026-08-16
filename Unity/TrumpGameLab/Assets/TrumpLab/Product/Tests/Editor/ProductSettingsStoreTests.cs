#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductSettingsStoreTests
    {
        private string temporaryRoot = null!;
        private FileProductSettingsStore store = null!;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(),
                "TrumpLab-M06-Settings-" + Guid.NewGuid().ToString("N"));
            store = new FileProductSettingsStore(temporaryRoot, "en-US");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryRoot))
                Directory.Delete(temporaryRoot, recursive: true);
        }

        [Test]
        public void DefaultsFollowQualityBaselineAndUiCulture()
        {
            ProductSettings english = ProductSettings.CreateDefaults("en-US");
            ProductSettings japanese = ProductSettings.CreateDefaults("ja-JP");

            AssertAll(() =>
            {
                Assert.That(english.FormatVersion,
                    Is.EqualTo(ProductSettings.CurrentFormatVersion));
                Assert.That(english.DisplayMode, Is.EqualTo(ProductDisplayMode.Windowed));
                Assert.That(english.Resolution, Is.EqualTo(new ProductResolution(1280, 720)));
                Assert.That(english.VSyncEnabled, Is.True);
                Assert.That(english.MasterVolume, Is.EqualTo(80));
                Assert.That(english.MusicVolume, Is.EqualTo(60));
                Assert.That(english.SfxVolume, Is.EqualTo(80));
                Assert.That(english.PresentationSpeed,
                    Is.EqualTo(ProductPresentationSpeed.Normal));
                Assert.That(english.Locale, Is.EqualTo("en-US"));
                Assert.That(japanese.Locale, Is.EqualTo("ja-JP"));
                Assert.That(ProductSettings.CreateDefaults("ja").Locale,
                    Is.EqualTo("ja-JP"));
                Assert.That(ProductSettings.CreateDefaults("fr-FR").Locale,
                    Is.EqualTo("en-US"));
                Assert.That(english.TextScalePercent, Is.EqualTo(100));
                Assert.That(english.HighContrast, Is.False);
                Assert.That(english.ReducedMotion, Is.False);
            });

            Assert.That(ProductResolution.Supported, Is.EquivalentTo(new[]
            {
                new ProductResolution(1280, 720),
                new ProductResolution(1280, 800),
                new ProductResolution(1920, 1080),
                new ProductResolution(1920, 1200),
                new ProductResolution(2560, 1080),
                new ProductResolution(3440, 1440),
                new ProductResolution(3840, 2160)
            }));
            Assert.That(ProductSettings.SupportedTextScalePercents,
                Is.EqualTo(new[] { 100, 125, 150 }));
        }

        [Test]
        public void SettingsAndBindingsRejectOutOfContractValuesWithoutMutation()
        {
            ProductSettings defaults = ProductSettings.CreateDefaults("en-US");
            ProductInputBindings original = defaults.InputBindings;
            ProductInputBindings rebound = original.With(ProductInputScheme.Keyboard,
                ProductInputCommand.Help, "<Keyboard>/h");

            AssertAll(() =>
            {
                Assert.That(original.Get(ProductInputScheme.Keyboard,
                    ProductInputCommand.Help), Is.EqualTo("<Keyboard>/f1"));
                Assert.That(rebound.Get(ProductInputScheme.Keyboard,
                    ProductInputCommand.Help), Is.EqualTo("<Keyboard>/h"));
                Assert.That(() => original.With(ProductInputScheme.Keyboard,
                        ProductInputCommand.Help, "<Keyboard>/enter"),
                    Throws.ArgumentException);
                Assert.That(() => original.With(ProductInputScheme.Keyboard,
                        ProductInputCommand.Help, "<Keyboard>/ENTER"),
                    Throws.ArgumentException);
                Assert.That(() => original.With(ProductInputScheme.Keyboard,
                        ProductInputCommand.Help, "<Gamepad>/buttonNorth"),
                    Throws.ArgumentException);
                Assert.That(() => original.With(ProductInputScheme.Gamepad,
                        ProductInputCommand.Help, "<Gamepad>//buttonNorth"),
                    Throws.ArgumentException);
                Assert.That(() => defaults.WithAudio(-1, 60, 80),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => defaults.WithAudio(80, 60, 101),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => defaults.WithResolution(new ProductResolution(1024, 768)),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => defaults.WithLocale("fr-FR"),
                    Throws.ArgumentException);
                Assert.That(() => defaults.WithTextScalePercent(110),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void CodecIsStrictDeterministicUtf8AndRoundTripsAllValues()
        {
            ProductInputBindings bindings = ProductInputBindings.Default
                .With(ProductInputScheme.Keyboard, ProductInputCommand.Up, "<Keyboard>/w")
                .With(ProductInputScheme.Gamepad, ProductInputCommand.Up,
                    "<Gamepad>/leftStick/up");
            ProductSettings expected = ProductSettings.CreateDefaults("en-US")
                .WithDisplay(ProductDisplayMode.BorderlessFullscreen,
                    new ProductResolution(3440, 1440), vSync: false)
                .WithAudio(0, 37, 100)
                .WithPresentationSpeed(ProductPresentationSpeed.Fast)
                .WithInputBindings(bindings)
                .WithAccessibility("ja-JP", 150, highContrast: true,
                    reducedMotion: true);

            byte[] first = ProductSettingsCodec.Encode(expected);
            byte[] second = ProductSettingsCodec.Encode(expected);
            ProductSettings decoded = ProductSettingsCodec.Decode(first);
            string text = new UTF8Encoding(false, true).GetString(first);

            AssertAll(() =>
            {
                Assert.That(second, Is.EqualTo(first));
                Assert.That(decoded, Is.EqualTo(expected));
                Assert.That(first.Take(3).ToArray(), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
                Assert.That(text, Does.StartWith("format=1\ndisplay_mode=borderless_fullscreen\n"));
                Assert.That(text, Does.EndWith("binding_gamepad_help=<Gamepad>/buttonNorth\n"));
                Assert.That(text, Does.Not.Contain("\r"));
            });

            byte[] unknownVersion = Encoding.UTF8.GetBytes(
                text.Replace("format=1\n", "format=2\n"));
            byte[] extraField = Encoding.UTF8.GetBytes(text + "extra=true\n");
            Assert.That(() => ProductSettingsCodec.Decode(unknownVersion),
                Throws.TypeOf<ProductSettingsFormatException>());
            Assert.That(() => ProductSettingsCodec.Decode(extraField),
                Throws.TypeOf<ProductSettingsFormatException>());
            Assert.That(() => ProductSettingsCodec.Decode(new byte[] { 0xC3, 0x28 }),
                Throws.TypeOf<ProductSettingsFormatException>());
            Assert.That(() => ProductSettingsCodec.Decode(
                    new byte[ProductSettingsCodec.MaximumEncodedBytes + 1]),
                Throws.TypeOf<ProductSettingsFormatException>());
        }

        [Test]
        public void MissingLoadReturnsDefaultsWithoutWritingAnything()
        {
            ProductSettingsLoadResult result = store.Load();

            AssertAll(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ProductSettingsLoadStatus.Missing));
                Assert.That(result.Settings, Is.EqualTo(ProductSettings.CreateDefaults("en-US")));
                Assert.That(result.Error, Is.Null);
                Assert.That(Directory.Exists(temporaryRoot), Is.False);
                Assert.That(File.Exists(store.SettingsPath), Is.False);
            });
        }

        [Test]
        public void SaveLoadAndUpdatePersistAcrossStoreInstancesWithPreviousBackup()
        {
            ProductSettings first = ProductSettings.CreateDefaults("en-US")
                .WithAudio(20, 30, 40)
                .WithPresentationSpeed(ProductPresentationSpeed.Reduced);
            ProductSettings second = first
                .WithDisplay(ProductDisplayMode.BorderlessFullscreen,
                    new ProductResolution(1920, 1080), vSync: false)
                .WithLocale("ja-JP");

            ProductSettingsSaveResult firstSave = store.Save(first);
            ProductSettingsSaveResult secondSave = store.Save(second);
            var restarted = new FileProductSettingsStore(temporaryRoot, "en-US");
            ProductSettingsLoadResult loaded = restarted.Load();

            AssertAll(() =>
            {
                Assert.That(firstSave.Succeeded, Is.True, firstSave.Error);
                Assert.That(firstSave.BackupPath, Is.Null);
                Assert.That(secondSave.Succeeded, Is.True, secondSave.Error);
                Assert.That(secondSave.BackupPath, Is.EqualTo(store.BackupPath));
                Assert.That(loaded.Status, Is.EqualTo(ProductSettingsLoadStatus.Loaded));
                Assert.That(loaded.Settings, Is.EqualTo(second));
                Assert.That(ProductSettingsCodec.Decode(File.ReadAllBytes(store.BackupPath)),
                    Is.EqualTo(first));
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(store.SettingsPath)!,
                    "settings.*.tmp"), Is.Empty);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidLoadPreservesCorruptAndUnknownVersionBytesWithoutWriting(bool unknown)
        {
            ProductSettings valid = ProductSettings.CreateDefaults("en-US");
            Assert.That(store.Save(valid).Succeeded, Is.True);
            byte[] invalid = unknown
                ? Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(
                    ProductSettingsCodec.Encode(valid)).Replace("format=1\n", "format=9\n"))
                : new byte[] { 0xFF, 0xFE, 0x00, 0x41 };
            File.WriteAllBytes(store.SettingsPath, invalid);

            ProductSettingsLoadResult result = store.Load();

            AssertAll(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ProductSettingsLoadStatus.Invalid));
                Assert.That(result.Settings, Is.EqualTo(valid));
                Assert.That(result.Error, Is.Not.Empty);
                Assert.That(File.ReadAllBytes(store.SettingsPath), Is.EqualTo(invalid));
                Assert.That(File.Exists(store.InvalidArchivePath), Is.False);
                Assert.That(File.Exists(store.BackupPath), Is.False);
            });
        }

        [TestCase("format=1\n", "format=01\n")]
        [TestCase("resolution_width=1280\n", "resolution_width=01280\n")]
        [TestCase("master_volume=80\n", "master_volume=080\n")]
        public void NonCanonicalIntegerLoadIsInvalidAndExplicitResetArchivesOriginal(
            string canonical, string nonCanonical)
        {
            ProductSettings defaults = ProductSettings.CreateDefaults("en-US");
            Assert.That(store.Save(defaults).Succeeded, Is.True);
            byte[] invalid = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(
                ProductSettingsCodec.Encode(defaults)).Replace(canonical, nonCanonical));
            File.WriteAllBytes(store.SettingsPath, invalid);

            ProductSettingsLoadResult loaded = store.Load();

            AssertAll(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(ProductSettingsLoadStatus.Invalid));
                Assert.That(loaded.Settings, Is.EqualTo(defaults));
                Assert.That(loaded.Error, Is.Not.Empty);
                Assert.That(File.ReadAllBytes(store.SettingsPath), Is.EqualTo(invalid));
                Assert.That(File.Exists(store.InvalidArchivePath), Is.False);
            });

            ProductSettingsSaveResult reset = store.Reset(defaults);

            AssertAll(() =>
            {
                Assert.That(reset.Succeeded, Is.True, reset.Error);
                Assert.That(reset.InvalidArchivePath, Is.EqualTo(store.InvalidArchivePath));
                Assert.That(File.ReadAllBytes(reset.InvalidArchivePath!), Is.EqualTo(invalid));
                Assert.That(store.Load().Status, Is.EqualTo(ProductSettingsLoadStatus.Loaded));
                Assert.That(store.Load().Settings, Is.EqualTo(defaults));
            });
        }

        [Test]
        public void ExplicitSaveArchivesInvalidOriginalBeforeReplacingIt()
        {
            Assert.That(store.Save(ProductSettings.CreateDefaults("en-US")).Succeeded, Is.True);
            byte[] invalid = Encoding.UTF8.GetBytes("format=99\nunknown=preserve-me\n");
            File.WriteAllBytes(store.SettingsPath, invalid);
            ProductSettings replacement = ProductSettings.CreateDefaults("ja-JP")
                .WithAudio(1, 2, 3);

            ProductSettingsSaveResult saved = store.Save(replacement);

            AssertAll(() =>
            {
                Assert.That(saved.Succeeded, Is.True, saved.Error);
                Assert.That(saved.InvalidArchivePath, Is.EqualTo(store.InvalidArchivePath));
                Assert.That(File.ReadAllBytes(saved.InvalidArchivePath!), Is.EqualTo(invalid));
                Assert.That(store.Load().Settings, Is.EqualTo(replacement));
                Assert.That(saved.BackupPath, Is.EqualTo(store.BackupPath));
                Assert.That(File.ReadAllBytes(saved.BackupPath!), Is.EqualTo(invalid));
            });
        }

        [Test]
        public void SemanticInvalidBindingIsPreservedAndArchivedOnExplicitReset()
        {
            var validatedStore = new FileProductSettingsStore(
                temporaryRoot, "en-US", new InputBindingValidator());
            ProductSettings defaults = ProductSettings.CreateDefaults("en-US");
            Assert.That(validatedStore.Save(defaults).Succeeded, Is.True);
            ProductSettings semanticInvalid = defaults.WithInputBindings(
                defaults.InputBindings.With(ProductInputScheme.Keyboard,
                    ProductInputCommand.Help, "<Keyboard>/notARealKey"));
            byte[] invalid = ProductSettingsCodec.Encode(semanticInvalid);
            File.WriteAllBytes(validatedStore.SettingsPath, invalid);

            ProductSettingsLoadResult loaded = validatedStore.Load();

            Assert.That(loaded.Status, Is.EqualTo(ProductSettingsLoadStatus.Invalid));
            Assert.That(loaded.Settings, Is.EqualTo(defaults));
            Assert.That(File.ReadAllBytes(validatedStore.SettingsPath), Is.EqualTo(invalid));
            Assert.That(File.Exists(validatedStore.InvalidArchivePath), Is.False);

            ProductSettingsSaveResult reset = validatedStore.Reset(defaults);

            Assert.That(reset.Succeeded, Is.True, reset.Error);
            Assert.That(reset.InvalidArchivePath, Is.EqualTo(validatedStore.InvalidArchivePath));
            Assert.That(File.ReadAllBytes(reset.InvalidArchivePath!), Is.EqualTo(invalid));
            Assert.That(validatedStore.Load().Settings, Is.EqualTo(defaults));
        }

        [Test]
        public void ResetChangesOnlySettingsAndPreservesSessionReplayAndProgressSiblings()
        {
            ProductSettings changed = ProductSettings.CreateDefaults("en-US")
                .WithAudio(5, 6, 7)
                .WithReducedMotion(true);
            Assert.That(store.Save(changed).Succeeded, Is.True);
            string productRoot = Path.GetDirectoryName(store.SettingsPath)!;
            string sessions = Path.Combine(productRoot, "Sessions");
            Directory.CreateDirectory(sessions);
            string sessionPath = Path.Combine(sessions,
                "0123456789abcdef0123456789abcdef.tgs");
            string replayPath = Path.Combine(sessions,
                "fedcba9876543210fedcba9876543210.replay");
            string progressPath = Path.Combine(productRoot, "progress.v1");
            byte[] sessionBytes = { 1, 3, 3, 7 };
            byte[] replayBytes = { 9, 8, 7, 6 };
            byte[] progressBytes = Encoding.UTF8.GetBytes("tutorial-progress");
            File.WriteAllBytes(sessionPath, sessionBytes);
            File.WriteAllBytes(replayPath, replayBytes);
            File.WriteAllBytes(progressPath, progressBytes);
            ProductSettings defaults = ProductSettings.CreateDefaults("ja-JP");

            ProductSettingsSaveResult reset = store.Reset(defaults);

            AssertAll(() =>
            {
                Assert.That(reset.Succeeded, Is.True, reset.Error);
                Assert.That(store.Load().Settings, Is.EqualTo(defaults));
                Assert.That(File.ReadAllBytes(sessionPath), Is.EqualTo(sessionBytes));
                Assert.That(File.ReadAllBytes(replayPath), Is.EqualTo(replayBytes));
                Assert.That(File.ReadAllBytes(progressPath), Is.EqualTo(progressBytes));
            });
        }

        private sealed class InputBindingValidator : IProductSettingsValidator
        {
            public bool TryValidate(ProductSettings settings, out string error) =>
                ProductInputController.TryValidate(settings.InputBindings, out error);
        }

        private static void AssertAll(System.Action assertions) => assertions();
    }
}
