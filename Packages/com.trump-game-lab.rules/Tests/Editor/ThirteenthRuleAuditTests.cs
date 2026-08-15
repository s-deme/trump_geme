using System;
using System.Linq;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class ThirteenthRuleAuditTests
    {
        [Test]
        public void Unit13FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            1301, "toepen", "war", "blackjack", "crazy_eights", "go_fish");

        [Test]
        public void CrazyEightsUsesTheTwoPlayerPagatDealAndVoluntaryDraw()
        {
            bool exercised = false;
            for (int seed = 1310; seed < 1340 && !exercised; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("crazy_eights", players: 2, seed: seed);
                Assert.That(ParseInts(Between(game.View(0), "hands=[", "]")), Is.EqualTo(new[] { 7, 7 }));
                Assert.That(Card.Parse(Between(game.View(0), "top=", " ")).Rank, Is.Not.EqualTo(8));
                if (!game.LegalActions().Any(action => action.Kind == "play")) continue;
                Assert.That(game.LegalActions().Select(action => action.Kind), Does.Contain("draw"));
                exercised = true;
            }
            Assert.That(exercised, Is.True, "fixed seeds must include an opening playable card");
        }

        [Test]
        public void CrazyEightsWinnerCollectsEveryRemainingCardPenalty()
        {
            IGame game = BuiltInGames.Registry.Create("crazy_eights", players: 3, seed: 1340);
            var random = new DeterministicRandom(134000);
            int[] expected = Array.Empty<int>(); int winner = -1;
            while (!game.IsTerminal)
            {
                int player = game.CurrentPlayer;
                TrumpLab.Action action = game.ChooseCpuAction(player, random);
                if (action.Kind == "play" && ParseHand(game.View(player)).Length == 1)
                {
                    winner = player;
                    expected = Enumerable.Range(0, game.Players).Select(viewer => viewer == winner ? 0 : -ParseHand(game.View(viewer)).Sum(Penalty)).ToArray();
                    expected[winner] = -expected.Sum();
                }
                game.Apply(action);
            }
            Assert.That(game.Result().Winners, Is.EqualTo(new[] { winner }));
            Assert.That(game.Result().Scores, Is.EqualTo(expected.Select(value => (double)value)));
        }

        [Test]
        public void GoFishUsesPlayerCountDealAndSuccessfulAskKeepsTurn()
        {
            Assert.That(ParseInts(Between(BuiltInGames.Registry.Create("go_fish", players: 2, seed: 1350).View(0), "hands=[", "]")), Is.EqualTo(new[] { 7, 7 }));
            Assert.That(ParseInts(Between(BuiltInGames.Registry.Create("go_fish", players: 3, seed: 1350).View(0), "hands=[", "]")), Is.EqualTo(new[] { 7, 7, 7 }));
            Assert.That(ParseInts(Between(BuiltInGames.Registry.Create("go_fish", players: 4, seed: 1350).View(0), "hands=[", "]")), Is.EqualTo(new[] { 5, 5, 5, 5 }));

            bool exercised = false;
            for (int seed = 1351; seed < 1380 && !exercised; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("go_fish", players: 3, seed: seed);
                int player = game.CurrentPlayer;
                TrumpLab.Action? catchAction = game.LegalActions().FirstOrDefault(action =>
                    ParseHand(game.View(action.Target!.Value)).Any(card => card.Rank == int.Parse(action.Value!)));
                if (catchAction == null) continue;
                game.Apply(catchAction.Value);
                Assert.That(game.CurrentPlayer, Is.EqualTo(player));
                exercised = true;
            }
            Assert.That(exercised, Is.True, "fixed seeds must include a successful request");
        }

        [Test]
        public void Unit13PrivateInformationStaysOutsideObservations()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresOpponentHandAndStock("crazy_eights", 1380);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresOpponentHandAndStock("go_fish", 1381);
        }

        private static int Penalty(Card card) => card.Rank == 8 ? 50 : Math.Min(card.Rank, 10);
        private static Card[] ParseHand(string view)
        {
            string cards = view.Substring(view.IndexOf("your hand: ", StringComparison.Ordinal) + "your hand: ".Length).Trim();
            return cards.Length == 0 ? Array.Empty<Card>() : cards.Split(' ').Select(Card.Parse).ToArray();
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
