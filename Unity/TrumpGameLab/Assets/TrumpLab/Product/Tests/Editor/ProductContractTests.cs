#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductContractTests
    {
        [Test]
        public void SettingsCreateValidatedImmutableRequest()
        {
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "-17", "8", CpuDifficulties.Hard,
                out GameStartRequest? request, out string error), Is.True, error);
            Assert.That(request, Is.EqualTo(new GameStartRequest(
                -17, 8, CpuDifficulties.Hard)));
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "-17", "8", out request, out error), Is.True, error);
            Assert.That(request!.Difficulty, Is.EqualTo(CpuDifficulties.Standard));
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "nope", "8", CpuDifficulties.Standard, out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "1", "0", CpuDifficulties.Standard, out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "1", "14", CpuDifficulties.Standard, out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(GameSettingsScreen.TryCreateRequest(
                "1", "8", 99, out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameStartRequest(1, 8, difficulty: 99));
        }

        [Test]
        public void MatchPresenterUsesStructuredActionsAndHidesOpponentCards()
        {
            IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed: 1);
            var provider = (IGamePresentationProvider)game;
            GamePresentation presentation = provider.Present(viewer: 0);
            MatchViewModel model = CrazyEightsMatchPresenter.Create(presentation, inputEnabled: true);

            Assert.That(model.InputEnabled, Is.EqualTo(presentation.CurrentPlayer == 0));
            Assert.That(model.Actions.Select(action => action.Id),
                Is.EqualTo(presentation.Actions.Select(action => action.Id)));
            Assert.That(model.Actions.All(action => !string.IsNullOrWhiteSpace(action.Label)), Is.True);
            Assert.That(model.OpponentHand, Does.StartWith("CPU hand: "));
            Assert.That(model.OpponentHand, Does.Not.Contain("♣"));
            Assert.That(model.OpponentHand, Does.Not.Contain("♦"));
            Assert.That(model.OpponentHand, Does.Not.Contain("♥"));
            Assert.That(model.OpponentHand, Does.Not.Contain("♠"));
        }

        [Test]
        public void SessionRejectsStaleInputAndCompletesWithHumanAndCpuActions()
        {
            var session = new GameSessionController(seed: 1);
            session.Begin();
            int humanActions = 0;
            int cpuActions = 0;
            for (int step = 0; step < 1000 && session.State != MatchSessionState.Finished; step++)
            {
                if (session.State == MatchSessionState.AwaitingHuman)
                {
                    string actionId = session.Snapshot.Actions[0].Id;
                    int turns = session.Game.TurnCount;
                    Assert.That(session.TryApplyHumanAction("not_current"), Is.False);
                    Assert.That(session.Game.TurnCount, Is.EqualTo(turns));
                    Assert.That(session.TryApplyHumanAction(actionId), Is.True);
                    Assert.That(session.TryApplyHumanAction(actionId), Is.False);
                    humanActions++;
                }
                else if (session.State == MatchSessionState.WaitingForCpu)
                {
                    Assert.That(session.TryApplyCpuAction(), Is.True);
                    cpuActions++;
                }
                else
                {
                    Assert.Fail("Unexpected session state: " + session.State);
                }
            }

            Assert.That(session.State, Is.EqualTo(MatchSessionState.Finished));
            Assert.That(session.Game.IsTerminal, Is.True);
            Assert.That(humanActions, Is.GreaterThan(0));
            Assert.That(cpuActions, Is.GreaterThan(0));
            Assert.That(session.Snapshot.Result, Is.Not.Null);
        }

        [Test]
        public void SameRequestReproducesInitialStructuredSnapshot()
        {
            var first = new GameSessionController(seed: 23, wildRank: 7);
            var rematch = new GameSessionController(seed: 23, wildRank: 7);
            first.Begin();
            rematch.Begin();

            Assert.That(SnapshotSignature(rematch.Snapshot),
                Is.EqualTo(SnapshotSignature(first.Snapshot)));
        }

        [Test]
        public void RecordedProductSessionResumesWithTheSameVisibleState()
        {
            var session = new GameSessionController(
                seed: 31, wildRank: 8, difficulty: CpuDifficulties.Hard);
            session.Begin();
            Assert.That(session.Archive.Configuration.Difficulty,
                Is.EqualTo(CpuDifficulties.Hard));
            for (int step = 0; step < 8 && !session.Game.IsTerminal; step++)
            {
                if (session.State == MatchSessionState.AwaitingHuman)
                    Assert.That(session.TryApplyHumanAction(session.Snapshot.Actions[0].Id), Is.True);
                else if (session.State == MatchSessionState.WaitingForCpu)
                    Assert.That(session.TryApplyCpuAction(), Is.True);
            }

            byte[] encoded = SessionArchiveCodec.Encode(session.Archive);
            var resumed = new GameSessionController(SessionArchiveCodec.Decode(encoded));
            resumed.Begin();

            Assert.That(resumed.Archive.Configuration.Difficulty,
                Is.EqualTo(CpuDifficulties.Hard));
            Assert.That(SnapshotSignature(resumed.Snapshot),
                Is.EqualTo(SnapshotSignature(session.Snapshot)));
            Assert.That(resumed.Archive.Actions.Count, Is.EqualTo(session.Archive.Actions.Count));
        }

        [Test]
        public void SessionSlotIdsAreGeneratedCanonicallyAndRejectPaths()
        {
            string id = SessionSlotIds.Create();
            Assert.That(SessionSlotIds.Require(id), Is.EqualTo(id));
            Assert.That(id, Does.Match("^[0-9a-f]{32}$"));
            Assert.Throws<ArgumentException>(() => SessionSlotIds.Require("../save"));
            Assert.Throws<ArgumentException>(() => SessionSlotIds.Require(id.ToUpperInvariant()));
        }

        [Test]
        public void FileSessionStoreSavesUpdatesLoadsAndExplicitlyDeletesSlot()
        {
            string temporaryRoot = Path.Combine(Path.GetTempPath(),
                "TrumpLab-T04-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                var store = new FileSessionStore(temporaryRoot);
                string slotId = SessionSlotIds.Create();
                var session = new GameSessionController(seed: 47, wildRank: 8);
                session.Begin();

                store.Save(slotId, session.Archive);
                Assert.That(store.List().Select(slot => slot.Id), Is.EqualTo(new[] { slotId }));
                Assert.That(store.Load(slotId).Actions.Count, Is.Zero);

                if (session.State == MatchSessionState.AwaitingHuman)
                    Assert.That(session.TryApplyHumanAction(session.Snapshot.Actions[0].Id), Is.True);
                else
                    Assert.That(session.TryApplyCpuAction(), Is.True);
                store.Save(slotId, session.Archive);
                Assert.That(store.Load(slotId).Actions.Count,
                    Is.EqualTo(session.Archive.Actions.Count));

                store.Delete(slotId);
                Assert.That(store.List(), Is.Empty);
            }
            finally
            {
                if (Directory.Exists(temporaryRoot))
                    Directory.Delete(temporaryRoot, recursive: true);
            }
        }

        [Test]
        public void ResultPresenterReportsHumanOutcomeAndScores()
        {
            var result = new GameResultPresentation(
                winners: new[] { 0 }, scores: new[] { 18d, -18d },
                reason: "empty hand", turns: 31);
            ResultViewModel model = CrazyEightsResultPresenter.Create(result);

            Assert.That(model.Summary, Does.Contain("You win!"));
            Assert.That(model.Summary, Does.Contain("You: 18"));
            Assert.That(model.Summary, Does.Contain("CPU: -18"));
            Assert.That(model.Summary, Does.Contain("empty hand"));
            Assert.That(model.Summary, Does.Contain("31"));
        }

        [Test]
        public void ProductPrefabsAndBootstrapSceneHaveNoMissingScripts()
        {
            string[] prefabs =
            {
                "TitleScreen.prefab",
                "GameSettingsScreen.prefab",
                "SessionLibraryScreen.prefab",
                "MatchScreen.prefab",
                "ReplayScreen.prefab",
                "ResultScreen.prefab"
            };
            foreach (string fileName in prefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/TrumpLab/Product/Prefabs/Screens/" + fileName);
                Assert.That(prefab, Is.Not.Null, fileName);
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab),
                    Is.Zero, fileName);
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                "Assets/TrumpLab/Product/Scenes/Bootstrap.unity"), Is.Not.Null);
        }

        private static string SnapshotSignature(GamePresentation snapshot)
        {
            IEnumerable<string> zones = snapshot.CardZones.Select(zone =>
                zone.Id + ":" + zone.Count + ":" + string.Join(",", zone.Cards.Select(card =>
                    ((int)card.Suit) + "-" + card.Rank)));
            IEnumerable<string> actions = snapshot.Actions.Select(action =>
                action.Id + ":" + action.Action.Kind + ":" +
                (action.Action.Card.HasValue
                    ? ((int)action.Action.Card.Value.Suit) + "-" + action.Action.Card.Value.Rank
                    : "-") + ":" + (action.Action.Value ?? "-"));
            return snapshot.CurrentPlayer + "|" + snapshot.Phase + "|" +
                string.Join("|", zones) + "|" + string.Join("|", actions);
        }
    }
}
