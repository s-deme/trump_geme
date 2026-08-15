using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class NinthRuleAuditTests
    {
        [Test]
        public void Unit09FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            901, "agony_aunt", "collusion", "confirmation", "big_two", "triple_crown");

        [Test]
        public void AgonyAuntFirstDealMatchesAuthorPenaltyBoard()
        {
            IGame game = BuiltInGames.Registry.Create("agony_aunt", seed: 910);
            Card dump = Card.Parse(Between(game.View(0), "dump=", " "));
            var random = new DeterministicRandom(91000);
            var tricks = new int[4];
            var chips = new[] { 17, 17, 17, 17 };
            var boards = new bool[4, 9];
            var captured = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToArray();
            var trick = new List<Tuple<int, Card, bool>>();

            for (int play = 0; play < 52; play++)
            {
                int player = game.CurrentPlayer;
                TrumpLab.Action action = game.ChooseCpuAction(player, random);
                bool joker = !action.Card.HasValue;
                trick.Add(Tuple.Create(player, action.Card ?? dump, joker));
                game.Apply(action);
                if (trick.Count != 4) continue;

                int trickNumber = play / 4 + 1;
                Suit led = trick[0].Item2.Suit;
                int winner = trick.Where(item => item.Item2.Suit == led)
                    .OrderByDescending(item => Strength(item.Item2)).First().Item1;
                tricks[winner]++;
                captured[winner].AddRange(trick.Select(item => item.Item2));
                foreach (Tuple<int, Card, bool> item in trick)
                {
                    if (item.Item3) Penalize(winner, 0, chips, boards);
                    if (item.Item2.Rank == 12)
                    {
                        Penalize(winner, QueenCell(item.Item2.Suit), chips, boards);
                        if (item.Item2.Suit == dump.Suit) Penalize(winner, 4, chips, boards);
                    }
                }
                if (trickNumber == 13) Penalize(winner, 2, chips, boards);
                if (trickNumber == dump.Rank) Penalize(winner, 8, chips, boards);
                trick.Clear();
            }

            int most = Enumerable.Range(0, 4).OrderByDescending(player => tricks[player])
                .ThenByDescending(player => captured[player].Count(card => card.Suit == dump.Suit))
                .ThenByDescending(player => captured[player].Where(card => card.Suit == dump.Suit)
                    .Select(Strength).DefaultIfEmpty(0).Max()).First();
            Penalize(most, 6, chips, boards);
            int[][] lines =
            {
                new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
                new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
                new[] { 0, 4, 8 }, new[] { 2, 4, 6 }
            };
            for (int player = 0; player < 4; player++)
                chips[player] -= lines.Count(line => line.All(cell => boards[player, cell]));
            int sweep = Enumerable.Range(0, 4).Where(player => tricks[player] == 13).DefaultIfEmpty(-1).First();
            for (int player = 0; player < 4; player++)
            {
                bool anyPenalty = Enumerable.Range(0, 9).Any(cell => boards[player, cell]);
                if (player == sweep || (sweep < 0 && tricks[player] == 0)) chips[player] = 17;
                else if (tricks[player] > 0 && !anyPenalty) chips[player] += (17 - chips[player]) / 2;
                chips[player] = Math.Max(0, Math.Min(17, chips[player]));
            }
            Assert.That(ParseInts(Between(game.View(0), "chips=[", "]")), Is.EqualTo(chips));
        }

        [Test]
        public void CollusionFirstDealMatchesAllAuthorBonusCasesAndPlainReachReversal()
        {
            IGame game = BuiltInGames.Registry.Create("collusion", seed: 911,
                options: new Dictionary<string, string> { { "target_score", "1" } });
            var random = new DeterministicRandom(91100);
            var tricks = new int[4];
            var trick = new List<Tuple<int, Card>>();
            while (!game.IsTerminal)
            {
                int player = game.CurrentPlayer;
                TrumpLab.Action action = game.ChooseCpuAction(player, random);
                trick.Add(Tuple.Create(player, action.Card!.Value));
                game.Apply(action);
                if (trick.Count != 4) continue;
                Suit led = trick[0].Item2.Suit;
                tricks[trick.Where(item => item.Item2.Suit == led)
                    .OrderByDescending(item => Strength(item.Item2)).First().Item1]++;
                trick.Clear();
            }
            int[] bonuses = CollusionBonuses(tricks);
            int[] expected = Enumerable.Range(0, 4)
                .Select(player => (bonuses[player] == 0 ? -tricks[player] : tricks[player]) + bonuses[player]).ToArray();
            Assert.That(game.Result().Scores, Is.EqualTo(expected.Select(value => (double)value)));
        }

        [Test]
        public void ConfirmationPublicProtectionAndFinalCardBidUsePublishedScores()
        {
            bool foundProtection = false;
            for (int seed = 920; seed <= 940 && !foundProtection; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("confirmation", seed: seed,
                    options: new Dictionary<string, string> { { "deals", "1" } });
                var random = new DeterministicRandom(seed * 100L);
                var tricks = new int[4];
                var protectedBid = new bool[4];
                var trick = new List<Tuple<int, Card>>();
                while (!game.IsTerminal)
                {
                    int player = game.CurrentPlayer;
                    TrumpLab.Action action = game.ChooseCpuAction(player, random);
                    if (action.Kind == "protect_and_play")
                    {
                        protectedBid[player] = true; foundProtection = true;
                        game.Apply(action);
                        Assert.That(game.View((player + 1) % 4), Does.Contain(action.Value!));
                    }
                    else game.Apply(action);
                    trick.Add(Tuple.Create(player, action.Card!.Value));
                    if (trick.Count != 4) continue;
                    Suit led = trick[0].Item2.Suit;
                    tricks[trick.Where(item => item.Item2.Suit == led)
                        .OrderByDescending(item => item.Item2.Rank).First().Item1]++;
                    trick.Clear();
                }
                if (!foundProtection) continue;
                int[] expected = Enumerable.Range(0, 4).Select(player =>
                {
                    Card target = ParseHand(game.View(player)).Single();
                    int bid = target.Rank == 10 ? 0 : target.Rank;
                    return tricks[player] + (tricks[player] == bid ? protectedBid[player] ? 5 : 10 : 0);
                }).ToArray();
                Assert.That(game.Result().Scores, Is.EqualTo(expected.Select(value => (double)value)));
            }
            Assert.That(foundProtection, Is.True);
        }

        [Test]
        public void BigTwoPublishedFiveCardOrderOpeningAndPenaltyMultipliers()
        {
            Assert.That(BigTwoCategory(new[] { C(3), D(4), H(5), S(6), C(7) }), Is.EqualTo(0));
            Assert.That(BigTwoCategory(new[] { H(3), H(5), H(7), H(9), H(11) }), Is.EqualTo(1));
            Assert.That(BigTwoCategory(new[] { C(3), D(3), H(3), C(4), D(4) }), Is.EqualTo(2));
            Assert.That(BigTwoCategory(new[] { C(3), D(3), H(3), S(3), C(4) }), Is.EqualTo(3));
            Assert.That(BigTwoCategory(new[] { H(3), H(4), H(5), H(6), H(7) }), Is.EqualTo(4));

            IGame game = BuiltInGames.Registry.Create("big_two", seed: 941);
            int starter = game.CurrentPlayer;
            TrumpLab.Action first = game.ChooseCpuAction(starter, new DeterministicRandom(94100));
            Assert.That(first.Kind, Is.EqualTo("play_combination"));
            Assert.That(first.Value, Does.Contain("3C"));
            game.Apply(first);
            Assert.That(game.CurrentPlayer, Is.EqualTo((starter + 3) % 4));
            RuleAuditTestSupport.PlayWithLegalCpu(game, 94101);

            int winner = game.Result().Winners.Single();
            int total = 0;
            for (int player = 0; player < 4; player++) if (player != winner)
            {
                Card[] hand = ParseHand(game.View(player));
                int multiplier = hand.Length == 13 ? 4 : hand.Length >= 8 ? 3 : 1;
                multiplier *= 1 << hand.Count(card => card.Rank == 2);
                int penalty = hand.Length * multiplier; total += penalty;
                Assert.That(game.Result().Scores[player], Is.EqualTo(-penalty), "P" + player);
            }
            Assert.That(game.Result().Scores[winner], Is.EqualTo(total));
        }

        [Test]
        public void TripleCrownScoresPublishedRolesAndDefaultsToFifteenPointSession()
        {
            IGame game = BuiltInGames.Registry.Create("triple_crown", seed: 950,
                options: new Dictionary<string, string> { { "deals", "1" } });
            Card[][] openingHands = Enumerable.Range(0, 4).Select(player => ParseHand(game.View(player))).ToArray();
            int highPlayer = Enumerable.Range(0, 4).Single(player => openingHands[player].Contains(S(1)));
            int lowPlayer = Enumerable.Range(0, 4).Single(player => openingHands[player].Contains(D(2)));
            int doublePlayer = highPlayer == lowPlayer ? highPlayer : -1;
            bool declaredHigh = false;
            Suit? trump = null;
            var random = new DeterministicRandom(95000);
            if (game.LegalActions()[0].Kind == "choose_double")
            {
                TrumpLab.Action choice = game.ChooseCpuAction(game.CurrentPlayer, random);
                declaredHigh = choice.Value!.StartsWith("high:", StringComparison.Ordinal);
                trump = Card.ParseSuit(choice.Value.Substring(choice.Value.Length - 1));
                game.Apply(choice);
            }
            var tricks = new int[4];
            var trick = new List<Tuple<int, Card>>();
            while (!game.IsTerminal)
            {
                int player = game.CurrentPlayer;
                TrumpLab.Action action = game.ChooseCpuAction(player, random);
                trick.Add(Tuple.Create(player, action.Card!.Value)); game.Apply(action);
                if (trick.Count != 4) continue;
                Suit led = trick[0].Item2.Suit;
                IEnumerable<Tuple<int, Card>> eligible = trump.HasValue && trick.Any(item => item.Item2.Suit == trump.Value)
                    ? trick.Where(item => item.Item2.Suit == trump.Value) : trick.Where(item => item.Item2.Suit == led);
                tricks[eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1]++;
                trick.Clear();
            }
            int[] expected = TripleCrownScores(tricks, highPlayer, lowPlayer, doublePlayer, declaredHigh);
            Assert.That(game.Result().Scores, Is.EqualTo(expected.Select(value => (double)value)));

            IGame target = BuiltInGames.Registry.Create("triple_crown", seed: 951,
                options: new Dictionary<string, string> { { "target_score", "1" } });
            RuleAuditTestSupport.PlayWithLegalCpu(target, 95100);
            Assert.That(target.View(0), Does.Contain("target=1"));
        }

        [Test]
        public void Unit09OpeningObservationsIgnoreOtherHands()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("agony_aunt", 960);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("collusion", 961);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("confirmation", 962);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("big_two", 963);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("triple_crown", 964);
        }

        private static int[] CollusionBonuses(int[] tricks)
        {
            var bonuses = new int[4];
            IGrouping<int, int>[] groups = Enumerable.Range(0, 4).GroupBy(player => tricks[player]).ToArray();
            if (groups.Any(group => group.Count() == 3))
                bonuses[groups.Single(group => group.Count() == 1).Single()] = 30;
            else if (groups.Length == 4)
                bonuses[Enumerable.Range(0, 4).OrderBy(player => tricks[player]).First()] = 20;
            else foreach (IGrouping<int, int> group in groups.Where(group => group.Count() == 2))
                foreach (int player in group) bonuses[player] += 10;
            return bonuses;
        }

        private static int[] TripleCrownScores(int[] tricks, int high, int low, int doublePlayer, bool declaredHigh)
        {
            var scores = new int[4];
            if (doublePlayer >= 0)
            {
                if (tricks[doublePlayer] >= 5 || tricks[doublePlayer] == 0) scores[doublePlayer] = 5;
                else
                {
                    int award = 2 * (declaredHigh ? 5 - tricks[doublePlayer] : tricks[doublePlayer]);
                    for (int player = 0; player < 4; player++) if (player != doublePlayer) scores[player] = award;
                }
            }
            else
            {
                if (tricks[high] >= 5) scores[high] = 2;
                if (tricks[low] == 0) scores[low] = 3;
                int teamAward = Math.Max(0, 5 - tricks[high]) + tricks[low];
                for (int player = 0; player < 4; player++) if (player != high && player != low) scores[player] = teamAward;
            }
            return scores;
        }

        private static int BigTwoCategory(Card[] cards)
        {
            MethodInfo method = typeof(TrumpLab.Games.BigTwoGame).GetMethod("FiveCardCombo",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            object combo = method.Invoke(null, new object[] { cards })!;
            return (int)combo.GetType().GetProperty("Category")!.GetValue(combo)!;
        }

        private static void Penalize(int player, int cell, int[] chips, bool[,] boards)
        { chips[player]--; boards[player, cell] = true; }
        private static int QueenCell(Suit suit) => suit == Suit.Clubs ? 1 : suit == Suit.Diamonds ? 3 : suit == Suit.Hearts ? 5 : 7;
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static Card C(int rank) => new Card(Suit.Clubs, rank);
        private static Card D(int rank) => new Card(Suit.Diamonds, rank);
        private static Card H(int rank) => new Card(Suit.Hearts, rank);
        private static Card S(int rank) => new Card(Suit.Spades, rank);

        private static Card[] ParseHand(string view)
        {
            string value = view.Substring(view.IndexOf("your hand: ", StringComparison.Ordinal) + 11).Trim();
            return value.Length == 0 ? Array.Empty<Card>() : value.Split(' ').Select(Card.Parse).ToArray();
        }
        private static int[] ParseInts(string value) => value.Split(',').Select(int.Parse).ToArray();
        private static string Between(string value, string prefix, string suffix)
        {
            int start = value.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            int end = value.IndexOf(suffix, start, StringComparison.Ordinal);
            return value.Substring(start, end - start);
        }
    }
}
