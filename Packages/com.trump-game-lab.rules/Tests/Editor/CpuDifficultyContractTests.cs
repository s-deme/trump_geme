using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class CpuDifficultyContractTests
    {
        [Test]
        public void DifficultyCatalogueKeepsStableIdsAndProductOrder()
        {
            Assert.That(CpuDifficulties.All.Select(value =>
                    (value.Id, value.Key, value.DisplayName)),
                Is.EqualTo(new[]
                {
                    (1, "standard", "Standard"),
                    (2, "easy", "Easy"),
                    (3, "hard", "Hard")
                }));
            Assert.That(CpuDifficulties.ProductOrder.Select(value => value.Id),
                Is.EqualTo(new[] { 2, 1, 3 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => CpuDifficulties.Get(0));

            Assert.That(BuiltInGames.Registry.Info("crazy_eights").SupportedCpuDifficulties,
                Is.EqualTo(new[] { 2, 1, 3 }));
            Assert.That(BuiltInGames.Registry.Info("war").SupportedCpuDifficulties,
                Is.EqualTo(new[] { 1 }));
        }

        [TestCase(CpuDifficulties.Standard)]
        [TestCase(CpuDifficulties.Easy)]
        [TestCase(CpuDifficulties.Hard)]
        public void CrazyEightsDifficultyAlwaysChoosesLegalActions(int difficulty)
        {
            for (long seed = 1; seed <= 30; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
                var policyRandom = new DeterministicRandom(seed + 99991);
                for (int step = 0; step < 50000 && !game.IsTerminal; step++)
                {
                    int player = game.CurrentPlayer;
                    IReadOnlyList<Action> legal = game.LegalActions(player);
                    Action selected = game.ChooseCpuAction(player, policyRandom, difficulty);
                    Assert.That(legal, Does.Contain(selected),
                        "difficulty=" + difficulty + " seed=" + seed + " step=" + step);
                    game.Apply(selected);
                }
                Assert.That(game.IsTerminal, Is.True,
                    "difficulty=" + difficulty + " seed=" + seed);
            }
        }

        [Test]
        public void StandardMatchesThePreM04HeuristicAcrossEveryVisitedState()
        {
            for (long seed = 40; seed < 70; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
                var policyRandom = new DeterministicRandom(seed + 99991);
                for (int step = 0; step < 2000 && !game.IsTerminal; step++)
                {
                    int player = game.CurrentPlayer;
                    IReadOnlyList<Action> actions = game.LegalActions(player);
                    Action expected = LegacyStandardAction(game, player, actions);
                    Action actual = game.ChooseCpuAction(
                        player, policyRandom, CpuDifficulties.Standard);
                    Assert.That(actual, Is.EqualTo(expected),
                        "seed=" + seed + " step=" + step);
                    game.Apply(actual);
                }
                Assert.That(game.IsTerminal, Is.True, "seed=" + seed);
            }
        }

        [Test]
        public void SimulatorAndSessionValidateGameSpecificDifficultySupport()
        {
            foreach (int difficulty in new[] { CpuDifficulties.Easy, CpuDifficulties.Hard })
            {
                SimulationReport report = Simulator.Simulate(
                    "crazy_eights", 10, seed: 7100, difficulty: difficulty);
                Assert.That(report.Failures, Is.Empty, "difficulty=" + difficulty);
                Assert.That(report.Completed, Is.EqualTo(10));
            }

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Simulator.Simulate("war", 1, difficulty: CpuDifficulties.Easy));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BuiltInGames.Registry.ValidateCpuDifficulty("crazy_eights", 99));

            var supported = new SessionConfiguration(
                "crazy_eights", 2, 17, CpuDifficulties.Hard, new[] { 0 },
                new Dictionary<string, string> { ["wild_rank"] = "8" });
            Assert.That(new SessionRecorder(supported).Archive.Configuration.Difficulty,
                Is.EqualTo(CpuDifficulties.Hard));
            var unsupported = new SessionConfiguration(
                "war", 2, 17, CpuDifficulties.Easy, new[] { 0 });
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionRecorder(unsupported));
        }

        [Test]
        public void CrazyEightsRejectsUnknownDifficultyWithoutApplyingAnAction()
        {
            IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed: 81);
            int turns = game.TurnCount;
            Assert.Throws<ArgumentOutOfRangeException>(() => game.ChooseCpuAction(
                game.CurrentPlayer, new DeterministicRandom(82), difficulty: 99));
            Assert.That(game.TurnCount, Is.EqualTo(turns));
        }

        private static Action LegacyStandardAction(
            IGame game, int player, IReadOnlyList<Action> actions)
        {
            GamePresentation presentation = ((IGamePresentationProvider)game).Present(player);
            IReadOnlyList<Card> hand = presentation.CardZones.Single(zone =>
                zone.Role == "hand" && zone.OwnerPlayer == player).Cards;
            if (presentation.Phase == "choose_starter_suit")
            {
                Suit starterSuit = Enum.GetValues(typeof(Suit)).Cast<Suit>()
                    .OrderByDescending(suit => hand.Count(card => card.Suit == suit))
                    .First();
                return actions.First(action => action.Value == Card.SuitCode(starterSuit));
            }

            Action[] plays = actions.Where(action =>
                action.Kind == "play" || action.Kind == "play_last_card").ToArray();
            if (plays.Length == 0) return actions[0];
            Action[] nonWild = plays.Where(action => action.Card!.Value.Rank != 8).ToArray();
            if (nonWild.Length > 0)
                return nonWild.OrderByDescending(action =>
                    hand.Count(card => card.Suit == action.Card!.Value.Suit)).First();
            Suit best = Enum.GetValues(typeof(Suit)).Cast<Suit>()
                .OrderByDescending(suit => hand.Count(card => card.Suit == suit)).First();
            return plays.First(action => action.Value == Card.SuitCode(best));
        }
    }
}
