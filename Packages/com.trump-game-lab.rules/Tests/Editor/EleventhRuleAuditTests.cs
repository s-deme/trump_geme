using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class EleventhRuleAuditTests
    {
        [Test]
        [Category("BroadSimulation")]
        public void Unit11FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            1101, "truf", "pass_cut_run", "finesse", "yaniv", "wuxing_xiangke");

        [Test]
        public void TrufPublishesBidsAndCompletedTrumpsAndUsesAdoptedScoreMethod()
        {
            IGame game = BuiltInGames.Registry.Create("truf", players: 4, seed: 1110,
                options: new Dictionary<string, string> { { "deals", "1" } });
            var random = new DeterministicRandom(111000);
            var bids = new int[4];
            var tricks = new int[4];
            var trick = new List<Tuple<int, Card>>();
            Suit trump = Suit.Clubs;
            bool highMode = false, sawHiddenTrump = false, sawRevealedTrump = false;

            while (!game.IsTerminal)
            {
                int player = game.CurrentPlayer;
                TrumpLab.Action action = game.ChooseCpuAction(player, random);
                if (action.Kind == "bid_card") bids[player] = BidValue(action.Card!.Value);
                else if (action.Kind == "increase_all" || action.Kind == "decrease_all")
                {
                    int shift = int.Parse(action.Value!) * (action.Kind == "increase_all" ? 1 : -1);
                    for (int p = 0; p < 4; p++) bids[p] += shift;
                    highMode = shift > 0;
                }
                else if (action.Kind == "play") trick.Add(Tuple.Create(player, action.Card!.Value));

                game.Apply(action);
                string view = game.View(game.IsTerminal ? 0 : game.CurrentPlayer);
                if (action.Kind == "bid_card" && !view.Contains("phase=bid", StringComparison.Ordinal))
                {
                    Assert.That(Between(view, "bid_cards=[", "]"), Does.Not.Contain("XX"));
                    trump = Card.ParseSuit(Between(view, "trump=", " "));
                    if (!view.Contains("phase=adjust", StringComparison.Ordinal)) highMode = bids.Sum() > 13;
                }
                if (action.Kind != "play") continue;
                Card played = action.Card!.Value;
                if (trick.Count < 4 && played.Suit == trump)
                    sawHiddenTrump |= view.Contains("P" + player + ":XX", StringComparison.Ordinal);
                if (trick.Count != 4) continue;
                Suit led = trick[0].Item2.Suit;
                IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == trump)
                    ? trick.Where(item => item.Item2.Suit == trump) : trick.Where(item => item.Item2.Suit == led);
                int winner = eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
                tricks[winner]++;
                if (trick.Any(item => item.Item2.Suit == trump))
                    sawRevealedTrump |= trick.Where(item => item.Item2.Suit == trump)
                        .All(item => view.Contains("P" + item.Item1 + ":" + item.Item2, StringComparison.Ordinal));
                trick.Clear();
            }

            int[] expected = Enumerable.Range(0, 4).Select(player =>
            {
                int difference = highMode ? tricks[player] - bids[player] : bids[player] - tricks[player];
                return !highMode && bids[player] == 0 && tricks[player] == 0 ? 5 : difference > 0 ? difference * 2 : difference;
            }).ToArray();
            Assert.That(game.Result().Scores, Is.EqualTo(expected.Select(value => (double)value)));
            Assert.That(sawHiddenTrump, Is.True);
            Assert.That(sawRevealedTrump, Is.True);
        }

        [Test]
        public void PassCutRunAlwaysSeatsPartnerFourthAndScoresPublishedDistances()
        {
            IGame game = BuiltInGames.Registry.Create("pass_cut_run", seed: 1120,
                options: new Dictionary<string, string> { { "deals", "1" } });
            var random = new DeterministicRandom(112000);
            while (game.View(0).Contains("phase=pass_cards", StringComparison.Ordinal))
                game.Apply(game.ChooseCpuAction(game.CurrentPlayer, random));

            string opening = game.View(0);
            int leader = int.Parse(Between(opening, "leader=P", " "));
            int[] order = Between(opening, "order=[", "]").Split(',').Select(value => int.Parse(value.Substring(1))).ToArray();
            Assert.That(order[3], Is.EqualTo(Partner(leader)));

            Suit trump = Card.ParseSuit(Between(opening, "trump=", " "));
            var scores = new int[4];
            var trick = new List<Tuple<int, Card>>();
            while (!game.IsTerminal)
            {
                int player = game.CurrentPlayer;
                TrumpLab.Action action = game.ChooseCpuAction(player, random);
                trick.Add(Tuple.Create(player, action.Card!.Value)); game.Apply(action);
                if (trick.Count != 4) continue;
                Suit led = trick[0].Item2.Suit;
                IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == trump)
                    ? trick.Where(item => item.Item2.Suit == trump) : trick.Where(item => item.Item2.Suit == led);
                int winner = eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
                int trickLeader = trick[0].Item1;
                scores[winner] += winner == trickLeader ? 1 : winner == Partner(trickLeader) ? 4 : winner == (trickLeader + 2) % 4 ? 3 : 2;
                trick.Clear();
            }
            double[] expected = { scores[0] + scores[1], scores[0] + scores[1], scores[2] + scores[3], scores[2] + scores[3] };
            Assert.That(game.Result().Scores, Is.EqualTo(expected));
        }

        [Test]
        public void FinesseUsesLeadSuitTrumpAndAddsLastTrickAfterTablePenalty()
        {
            IGame game = BuiltInGames.Registry.Create("finesse", seed: 1130);
            var random = new DeterministicRandom(113000);
            List<List<Card>> tables = ParseFinesseTables(game.View(0));
            var pairTricks = new int[2];
            var trick = new List<Tuple<int, Card>>();
            Suit trump = Suit.Clubs;
            int lastTeam = -1;

            for (int turn = 0; turn < 500 && (pairTricks.Sum() < 13 || game.View(0).Contains("phase=refill", StringComparison.Ordinal)); turn++)
            {
                int player = game.CurrentPlayer;
                TrumpLab.Action action = game.ChooseCpuAction(player, random);
                Assert.That(action.Kind, Does.Not.Contain("no_trump"));
                if (pairTricks.Sum() == 0 && trick.Count == 0) trump = action.Card!.Value.Suit;
                if (action.Kind == "refill_table") tables[player].Add(action.Card!.Value);
                else
                {
                    int owner = action.Kind == "lead_partner_table" ? int.Parse(action.Value!.Split(':')[0]) : player;
                    if (action.Kind == "lead_partner_table") tables[owner].Remove(action.Card!.Value);
                    trick.Add(Tuple.Create(owner, action.Card!.Value));
                }
                game.Apply(action);
                if (trick.Count != 4) continue;
                Suit led = trick[0].Item2.Suit;
                IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == trump)
                    ? trick.Where(item => item.Item2.Suit == trump) : trick.Where(item => item.Item2.Suit == led);
                int winner = eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
                pairTricks[winner % 2]++; lastTeam = winner % 2; trick.Clear();
            }

            int[] expected = Enumerable.Range(0, 2).Select(team =>
            {
                int won = pairTricks[team];
                int trickScore = won < 7 ? 0 : won == 7 ? 2 : won == 8 ? 5 : won == 9 ? 10 : won == 10 ? 20 : won == 11 ? 10 : won == 12 ? 5 : 2;
                int penalty = Enumerable.Range(0, 4).Where(player => player % 2 == team)
                    .Sum(player => tables[player].Count(card => card.Suit == trump)) * 3;
                return Math.Max(0, trickScore - penalty) + (team == lastTeam ? 4 : 0);
            }).ToArray();
            Assert.That(ParseInts(Between(game.View(0), "scores=[", "]")), Is.EqualTo(expected));
        }

        [Test]
        public void YanivDrawsOnlyFromPreviousGroupUsesFourPlayerDoubleDeckAndRevealsHands()
        {
            IGame three = BuiltInGames.Registry.Create("yaniv", players: 3, seed: 1140);
            IGame four = BuiltInGames.Registry.Create("yaniv", players: 4, seed: 1140);
            Assert.That(int.Parse(Between(three.View(0), "stock=", " ")), Is.EqualTo(38));
            Assert.That(int.Parse(Between(four.View(0), "stock=", " ")), Is.EqualTo(87));

            IGame game = BuiltInGames.Registry.Create("yaniv", players: 2, seed: 1141);
            string previous = Between(game.View(0), "discard=[", "]");
            TrumpLab.Action discard = game.LegalActions().First(action => action.Kind.StartsWith("discard", StringComparison.Ordinal));
            game.Apply(discard);
            Assert.That(Between(game.View(0), "draw_options=[", "]"), Is.EqualTo(previous));
            Assert.That(game.LegalActions().Where(action => action.Kind == "draw_discard").Select(action => action.Value),
                Does.Contain(previous));

            var random = new DeterministicRandom(114100);
            bool revealed = false;
            for (int turn = 0; turn < 20000 && !game.IsTerminal && !revealed; turn++)
            {
                TrumpLab.Action action = game.ChooseCpuAction(game.CurrentPlayer, random);
                bool declaration = action.Kind == "declare_yaniv";
                game.Apply(action);
                if (declaration) revealed = !string.IsNullOrEmpty(Between(game.View(0), "revealed_hands=[", "]"));
            }
            Assert.That(revealed, Is.True);
        }

        [Test]
        public void WuxingUsesRightOfRightPartnerInOnePartnerDeals()
        {
            bool exercised = false;
            for (int seed = 1150; seed < 1170 && !exercised; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("wuxing_xiangke", seed: seed);
                string opening = game.View(0);
                if (!opening.Contains("mode=one-partner", StringComparison.Ordinal)) continue;
                Card[] kitty = Between(opening, "kitty=[", "]").Split(' ').Select(Card.Parse).ToArray();
                var points = new int[5];
                var trick = new List<Tuple<int, Card>>();
                var random = new DeterministicRandom(seed * 100L);
                for (int play = 0; play < 50; play++)
                {
                    int player = game.CurrentPlayer;
                    TrumpLab.Action action = game.ChooseCpuAction(player, random);
                    trick.Add(Tuple.Create(player, action.Card!.Value)); game.Apply(action);
                    if (trick.Count != 5) continue;
                    Suit led = trick[0].Item2.Suit;
                    IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == Suit.Spades)
                        ? trick.Where(item => item.Item2.Suit == Suit.Spades) : trick.Where(item => item.Item2.Suit == led);
                    int winner = eligible.OrderByDescending(item => Strength(item.Item2)).First().Item1;
                    points[winner] += trick.Count(item => PointCard(item.Item2));
                    if (play < 5) points[winner] += kitty.Count(PointCard);
                    trick.Clear();
                }
                int[] expected = Enumerable.Range(0, 5).Select(player =>
                {
                    int partnerPoints = points[(player + 3) % 5];
                    return points[player] <= partnerPoints ? points[player] : -(points[player] - partnerPoints);
                }).ToArray();
                Assert.That(ParseInts(Between(game.View(0), "scores=[", "]")), Is.EqualTo(expected));
                exercised = true;
            }
            Assert.That(exercised, Is.True, "fixed seeds must include a one-partner deal");
        }

        [Test]
        public void Unit11OpeningObservationsIgnoreHiddenHandsAndStock()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("truf", 1180);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("pass_cut_run", 1181);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("finesse", 1182);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresOpponentHandAndStock("yaniv", 1183);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("wuxing_xiangke", 1184);
        }

        private static List<List<Card>> ParseFinesseTables(string view) => Between(view, "tables=[", "]").Split('|')
            .Select(part => part.Substring(part.IndexOf(':') + 1).Trim())
            .Select(cards => cards.Length == 0 ? new List<Card>() : cards.Split(' ')
                .Select(value => Card.Parse(value.Substring(0, value.IndexOf('#')))).ToList()).ToList();
        private static int Partner(int player) => player % 2 == 0 ? player + 1 : player - 1;
        private static bool PointCard(Card card) => card.Rank == 1 || card.Rank >= 10;
        private static int BidValue(Card card) => card.Rank == 1 ? 1 : card.Rank >= 11 ? 0 : card.Rank;
        private static int Strength(Card card) => card.Rank == 1 ? 14 : card.Rank;
        private static int[] ParseInts(string value) => value.Split(',').Select(int.Parse).ToArray();
        private static string Between(string value, string prefix, string suffix)
        {
            int start = value.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            int end = value.IndexOf(suffix, start, StringComparison.Ordinal);
            return value.Substring(start, end - start);
        }
    }
}
