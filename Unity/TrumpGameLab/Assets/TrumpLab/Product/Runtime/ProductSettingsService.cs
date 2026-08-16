#nullable enable

using System;
using UnityEngine;

namespace TrumpLab.Product
{
    /// <summary>
    /// Applies product presentation settings to the host environment. Persistence and
    /// input binding ownership remain outside this contract.
    /// </summary>
    public interface IProductSettingsApplier
    {
        void Apply(ProductSettings settings);
    }

    public interface IProductSettingsValidator
    {
        bool TryValidate(ProductSettings settings, out string error);
    }

    public interface IProductDisplayGuard
    {
        void MaintainValidDisplay(ProductSettings settings);
    }

    public static class ProductDisplayPolicy
    {
        public const int MinimumWidth = 1280;
        public const int MinimumHeight = 720;

        public static bool RequiresRestore(int width, int height) =>
            width < MinimumWidth || height < MinimumHeight;
    }

    public sealed class UnityProductSettingsApplier : IProductSettingsApplier,
        IProductDisplayGuard
    {
        public void Apply(ProductSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            // EditMode and PlayMode contract tests must not resize the Editor, change its
            // quality settings, or mute other Editor audio.
            if (Application.isEditor) return;

            ApplyDisplay(settings);
            QualitySettings.vSyncCount = settings.VSyncEnabled ? 1 : 0;
            AudioListener.volume = settings.MasterVolume / 100f;
        }

        public void MaintainValidDisplay(ProductSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (Application.isEditor ||
                !ProductDisplayPolicy.RequiresRestore(Screen.width, Screen.height)) return;
            ApplyDisplay(settings);
        }

        private static void ApplyDisplay(ProductSettings settings)
        {
            FullScreenMode fullScreenMode = settings.DisplayMode switch
            {
                ProductDisplayMode.Windowed => FullScreenMode.Windowed,
                ProductDisplayMode.BorderlessFullscreen => FullScreenMode.FullScreenWindow,
                _ => throw new ArgumentOutOfRangeException(nameof(settings),
                    "The product display mode is unsupported.")
            };
            Screen.SetResolution(settings.Resolution.Width, settings.Resolution.Height,
                fullScreenMode);
        }
    }

    /// <summary>
    /// Coordinates product-setting persistence and host application. A missing or invalid
    /// settings file never causes an implicit write; callers must explicitly save or reset.
    /// </summary>
    public sealed class ProductSettingsService
    {
        private readonly IProductSettingsStore store;
        private readonly IProductSettingsApplier applier;
        private readonly IProductSettingsValidator? validator;

        public ProductSettings Defaults { get; }
        public ProductSettings Current { get; private set; }
        public ProductSettingsLoadResult? LastLoadResult { get; private set; }

        public ProductSettingsService(IProductSettingsStore store,
            IProductSettingsApplier applier, ProductSettings? defaults = null,
            IProductSettingsValidator? validator = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.applier = applier ?? throw new ArgumentNullException(nameof(applier));
            Defaults = defaults ?? ProductSettings.CreateDefaults();
            this.validator = validator;
            if (!TryValidate(Defaults, out string error))
                throw new ArgumentException(
                    "Product settings defaults are invalid: " + error, nameof(defaults));
            Current = Defaults;
        }

        public ProductSettingsLoadResult Initialize()
        {
            ProductSettingsLoadResult loadResult;
            try
            {
                loadResult = store.Load();
                if (loadResult == null)
                    throw new InvalidOperationException(
                        "The product settings store returned no load result.");
            }
            catch (Exception exception)
            {
                loadResult = InvalidLoadResult(exception);
            }

            if (loadResult.Status == ProductSettingsLoadStatus.Loaded &&
                !TryValidate(loadResult.Settings, out string validationError))
            {
                loadResult = new ProductSettingsLoadResult(ProductSettingsLoadStatus.Invalid,
                    Defaults, validationError);
            }

            ProductSettings settingsToApply = loadResult.Status ==
                ProductSettingsLoadStatus.Loaded
                ? loadResult.Settings
                : Defaults;

            // Normalize Missing and Invalid results to the service's explicitly injected
            // defaults. This keeps fallback and Reset behavior identical, even for a custom
            // store implementation.
            if (loadResult.Status != ProductSettingsLoadStatus.Loaded &&
                !ReferenceEquals(loadResult.Settings, Defaults))
            {
                loadResult = new ProductSettingsLoadResult(loadResult.Status, Defaults,
                    loadResult.Error);
            }

            try
            {
                applier.Apply(settingsToApply);
                Current = settingsToApply;
            }
            catch (Exception exception)
            {
                loadResult = InvalidLoadResult(exception, loadResult.Error);
            }

            LastLoadResult = loadResult;
            return loadResult;
        }

        public ProductSettingsLoadResult Load() => Initialize();

        public ProductSettingsSaveResult SaveAndApply(ProductSettings settings)
        {
            if (settings == null)
                return ProductSettingsSaveResult.Failure(
                    "Product settings cannot be null.");
            if (!TryValidate(settings, out string error))
                return ProductSettingsSaveResult.Failure(error);
            return PersistAndApply(() => store.Save(settings), settings);
        }

        public ProductSettingsSaveResult Save(ProductSettings settings) =>
            SaveAndApply(settings);

        public ProductSettingsSaveResult ResetToDefaults()
        {
            return ResetToDefaults(Defaults);
        }

        public ProductSettingsSaveResult ResetToDefaults(ProductSettings defaults)
        {
            if (defaults == null)
                return ProductSettingsSaveResult.Failure(
                    "Product settings defaults cannot be null.");
            if (!TryValidate(defaults, out string error))
                return ProductSettingsSaveResult.Failure(error);
            return PersistAndApply(() => store.Reset(defaults), defaults);
        }

        public ProductSettingsSaveResult Reset() => ResetToDefaults();

        public ProductSettingsSaveResult Reset(ProductSettings defaults) =>
            ResetToDefaults(defaults);

        private ProductSettingsSaveResult PersistAndApply(
            Func<ProductSettingsSaveResult> save, ProductSettings settings)
        {
            ProductSettingsSaveResult saveResult;
            try
            {
                saveResult = save();
                if (saveResult == null)
                    return ProductSettingsSaveResult.Failure(
                        "The product settings store returned no save result.");
            }
            catch (Exception exception)
            {
                return ProductSettingsSaveResult.Failure(SafeError(exception));
            }

            if (!saveResult.Succeeded) return saveResult;

            try
            {
                applier.Apply(settings);
                Current = settings;
                return saveResult;
            }
            catch (Exception exception)
            {
                return ProductSettingsSaveResult.Failure(SafeError(exception));
            }
        }

        private ProductSettingsLoadResult InvalidLoadResult(Exception exception,
            string? precedingError = null)
        {
            string error = SafeError(exception);
            if (!string.IsNullOrWhiteSpace(precedingError))
                error = precedingError + " " + error;
            return new ProductSettingsLoadResult(ProductSettingsLoadStatus.Invalid,
                Defaults, error);
        }

        private bool TryValidate(ProductSettings settings, out string error)
        {
            if (validator == null)
            {
                error = string.Empty;
                return true;
            }
            try
            {
                if (validator.TryValidate(settings, out error)) return true;
                if (string.IsNullOrWhiteSpace(error)) error = "Product settings are invalid.";
                return false;
            }
            catch (Exception exception)
            {
                error = SafeError(exception);
                return false;
            }
        }

        private static string SafeError(Exception exception)
        {
            return string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : exception.Message;
        }
    }
}
