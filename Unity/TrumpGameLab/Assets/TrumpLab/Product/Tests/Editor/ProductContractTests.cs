#nullable enable

using System;
using System.Collections.Generic;
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
            Assert.That(GameSettingsScreen.TryCreateRequest("-17", "8", out GameStartRequest? request,
                out string error), Is.True, error);
            Assert.That(request, Is.EqualTo(new GameStartRequest(-17, 8)));
            Assert.That(GameSettingsScreen.TryCreateRequest("nope", "8", out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(GameSettingsScreen.TryCreateRequest("1", "0", out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(GameSettingsScreen.TryCreateRequest("1", "14", out _, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
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
                "MatchScreen.prefab",
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
