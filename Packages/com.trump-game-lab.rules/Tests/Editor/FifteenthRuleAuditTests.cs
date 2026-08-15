using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class FifteenthRuleAuditTests
    {
        [Test]
        [Category("BroadSimulation")]
        public void Unit15FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            1501, "golf", "sevens", "concentration", "cheat", "page_one");

        [Test]
        public void GolfForcesATakenDiscardIntoTheLayoutAndPublishesFaceUpCards()
        {
            IGame game = BuiltInGames.Registry.Create("golf", players: 3, seed: 1510);
            for (int player = 0; player < game.Players; player++) game.Apply(game.LegalActions()[0]);
            Assert.That(FirstLine(game.View(1)), Is.EqualTo(FirstLine(game.View(2))));

            game.Apply(game.LegalActions().Single(action => action.Kind == "draw_discard"));
            Assert.That(game.LegalActions().Select(action => action.Kind), Does.Not.Contain("discard_drawn"));
            Assert.That(game.LegalActions().Count(action => action.Kind == "swap"), Is.EqualTo(6));
            Assert.That(game.View(), Does.Contain("discard=-"));
        }

        [Test]
        public void SevensUsesThreeVoluntaryPassesAndKeepsBankruptCardsDisconnected()
        {
            bool exercised = false;
            for (int seed = 1520; seed < 1600 && !exercised; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("sevens", players: 4, seed: seed);
                for (int pass = 0; pass < 12; pass++)
                    game.Apply(game.LegalActions().Single(action => action.Kind == "pass"));
                Assert.That(game.LegalActions().Select(action => action.Kind), Does.Not.Contain("pass"));
                if (!game.LegalActions().Any(action => action.Kind == "bankrupt")) continue;
                Card isolated = ParseHand(game.View()).First(card => card.Rank < 6 || card.Rank > 8);
                game.Apply(game.LegalActions().Single(action => action.Kind == "bankrupt"));
                string suit = Card.SuitCode(isolated.Suit);
                Assert.That(game.View(), Does.Contain(suit + ":7-7"));
                Assert.That(game.View(), Does.Contain(suit + ":["));
                exercised = true;
            }
            Assert.That(exercised, Is.True, "fixed seeds must include a forced fourth pass");
        }

        [Test]
        public void ConcentrationMatchingPairKeepsTheTurnAndScoresOnePair()
        {
            IGame game = BuiltInGames.Registry.Create("concentration", players: 3, seed: 1530);
            List<Card> layout = (List<Card>)typeof(TrumpLab.Games.ConcentrationGame)
                .GetField("layout", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game)!;
            int[] pair = layout.Select((card, index) => new { card.Rank, index })
                .GroupBy(item => item.Rank).First().Take(2).Select(item => item.index).ToArray();
            int player = game.CurrentPlayer;
            game.Apply(new TrumpLab.Action("flip", value: pair[0].ToString()));
            game.Apply(new TrumpLab.Action("flip", value: pair[1].ToString()));
            Assert.That(game.View(), Does.Contain(layout[pair[0]].ToString()));
            game.Apply(game.LegalActions().Single());
            Assert.That(game.CurrentPlayer, Is.EqualTo(player));
            Assert.That(game.View(), Does.Contain("pairs=[1,0,0]"));
        }

        [Test]
        public void PageOneAnnouncementActionAvoidsTheFiveCardPenalty()
        {
            bool exercised = false;
            for (int seed = 1540; seed < 1560 && !exercised; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("page_one", players: 2, seed: seed);
                var random = new DeterministicRandom(seed * 100);
                for (int turn = 0; turn < 10000 && !game.IsTerminal; turn++)
                {
                    TrumpLab.Action[] declarations = game.LegalActions()
                        .Where(action => action.Kind == "play_page_one").ToArray();
                    if (declarations.Length > 0)
                    {
                        int player = game.CurrentPlayer;
                        game.Apply(new TrumpLab.Action("play", value: declarations[0].Value));
                        Assert.That(ParseHand(game.View(player)).Length, Is.GreaterThan(1));
                        exercised = true;
                        break;
                    }
                    game.Apply(game.ChooseCpuAction(game.CurrentPlayer, random));
                }
            }
            Assert.That(exercised, Is.True, "fixed seeds must reach a Page One declaration boundary");
        }

        [Test]
        public void Unit15PromotedGamesRespectPublicAndPrivateObservations()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresStockOrder("golf", 2, 1580);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("sevens", 3, 1581);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresHiddenListOrder("concentration", "layout", 1582);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresOpponentHandAndStock("page_one", 1583);
        }

        private static string FirstLine(string value) => value.Substring(0, value.IndexOf('\n'));
        private static Card[] ParseHand(string view)
        {
            string cards = view.Substring(view.IndexOf("your hand: ", StringComparison.Ordinal) + "your hand: ".Length).Trim();
            return cards.Length == 0 ? Array.Empty<Card>() : cards.Split(' ').Select(Card.Parse).ToArray();
        }
    }
}
