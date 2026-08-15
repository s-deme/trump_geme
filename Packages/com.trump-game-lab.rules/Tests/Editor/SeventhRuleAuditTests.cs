using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class SeventhRuleAuditTests
    {
        [Test]
        public void Unit07FixedSeedAudit() =>
            RuleAuditTestSupport.AssertFixedSeedBatch(701, "italian_whist", "minimo", "kaedama_trick", "trick_of_the_dead", "corpo");

        [Test]
        public void KaedamaKeepsPartnersSecretUntilTheSecondJokerAndThenPublishesRoles()
        {
            IGame game = BuiltInGames.Registry.Create("kaedama_trick", seed: 706);
            bool firstJoker = false;
            for (int turn = 0; turn < 30 && !game.IsTerminal; turn++)
            {
                System.Collections.Generic.IReadOnlyList<TrumpLab.Action> actions = game.LegalActions();
                TrumpLab.Action action = actions.Any(candidate => candidate.Value!.StartsWith("X"))
                    ? actions.First(candidate => candidate.Value!.StartsWith("X")) : actions[0];
                game.Apply(action);
                string roles = game.View();
                if (action.Value!.StartsWith("X") && !firstJoker)
                {
                    firstJoker = true;
                    Assert.That(roles, Does.Contain("partners hidden"));
                }
                else if (action.Value!.StartsWith("X")) Assert.That(roles, Does.Not.Contain("partners hidden"));
            }

            Assert.That(firstJoker, Is.True);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("kaedama_trick", 707);
        }

        [Test]
        public void MinimoDoubleScoresEveryThreeTrickShape()
        {
            var covered = new bool[3];
            for (int seed = 710; seed <= 759; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("minimo", 3, seed,
                    new Dictionary<string, string> { ["starting_chips"] = "2" });
                int doublePlayer = game.CurrentPlayer;
                game.Apply(new TrumpLab.Action("double"));
                var random = new DeterministicRandom(seed * 100);
                while (!game.IsTerminal)
                {
                    TrumpLab.Action action = game.ChooseCpuAction(game.CurrentPlayer, random);
                    Assert.That(game.LegalActions(), Does.Contain(action));
                    game.Apply(action);
                }

                string view = game.View();
                int[] tricks = ParseIntegers(view, @"tricks=\[(\d+),(\d+),(\d+)\]");
                int[] chips = ParseIntegers(view, @"chips=\[(\d+),(\d+),(\d+)\]");
                int pot = ParseIntegers(view, @"pot=(\d+)")[0];
                var expected = new[] { 1, 1, 1 };
                int expectedPot = 3;
                void Pay(int player)
                {
                    if (expected[player] == 0) return;
                    expected[player]--; expectedPot++;
                }

                int[] one = Enumerable.Range(0, 3).Where(player => tricks[player] == 1).ToArray();
                int sweep = Array.FindIndex(tricks, value => value == 3);
                if (one.Length == 1)
                {
                    covered[0] = true;
                    int winner = one[0];
                    for (int player = 0; player < 3; player++) if (player != winner) Pay(player);
                    expected[winner] += expectedPot; expectedPot = 0;
                }
                else if (sweep >= 0)
                {
                    covered[2] = true; Pay(sweep); Pay(doublePlayer);
                }
                else
                {
                    covered[1] = true; Pay(doublePlayer);
                }
                Assert.That(chips, Is.EqualTo(expected), "seed " + seed);
                Assert.That(pot, Is.EqualTo(expectedPot), "seed " + seed);
            }
            Assert.That(covered, Is.EqualTo(new[] { true, true, true }),
                "fixed seeds must cover 2-1-0, 1-1-1, and 3-0-0");
        }

        [Test]
        public void TrickOfTheDeadFirstHalfUsesRankAndLowCardsChooseZombiesFirst()
        {
            bool covered = false;
            for (int seed = 760; seed <= 799 && !covered; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("trick_of_the_dead", 3, seed);
                var played = new List<Tuple<int, Card>>();
                int leader = game.CurrentPlayer;
                TrumpLab.Action lead = game.LegalActions().OrderBy(action => action.Card!.Value.Rank).First();
                played.Add(Tuple.Create(leader, lead.Card!.Value)); game.Apply(lead);

                int secondPlayer = game.CurrentPlayer;
                TrumpLab.Action[] highOffSuitActions = game.LegalActions()
                    .Where(action => action.Card!.Value.Suit != lead.Card!.Value.Suit &&
                        action.Card.Value.Rank > lead.Card.Value.Rank)
                    .OrderByDescending(action => action.Card!.Value.Rank).ToArray();
                if (highOffSuitActions.Length == 0) continue;
                TrumpLab.Action highOffSuit = highOffSuitActions[0];
                played.Add(Tuple.Create(secondPlayer, highOffSuit.Card!.Value)); game.Apply(highOffSuit);

                int thirdPlayer = game.CurrentPlayer;
                TrumpLab.Action[] lowerActions = game.LegalActions()
                    .Where(action => action.Card!.Value.Rank < highOffSuit.Card!.Value.Rank)
                    .OrderBy(action => action.Card!.Value.Rank).ToArray();
                if (lowerActions.Length == 0) continue;
                TrumpLab.Action lower = lowerActions[0];
                played.Add(Tuple.Create(thirdPlayer, lower.Card!.Value)); game.Apply(lower);

                Tuple<int, Card>[] pickOrder = played.OrderBy(item => item.Item2.Rank).ToArray();
                for (int index = 0; index < pickOrder.Length; index++)
                {
                    Assert.That(game.CurrentPlayer, Is.EqualTo(pickOrder[index].Item1),
                        "zombie pick " + index + "/seed " + seed);
                    game.Apply(game.LegalActions()[0]);
                }
                Assert.That(game.CurrentPlayer, Is.EqualTo(secondPlayer),
                    "off-suit high rank must lead next/seed " + seed);
                Assert.That(game.View(), Does.Contain("phase=first_half first_tricks=1/6"));
                covered = true;
            }
            Assert.That(covered, Is.True, "fixed seeds must contain an off-suit higher-rank boundary");
        }

        [Test]
        public void CorpoPublishesAllPokerHandsAtShowdown()
        {
            IGame? covered = null;
            for (int seed = 800; seed <= 819 && covered == null; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("corpo", 3, seed);
                var random = new DeterministicRandom(seed * 100);
                for (int turn = 0; turn < 300 && !game.View().Contains("revealed_poker=[P0:"); turn++)
                {
                    IReadOnlyList<TrumpLab.Action> actions = game.LegalActions();
                    TrumpLab.Action[] passes = actions.Where(candidate => candidate.Kind == "pass").ToArray();
                    TrumpLab.Action action = passes.Length > 0 ? passes[0] :
                        actions[0].Kind == "reserve_for_poker"
                            ? game.ChooseCpuAction(game.CurrentPlayer, random)
                            : actions.OrderBy(candidate => candidate.Card!.Value.Rank).First();
                    Assert.That(actions, Does.Contain(action));
                    game.Apply(action);
                }
                if (game.View().Contains("revealed_poker=[P0:")) covered = game;
            }
            Assert.That(covered, Is.Not.Null, "fixed seeds must reach a poker showdown");

            string? publicPoker = null;
            for (int viewer = 0; viewer < 3; viewer++)
            {
                Match match = Regex.Match(covered!.View(viewer), @"revealed_poker=\[(.*?)\] hand_counts");
                Assert.That(match.Success, Is.True, covered.View(viewer));
                publicPoker ??= match.Groups[1].Value;
                Assert.That(match.Groups[1].Value, Is.EqualTo(publicPoker));
            }
            string[] shownHands = publicPoker!.Split(new[] { " | " }, StringSplitOptions.None);
            Assert.That(shownHands, Has.Length.EqualTo(3));
            for (int player = 0; player < 3; player++)
            {
                Assert.That(shownHands[player], Does.StartWith("P" + player + ":"));
                Assert.That(shownHands[player].Split(':')[1]
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(5));
            }
        }

        [Test]
        public void Unit07VerifiedGamesKeepOpponentHandsOutsideObservation()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("minimo", 820);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("trick_of_the_dead", 821);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("corpo", 822);
        }

        private static int[] ParseIntegers(string text, string pattern)
        {
            Match match = Regex.Match(text, pattern);
            Assert.That(match.Success, Is.True, text);
            return Enumerable.Range(1, match.Groups.Count - 1)
                .Select(index => int.Parse(match.Groups[index].Value)).ToArray();
        }
    }
}
