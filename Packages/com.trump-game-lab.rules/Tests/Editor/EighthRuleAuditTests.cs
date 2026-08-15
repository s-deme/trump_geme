using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class EighthRuleAuditTests
    {
        [Test]
        public void Unit08FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            801, "tanuki", "multi_stack", "dubito", "three_tricks", "mini_misere");

        [Test]
        public void TanukiRevealsCompletedDealRolesAndUsesMayMustMayFollowPhases()
        {
            IGame game = BuiltInGames.Registry.Create("tanuki", seed: 830);
            var random = new DeterministicRandom(83000);
            bool sawCompletedDeal = false;
            for (int turn = 0; turn < 5000 && !game.IsTerminal; turn++)
            {
                string view = game.View(game.CurrentPlayer);
                int deal = int.Parse(Between(view, "deal=", "/9"));
                string expectedFollow = deal >= 4 && deal <= 6 ? "must" : "may";
                Assert.That(view, Does.Contain("follow=" + expectedFollow), "deal " + deal);

                TrumpLab.Action action = game.ChooseCpuAction(game.CurrentPlayer, random);
                game.Apply(action);
                string after = game.View(0);
                if (!sawCompletedDeal && after.Contains("deal=2/9", StringComparison.Ordinal))
                {
                    string reveal = Between(after, "revealed_roles=[", "]");
                    Assert.That(reveal, Does.StartWith("deal=1,trump=P"));
                    Assert.That(reveal, Does.Contain(",minus=P"));
                    Assert.That(reveal, Does.Contain(",plus=P"));
                    for (int player = 1; player < 3; player++)
                        Assert.That(Between(game.View(player), "revealed_roles=[", "]"), Is.EqualTo(reveal));
                    sawCompletedDeal = true;
                }
            }
            Assert.That(sawCompletedDeal, Is.True);
            Assert.That(game.IsTerminal, Is.True);
        }

        [Test]
        public void MultiStackTwoPlayerJackNeverIntroducesFourPlayerRoles()
        {
            bool playedJack = false;
            for (int seed = 831; seed <= 870 && !playedJack; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("multi_stack", players: 2, seed: seed);
                var random = new DeterministicRandom(seed * 100L);
                for (int turn = 0; turn < 1000 && !game.IsTerminal; turn++)
                {
                    AssertTwoPlayerRole(game.View(game.CurrentPlayer));
                    IReadOnlyList<TrumpLab.Action> legal = game.LegalActions();
                    TrumpLab.Action[] jacks = legal.Where(action =>
                        action.Kind == "play_to_stack" && action.Card.HasValue && action.Card.Value.Rank == 11).ToArray();
                    TrumpLab.Action action = jacks.Length > 0 ? jacks[0] : game.ChooseCpuAction(game.CurrentPlayer, random);
                    game.Apply(action);
                    if (jacks.Length > 0)
                    {
                        playedJack = true;
                        AssertTwoPlayerRole(game.View(game.CurrentPlayer));
                    }
                }
            }
            Assert.That(playedJack, Is.True, "fixed seed range must exercise a legal Jack placement");
        }

        [Test]
        public void ThreeTricksScoresEachOfFourThirteenTrickRounds()
        {
            IGame game = BuiltInGames.Registry.Create("three_tricks", seed: 880);
            var random = new DeterministicRandom(88000);
            var expectedScores = new int[4];
            var roundTricks = new int[4];
            var trick = new List<Tuple<int, Card>>();
            int completedTricks = 0;

            while (!game.IsTerminal)
            {
                int player = game.CurrentPlayer;
                TrumpLab.Action action = game.ChooseCpuAction(player, random);
                trick.Add(Tuple.Create(player, action.Card!.Value));
                game.Apply(action);
                if (trick.Count != 4) continue;

                Suit led = trick[0].Item2.Suit;
                int winner = trick.Where(item => item.Item2.Suit == led)
                    .OrderByDescending(item => Strength(item.Item2)).First().Item1;
                roundTricks[winner]++;
                trick.Clear();
                completedTricks++;
                if (completedTricks % 13 != 0) continue;
                for (int playerIndex = 0; playerIndex < 4; playerIndex++)
                {
                    int count = roundTricks[playerIndex];
                    expectedScores[playerIndex] += count == 0 ? -5 : count <= 3 ? count * count : -count;
                }
                Array.Clear(roundTricks, 0, roundTricks.Length);
            }

            Assert.That(completedTricks, Is.EqualTo(52));
            Assert.That(game.Result().Turns, Is.EqualTo(208));
            Assert.That(game.Result().Scores, Is.EqualTo(expectedScores.Select(value => (double)value)));
        }

        [Test]
        public void MiniMisereUsesAuthorPlayerCountProfilesAndThirdSeatJokerChoice()
        {
            var expectedHandSizes = new Dictionary<int, int> { { 3, 7 }, { 4, 6 }, { 5, 5 }, { 6, 6 } };
            foreach (KeyValuePair<int, int> profile in expectedHandSizes)
            {
                IGame game = BuiltInGames.Registry.Create("mini_misere", players: profile.Key, seed: 890 + profile.Key,
                    options: new Dictionary<string, string> { { "target_score", "1" } });
                string view = game.View(game.CurrentPlayer);
                Assert.That(view, Does.Contain("hand_size=" + profile.Value));
                Assert.That(view, Does.Contain(profile.Key == 3 ? "phase=play" : "phase=lot"));
                RuleAuditTestSupport.PlayWithLegalCpu(game, 89000 + profile.Key);
            }

            bool found = false;
            for (int seed = 900; seed <= 1000 && !found; seed++)
            {
                IGame probe = BuiltInGames.Registry.Create("mini_misere", players: 3, seed: seed);
                foreach (TrumpLab.Action first in probe.LegalActions().Where(action => action.Card.HasValue))
                {
                    IGame win = BuiltInGames.Registry.Create("mini_misere", players: 3, seed: seed);
                    int firstPlayer = win.CurrentPlayer;
                    win.Apply(first);
                    TrumpLab.Action second = win.ChooseCpuAction(win.CurrentPlayer,
                        new DeterministicRandom(seed * 10L + first.Card!.Value.Rank));
                    if (!second.Card.HasValue) continue;
                    int secondPlayer = win.CurrentPlayer;
                    win.Apply(second);
                    IReadOnlyList<TrumpLab.Action> legal = win.LegalActions();
                    if (!legal.Any(action => action.Kind == "play_joker_win") ||
                        !legal.Any(action => action.Kind == "play_joker_lose")) continue;

                    int jokerPlayer = win.CurrentPlayer;
                    IGame lose = BuiltInGames.Registry.Create("mini_misere", players: 3, seed: seed);
                    lose.Apply(first); lose.Apply(second);
                    win.Apply(legal.Single(action => action.Kind == "play_joker_win"));
                    lose.Apply(lose.LegalActions().Single(action => action.Kind == "play_joker_lose"));
                    Assert.That(win.CurrentPlayer, Is.EqualTo(jokerPlayer));
                    int expectedLoseWinner = first.Card.Value.Suit == second.Card.Value.Suit &&
                        Strength(second.Card.Value) > Strength(first.Card.Value) ? secondPlayer : firstPlayer;
                    Assert.That(lose.CurrentPlayer, Is.EqualTo(expectedLoseWinner));
                    found = true;
                    break;
                }
            }
            Assert.That(found, Is.True, "fixed seed range must reach third-seat Joker win/lose choice");
        }

        [Test]
        public void MiniMisereLotDeclarationsOccurAtEachPlayersFirstPlay()
        {
            IGame game = BuiltInGames.Registry.Create("mini_misere", players: 5, seed: 1001);
            int leader = game.CurrentPlayer;
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "no_lot", "declare_lot" }));
            game.Apply(game.LegalActions().Single(action => action.Kind == "no_lot"));
            Assert.That(game.CurrentPlayer, Is.EqualTo(leader));
            Assert.That(game.LegalActions().All(action => action.Kind == "play"), Is.True);
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.CurrentPlayer, Is.EqualTo((leader + 1) % 5));
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "no_lot", "declare_lot" }));
            Assert.That(game.View(game.CurrentPlayer), Does.Contain("trick=[P" + leader + ":"));
        }

        [Test]
        public void Unit08OpeningObservationsIgnoreHiddenInformation()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("tanuki", 1001);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresStockOrder("multi_stack", 2, 1002);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresStockOrder("dubito", 2, 1003);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("three_tricks", 1004);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("mini_misere", 1005);
        }

        private static void AssertTwoPlayerRole(string view)
        {
            Assert.That(view.Contains("role=same_color", StringComparison.Ordinal) ||
                view.Contains("role=alternating", StringComparison.Ordinal), Is.True, view);
        }

        private static string Between(string value, string prefix, string suffix)
        {
            int start = value.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            int end = value.IndexOf(suffix, start, StringComparison.Ordinal);
            return value.Substring(start, end - start);
        }

        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
    }
}
