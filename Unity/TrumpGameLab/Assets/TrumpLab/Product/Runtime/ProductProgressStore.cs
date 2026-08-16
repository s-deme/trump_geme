#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace TrumpLab.Product
{
    public sealed class ProductProgressFormatException : Exception
    {
        public ProductProgressFormatException(string message) : base(message) { }
    }

    public sealed class ProductProgress
    {
        public const int CurrentFormatVersion = 1;
        public static ProductProgress Empty { get; } = new ProductProgress();

        public int FormatVersion { get; }
        public string? TutorialId { get; }
        public int TutorialVersion { get; }
        public bool TutorialCompleted { get; }

        private ProductProgress()
        {
            FormatVersion = CurrentFormatVersion;
        }

        public ProductProgress(int formatVersion, string tutorialId,
            int tutorialVersion, bool tutorialCompleted)
        {
            if (formatVersion != CurrentFormatVersion)
                throw new ArgumentOutOfRangeException(nameof(formatVersion));
            if (!ValidIdentifier(tutorialId))
                throw new ArgumentException("Tutorial ID is invalid.", nameof(tutorialId));
            if (tutorialVersion <= 0) throw new ArgumentOutOfRangeException(nameof(tutorialVersion));
            FormatVersion = formatVersion;
            TutorialId = tutorialId;
            TutorialVersion = tutorialVersion;
            TutorialCompleted = tutorialCompleted;
        }

        public bool IsTutorialCompleted(TutorialDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return TutorialCompleted && TutorialId == definition.Id &&
                TutorialVersion == definition.Version;
        }

        internal static bool ValidIdentifier(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > 64) return false;
            foreach (char character in value)
            {
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') && character != '_') return false;
            }
            return true;
        }
    }

    public interface IProductProgressStore
    {
        ProductProgress Load();
        void SaveTutorialCompleted(TutorialDefinition definition);
    }

    public sealed class FileProductProgressStore : IProductProgressStore
    {
        private const int MaximumBytes = 4096;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly string root;

        public string ProgressPath => Path.Combine(root, "progress.v1");

        public FileProductProgressStore(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
                throw new ArgumentException("Persistent data path cannot be empty.",
                    nameof(persistentDataPath));
            root = Path.GetFullPath(Path.Combine(persistentDataPath, "TrumpGameLab"));
        }

        public ProductProgress Load()
        {
            EnsureRoot();
            if (!File.Exists(ProgressPath)) return ProductProgress.Empty;
            ValidateRegularFile(ProgressPath);
            return Decode(File.ReadAllBytes(ProgressPath));
        }

        public void SaveTutorialCompleted(TutorialDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            EnsureRoot();
            if (File.Exists(ProgressPath))
            {
                ValidateRegularFile(ProgressPath);
                ProductProgress existing = Decode(File.ReadAllBytes(ProgressPath));
                if (existing.IsTutorialCompleted(definition)) return;
            }

            var progress = new ProductProgress(
                ProductProgress.CurrentFormatVersion, definition.Id, definition.Version,
                tutorialCompleted: true);
            byte[] encoded = Encode(progress);
            string temporary = Path.Combine(root, Guid.NewGuid().ToString("N") + ".tmp");
            string backup = ProgressPath + ".bak";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(encoded, 0, encoded.Length);
                    stream.Flush(true);
                }
                ValidateRegularFile(temporary);
                ProductProgress verified = Decode(File.ReadAllBytes(temporary));
                if (!verified.IsTutorialCompleted(definition))
                    throw new IOException("Temporary product progress verification failed.");

                if (File.Exists(ProgressPath))
                    File.Replace(temporary, ProgressPath, backup, ignoreMetadataErrors: true);
                else
                    File.Move(temporary, ProgressPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private void EnsureRoot()
        {
            Directory.CreateDirectory(root);
            var directory = new DirectoryInfo(root);
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Product progress directory cannot be a reparse point.");
        }

        private static void ValidateRegularFile(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists) throw new FileNotFoundException("Product progress does not exist.");
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Product progress cannot be a reparse point.");
            if (file.Length <= 0 || file.Length > MaximumBytes)
                throw new ProductProgressFormatException("Product progress size is invalid.");
        }

        private static byte[] Encode(ProductProgress progress)
        {
            string payload = "format=" + progress.FormatVersion + "\n" +
                "tutorial_id=" + progress.TutorialId + "\n" +
                "tutorial_version=" + progress.TutorialVersion + "\n" +
                "completed=" + (progress.TutorialCompleted ? "true" : "false") + "\n";
            return StrictUtf8.GetBytes(payload);
        }

        private static ProductProgress Decode(byte[] payload)
        {
            if (payload == null || payload.Length <= 0 || payload.Length > MaximumBytes)
                throw new ProductProgressFormatException("Product progress size is invalid.");
            string text;
            try
            {
                text = StrictUtf8.GetString(payload);
            }
            catch (DecoderFallbackException exception)
            {
                throw new ProductProgressFormatException(
                    "Product progress is not valid UTF-8: " + exception.Message);
            }
            string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
            if (lines.Length != 5 || lines[4].Length != 0 ||
                !lines[0].StartsWith("format=", StringComparison.Ordinal) ||
                !lines[1].StartsWith("tutorial_id=", StringComparison.Ordinal) ||
                !lines[2].StartsWith("tutorial_version=", StringComparison.Ordinal) ||
                !lines[3].StartsWith("completed=", StringComparison.Ordinal))
                throw new ProductProgressFormatException("Product progress fields are invalid.");
            if (!int.TryParse(lines[0].Substring("format=".Length), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int format) ||
                format != ProductProgress.CurrentFormatVersion)
                throw new ProductProgressFormatException("Product progress version is unsupported.");
            string tutorialId = lines[1].Substring("tutorial_id=".Length);
            if (!ProductProgress.ValidIdentifier(tutorialId))
                throw new ProductProgressFormatException("Product progress tutorial ID is invalid.");
            if (!int.TryParse(lines[2].Substring("tutorial_version=".Length), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int tutorialVersion) || tutorialVersion <= 0)
                throw new ProductProgressFormatException(
                    "Product progress tutorial version is invalid.");
            string completedText = lines[3].Substring("completed=".Length);
            if (completedText != "true" && completedText != "false")
                throw new ProductProgressFormatException(
                    "Product progress completion flag is invalid.");
            return new ProductProgress(format, tutorialId, tutorialVersion,
                completedText == "true");
        }
    }
}
