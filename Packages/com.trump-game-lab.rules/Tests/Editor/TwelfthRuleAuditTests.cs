using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class TwelfthRuleAuditTests
    {
        private static readonly int[] BriscolaRanks = { 1, 3, 13, 12, 11, 7, 6, 5, 4, 2 };

        [Test]
        [Category("BroadSimulation")]
        public void Unit12FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            1201, "schmear", "briscola_chiamata", "briscola_bugiarda", "goninkan", "portland");

        [Test]
        public void SchmearUsesAdoptedPacksSameDealerRedealAndThreeCardExchangeLimit()
        {
            IGame five = BuiltInGames.Registry.Create("schmear", players: 5, seed: 1210);
            IGame six = BuiltInGames.Registry.Create("schmear", players: 6, seed: 1210);
            Assert.That(int.Parse(Between(five.View(0), "stock=", " ")), Is.EqualTo(15));
            Assert.That(int.Parse(Between(six.View(0), "stock=", " ")), Is.EqualTo(17));

            string dealer = Between(five.View(0), "dealer=", " ");
            for (int player = 0; player < 5; player++) five.Apply(five.LegalActions().Single(action => action.Kind == "pass"));
            Assert.That(Between(five.View(0), "dealer=", " "), Is.EqualTo(dealer));

            five.Apply(five.LegalActions().Single(action => action.Kind == "bid" && action.Value == "3"));
            for (int player = 1; player < 5; player++) five.Apply(five.LegalActions().Single(action => action.Kind == "pass"));
            five.Apply(five.LegalActions().First(action => action.Kind == "choose_trump" && action.Value == "C"));

            bool reachedLimit = false;
            while (five.View(0).Contains("phase=exchange", StringComparison.Ordinal))
            {
                int discarded = 0;
                while (discarded < 3 && five.LegalActions().Any(action => action.Kind == "discard_exchange"))
                {
                    five.Apply(five.LegalActions().First(action => action.Kind == "discard_exchange")); discarded++;
                }
                if (discarded == 3)
                {
                    Assert.That(five.LegalActions(), Has.None.Matches<TrumpLab.Action>(action => action.Kind == "discard_exchange"));
                    reachedLimit = true;
                }
                five.Apply(five.LegalActions().Single(action => action.Kind == "finish_exchange"));
            }
            while (five.View(0).Contains("phase=dealer_discard", StringComparison.Ordinal))
                five.Apply(five.LegalActions().First());

            Assert.That(reachedLimit, Is.True, "the fixed deal must exercise the three-card exchange boundary");
            TrumpLab.Action calledAce = five.LegalActions().First(action => action.Kind == "call_partner" && action.Card!.Value.Rank == 1);
            five.Apply(calledAce);
            Assert.That(Enumerable.Range(0, 5).Select(player => five.View(player)), Has.All.Contains("called_card=" + calledAce.Card));
        }

        [Test]
        public void ChiamataRedealsWithSameDealerAndSettlementMatchesCardPoints()
        {
            IGame redeal = BuiltInGames.Registry.Create("briscola_chiamata", seed: 1220);
            string dealer = Between(redeal.View(0), "dealer=", " ");
            for (int player = 0; player < 5; player++) redeal.Apply(redeal.LegalActions().Single(action => action.Kind == "pass"));
            Assert.That(Between(redeal.View(0), "dealer=", " "), Is.EqualTo(dealer));

            IGame game = BuiltInGames.Registry.Create("briscola_chiamata", seed: 1221);
            var random = new DeterministicRandom(122100);
            while (!game.View(0).Contains("phase=play", StringComparison.Ordinal))
                game.Apply(game.ChooseCpuAction(game.CurrentPlayer, random));

            string opening = game.View(0);
            int declarer = int.Parse(Between(opening, "declarer=P", " "));
            int calledRank = int.Parse(Between(opening, "called_rank=", " "));
            Suit trump = Card.ParseSuit(Between(opening, "trump=", " "));
            Card called = new Card(trump, calledRank);
            int partner = Enumerable.Range(0, 5).Where(player => ParseHand(game.View(player)).Contains(called)).DefaultIfEmpty(-1).First();
            if (partner == declarer) partner = -1;
            var side = new HashSet<int> { declarer }; if (partner >= 0) side.Add(partner);
            var captured = Enumerable.Range(0, 5).Select(_ => new List<Card>()).ToArray();
            var trick = new List<Tuple<int, Card>>();

            while (game.View(0).Contains("phase=play", StringComparison.Ordinal))
            {
                int player = game.CurrentPlayer;
                Card[] hand = ParseHand(game.View(player));
                if (trick.Count > 0 && hand.Any(card => card.Suit == trick[0].Item2.Suit))
                    Assert.That(game.LegalActions(), Has.All.Matches<TrumpLab.Action>(action => action.Card!.Value.Suit == trick[0].Item2.Suit));
                TrumpLab.Action action = game.ChooseCpuAction(player, random);
                trick.Add(Tuple.Create(player, action.Card!.Value)); game.Apply(action);
                if (trick.Count != 5) continue;
                Suit led = trick[0].Item2.Suit;
                IEnumerable<Tuple<int, Card>> eligible = trick.Any(item => item.Item2.Suit == trump)
                    ? trick.Where(item => item.Item2.Suit == trump) : trick.Where(item => item.Item2.Suit == led);
                int winner = eligible.OrderBy(item => Array.IndexOf(BriscolaRanks, item.Item2.Rank)).First().Item1;
                captured[winner].AddRange(trick.Select(item => item.Item2)); trick.Clear();
            }

            int points = side.Sum(player => captured[player].Sum(BriscolaPoints));
            int unit = points >= 61 ? 1 : -1; if (side.Sum(player => captured[player].Count) == 40) unit *= 2;
            int[] expected = new int[5];
            if (partner < 0)
            {
                expected[declarer] = 4 * unit;
                foreach (int player in Enumerable.Range(0, 5).Where(player => player != declarer)) expected[player] = -unit;
            }
            else
            {
                expected[declarer] = 2 * unit; expected[partner] = unit;
                foreach (int player in Enumerable.Range(0, 5).Where(player => !side.Contains(player))) expected[player] = -unit;
            }
            Assert.That(ParseInts(Between(game.View(0), "scores=[", "]")), Is.EqualTo(expected));
        }

        [Test]
        public void PortlandPublishesTablesAndRequiresOverwriteAfterDrawing()
        {
            IGame game = BuiltInGames.Registry.Create("portland", players: 4, seed: 1230);
            string tables = Between(game.View(0), "tables=[", "]");
            Assert.That(Enumerable.Range(0, 4).Select(player => Between(game.View(player), "tables=[", "]")), Has.All.EqualTo(tables));
            Assert.That(game.LegalActions().Select(action => action.Kind), Is.EquivalentTo(new[] { "pass_round", "reveal_next" }));
            game.Apply(game.LegalActions().Single(action => action.Kind == "reveal_next"));
            Assert.That(game.LegalActions(), Is.Not.Empty.And.All.Matches<TrumpLab.Action>(action => action.Kind == "overwrite"));
            game.Apply(game.LegalActions().First());
            Assert.That(game.LegalActions().Select(action => action.Kind), Is.EquivalentTo(new[] { "pass_round", "reveal_next" }));
        }

        [Test]
        public void Unit12PromotedGamesKeepPrivateInformationOutsideObservations()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("schmear", 1240);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("briscola_chiamata", 1241);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresPrivateDeckOrder("portland", 4, 1242);
        }

        private static Card[] ParseHand(string view)
        {
            string cards = view.Substring(view.IndexOf("your hand: ", StringComparison.Ordinal) + "your hand: ".Length).Trim();
            return cards.Length == 0 ? Array.Empty<Card>() : cards.Split(' ').Select(Card.Parse).ToArray();
        }
        private static int BriscolaPoints(Card card) => card.Rank == 1 ? 11 : card.Rank == 3 ? 10 : card.Rank == 13 ? 4 : card.Rank == 12 ? 3 : card.Rank == 11 ? 2 : 0;
        private static int[] ParseInts(string value) => value.Split(',').Select(int.Parse).ToArray();
        private static string Between(string value, string prefix, string suffix)
        {
            int start = value.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            int end = value.IndexOf(suffix, start, StringComparison.Ordinal);
            return value.Substring(start, end - start);
        }
    }
}
