#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace TrumpLab.Product.Tests
{
    public sealed class SessionStoreHardeningTests
    {
        private string temporaryRoot = null!;
        private FileSessionStore store = null!;
        private string storeDirectory = null!;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(),
                "TrumpLab-T05-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            store = new FileSessionStore(temporaryRoot);
            storeDirectory = Path.Combine(temporaryRoot, "TrumpGameLab", "Sessions");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryRoot))
                Directory.Delete(temporaryRoot, recursive: true);
        }

        [Test]
        public void LoadClassifiesTruncatedOldAndTamperedArchivesWithoutChangingThem()
        {
            SessionArchive archive = CreateArchive(actions: 2);
            string slotId = SessionSlotIds.Create();
            store.Save(slotId, archive);
            string path = SlotPath(slotId);
            byte[] canonical = File.ReadAllBytes(path);
            string json = Encoding.UTF8.GetString(canonical);

            AssertRejectedUnchanged(slotId, canonical.Take(canonical.Length / 2).ToArray(),
                typeof(SessionFormatException));
            AssertRejectedUnchanged(slotId,
                Encoding.UTF8.GetBytes(json.Replace(
                    "\"format_version\":1", "\"format_version\":0")),
                typeof(UnsupportedSessionVersionException));
            AssertRejectedUnchanged(slotId,
                Encoding.UTF8.GetBytes(json.Replace("\"seed\":\"59\"", "\"seed\":\"58\"")),
                typeof(SessionIntegrityException));
        }

        [Test]
        public void FailedValidatedSavePreservesExistingSlotAndRemovesItsTemporaryFile()
        {
            SessionArchive archive = CreateArchive(actions: 2);
            string slotId = SessionSlotIds.Create();
            store.Save(slotId, archive);
            byte[] expected = File.ReadAllBytes(SlotPath(slotId));

            var unsupported = new SessionArchive(archive.Configuration, archive.Actions,
                formatVersion: SessionArchive.CurrentFormatVersion + 1);
            Assert.Throws<UnsupportedSessionVersionException>(() => store.Save(slotId, unsupported));
            Assert.That(File.ReadAllBytes(SlotPath(slotId)), Is.EqualTo(expected));
            Assert.That(Directory.GetFiles(storeDirectory, "*.tmp"), Is.Empty);

            SessionActionRecord first = archive.Actions[0];
            var divergent = new SessionArchive(archive.Configuration, new[]
            {
                new SessionActionRecord(first.Actor, first.Action, first.TurnAfter + 1,
                    first.CurrentPlayerAfter, first.TerminalAfter)
            });
            Assert.Throws<ReplayDivergedException>(() => store.Save(slotId, divergent));
            Assert.That(File.ReadAllBytes(SlotPath(slotId)), Is.EqualTo(expected));
            Assert.That(store.Load(slotId).Actions.Count, Is.EqualTo(archive.Actions.Count));
        }

        [Test]
        public void ListIgnoresAndCleansOnlyCanonicalIncompleteTemporaryFiles()
        {
            Assert.That(store.List(), Is.Empty);
            string canonicalTemp = Path.Combine(storeDirectory, SessionSlotIds.Create() + ".tmp");
            string foreignTemp = Path.Combine(storeDirectory, "manual-recovery.tmp");
            string uppercaseTemp = Path.Combine(storeDirectory,
                SessionSlotIds.Create().ToUpperInvariant() + ".tmp");
            File.WriteAllText(canonicalTemp, "incomplete");
            File.WriteAllText(foreignTemp, "keep");
            File.WriteAllText(uppercaseTemp, "keep");

            Assert.That(store.List(), Is.Empty);
            Assert.That(File.Exists(canonicalTemp), Is.False);
            Assert.That(File.ReadAllText(foreignTemp), Is.EqualTo("keep"));
            Assert.That(File.ReadAllText(uppercaseTemp), Is.EqualTo("keep"));
        }

        [Test]
        public void RepeatedAtomicReplacementKeepsLatestSlotAndPreviousBackup()
        {
            string slotId = SessionSlotIds.Create();
            SessionArchive first = CreateArchive(actions: 0);
            SessionArchive second = CreateArchive(actions: 1);
            SessionArchive third = CreateArchive(actions: 2);

            store.Save(slotId, first);
            store.Save(slotId, second);
            store.Save(slotId, third);

            Assert.That(store.Load(slotId).Actions.Count, Is.EqualTo(2));
            string backupPath = SlotPath(slotId) + ".bak";
            Assert.That(File.Exists(backupPath), Is.True);
            SessionArchive backup = SessionArchiveCodec.Decode(File.ReadAllBytes(backupPath));
            Assert.That(backup.Actions.Count, Is.EqualTo(1));
            SessionReplayer.Replay(backup);
        }

        private void AssertRejectedUnchanged(string slotId, byte[] bytes, Type exceptionType)
        {
            string path = SlotPath(slotId);
            File.WriteAllBytes(path, bytes);
            byte[] before = File.ReadAllBytes(path);
            Assert.That(() => store.Load(slotId), Throws.TypeOf(exceptionType));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        private string SlotPath(string slotId) => Path.Combine(storeDirectory, slotId + ".tgs");

        private static SessionArchive CreateArchive(int actions)
        {
            var configuration = new SessionConfiguration(
                "crazy_eights", 2, seed: 59, difficulty: 1, humanPlayers: new[] { 0 },
                options: new Dictionary<string, string> { ["wild_rank"] = "8" });
            var recorder = new SessionRecorder(configuration);
            for (int index = 0; index < actions && !recorder.Game.IsTerminal; index++)
            {
                if (recorder.Game.CurrentPlayer == 0)
                    recorder.ApplyHumanAction(0, recorder.Game.LegalActions(0)[0]);
                else
                    recorder.ApplyCpuAction();
            }
            return recorder.Archive;
        }
    }
}
