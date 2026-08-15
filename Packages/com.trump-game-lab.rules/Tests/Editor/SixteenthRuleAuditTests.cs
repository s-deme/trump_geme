using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class SixteenthRuleAuditTests
    {
        [Test]
        public void Unit16FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            1601, "seven_bridge", "rummy_500", "canasta", "pinochle", "hearts");

        [Test]
        public void Rummy500ScoresAceLowOnlyInALowRunAndEndsWithAUniqueLeader()
        {
            MethodInfo score = typeof(TrumpLab.Games.Rummy500Game)
                .GetMethod("MeldValue", BindingFlags.Static | BindingFlags.NonPublic)!;
            int Score(params Card[] cards) => Convert.ToInt32(score.Invoke(null, new object[] { cards }));
            Assert.That(Score(new Card(Suit.Clubs, 1), new Card(Suit.Clubs, 2), new Card(Suit.Clubs, 3)), Is.EqualTo(6));
            Assert.That(Score(new Card(Suit.Clubs, 12), new Card(Suit.Clubs, 13), new Card(Suit.Clubs, 1)), Is.EqualTo(35));
            Assert.That(Score(new Card(Suit.Clubs, 1), new Card(Suit.Diamonds, 1), new Card(Suit.Hearts, 1)), Is.EqualTo(45));

            IGame game = BuiltInGames.Registry.Create("rummy_500", players: 2, seed: 1620,
                options: new Dictionary<string, string> { { "target_score", "1" } });
            GameResult result = RuleAuditTestSupport.PlayWithLegalCpu(game, 162000);
            Assert.That(result.Winners, Has.Count.EqualTo(1));
            Assert.That(result.Scores.Count(value => value == result.Scores.Max()), Is.EqualTo(1));
        }

        [Test]
        public void HeartsLeavesClubTwoInTheKittyAndLowestHeldClubLeads()
        {
            bool exercised = false;
            for (int seed = 1630; seed < 1750 && !exercised; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("hearts", players: 3, seed: seed);
                List<Card> kitty = Field<List<Card>>(game, "kitty");
                if (!kitty.Contains(new Card(Suit.Clubs, 2))) continue;
                for (int player = 0; player < 3; player++) game.Apply(game.LegalActions()[0]);
                List<List<Card>> hands = Field<List<List<Card>>>(game, "hands");
                Assert.That(hands.All(hand => !hand.Contains(new Card(Suit.Clubs, 2))), Is.True);
                int expected = hands.Select((hand, player) => new
                    {
                        Player = player,
                        Rank = hand.Where(card => card.Suit == Suit.Clubs).Select(card => card.Rank).DefaultIfEmpty(99).Min()
                    })
                    .OrderBy(item => item.Rank).First().Player;
                Assert.That(game.CurrentPlayer, Is.EqualTo(expected));
                exercised = true;
            }
            Assert.That(exercised, Is.True, "fixed seeds must put the club two in the three-player kitty");
        }

        [Test]
        public void Unit16AuditedPrivateInformationStaysHidden()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresOpponentHandAndStock("rummy_500", 1680);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("hearts", 3, 1681);
        }

        private static T Field<T>(object source, string name) => (T)source.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(source)!;
    }
}
