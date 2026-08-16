#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TrumpLab.Product
{
    public sealed class ProductSettingsFormatException : Exception
    {
        public ProductSettingsFormatException(string message) : base(message) { }
        public ProductSettingsFormatException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public enum ProductSettingsLoadStatus
    {
        Missing = 0,
        Loaded = 1,
        Invalid = 2
    }

    public sealed class ProductSettingsLoadResult
    {
        public ProductSettingsLoadStatus Status { get; }
        public ProductSettings Settings { get; }
        public string? Error { get; }

        public bool IsLoaded => Status == ProductSettingsLoadStatus.Loaded;

        public ProductSettingsLoadResult(ProductSettingsLoadStatus status,
            ProductSettings settings, string? error)
        {
            if (status < ProductSettingsLoadStatus.Missing ||
                status > ProductSettingsLoadStatus.Invalid)
                throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Error = error;
        }
    }

    public sealed class ProductSettingsSaveResult
    {
        public bool Succeeded { get; }
        public bool WasSaved => Succeeded;
        public string? Error { get; }
        public string? InvalidArchivePath { get; }
        public string? BackupPath { get; }

        private ProductSettingsSaveResult(bool succeeded, string? error,
            string? invalidArchivePath, string? backupPath)
        {
            Succeeded = succeeded;
            Error = error;
            InvalidArchivePath = invalidArchivePath;
            BackupPath = backupPath;
        }

        public static ProductSettingsSaveResult Success(string? invalidArchivePath = null,
            string? backupPath = null) =>
            new ProductSettingsSaveResult(true, null, invalidArchivePath, backupPath);

        public static ProductSettingsSaveResult Failure(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("A save failure requires an error.", nameof(error));
            return new ProductSettingsSaveResult(false, error, null, null);
        }
    }

    public interface IProductSettingsStore
    {
        ProductSettingsLoadResult Load();
        ProductSettingsSaveResult Save(ProductSettings settings);
        ProductSettingsSaveResult Reset(ProductSettings defaults);
    }

    public static class ProductSettingsCodec
    {
        public const int MaximumEncodedBytes = 16 * 1024;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static byte[] Encode(ProductSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var builder = new StringBuilder(1024);
            Append(builder, "format", settings.FormatVersion.ToString(CultureInfo.InvariantCulture));
            Append(builder, "display_mode", DisplayModeText(settings.DisplayMode));
            Append(builder, "resolution_width",
                settings.Resolution.Width.ToString(CultureInfo.InvariantCulture));
            Append(builder, "resolution_height",
                settings.Resolution.Height.ToString(CultureInfo.InvariantCulture));
            Append(builder, "vsync", BooleanText(settings.VSyncEnabled));
            Append(builder, "master_volume",
                settings.MasterVolume.ToString(CultureInfo.InvariantCulture));
            Append(builder, "music_volume",
                settings.MusicVolume.ToString(CultureInfo.InvariantCulture));
            Append(builder, "sfx_volume",
                settings.SfxVolume.ToString(CultureInfo.InvariantCulture));
            Append(builder, "presentation_speed", SpeedText(settings.PresentationSpeed));
            Append(builder, "locale", settings.Locale);
            Append(builder, "text_scale_percent",
                settings.TextScalePercent.ToString(CultureInfo.InvariantCulture));
            Append(builder, "high_contrast", BooleanText(settings.HighContrast));
            Append(builder, "reduced_motion", BooleanText(settings.ReducedMotion));
            foreach (ProductInputScheme scheme in ProductInputBindings.EnumerateSchemes())
            {
                foreach (ProductInputCommand command in ProductInputBindings.EnumerateCommands())
                {
                    string key = ProductInputBindings.StorageKey(scheme, command);
                    Append(builder, "binding_" + key,
                        settings.InputBindings.Get(scheme, command));
                }
            }

            byte[] encoded = StrictUtf8.GetBytes(builder.ToString());
            if (encoded.Length <= 0 || encoded.Length > MaximumEncodedBytes)
                throw new ProductSettingsFormatException(
                    "Encoded product settings size is invalid.");
            return encoded;
        }

        public static ProductSettings Decode(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length <= 0 || payload.Length > MaximumEncodedBytes)
                throw new ProductSettingsFormatException("Product settings size is invalid.");

            string text;
            try
            {
                text = StrictUtf8.GetString(payload);
            }
            catch (DecoderFallbackException exception)
            {
                throw new ProductSettingsFormatException(
                    "Product settings are not valid UTF-8.", exception);
            }

            string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
            const int fieldCount = 27;
            if (lines.Length != fieldCount + 1 || lines[fieldCount].Length != 0)
                throw new ProductSettingsFormatException(
                    "Product settings fields are incomplete or unexpected.");

            int line = 0;
            int format = ParseInteger(Value(lines, line++, "format"), "format");
            if (format != ProductSettings.CurrentFormatVersion)
                throw new ProductSettingsFormatException(
                    "Product settings version is unsupported.");
            ProductDisplayMode displayMode = ParseDisplayMode(
                Value(lines, line++, "display_mode"));
            int width = ParseInteger(Value(lines, line++, "resolution_width"),
                "resolution_width");
            int height = ParseInteger(Value(lines, line++, "resolution_height"),
                "resolution_height");
            bool vSync = ParseBoolean(Value(lines, line++, "vsync"), "vsync");
            int masterVolume = ParseInteger(Value(lines, line++, "master_volume"),
                "master_volume");
            int musicVolume = ParseInteger(Value(lines, line++, "music_volume"),
                "music_volume");
            int sfxVolume = ParseInteger(Value(lines, line++, "sfx_volume"),
                "sfx_volume");
            ProductPresentationSpeed speed = ParseSpeed(
                Value(lines, line++, "presentation_speed"));
            string locale = Value(lines, line++, "locale");
            int textScale = ParseInteger(Value(lines, line++, "text_scale_percent"),
                "text_scale_percent");
            bool highContrast = ParseBoolean(Value(lines, line++, "high_contrast"),
                "high_contrast");
            bool reducedMotion = ParseBoolean(Value(lines, line++, "reduced_motion"),
                "reduced_motion");

            var bindingValues = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ProductInputScheme scheme in ProductInputBindings.EnumerateSchemes())
            {
                foreach (ProductInputCommand command in ProductInputBindings.EnumerateCommands())
                {
                    string key = ProductInputBindings.StorageKey(scheme, command);
                    bindingValues.Add(key, Value(lines, line++, "binding_" + key));
                }
            }
            if (line != fieldCount)
                throw new ProductSettingsFormatException(
                    "Product settings field count is invalid.");

            try
            {
                return new ProductSettings(format, displayMode,
                    new ProductResolution(width, height), vSync, masterVolume,
                    musicVolume, sfxVolume, speed,
                    ProductInputBindings.Create(bindingValues), locale, textScale,
                    highContrast, reducedMotion);
            }
            catch (ArgumentException exception)
            {
                throw new ProductSettingsFormatException(
                    "Product settings contain an unsupported value.", exception);
            }
        }

        private static void Append(StringBuilder builder, string key, string value)
        {
            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
                throw new ProductSettingsFormatException(
                    "Product settings values cannot contain line breaks.");
            builder.Append(key).Append('=').Append(value).Append('\n');
        }

        private static string Value(string[] lines, int index, string key)
        {
            string prefix = key + "=";
            string line = lines[index];
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                throw new ProductSettingsFormatException(
                    "Product settings field order or name is invalid.");
            return line.Substring(prefix.Length);
        }

        private static int ParseInteger(string value, string field)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                    out int parsed) ||
                !string.Equals(parsed.ToString(CultureInfo.InvariantCulture), value,
                    StringComparison.Ordinal))
                throw new ProductSettingsFormatException(
                    "Product settings " + field + " is invalid.");
            return parsed;
        }

        private static bool ParseBoolean(string value, string field)
        {
            if (value == "true") return true;
            if (value == "false") return false;
            throw new ProductSettingsFormatException(
                "Product settings " + field + " is invalid.");
        }

        private static ProductDisplayMode ParseDisplayMode(string value)
        {
            return value switch
            {
                "windowed" => ProductDisplayMode.Windowed,
                "borderless_fullscreen" => ProductDisplayMode.BorderlessFullscreen,
                _ => throw new ProductSettingsFormatException(
                    "Product settings display mode is invalid.")
            };
        }

        private static ProductPresentationSpeed ParseSpeed(string value)
        {
            return value switch
            {
                "reduced" => ProductPresentationSpeed.Reduced,
                "normal" => ProductPresentationSpeed.Normal,
                "fast" => ProductPresentationSpeed.Fast,
                _ => throw new ProductSettingsFormatException(
                    "Product settings presentation speed is invalid.")
            };
        }

        private static string DisplayModeText(ProductDisplayMode value)
        {
            return value switch
            {
                ProductDisplayMode.Windowed => "windowed",
                ProductDisplayMode.BorderlessFullscreen => "borderless_fullscreen",
                _ => throw new ProductSettingsFormatException(
                    "Product settings display mode is invalid.")
            };
        }

        private static string SpeedText(ProductPresentationSpeed value)
        {
            return value switch
            {
                ProductPresentationSpeed.Reduced => "reduced",
                ProductPresentationSpeed.Normal => "normal",
                ProductPresentationSpeed.Fast => "fast",
                _ => throw new ProductSettingsFormatException(
                    "Product settings presentation speed is invalid.")
            };
        }

        private static string BooleanText(bool value) => value ? "true" : "false";
    }

    public sealed class FileProductSettingsStore : IProductSettingsStore
    {
        private readonly string root;
        private readonly ProductSettings defaults;
        private readonly IProductSettingsValidator? validator;

        public string SettingsPath { get; }
        public string BackupPath => SettingsPath + ".bak";
        public string InvalidArchivePath => SettingsPath + ".invalid";

        public FileProductSettingsStore(string persistentDataPath,
            string? uiCultureName = null, IProductSettingsValidator? validator = null)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
                throw new ArgumentException("Persistent data path cannot be empty.",
                    nameof(persistentDataPath));
            root = Path.GetFullPath(Path.Combine(persistentDataPath, "TrumpGameLab"));
            SettingsPath = Path.Combine(root, "settings.v1");
            defaults = ProductSettings.CreateDefaults(uiCultureName);
            this.validator = validator;
        }

        public ProductSettingsLoadResult Load()
        {
            try
            {
                if (!Directory.Exists(root))
                {
                    if (File.Exists(root))
                        return Invalid("Product settings directory is not a directory.");
                    return Missing();
                }
                ValidateDirectory();
                if (!File.Exists(SettingsPath))
                {
                    if (Directory.Exists(SettingsPath))
                        return Invalid("Product settings path is not a regular file.");
                    return Missing();
                }
                ValidateRegularSettingsFile(SettingsPath);
                ProductSettings loaded = ProductSettingsCodec.Decode(
                    ReadBoundedSettingsFile(SettingsPath));
                if (!TryValidate(loaded, out string validationError))
                    return Invalid(validationError);
                return new ProductSettingsLoadResult(ProductSettingsLoadStatus.Loaded,
                    loaded, null);
            }
            catch (Exception exception) when (IsRecoverableFileFailure(exception))
            {
                return Invalid(exception.Message);
            }
        }

        public ProductSettingsSaveResult Save(ProductSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!TryValidate(settings, out string validationError))
                return ProductSettingsSaveResult.Failure(validationError);
            string? temporary = null;
            try
            {
                EnsureDirectory();
                if (Directory.Exists(SettingsPath))
                    throw new IOException("Product settings path is not a regular file.");
                if (File.Exists(SettingsPath) &&
                    (File.GetAttributes(SettingsPath) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Product settings cannot be a reparse point.");

                byte[] encoded = ProductSettingsCodec.Encode(settings);
                ProductSettings decoded = ProductSettingsCodec.Decode(encoded);
                if (!decoded.Equals(settings))
                    throw new IOException("Encoded product settings verification failed.");

                temporary = Path.Combine(root,
                    "settings." + Guid.NewGuid().ToString("N") + ".tmp");
                WriteAndFlush(temporary, encoded);
                ValidateRegularSettingsFile(temporary);
                byte[] temporaryBytes = ReadBoundedSettingsFile(temporary);
                ProductSettings verified = ProductSettingsCodec.Decode(temporaryBytes);
                if (!verified.Equals(settings) || !temporaryBytes.SequenceEqual(encoded))
                    throw new IOException("Temporary product settings verification failed.");

                bool targetExists = File.Exists(SettingsPath);
                bool targetIsInvalid = targetExists && !ExistingSettingsAreValid();
                string? invalidArchive = null;
                if (targetIsInvalid)
                {
                    invalidArchive = NextAvailablePath(InvalidArchivePath);
                    CopyAndFlush(SettingsPath, invalidArchive);
                }

                string? backup = null;
                if (targetExists)
                {
                    backup = NextAvailablePath(BackupPath);
                    File.Replace(temporary, SettingsPath, backup,
                        ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temporary, SettingsPath);
                }
                temporary = null;
                return ProductSettingsSaveResult.Success(invalidArchive, backup);
            }
            catch (Exception exception) when (IsRecoverableFileFailure(exception))
            {
                return ProductSettingsSaveResult.Failure(exception.Message);
            }
            finally
            {
                if (temporary != null && File.Exists(temporary))
                {
                    try
                    {
                        File.Delete(temporary);
                    }
                    catch (IOException)
                    {
                        // A failed save is already reported; the unique settings temp is harmless.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // A failed save is already reported; the unique settings temp is harmless.
                    }
                }
            }
        }

        public ProductSettingsSaveResult Reset(ProductSettings defaultsToSave)
        {
            if (defaultsToSave == null)
                throw new ArgumentNullException(nameof(defaultsToSave));
            return Save(defaultsToSave);
        }

        private bool ExistingSettingsAreValid()
        {
            try
            {
                ValidateRegularSettingsFile(SettingsPath);
                ProductSettings existing = ProductSettingsCodec.Decode(
                    ReadBoundedSettingsFile(SettingsPath));
                return TryValidate(existing, out _);
            }
            catch (Exception exception) when (IsRecoverableFileFailure(exception))
            {
                return false;
            }
        }

        private ProductSettingsLoadResult Missing() =>
            new ProductSettingsLoadResult(ProductSettingsLoadStatus.Missing, defaults, null);

        private ProductSettingsLoadResult Invalid(string error) =>
            new ProductSettingsLoadResult(ProductSettingsLoadStatus.Invalid, defaults, error);

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
                error = string.IsNullOrWhiteSpace(exception.Message)
                    ? exception.GetType().Name
                    : exception.Message;
                return false;
            }
        }

        private void EnsureDirectory()
        {
            Directory.CreateDirectory(root);
            ValidateDirectory();
        }

        private void ValidateDirectory()
        {
            var directory = new DirectoryInfo(root);
            if (!directory.Exists)
                throw new DirectoryNotFoundException(
                    "Product settings directory does not exist.");
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException(
                    "Product settings directory cannot be a reparse point.");
        }

        private static void ValidateRegularSettingsFile(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists)
                throw new FileNotFoundException("Product settings do not exist.");
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Product settings cannot be a reparse point.");
            if (file.Length <= 0 || file.Length > ProductSettingsCodec.MaximumEncodedBytes)
                throw new ProductSettingsFormatException(
                    "Product settings size is invalid.");
        }

        private static void WriteAndFlush(string path, byte[] payload)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough);
            stream.Write(payload, 0, payload.Length);
            stream.Flush(true);
        }

        private static byte[] ReadBoundedSettingsFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, FileOptions.SequentialScan);
            if (stream.Length <= 0 || stream.Length > ProductSettingsCodec.MaximumEncodedBytes)
                throw new ProductSettingsFormatException(
                    "Product settings size is invalid.");
            var payload = new byte[(int)stream.Length];
            int offset = 0;
            while (offset < payload.Length)
            {
                int read = stream.Read(payload, offset, payload.Length - offset);
                if (read == 0)
                    throw new EndOfStreamException("Product settings were truncated while reading.");
                offset += read;
            }
            if (stream.ReadByte() != -1)
                throw new ProductSettingsFormatException(
                    "Product settings changed size while reading.");
            return payload;
        }

        private static void CopyAndFlush(string source, string destination)
        {
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, FileOptions.SequentialScan);
            using var output = new FileStream(destination, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            input.CopyTo(output);
            output.Flush(true);
            if (input.Length != output.Length)
                throw new IOException("Invalid product settings archive verification failed.");
        }

        private static string NextAvailablePath(string basePath)
        {
            if (!File.Exists(basePath) && !Directory.Exists(basePath)) return basePath;
            for (int suffix = 1; suffix < 10000; suffix++)
            {
                string candidate = basePath + "." +
                    suffix.ToString(CultureInfo.InvariantCulture);
                if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            }
            throw new IOException("No product settings archive name is available.");
        }

        private static bool IsRecoverableFileFailure(Exception exception) =>
            exception is ProductSettingsFormatException ||
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is NotSupportedException;
    }
}
