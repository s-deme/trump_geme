#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TrumpLab.Product
{
    public sealed class SessionSlotInfo
    {
        public string Id { get; }
        public DateTime SavedAtUtc { get; }

        public SessionSlotInfo(string id, DateTime savedAtUtc)
        {
            Id = SessionSlotIds.Require(id);
            SavedAtUtc = savedAtUtc.Kind == DateTimeKind.Utc
                ? savedAtUtc
                : savedAtUtc.ToUniversalTime();
        }
    }

    public interface ISessionStore
    {
        IReadOnlyList<SessionSlotInfo> List();
        SessionArchive Load(string slotId);
        void Save(string slotId, SessionArchive archive);
        void Delete(string slotId);
    }

    public static class SessionSlotIds
    {
        public static string Create() => Guid.NewGuid().ToString("N");

        public static string Require(string value)
        {
            if (value == null || value.Length != 32 ||
                !Guid.TryParseExact(value, "N", out Guid parsed) ||
                !string.Equals(parsed.ToString("N"), value, StringComparison.Ordinal))
                throw new ArgumentException("Session slot ID is invalid.", nameof(value));
            return value;
        }
    }

    public sealed class FileSessionStore : ISessionStore
    {
        private const long MaximumArchiveBytes = 1024L * 1024L;
        private readonly string root;

        public FileSessionStore(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
                throw new ArgumentException("Persistent data path cannot be empty.",
                    nameof(persistentDataPath));
            root = Path.GetFullPath(Path.Combine(
                persistentDataPath, "TrumpGameLab", "Sessions"));
        }

        public IReadOnlyList<SessionSlotInfo> List()
        {
            EnsureRoot();
            CleanupTemporaryFiles();
            var slots = new List<SessionSlotInfo>();
            foreach (string path in Directory.GetFiles(root, "*.tgs", SearchOption.TopDirectoryOnly))
            {
                var file = new FileInfo(path);
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                string id = Path.GetFileNameWithoutExtension(file.Name);
                try
                {
                    slots.Add(new SessionSlotInfo(id, file.LastWriteTimeUtc));
                }
                catch (ArgumentException)
                {
                    // Files that are not Product-generated GUID slots are never exposed to the UI.
                }
            }
            return Array.AsReadOnly(slots
                .OrderByDescending(slot => slot.SavedAtUtc)
                .ThenBy(slot => slot.Id, StringComparer.Ordinal)
                .ToArray());
        }

        public SessionArchive Load(string slotId)
        {
            EnsureRoot();
            string path = SlotPath(slotId);
            var file = new FileInfo(path);
            if (!file.Exists) throw new FileNotFoundException("Session slot does not exist.");
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Session slot cannot be a reparse point.");
            if (file.Length <= 0 || file.Length > MaximumArchiveBytes)
                throw new SessionFormatException("Session archive size is invalid.");
            SessionArchive archive = SessionArchiveCodec.Decode(File.ReadAllBytes(path));
            SessionReplayer.Replay(archive);
            return archive;
        }

        public void Save(string slotId, SessionArchive archive)
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            EnsureRoot();
            string target = SlotPath(slotId);
            string backup = target + ".bak";
            string temporary = Path.Combine(root, SessionSlotIds.Create() + ".tmp");
            byte[] encoded = SessionArchiveCodec.Encode(archive);

            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(encoded, 0, encoded.Length);
                    stream.Flush(true);
                }

                var temporaryFile = new FileInfo(temporary);
                if (temporaryFile.Length != encoded.Length || temporaryFile.Length > MaximumArchiveBytes)
                    throw new IOException("Temporary session verification failed.");
                SessionArchive verified = SessionArchiveCodec.Decode(File.ReadAllBytes(temporary));
                SessionReplayer.Replay(verified);

                if (File.Exists(target))
                    File.Replace(temporary, target, backup, ignoreMetadataErrors: true);
                else
                    File.Move(temporary, target);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public void Delete(string slotId)
        {
            EnsureRoot();
            string target = SlotPath(slotId);
            if (!File.Exists(target)) throw new FileNotFoundException("Session slot does not exist.");
            var file = new FileInfo(target);
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Session slot cannot be a reparse point.");
            File.Delete(target);
            string backup = target + ".bak";
            if (File.Exists(backup)) File.Delete(backup);
        }

        private string SlotPath(string slotId)
        {
            string id = SessionSlotIds.Require(slotId);
            string path = Path.GetFullPath(Path.Combine(root, id + ".tgs"));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Session slot is outside the store.", nameof(slotId));
            return path;
        }

        private void EnsureRoot()
        {
            Directory.CreateDirectory(root);
            var directory = new DirectoryInfo(root);
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Session directory cannot be a reparse point.");
        }

        private void CleanupTemporaryFiles()
        {
            foreach (string path in Directory.GetFiles(root, "*.tmp", SearchOption.TopDirectoryOnly))
            {
                var file = new FileInfo(path);
                string id = Path.GetFileNameWithoutExtension(file.Name);
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                try
                {
                    SessionSlotIds.Require(id);
                    file.Delete();
                }
                catch (ArgumentException)
                {
                    // Only canonical Product-generated temp names are safe to remove.
                }
            }
        }
    }
}
