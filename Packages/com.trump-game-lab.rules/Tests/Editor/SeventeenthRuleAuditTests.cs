using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class SeventeenthRuleAuditTests
    {
        [Test]
        public void Unit17FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            1701, "spades", "euchre", "oh_hell", "texas_holdem", "five_card_draw");

        [Test]
        public void SpadesPenalizesABrokenContractAndExtendsATiedTarget()
        {
            IGame failedContract = BuiltInGames.Registry.Create("spades", players: 4, seed: 1710,
                options: new Dictionary<string, string> { { "target_score", "1" } });
            int[] bids = Field<int[]>(failedContract, "bids");
            int[] tricks = Field<int[]>(failedContract, "tricks");
            bids[0] = 7; bids[2] = 7;
            bids[1] = 6; bids[3] = 7;
            tricks[1] = 13;
            Invoke(failedContract, "ScoreHand");
            Assert.That(failedContract.Result().Scores, Is.EqualTo(new[] { -140d, 130d, -140d, 130d }));

            IGame tiedTarget = BuiltInGames.Registry.Create("spades", players: 4, seed: 1711,
                options: new Dictionary<string, string> { { "target_score", "1" } });
            int[] scores = Field<int[]>(tiedTarget, "teamScores");
            scores[0] = 1; scores[1] = 1;
            Array.Fill(Field<int[]>(tiedTarget, "bids"), 13);
            Invoke(tiedTarget, "ScoreHand");
            Assert.That(tiedTarget.IsTerminal, Is.False);
        }

        [Test]
        public void EuchreDealsThreeThenTwoAndUsesTheBicycleScoreTable()
        {
            const int seed = 1720;
            IGame game = BuiltInGames.Registry.Create("euchre", players: 4, seed: seed,
                options: new Dictionary<string, string> { { "target_score", "100" } });
            List<Card> deck = Cards.Shuffled(Cards.StandardDeck(new[] { 1, 9, 10, 11, 12, 13 }),
                new DeterministicRandom(seed));
            var expected = Enumerable.Range(0, 4).Select(_ => new List<Card>()).ToList();
            for (int offset = 1; offset <= 4; offset++)
                for (int card = 0; card < 3; card++) expected[offset % 4].Add(Pop(deck));
            for (int offset = 1; offset <= 4; offset++)
                for (int card = 0; card < 2; card++) expected[offset % 4].Add(Pop(deck));
            Card upcard = Pop(deck);
            Assert.That(Field<List<List<Card>>>(game, "hands"), Is.EqualTo(expected));
            Assert.That(Field<Card>(game, "upcard"), Is.EqualTo(upcard));

            int[] tricks = Field<int[]>(game, "tricks");
            tricks[0] = 5;
            SetField(game, "maker", 0);
            SetField(game, "alone", true);
            Invoke(game, "ScoreHand");
            Assert.That(Field<int[]>(game, "teamScores"), Is.EqualTo(new[] { 4, 0 }));
        }

        [Test]
        public void OhHellUsesThePagatScheduleHookAndWidespreadScoring()
        {
            IGame game = BuiltInGames.Registry.Create("oh_hell", players: 3, seed: 1730);
            Assert.That(Field<int[]>(game, "handSizes"), Is.EqualTo(
                Enumerable.Range(1, 10).Reverse().Concat(Enumerable.Range(2, 9)).ToArray()));
            game.Apply(new TrumpLab.Action("bid", value: "0"));
            game.Apply(new TrumpLab.Action("bid", value: "0"));
            Assert.That(game.LegalActions().Any(action => action.Value == "10"), Is.False);

            int[] bids = Field<int[]>(game, "bids");
            int[] tricks = Field<int[]>(game, "tricks");
            bids[0] = 2; bids[1] = 0; bids[2] = 8;
            tricks[0] = 2; tricks[1] = 1; tricks[2] = 7;
            Invoke(game, "ScoreHand");
            Assert.That(Field<int[]>(game, "scores"), Is.EqualTo(new[] { 12, 1, 7 }));
        }

        [Test]
        public void Unit17PromotedGamesKeepOpponentHandsOutOfObservations()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("spades", 1780);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("euchre", 1781);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("oh_hell", 1782);
        }

        private static T Field<T>(object source, string name) => (T)source.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source)!;
        private static void SetField(object source, string name, object value) => source.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(source, value);
        private static void Invoke(object source, string name) => source.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(source, null);
        private static Card Pop(List<Card> cards)
        {
            Card card = cards[cards.Count - 1];
            cards.RemoveAt(cards.Count - 1);
            return card;
        }
    }
}
