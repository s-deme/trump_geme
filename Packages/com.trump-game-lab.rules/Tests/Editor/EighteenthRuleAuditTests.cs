using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class EighteenthRuleAuditTests
    {
        [Test]
        [Category("BroadSimulation")]
        public void Unit18FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            1801, "baccarat", "twenty_four", "black_lady", "four_tricks");

        [Test]
        public void BaccaratUsesEightDecksNaturalStandAndNetPayouts()
        {
            IGame game = BuiltInGames.Registry.Create("baccarat", players: 3, seed: 1810);
            List<Card> deck = Field<List<Card>>(game, "deck");
            Assert.That(deck.Count, Is.EqualTo(416));
            string?[] bets = Field<string?[]>(game, "bets");
            bets[0] = "bet_player"; bets[1] = "bet_banker"; bets[2] = "bet_tie";
            deck.Clear();
            deck.AddRange(new[]
            {
                new Card(Suit.Diamonds, 10), new Card(Suit.Clubs, 10),
                new Card(Suit.Hearts, 8), new Card(Suit.Spades, 9)
            });
            Invoke(game, "DealAndSettle");
            Assert.That(game.Result().Scores, Is.EqualTo(new[] { 1d, -1d, -1d }));
            Assert.That(Field<List<Card>>(game, "playerHand").Count, Is.EqualTo(2));
            Assert.That(Field<List<Card>>(game, "bankerHand").Count, Is.EqualTo(2));
        }

        [Test]
        public void BlackLadyPublishesOnlyTheRequiredKittyCardsAndCarriesClearRemainders()
        {
            IGame game = BuiltInGames.Registry.Create("black_lady", players: 5, seed: 1820);
            List<Card> table = Field<List<Card>>(game, "table");
            List<Card> faceUp = Field<List<Card>>(game, "faceUpTable");
            Assert.That(table.Count, Is.EqualTo(2));
            Assert.That(faceUp.Count, Is.EqualTo(1));
            Assert.That(game.View(0), Does.Contain("table=[" + faceUp[0] + "] hidden_table=1"));
            Assert.That(game.View(1), Does.Contain("table=[" + faceUp[0] + "] hidden_table=1"));

            List<List<Card>> captured = Field<List<List<Card>>>(game, "captured");
            captured[0].Add(new Card(Suit.Hearts, 2));
            captured[1].AddRange(Cards.StandardDeck().Where(card =>
                card.Suit == Suit.Hearts && card.Rank != 2 || card.Suit == Suit.Spades && card.Rank == 12));
            Invoke(game, "ScoreRound");
            Assert.That(Field<int[]>(game, "totalScores"), Is.EqualTo(new[] { -1, -25, 8, 8, 8 }));
            Assert.That(Field<int>(game, "carry"), Is.EqualTo(2));
        }

        [Test]
        public void FourTricksUsesTheCompleteDeckFinalDoubleAndScoreTable()
        {
            IGame game = BuiltInGames.Registry.Create("four_tricks", players: 3, seed: 1830);
            List<Card> deck = (List<Card>)InvokeWithResult(game, "MakeDeck");
            Assert.That(deck.Count, Is.EqualTo(36));
            Assert.That(deck.Select(card => card.Rank).Distinct(), Is.EquivalentTo(new[] { 1, 6, 7, 8, 9, 10, 11, 12, 13 }));
            Assert.That(InvokeWithResult(game, "TrickValue"), Is.EqualTo(1));
            foreach (IList hand in Field<IList>(game, "Hands")) hand.Clear();
            Assert.That(InvokeWithResult(game, "TrickValue"), Is.EqualTo(2));
            int[] tricks = Field<int[]>(game, "RoundTricks");
            tricks[0] = 0; tricks[1] = 4; tricks[2] = 9;
            Assert.That(InvokeWithResult(game, "ScoreRound"), Is.EqualTo(new[] { -5, 10, -9 }));
        }

        [Test]
        public void Unit18PromotedGamesRespectPublicAndPrivateObservations()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresHiddenListOrder("baccarat", "deck", 1880);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("black_lady", 5, 1881);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("four_tricks", 1882);
        }

        private static T Field<T>(object source, string name)
        {
            Type? type = source.GetType();
            while (type != null)
            {
                FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) return (T)field.GetValue(source)!;
                type = type.BaseType;
            }
            throw new InvalidOperationException("Missing field " + name);
        }

        private static void Invoke(object source, string name) => InvokeWithResult(source, name);
        private static object InvokeWithResult(object source, string name)
        {
            Type? type = source.GetType();
            while (type != null)
            {
                MethodInfo? method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (method != null) return method.Invoke(source, null)!;
                type = type.BaseType;
            }
            throw new InvalidOperationException("Missing method " + name);
        }
    }
}
