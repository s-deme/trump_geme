#nullable enable

using NUnit.Framework;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductSettingsServiceTests
    {
        [TestCase(ProductSettingsLoadStatus.Missing, null)]
        [TestCase(ProductSettingsLoadStatus.Invalid, "settings are corrupt")]
        public void MissingOrInvalidLoadAppliesDefaultsWithoutWriting(
            ProductSettingsLoadStatus status, string? error)
        {
            ProductSettings defaults = ProductSettings.CreateDefaults("en-US");
            ProductSettings unsafeFallback = defaults.WithAudio(1, 2, 3);
            var store = new MemoryProductSettingsStore(
                new ProductSettingsLoadResult(status, unsafeFallback, error));
            var applier = new RecordingProductSettingsApplier();
            var service = new ProductSettingsService(store, applier, defaults);

            ProductSettingsLoadResult result = service.Initialize();

            AssertAll(() =>
            {
                Assert.That(result.Status, Is.EqualTo(status));
                Assert.That(result.Settings, Is.SameAs(defaults));
                Assert.That(result.Error, Is.EqualTo(error));
                Assert.That(service.Current, Is.SameAs(defaults));
                Assert.That(applier.ApplyCount, Is.EqualTo(1));
                Assert.That(applier.LastApplied, Is.SameAs(defaults));
                Assert.That(store.LoadCount, Is.EqualTo(1));
                Assert.That(store.SaveCount, Is.Zero);
                Assert.That(store.ResetCount, Is.Zero);
            });
        }

        [Test]
        public void LoadedSettingsBecomeCurrentAndAreApplied()
        {
            ProductSettings defaults = ProductSettings.CreateDefaults("en-US");
            ProductSettings loaded = defaults
                .WithDisplay(ProductDisplayMode.BorderlessFullscreen,
                    new ProductResolution(1920, 1080), vSync: false)
                .WithAudio(25, 50, 75);
            var store = new MemoryProductSettingsStore(
                new ProductSettingsLoadResult(ProductSettingsLoadStatus.Loaded,
                    loaded, null));
            var applier = new RecordingProductSettingsApplier();
            var service = new ProductSettingsService(store, applier, defaults);

            ProductSettingsLoadResult result = service.Initialize();

            AssertAll(() =>
            {
                Assert.That(result.IsLoaded, Is.True);
                Assert.That(service.Current, Is.SameAs(loaded));
                Assert.That(applier.ApplyCount, Is.EqualTo(1));
                Assert.That(applier.LastApplied, Is.SameAs(loaded));
                Assert.That(store.SaveCount, Is.Zero);
                Assert.That(store.ResetCount, Is.Zero);
            });
        }

        [Test]
        public void SuccessfulSaveUpdatesCurrentAfterPersistenceAndApplies()
        {
            ProductSettings defaults = ProductSettings.CreateDefaults("en-US");
            ProductSettings changed = defaults
                .WithPresentationSpeed(ProductPresentationSpeed.Fast)
                .WithAudio(10, 20, 30);
            var store = new MemoryProductSettingsStore(
                new ProductSettingsLoadResult(ProductSettingsLoadStatus.Missing,
                    defaults, null));
            var applier = new RecordingProductSettingsApplier();
            var service = new ProductSettingsService(store, applier, defaults);

            ProductSettingsSaveResult result = service.SaveAndApply(changed);

            AssertAll(() =>
            {
                Assert.That(result.Succeeded, Is.True, result.Error);
                Assert.That(store.SaveCount, Is.EqualTo(1));
                Assert.That(store.LastSaved, Is.SameAs(changed));
                Assert.That(store.ResetCount, Is.Zero);
                Assert.That(service.Current, Is.SameAs(changed));
                Assert.That(applier.ApplyCount, Is.EqualTo(1));
                Assert.That(applier.LastApplied, Is.SameAs(changed));
            });
        }

        [Test]
        public void FailedSaveKeepsCurrentAndDoesNotApplyCandidate()
        {
            ProductSettings defaults = ProductSettings.CreateDefaults("en-US");
            ProductSettings changed = defaults.WithAudio(10, 20, 30);
            var store = new MemoryProductSettingsStore(
                new ProductSettingsLoadResult(ProductSettingsLoadStatus.Missing,
                    defaults, null))
            {
                SaveResult = ProductSettingsSaveResult.Failure("disk is read-only")
            };
            var applier = new RecordingProductSettingsApplier();
            var service = new ProductSettingsService(store, applier, defaults);

            ProductSettingsSaveResult result = service.SaveAndApply(changed);

            AssertAll(() =>
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Error, Is.EqualTo("disk is read-only"));
                Assert.That(store.SaveCount, Is.EqualTo(1));
                Assert.That(store.LastSaved, Is.SameAs(changed));
                Assert.That(service.Current, Is.SameAs(defaults));
                Assert.That(applier.ApplyCount, Is.Zero);
            });
        }

        [Test]
        public void ResetPersistsAndAppliesOnlyProductDefaults()
        {
            ProductSettings defaults = ProductSettings.CreateDefaults("ja-JP");
            var store = new MemoryProductSettingsStore(
                new ProductSettingsLoadResult(ProductSettingsLoadStatus.Missing,
                    defaults, null));
            var applier = new RecordingProductSettingsApplier();
            var service = new ProductSettingsService(store, applier, defaults);

            ProductSettingsSaveResult result = service.ResetToDefaults();

            AssertAll(() =>
            {
                Assert.That(result.Succeeded, Is.True, result.Error);
                Assert.That(store.ResetCount, Is.EqualTo(1));
                Assert.That(store.LastReset, Is.SameAs(defaults));
                Assert.That(store.SaveCount, Is.Zero);
                Assert.That(service.Current, Is.SameAs(defaults));
                Assert.That(applier.ApplyCount, Is.EqualTo(1));
                Assert.That(applier.LastApplied, Is.SameAs(defaults));
            });
        }

        [Test]
        public void LoadedSettingsRejectedBySemanticValidatorUseDefaultsWithoutWriting()
        {
            ProductSettings defaults = ProductSettings.CreateDefaults("en-US");
            ProductSettings invalid = defaults.WithInputBindings(
                ProductInputBindings.Default.With(ProductInputScheme.Keyboard,
                    ProductInputCommand.Help, "<Keyboard>/notARealKey"));
            var store = new MemoryProductSettingsStore(
                new ProductSettingsLoadResult(ProductSettingsLoadStatus.Loaded, invalid, null));
            var applier = new RecordingProductSettingsApplier();
            var service = new ProductSettingsService(store, applier, defaults,
                new RejectingCandidateValidator(invalid));

            ProductSettingsLoadResult result = service.Initialize();

            Assert.That(result.Status, Is.EqualTo(ProductSettingsLoadStatus.Invalid));
            Assert.That(result.Settings, Is.SameAs(defaults));
            Assert.That(result.Error, Does.Contain("semantic"));
            Assert.That(service.Current, Is.SameAs(defaults));
            Assert.That(applier.LastApplied, Is.SameAs(defaults));
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(store.ResetCount, Is.Zero);
        }

        [TestCase(1279, 720, true)]
        [TestCase(1280, 719, true)]
        [TestCase(1280, 720, false)]
        [TestCase(3840, 2160, false)]
        public void DisplayPolicyRestoresOnlyBelowTheSupportedMinimum(
            int width, int height, bool expected)
        {
            Assert.That(ProductDisplayPolicy.RequiresRestore(width, height),
                Is.EqualTo(expected));
        }

        private sealed class MemoryProductSettingsStore : IProductSettingsStore
        {
            private readonly ProductSettingsLoadResult loadResult;

            public ProductSettingsSaveResult SaveResult { get; set; } =
                ProductSettingsSaveResult.Success();
            public ProductSettingsSaveResult ResetResult { get; set; } =
                ProductSettingsSaveResult.Success();
            public int LoadCount { get; private set; }
            public int SaveCount { get; private set; }
            public int ResetCount { get; private set; }
            public ProductSettings? LastSaved { get; private set; }
            public ProductSettings? LastReset { get; private set; }

            public MemoryProductSettingsStore(ProductSettingsLoadResult loadResult)
            {
                this.loadResult = loadResult;
            }

            public ProductSettingsLoadResult Load()
            {
                LoadCount++;
                return loadResult;
            }

            public ProductSettingsSaveResult Save(ProductSettings settings)
            {
                SaveCount++;
                LastSaved = settings;
                return SaveResult;
            }

            public ProductSettingsSaveResult Reset(ProductSettings defaults)
            {
                ResetCount++;
                LastReset = defaults;
                return ResetResult;
            }
        }

        private sealed class RecordingProductSettingsApplier : IProductSettingsApplier
        {
            public int ApplyCount { get; private set; }
            public ProductSettings? LastApplied { get; private set; }

            public void Apply(ProductSettings settings)
            {
                ApplyCount++;
                LastApplied = settings;
            }
        }

        private sealed class RejectingCandidateValidator : IProductSettingsValidator
        {
            private readonly ProductSettings rejected;

            public RejectingCandidateValidator(ProductSettings rejected)
            {
                this.rejected = rejected;
            }

            public bool TryValidate(ProductSettings settings, out string error)
            {
                if (ReferenceEquals(settings, rejected))
                {
                    error = "The semantic input binding is invalid.";
                    return false;
                }
                error = string.Empty;
                return true;
            }
        }

        private static void AssertAll(System.Action assertions) => assertions();
    }
}
