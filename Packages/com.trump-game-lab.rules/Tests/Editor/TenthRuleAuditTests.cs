using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class TenthRuleAuditTests
    {
        [Test]
        [Category("BroadSimulation")]
        public void Unit10FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            1001, "doppelkopf", "guillotine", "sasaki_44a", "schafkopf", "the_trick");

        [Test]
        public void GuillotineDominoAceRunMustExhaustEveryCurrentlyPlayableCard()
        {
            bool exercised = false;
            for (int seed = 1010; seed <= 1020 && !exercised; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("guillotine", seed: seed);
                var random = new DeterministicRandom(seed * 100L);
                for (int turn = 0; turn < 200000 && !game.IsTerminal && !exercised; turn++)
                {
                    string before = game.View(game.CurrentPlayer);
                    IReadOnlyList<TrumpLab.Action> legal = game.LegalActions();
                    TrumpLab.Action[] aces = before.Contains("phase=domino", StringComparison.Ordinal) &&
                        !before.Contains("layout=[]", StringComparison.Ordinal)
                        ? legal.Where(action => action.Kind == "place_domino" && action.Card.HasValue && action.Card.Value.Rank == 1).ToArray()
                        : Array.Empty<TrumpLab.Action>();
                    TrumpLab.Action action = aces.Length > 0 ? aces[0] : game.ChooseCpuAction(game.CurrentPlayer, random);
                    game.Apply(action);
                    if (aces.Length == 0 || game.IsTerminal ||
                        !game.View(game.CurrentPlayer).Contains("phase=domino", StringComparison.Ordinal)) continue;
                    IReadOnlyList<TrumpLab.Action> after = game.LegalActions();
                    bool hasPlacement = after.Any(candidate => candidate.Kind == "place_domino");
                    Assert.That(after.Any(candidate => candidate.Kind == "finish_ace_run"), Is.EqualTo(!hasPlacement));
                    exercised = true;
                }
            }
            Assert.That(exercised, Is.True, "fixed seeds must reach a non-opening Domino Ace");
        }

        [Test]
        public void TheTrickUsesPublishedThreeAndFourPlayerQuotasAndVictoryScore()
        {
            foreach (int players in new[] { 3, 4 })
            {
                IGame game = BuiltInGames.Registry.Create("the_trick", players: players, seed: 1030 + players);
                Card starter = new Card(Suit.Clubs, players == 3 ? 5 : 2);
                Assert.That(ParseHand(game.View(game.CurrentPlayer)), Does.Contain(starter));
                Assert.That(game.LegalActions().Count, Is.EqualTo(13), "starter card is not forced");
                var random = new DeterministicRandom(103000 + players);
                var tricks = new int[players];
                var trick = new List<Tuple<int, Card>>();
                while (!game.IsTerminal)
                {
                    int player = game.CurrentPlayer;
                    TrumpLab.Action action = game.ChooseCpuAction(player, random);
                    trick.Add(Tuple.Create(player, action.Card!.Value)); game.Apply(action);
                    if (trick.Count != players) continue;
                    Suit led = trick[0].Item2.Suit;
                    IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == Suit.Spades)
                        ? trick.Where(item => item.Item2.Suit == Suit.Spades) : trick.Where(item => item.Item2.Suit == led);
                    tricks[eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1]++;
                    trick.Clear();
                }
                Assert.That(game.Result().Turns, Is.EqualTo(players * 12));
                Assert.That(Enumerable.Range(0, players).All(player => ParseHand(game.View(player)).Length == 1), Is.True);
                if (players != 4) continue;
                Card[] remaining = Enumerable.Range(0, 4).Select(player => ParseHand(game.View(player)).Single()).ToArray();
                bool success = tricks.OrderBy(value => value).SequenceEqual(new[] { 0, 2, 4, 6 }) &&
                    remaining.Select(card => card.Suit).Distinct().Count() == 4;
                int highPlayer = Enumerable.Range(0, 4).OrderByDescending(player => tricks[player]).First();
                int lowPlayer = Enumerable.Range(0, 4).OrderBy(player => tricks[player]).First();
                double expectedScore = success ? Strength(remaining[highPlayer]) - Strength(remaining[lowPlayer]) + 12 : 0;
                Assert.That(game.Result().Winners.Count > 0, Is.EqualTo(success));
                Assert.That(game.Result().Scores, Is.All.EqualTo(expectedScore));
            }
        }

        [Test]
        public void Unit10PromotedGameObservationsRespectHiddenInformation()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("guillotine", 1040);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHandRanksWithinSuit("the_trick", 1041);
        }

        private static Card[] ParseHand(string view)
        {
            string value = view.Substring(view.IndexOf("your hand: ", StringComparison.Ordinal) + 11).Trim();
            return value.Length == 0 ? Array.Empty<Card>() : value.Split(' ').Select(Card.Parse).ToArray();
        }
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
    }
}
