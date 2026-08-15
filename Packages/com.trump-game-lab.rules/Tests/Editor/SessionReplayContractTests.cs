using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class SessionReplayContractTests
    {
        [Test]
        public void CrazyEightsRecorderReplaysEveryStructuredCheckpoint()
        {
            var configuration = new SessionConfiguration(
                "crazy_eights", players: 2, seed: 3101, difficulty: 1,
                humanPlayers: new[] { 0 },
                options: new Dictionary<string, string> { ["wild_rank"] = "8" });
            var recorder = new SessionRecorder(configuration);
            var expected = new List<string>
            {
                PresentationSignature(((IGamePresentationProvider)recorder.Game).Present(0))
            };
            int humanActions = 0;
            int cpuActions = 0;

            for (int step = 0; step < 1000 && !recorder.Game.IsTerminal; step++)
            {
                int actor = recorder.Game.CurrentPlayer;
                if (actor == 0)
                {
                    Action selected = recorder.Game.LegalActions(actor)[0];
                    recorder.ApplyHumanAction(actor, selected);
                    humanActions++;
                }
                else
                {
                    recorder.ApplyCpuAction();
                    cpuActions++;
                }
                expected.Add(PresentationSignature(
                    ((IGamePresentationProvider)recorder.Game).Present(0)));
            }

            SessionArchive archive = recorder.Archive;
            SessionReplayResult replay = SessionReplayer.Replay(archive, viewer: 0);

            Assert.That(recorder.Game.IsTerminal, Is.True);
            Assert.That(humanActions, Is.GreaterThan(0));
            Assert.That(cpuActions, Is.GreaterThan(0));
            Assert.That(archive.Actions, Has.Count.EqualTo(expected.Count - 1));
            Assert.That(replay.Checkpoints.Select(checkpoint =>
                    PresentationSignature(checkpoint.Presentation!)),
                Is.EqualTo(expected));
            Assert.That(replay.Game.Result().Winners, Is.EqualTo(recorder.Game.Result().Winners));
            Assert.That(replay.Game.Result().Scores, Is.EqualTo(recorder.Game.Result().Scores));
            Assert.That(replay.Game.Result().Turns, Is.EqualTo(recorder.Game.Result().Turns));
            Assert.That(archive.Actions.All(record =>
                record.TurnAfter >= 1 && record.CurrentPlayerAfter >= 0), Is.True);
        }

        [Test]
        public void ResumeAdvancesCpuRandomToTheRecordedPosition()
        {
            GameRegistry registry = RandomCpuRegistry();
            var configuration = new SessionConfiguration(
                "random_cpu_test", players: 1, seed: 77, difficulty: 1,
                humanPlayers: Array.Empty<int>());
            var original = new SessionRecorder(configuration, registry);
            original.ApplyCpuAction();
            original.ApplyCpuAction();

            SessionRecorder resumed = SessionRecorder.Resume(original.Archive, registry);
            while (!original.Game.IsTerminal)
            {
                Action expected = original.ApplyCpuAction();
                Action actual = resumed.ApplyCpuAction();
                Assert.That(actual, Is.EqualTo(expected));
            }

            Assert.That(resumed.Game.IsTerminal, Is.True);
            Assert.That(resumed.Archive.Actions.Select(record => record.Action),
                Is.EqualTo(original.Archive.Actions.Select(record => record.Action)));
        }

        [Test]
        public void ReplayRejectsCpuChoiceCheckpointAndVersionDivergence()
        {
            GameRegistry registry = RandomCpuRegistry();
            var configuration = new SessionConfiguration(
                "random_cpu_test", 1, seed: 91, difficulty: 1,
                humanPlayers: Array.Empty<int>());
            var recorder = new SessionRecorder(configuration, registry);
            recorder.ApplyCpuAction();
            SessionArchive archive = recorder.Archive;
            SessionActionRecord recorded = archive.Actions[0];
            string differentValue = recorded.Action.Value == "0" ? "1" : "0";
            var alteredAction = new SessionActionRecord(
                recorded.Actor, new Action("choose", value: differentValue),
                recorded.TurnAfter, recorded.CurrentPlayerAfter, recorded.TerminalAfter);
            var altered = new SessionArchive(configuration, new[] { alteredAction });
            var badCheckpoint = new SessionArchive(configuration, new[]
            {
                new SessionActionRecord(recorded.Actor, recorded.Action,
                    recorded.TurnAfter + 1, recorded.CurrentPlayerAfter, recorded.TerminalAfter)
            });
            var futureFormat = new SessionArchive(configuration, archive.Actions, formatVersion: 2);
            var futureRules = new SessionArchive(
                configuration, archive.Actions, rulesVersion: 2);

            ReplayDivergedException cpuError = Assert.Throws<ReplayDivergedException>(
                () => SessionReplayer.Replay(altered, registry: registry))!;
            Assert.That(cpuError.ActionIndex, Is.Zero);
            Assert.Throws<ReplayDivergedException>(
                () => SessionReplayer.Replay(badCheckpoint, registry: registry));
            Assert.Throws<UnsupportedSessionVersionException>(
                () => SessionReplayer.Replay(futureFormat, registry: registry));
            Assert.Throws<UnsupportedSessionVersionException>(
                () => SessionReplayer.Replay(futureRules, registry: registry));
        }

        [Test]
        public void ArchiveCopiesAndCanonicalizesConfigurationAndTypedActionFields()
        {
            var options = new Dictionary<string, string>
            {
                ["z_option"] = "last",
                ["a_option"] = "first"
            };
            var humans = new[] { 1, 0 };
            var configuration = new SessionConfiguration(
                "crazy_eights", 2, long.MinValue, 3, humans, options);
            var action = new Action("play", new Card(Suit.Hearts, 8), target: 1, value: "S");
            var archive = new SessionArchive(configuration, new[]
            {
                new SessionActionRecord(0, action, 1, 1, terminalAfter: false)
            });
            options["a_option"] = "changed";
            humans[0] = 0;

            Assert.That(configuration.Options.Keys, Is.EqualTo(new[] { "a_option", "z_option" }));
            Assert.That(configuration.Options["a_option"], Is.EqualTo("first"));
            Assert.That(configuration.HumanPlayers, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(archive.Actions[0].Action.Kind, Is.EqualTo("play"));
            Assert.That(archive.Actions[0].Action.Card, Is.EqualTo(new Card(Suit.Hearts, 8)));
            Assert.That(archive.Actions[0].Action.Target, Is.EqualTo(1));
            Assert.That(archive.Actions[0].Action.Value, Is.EqualTo("S"));
        }

        [Test]
        public void CodecResumesCrazyEightsAcrossStarterPlayAndTerminalPhases()
        {
            long seed = FindStarterSuitSeed();
            var configuration = new SessionConfiguration(
                "crazy_eights", 2, seed, 1, new[] { 0 },
                new Dictionary<string, string> { ["wild_rank"] = "8" });
            var recorder = new SessionRecorder(configuration);
            Assert.That(((IGamePresentationProvider)recorder.Game).Present(0).Phase,
                Is.EqualTo("choose_starter_suit"));
            AssertCodecResume(recorder);

            recorder.ApplyCpuAction();
            Assert.That(((IGamePresentationProvider)recorder.Game).Present(0).Phase,
                Is.EqualTo("play"));
            for (int index = 0; index < 8 && !recorder.Game.IsTerminal; index++)
                ApplyNext(recorder);
            AssertCodecResume(recorder);

            for (int index = 0; index < 1000 && !recorder.Game.IsTerminal; index++)
                ApplyNext(recorder);
            Assert.That(recorder.Game.IsTerminal, Is.True);
            AssertCodecResume(recorder);
        }

        [Test]
        public void CodecIsCanonicalAndRejectsDigestOrVersionChanges()
        {
            var configuration = new SessionConfiguration(
                "crazy_eights", 2, seed: -9, difficulty: 1, humanPlayers: new[] { 0 },
                options: new Dictionary<string, string> { ["wild_rank"] = "8" });
            var recorder = new SessionRecorder(configuration);
            if (recorder.Game.CurrentPlayer == 0)
                recorder.ApplyHumanAction(0, recorder.Game.LegalActions(0)[0]);
            else
                recorder.ApplyCpuAction();
            byte[] first = SessionArchiveCodec.Encode(recorder.Archive);
            byte[] second = SessionArchiveCodec.Encode(recorder.Archive);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(Encoding.UTF8.GetString(first), Does.StartWith(
                "{\"format\":\"trumplab_session\",\"format_version\":1"));

            byte[] corrupted = (byte[])first.Clone();
            string json = Encoding.UTF8.GetString(corrupted);
            int digest = json.IndexOf("\"digest\":\"", StringComparison.Ordinal) + 10;
            Assert.That(digest, Is.GreaterThan(9));
            corrupted[digest] = corrupted[digest] == (byte)'0' ? (byte)'1' : (byte)'0';
            Assert.Throws<SessionIntegrityException>(() => SessionArchiveCodec.Decode(corrupted));

            var future = new SessionArchive(configuration, recorder.Archive.Actions,
                formatVersion: 2);
            Assert.Throws<UnsupportedSessionVersionException>(
                () => SessionArchiveCodec.Decode(SessionArchiveCodec.Encode(future)));
        }

        private static string PresentationSignature(GamePresentation presentation)
        {
            IEnumerable<string> players = presentation.Players.Select(player =>
                player.PlayerIndex + ":" + player.IsCurrent + ":" + player.IsViewer);
            IEnumerable<string> zones = presentation.CardZones.Select(zone =>
                zone.Id + ":" + zone.Role + ":" + zone.OwnerPlayer + ":" +
                zone.Visibility + ":" + zone.Count + ":" + string.Join(",", zone.Cards.Select(card =>
                    ((int)card.Suit) + "-" + card.Rank)));
            IEnumerable<string> fields = presentation.Fields.Select(field =>
                field.Id + ":" + PresentationValueSignature(field.Value));
            IEnumerable<string> actions = presentation.Actions.Select(action =>
                action.Id + ":" + ActionSignature(action.Action));
            string result = presentation.Result == null ? "-" :
                string.Join(",", presentation.Result.Winners) + ":" +
                string.Join(",", presentation.Result.Scores) + ":" +
                presentation.Result.Reason + ":" + presentation.Result.Turns;
            return presentation.GameId + "|" + presentation.Phase + "|" +
                presentation.Viewer + "|" + presentation.CurrentPlayer + "|" +
                presentation.TurnCount + "|" + presentation.IsTerminal + "|" +
                string.Join(";", players) + "|" + string.Join(";", zones) + "|" +
                string.Join(";", fields) + "|" + string.Join(";", actions) + "|" + result;
        }

        private static string ActionSignature(Action action) =>
            action.Kind + ":" +
            (action.Card.HasValue
                ? ((int)action.Card.Value.Suit) + "-" + action.Card.Value.Rank
                : "-") + ":" + action.Target + ":" + action.Value;

        private static string PresentationValueSignature(PresentationValue value) =>
            value.Kind + ":" + value.TextValue + ":" + value.NumberValue + ":" +
            value.BooleanValue + ":" + value.SuitValue + ":" + value.PlayerValue + ":" +
            (value.CardValue.HasValue
                ? ((int)value.CardValue.Value.Suit) + "-" + value.CardValue.Value.Rank
                : "-");

        private static void AssertCodecResume(SessionRecorder recorder)
        {
            byte[] encoded = SessionArchiveCodec.Encode(recorder.Archive);
            SessionArchive decoded = SessionArchiveCodec.Decode(encoded);
            SessionRecorder resumed = SessionRecorder.Resume(decoded);
            GamePresentation expected =
                ((IGamePresentationProvider)recorder.Game).Present(0);
            GamePresentation actual =
                ((IGamePresentationProvider)resumed.Game).Present(0);
            Assert.That(PresentationSignature(actual), Is.EqualTo(PresentationSignature(expected)));
            Assert.That(decoded.Actions.Select(record => record.Action),
                Is.EqualTo(recorder.Archive.Actions.Select(record => record.Action)));
            if (!recorder.Game.IsTerminal)
                Assert.That(resumed.Game.LegalActions(), Is.EqualTo(recorder.Game.LegalActions()));
            else
                Assert.That(resumed.Game.Result().Scores, Is.EqualTo(recorder.Game.Result().Scores));
        }

        private static void ApplyNext(SessionRecorder recorder)
        {
            int actor = recorder.Game.CurrentPlayer;
            if (actor == 0)
                recorder.ApplyHumanAction(actor, recorder.Game.LegalActions(actor)[0]);
            else
                recorder.ApplyCpuAction();
        }

        private static long FindStarterSuitSeed()
        {
            for (long seed = 1; seed <= 10000; seed++)
            {
                IGame game = BuiltInGames.Registry.Create(
                    "crazy_eights", 2, seed,
                    new Dictionary<string, string> { ["wild_rank"] = "8" });
                if (((IGamePresentationProvider)game).Present(0).Phase == "choose_starter_suit")
                    return seed;
            }
            throw new AssertionException("Could not find a Crazy Eights starter-suit seed.");
        }

        private static GameRegistry RandomCpuRegistry()
        {
            var registry = new GameRegistry();
            registry.Register(
                new GameInfo("random_cpu_test", "Random CPU Test", 1, 1, "test", "test", "test"),
                (players, random, options) => new RandomCpuGame());
            return registry;
        }

        private sealed class RandomCpuGame : GameBase
        {
            private int steps;

            public RandomCpuGame()
            {
                Players = 1;
                CurrentPlayer = 0;
            }

            public override string GameId => "random_cpu_test";
            public override string Name => "Random CPU Test";
            public override IReadOnlyList<Action> LegalActions(int? player = null)
            {
                ValidateTurn(player);
                return new[]
                {
                    new Action("choose", value: "0"),
                    new Action("choose", value: "1"),
                    new Action("choose", value: "2")
                };
            }

            public override void Apply(Action action)
            {
                ValidateTurn(null);
                if (!LegalActions().Contains(action))
                    throw new InvalidOperationException("Illegal action.");
                steps++;
                TurnCount++;
            }

            public override Action ChooseCpuAction(
                int player, DeterministicRandom random, int difficulty = 1)
            {
                IReadOnlyList<Action> actions = LegalActions(player);
                return actions[random.Next(actions.Count)];
            }

            public override bool IsTerminal => steps >= 5;
            public override GameResult Result()
            {
                if (!IsTerminal) throw new InvalidOperationException("Game is not over.");
                return new GameResult(new[] { 0 }, new[] { 1d }, "five choices", TurnCount);
            }
            public override string View(int? player = null) => "steps=" + steps;
        }
    }
}
