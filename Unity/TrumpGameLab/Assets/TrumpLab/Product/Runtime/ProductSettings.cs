#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace TrumpLab.Product
{
    public enum ProductDisplayMode
    {
        Windowed = 0,
        Borderless = 1,
        BorderlessFullscreen = Borderless
    }

    public enum ProductPresentationSpeed
    {
        Reduced = 0,
        Normal = 1,
        Fast = 2
    }

    public enum ProductInputScheme
    {
        Keyboard = 0,
        Gamepad = 1
    }

    public enum ProductInputCommand
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
        Submit = 4,
        Cancel = 5,
        Help = 6
    }

    public readonly struct ProductResolution : IEquatable<ProductResolution>
    {
        private static readonly IReadOnlyList<ProductResolution> SupportedValues =
            Array.AsReadOnly(new[]
            {
                new ProductResolution(1280, 720),
                new ProductResolution(1280, 800),
                new ProductResolution(1920, 1080),
                new ProductResolution(1920, 1200),
                new ProductResolution(2560, 1080),
                new ProductResolution(3440, 1440),
                new ProductResolution(3840, 2160)
            });

        public static IReadOnlyList<ProductResolution> Supported => SupportedValues;
        public int Width { get; }
        public int Height { get; }

        public ProductResolution(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
        }

        public bool Equals(ProductResolution other) =>
            Width == other.Width && Height == other.Height;

        public override bool Equals(object? obj) =>
            obj is ProductResolution other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        public override string ToString() =>
            Width.ToString(CultureInfo.InvariantCulture) + "x" +
            Height.ToString(CultureInfo.InvariantCulture);

        public static bool operator ==(ProductResolution left, ProductResolution right) =>
            left.Equals(right);

        public static bool operator !=(ProductResolution left, ProductResolution right) =>
            !left.Equals(right);
    }

    public sealed class ProductInputBindings : IEquatable<ProductInputBindings>
    {
        private const int SchemeCount = 2;
        private const int CommandCount = 7;
        private const int MaximumPathLength = 64;
        private readonly string[,] paths;

        public static ProductInputBindings Default { get; } = CreateDefaults();

        private ProductInputBindings(string[,] pathsToCopy)
        {
            paths = (string[,])pathsToCopy.Clone();
            ValidateAll(paths);
        }

        public string Get(ProductInputScheme scheme, ProductInputCommand command)
        {
            int schemeIndex = RequireScheme(scheme);
            int commandIndex = RequireCommand(command);
            return paths[schemeIndex, commandIndex];
        }

        public ProductInputBindings With(ProductInputScheme scheme,
            ProductInputCommand command, string path)
        {
            int schemeIndex = RequireScheme(scheme);
            int commandIndex = RequireCommand(command);
            RequireCanonicalPath(scheme, path, nameof(path));

            var updated = (string[,])paths.Clone();
            updated[schemeIndex, commandIndex] = path;
            return new ProductInputBindings(updated);
        }

        public bool Equals(ProductInputBindings? other)
        {
            if (other == null) return false;
            foreach (ProductInputScheme scheme in EnumerateSchemes())
            {
                foreach (ProductInputCommand command in EnumerateCommands())
                {
                    if (!string.Equals(Get(scheme, command), other.Get(scheme, command),
                            StringComparison.Ordinal)) return false;
                }
            }
            return true;
        }

        public override bool Equals(object? obj) =>
            obj is ProductInputBindings other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (ProductInputScheme scheme in EnumerateSchemes())
                {
                    foreach (ProductInputCommand command in EnumerateCommands())
                        hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Get(scheme, command));
                }
                return hash;
            }
        }

        internal static ProductInputBindings Create(IReadOnlyDictionary<string, string> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var result = new string[SchemeCount, CommandCount];
            foreach (ProductInputScheme scheme in EnumerateSchemes())
            {
                foreach (ProductInputCommand command in EnumerateCommands())
                {
                    string key = StorageKey(scheme, command);
                    if (!values.TryGetValue(key, out string? path) || path == null)
                        throw new ArgumentException("An input binding is missing.", nameof(values));
                    result[(int)scheme, (int)command] = path;
                }
            }
            if (values.Count != SchemeCount * CommandCount)
                throw new ArgumentException("Unexpected input bindings are not supported.",
                    nameof(values));
            return new ProductInputBindings(result);
        }

        internal static IEnumerable<ProductInputScheme> EnumerateSchemes()
        {
            yield return ProductInputScheme.Keyboard;
            yield return ProductInputScheme.Gamepad;
        }

        internal static IEnumerable<ProductInputCommand> EnumerateCommands()
        {
            yield return ProductInputCommand.Up;
            yield return ProductInputCommand.Down;
            yield return ProductInputCommand.Left;
            yield return ProductInputCommand.Right;
            yield return ProductInputCommand.Submit;
            yield return ProductInputCommand.Cancel;
            yield return ProductInputCommand.Help;
        }

        internal static string StorageKey(ProductInputScheme scheme, ProductInputCommand command) =>
            SchemeStorageName(scheme) + "_" + CommandStorageName(command);

        private static ProductInputBindings CreateDefaults()
        {
            var defaults = new string[SchemeCount, CommandCount];
            defaults[(int)ProductInputScheme.Keyboard, (int)ProductInputCommand.Up] =
                "<Keyboard>/upArrow";
            defaults[(int)ProductInputScheme.Keyboard, (int)ProductInputCommand.Down] =
                "<Keyboard>/downArrow";
            defaults[(int)ProductInputScheme.Keyboard, (int)ProductInputCommand.Left] =
                "<Keyboard>/leftArrow";
            defaults[(int)ProductInputScheme.Keyboard, (int)ProductInputCommand.Right] =
                "<Keyboard>/rightArrow";
            defaults[(int)ProductInputScheme.Keyboard, (int)ProductInputCommand.Submit] =
                "<Keyboard>/enter";
            defaults[(int)ProductInputScheme.Keyboard, (int)ProductInputCommand.Cancel] =
                "<Keyboard>/escape";
            defaults[(int)ProductInputScheme.Keyboard, (int)ProductInputCommand.Help] =
                "<Keyboard>/f1";

            defaults[(int)ProductInputScheme.Gamepad, (int)ProductInputCommand.Up] =
                "<Gamepad>/dpad/up";
            defaults[(int)ProductInputScheme.Gamepad, (int)ProductInputCommand.Down] =
                "<Gamepad>/dpad/down";
            defaults[(int)ProductInputScheme.Gamepad, (int)ProductInputCommand.Left] =
                "<Gamepad>/dpad/left";
            defaults[(int)ProductInputScheme.Gamepad, (int)ProductInputCommand.Right] =
                "<Gamepad>/dpad/right";
            defaults[(int)ProductInputScheme.Gamepad, (int)ProductInputCommand.Submit] =
                "<Gamepad>/buttonSouth";
            defaults[(int)ProductInputScheme.Gamepad, (int)ProductInputCommand.Cancel] =
                "<Gamepad>/buttonEast";
            defaults[(int)ProductInputScheme.Gamepad, (int)ProductInputCommand.Help] =
                "<Gamepad>/buttonNorth";
            return new ProductInputBindings(defaults);
        }

        private static void ValidateAll(string[,] values)
        {
            foreach (ProductInputScheme scheme in EnumerateSchemes())
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ProductInputCommand command in EnumerateCommands())
                {
                    string path = values[(int)scheme, (int)command];
                    RequireCanonicalPath(scheme, path, nameof(values));
                    if (!used.Add(path))
                        throw new ArgumentException(
                            "Input bindings must be unique within a device scheme.",
                            nameof(values));
                }
            }
        }

        private static void RequireCanonicalPath(ProductInputScheme scheme, string path,
            string parameterName)
        {
            if (string.IsNullOrEmpty(path) || path.Length > MaximumPathLength)
                throw new ArgumentException("Input binding path is invalid.", parameterName);
            string prefix = scheme == ProductInputScheme.Keyboard
                ? "<Keyboard>/"
                : scheme == ProductInputScheme.Gamepad
                    ? "<Gamepad>/"
                    : throw new ArgumentOutOfRangeException(nameof(scheme));
            if (!path.StartsWith(prefix, StringComparison.Ordinal) || path.Length == prefix.Length)
                throw new ArgumentException(
                    "Input binding path does not match its device scheme.", parameterName);
            bool previousWasSlash = true;
            for (int index = prefix.Length; index < path.Length; index++)
            {
                char character = path[index];
                bool asciiLetterOrDigit =
                    (character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9');
                if (!asciiLetterOrDigit && character != '/')
                    throw new ArgumentException("Input binding path is not canonical.",
                        parameterName);
                if (character == '/' && (previousWasSlash || index == path.Length - 1))
                    throw new ArgumentException("Input binding path is not canonical.",
                        parameterName);
                previousWasSlash = character == '/';
            }
        }

        private static int RequireScheme(ProductInputScheme scheme)
        {
            if (scheme != ProductInputScheme.Keyboard && scheme != ProductInputScheme.Gamepad)
                throw new ArgumentOutOfRangeException(nameof(scheme));
            return (int)scheme;
        }

        private static int RequireCommand(ProductInputCommand command)
        {
            if (command < ProductInputCommand.Up || command > ProductInputCommand.Help)
                throw new ArgumentOutOfRangeException(nameof(command));
            return (int)command;
        }

        private static string SchemeStorageName(ProductInputScheme scheme)
        {
            return scheme switch
            {
                ProductInputScheme.Keyboard => "keyboard",
                ProductInputScheme.Gamepad => "gamepad",
                _ => throw new ArgumentOutOfRangeException(nameof(scheme))
            };
        }

        private static string CommandStorageName(ProductInputCommand command)
        {
            return command switch
            {
                ProductInputCommand.Up => "up",
                ProductInputCommand.Down => "down",
                ProductInputCommand.Left => "left",
                ProductInputCommand.Right => "right",
                ProductInputCommand.Submit => "submit",
                ProductInputCommand.Cancel => "cancel",
                ProductInputCommand.Help => "help",
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            };
        }
    }

    public sealed class ProductSettings : IEquatable<ProductSettings>
    {
        public const int CurrentFormatVersion = 1;

        public static IReadOnlyList<ProductResolution> SupportedResolutions =>
            ProductResolution.Supported;
        public static IReadOnlyList<int> SupportedTextScalePercents { get; } =
            Array.AsReadOnly(new[] { 100, 125, 150 });

        public int FormatVersion { get; }
        public ProductDisplayMode DisplayMode { get; }
        public ProductResolution Resolution { get; }
        public bool VSyncEnabled { get; }
        public bool VSync => VSyncEnabled;
        public int MasterVolume { get; }
        public int MusicVolume { get; }
        public int SfxVolume { get; }
        public int SFXVolume => SfxVolume;
        public ProductPresentationSpeed PresentationSpeed { get; }
        public ProductInputBindings InputBindings { get; }
        public string Locale { get; }
        public int TextScalePercent { get; }
        public bool HighContrast { get; }
        public bool ReducedMotion { get; }

        public ProductSettings(int formatVersion, ProductDisplayMode displayMode,
            ProductResolution resolution, bool vSync, int masterVolume, int musicVolume,
            int sfxVolume, ProductPresentationSpeed presentationSpeed,
            ProductInputBindings inputBindings, string locale, int textScalePercent,
            bool highContrast, bool reducedMotion)
        {
            if (formatVersion != CurrentFormatVersion)
                throw new ArgumentOutOfRangeException(nameof(formatVersion));
            if (displayMode != ProductDisplayMode.Windowed &&
                displayMode != ProductDisplayMode.BorderlessFullscreen)
                throw new ArgumentOutOfRangeException(nameof(displayMode));
            if (!ContainsResolution(resolution))
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution,
                    "Resolution is not supported.");
            RequireVolume(masterVolume, nameof(masterVolume));
            RequireVolume(musicVolume, nameof(musicVolume));
            RequireVolume(sfxVolume, nameof(sfxVolume));
            if (presentationSpeed < ProductPresentationSpeed.Reduced ||
                presentationSpeed > ProductPresentationSpeed.Fast)
                throw new ArgumentOutOfRangeException(nameof(presentationSpeed));
            InputBindings = inputBindings ?? throw new ArgumentNullException(nameof(inputBindings));
            if (locale != "ja-JP" && locale != "en-US")
                throw new ArgumentException("Locale must be ja-JP or en-US.", nameof(locale));
            if (!ContainsTextScale(textScalePercent))
                throw new ArgumentOutOfRangeException(nameof(textScalePercent));

            FormatVersion = formatVersion;
            DisplayMode = displayMode;
            Resolution = resolution;
            VSyncEnabled = vSync;
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
            PresentationSpeed = presentationSpeed;
            Locale = locale;
            TextScalePercent = textScalePercent;
            HighContrast = highContrast;
            ReducedMotion = reducedMotion;
        }

        public static ProductSettings CreateDefaults(string? uiCultureName = null)
        {
            string cultureName = uiCultureName ?? CultureInfo.CurrentUICulture.Name;
            string locale = IsJapaneseCulture(cultureName) ? "ja-JP" : "en-US";
            return new ProductSettings(CurrentFormatVersion, ProductDisplayMode.Windowed,
                new ProductResolution(1280, 720), vSync: true, masterVolume: 80,
                musicVolume: 60, sfxVolume: 80,
                ProductPresentationSpeed.Normal, ProductInputBindings.Default, locale,
                textScalePercent: 100, highContrast: false, reducedMotion: false);
        }

        public ProductSettings WithDisplay(ProductDisplayMode displayMode,
            ProductResolution resolution, bool vSync) =>
            Copy(displayMode: displayMode, resolution: resolution, vSync: vSync);

        public ProductSettings WithDisplayMode(ProductDisplayMode value) =>
            Copy(displayMode: value);

        public ProductSettings WithResolution(ProductResolution value) =>
            Copy(resolution: value);

        public ProductSettings WithVSync(bool value) => Copy(vSync: value);

        public ProductSettings WithAudio(int masterVolume, int musicVolume, int sfxVolume) =>
            Copy(masterVolume: masterVolume, musicVolume: musicVolume, sfxVolume: sfxVolume);

        public ProductSettings WithVolumes(int masterVolume, int musicVolume, int sfxVolume) =>
            WithAudio(masterVolume, musicVolume, sfxVolume);

        public ProductSettings WithPresentation(ProductPresentationSpeed value) =>
            Copy(presentationSpeed: value);

        public ProductSettings WithPresentationSpeed(ProductPresentationSpeed value) =>
            WithPresentation(value);

        public ProductSettings WithInputBindings(ProductInputBindings value) =>
            Copy(inputBindings: value);

        public ProductSettings WithAccessibility(string locale, int textScalePercent,
            bool highContrast, bool reducedMotion) =>
            Copy(locale: locale, textScalePercent: textScalePercent,
                highContrast: highContrast, reducedMotion: reducedMotion);

        public ProductSettings WithLocale(string value) => Copy(locale: value);
        public ProductSettings WithTextScalePercent(int value) => Copy(textScalePercent: value);
        public ProductSettings WithHighContrast(bool value) => Copy(highContrast: value);
        public ProductSettings WithReducedMotion(bool value) => Copy(reducedMotion: value);

        public bool Equals(ProductSettings? other)
        {
            return other != null &&
                FormatVersion == other.FormatVersion &&
                DisplayMode == other.DisplayMode &&
                Resolution == other.Resolution &&
                VSyncEnabled == other.VSyncEnabled &&
                MasterVolume == other.MasterVolume &&
                MusicVolume == other.MusicVolume &&
                SfxVolume == other.SfxVolume &&
                PresentationSpeed == other.PresentationSpeed &&
                InputBindings.Equals(other.InputBindings) &&
                string.Equals(Locale, other.Locale, StringComparison.Ordinal) &&
                TextScalePercent == other.TextScalePercent &&
                HighContrast == other.HighContrast &&
                ReducedMotion == other.ReducedMotion;
        }

        public override bool Equals(object? obj) =>
            obj is ProductSettings other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = FormatVersion;
                hash = (hash * 397) ^ (int)DisplayMode;
                hash = (hash * 397) ^ Resolution.GetHashCode();
                hash = (hash * 397) ^ VSyncEnabled.GetHashCode();
                hash = (hash * 397) ^ MasterVolume;
                hash = (hash * 397) ^ MusicVolume;
                hash = (hash * 397) ^ SfxVolume;
                hash = (hash * 397) ^ (int)PresentationSpeed;
                hash = (hash * 397) ^ InputBindings.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Locale);
                hash = (hash * 397) ^ TextScalePercent;
                hash = (hash * 397) ^ HighContrast.GetHashCode();
                hash = (hash * 397) ^ ReducedMotion.GetHashCode();
                return hash;
            }
        }

        private ProductSettings Copy(ProductDisplayMode? displayMode = null,
            ProductResolution? resolution = null, bool? vSync = null,
            int? masterVolume = null, int? musicVolume = null, int? sfxVolume = null,
            ProductPresentationSpeed? presentationSpeed = null,
            ProductInputBindings? inputBindings = null, string? locale = null,
            int? textScalePercent = null, bool? highContrast = null,
            bool? reducedMotion = null)
        {
            return new ProductSettings(FormatVersion, displayMode ?? DisplayMode,
                resolution ?? Resolution, vSync ?? VSyncEnabled,
                masterVolume ?? MasterVolume, musicVolume ?? MusicVolume,
                sfxVolume ?? SfxVolume, presentationSpeed ?? PresentationSpeed,
                inputBindings ?? InputBindings, locale ?? Locale,
                textScalePercent ?? TextScalePercent, highContrast ?? HighContrast,
                reducedMotion ?? ReducedMotion);
        }

        private static bool IsJapaneseCulture(string cultureName) =>
            string.Equals(cultureName, "ja", StringComparison.OrdinalIgnoreCase) ||
            cultureName.StartsWith("ja-", StringComparison.OrdinalIgnoreCase);

        private static bool ContainsResolution(ProductResolution resolution)
        {
            for (int index = 0; index < ProductResolution.Supported.Count; index++)
            {
                if (ProductResolution.Supported[index] == resolution) return true;
            }
            return false;
        }

        private static bool ContainsTextScale(int value)
        {
            for (int index = 0; index < SupportedTextScalePercents.Count; index++)
            {
                if (SupportedTextScalePercents[index] == value) return true;
            }
            return false;
        }

        private static void RequireVolume(int value, string parameterName)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(parameterName, value,
                    "Volume must be from 0 through 100.");
        }
    }
}
