using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TrumpLab.Games;

namespace TrumpLab.Tests
{
    public sealed class InitialRuleAuditTests
    {
        [Test]
        public void CardCaptureFixedSeedExecutesDiscardCaptureAndEndPhases()
        {
            IGame game = BuiltInGames.Registry.Create("card_capture", 1, 94);
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "discard_cards" }));
            game.Apply(game.LegalActions().Single());
            Assert.That(game.LegalActions().Select(action => action.Kind), Is.All.EqualTo("capture"));
            Assert.That(game.LegalActions().Any(action => action.Value!.Contains("53")), Is.True,
                "The fixed seed exercises the Joker-copy capture exception.");

            GameResult result = PlayToEnd(game, 94001);

            Assert.That(result.Turns, Is.GreaterThan(1));
            Assert.That(result.Reason, Is.EqualTo("enemy deck cleared").Or.EqualTo("uncapturable court card"));
        }

        [Test]
        public void ScoundrelDeathScoreIncludesCurrentHealthAndRemainingMonsters()
        {
            IGame game = BuiltInGames.Registry.Create("scoundrel", 1, 95);
            SetField(game, "health", 1);
            List<Card> dungeon = Field<List<Card>>(game, "dungeon");
            List<Card> room = Field<List<Card>>(game, "room");
            dungeon.Clear(); dungeon.Add(new Card(Suit.Clubs, 3));
            room.Clear(); room.Add(new Card(Suit.Spades, 2));

            game.Apply(new TrumpLab.Action("fight_bare", target: 0));

            Assert.That(game.IsTerminal, Is.True);
            Assert.That(game.Result().Scores.Single(), Is.EqualTo(-4d));
            Assert.That(game.Result().Reason, Is.EqualTo("health depleted"));
        }

        [Test]
        public void ScoundrelScoresTheFinalDungeonPotionEvenWhenAnotherRoomCardCarriesOver()
        {
            IGame game = BuiltInGames.Registry.Create("scoundrel", 1, 95);
            SetField(game, "health", 20); SetField(game, "selected", 2); SetField(game, "potionUsed", false);
            List<Card> dungeon = Field<List<Card>>(game, "dungeon");
            List<Card> room = Field<List<Card>>(game, "room");
            dungeon.Clear(); dungeon.Add(new Card(Suit.Hearts, 5));
            room.Clear(); room.Add(new Card(Suit.Hearts, 2)); room.Add(new Card(Suit.Clubs, 2));

            game.Apply(new TrumpLab.Action("potion", target: 0));

            Assert.That(game.IsTerminal, Is.True);
            Assert.That(game.Result().Scores.Single(), Is.EqualTo(25d));
        }

        [Test]
        public void GosankyoKeepsHiddenOpponentRanksObservationallyEquivalent()
        {
            IGame left = BuiltInGames.Registry.Create("gosankyo", 1, 96);
            IGame right = BuiltInGames.Registry.Create("gosankyo", 1, 96);
            List<List<Card>> hiddenHands = Field<List<List<Card>>>(right, "hands");
            Card original = hiddenHands[1][0];
            hiddenHands[1][0] = new Card(original.Suit, original.Rank == 1 ? 2 : 1);

            Assert.That(right.View(0), Is.EqualTo(left.View(0)));
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()));
            Assert.That(right.ChooseCpuAction(0, new DeterministicRandom(96001)),
                Is.EqualTo(left.ChooseCpuAction(0, new DeterministicRandom(96001))));

            GameResult result = PlayToEnd(left, 96002);
            Assert.That(result.Reason, Is.EqualTo("four exact bids").Or.EqualTo("exact bid failed"));
            Assert.That(result.Scores.Single(), Is.InRange(0d, 4d));
        }

        [Test]
        public void GermanWhistFixedSeedCompletesBothStagesAndKeepsOpponentHandPrivate()
        {
            IGame left = BuiltInGames.Registry.Create("german_whist", 2, 97);
            IGame right = BuiltInGames.Registry.Create("german_whist", 2, 97);
            List<List<Card>> hiddenHands = Field<List<List<Card>>>(right, "hands");
            Card original = hiddenHands[1][0];
            hiddenHands[1][0] = new Card(original.Suit, original.Rank == 1 ? 2 : 1);

            Assert.That(right.View(0), Is.EqualTo(left.View(0)));
            Assert.That(right.ChooseCpuAction(0, new DeterministicRandom(97001)),
                Is.EqualTo(left.ChooseCpuAction(0, new DeterministicRandom(97001))));

            GameResult result = PlayToEnd(left, 97002);
            Assert.That(result.Turns, Is.EqualTo(52));
            Assert.That(result.Reason, Is.EqualTo("second-phase tricks"));
            Assert.That(result.Scores.Sum(), Is.EqualTo(13d));
        }

        [Test]
        public void GermanWhistRequiresFollowingSuitWhenTheResponderCanFollow()
        {
            IGame game = BuiltInGames.Registry.Create("german_whist", 2, 97);
            List<List<Card>> hands = Field<List<List<Card>>>(game, "hands");
            hands[0].Clear(); hands[0].Add(new Card(Suit.Clubs, 2));
            hands[1].Clear(); hands[1].AddRange(new[] { new Card(Suit.Clubs, 5), new Card(Suit.Hearts, 7) });

            game.Apply(new TrumpLab.Action("play", new Card(Suit.Clubs, 2)));

            Assert.That(game.LegalActions(), Is.EquivalentTo(new[]
            {
                new TrumpLab.Action("play", new Card(Suit.Clubs, 5))
            }));
        }

        [Test]
        public void GinRummyUsesClassicGinAndMatchBonuses()
        {
            IGame game = BuiltInGames.Registry.Create("gin_rummy", 2, 1,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.CurrentPlayer, Is.EqualTo(1));
            List<List<Card>> hands = Field<List<List<Card>>>(game, "hands");
            hands[0].Clear(); hands[0].AddRange(new[]
            {
                new Card(Suit.Clubs, 5), new Card(Suit.Diamonds, 6), new Card(Suit.Hearts, 7),
                new Card(Suit.Spades, 8), new Card(Suit.Clubs, 9), new Card(Suit.Diamonds, 10),
                new Card(Suit.Hearts, 11), new Card(Suit.Spades, 12), new Card(Suit.Clubs, 13),
                new Card(Suit.Diamonds, 2)
            });
            hands[1].Clear(); hands[1].AddRange(new[]
            {
                new Card(Suit.Clubs, 1), new Card(Suit.Clubs, 2), new Card(Suit.Clubs, 3),
                new Card(Suit.Clubs, 4), new Card(Suit.Diamonds, 1), new Card(Suit.Diamonds, 2),
                new Card(Suit.Diamonds, 3), new Card(Suit.Hearts, 1), new Card(Suit.Hearts, 2),
                new Card(Suit.Hearts, 3), new Card(Suit.Spades, 13)
            });
            SetField(game, "phase", "discard");

            game.Apply(new TrumpLab.Action("knock", new Card(Suit.Spades, 13)));

            Assert.That(game.IsTerminal, Is.True);
            Assert.That(game.Result().Scores, Is.EqualTo(new[] { 0d, 317d }));
            Assert.That(game.Result().Reason, Does.Contain("gin +97").And.Contain("game bonus +200"));
        }

        [Test]
        public void GinRummyCpuAndViewIgnoreTheOtherPlayersPrivateHand()
        {
            IGame left = BuiltInGames.Registry.Create("gin_rummy", 2, 1);
            IGame right = BuiltInGames.Registry.Create("gin_rummy", 2, 1);
            List<List<Card>> hiddenHands = Field<List<List<Card>>>(right, "hands");
            Card original = hiddenHands[0][0];
            hiddenHands[0][0] = new Card(original.Suit, original.Rank == 1 ? 2 : 1);

            Assert.That(right.View(1), Is.EqualTo(left.View(1)));
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()));
            Assert.That(right.ChooseCpuAction(1, new DeterministicRandom(100001)),
                Is.EqualTo(left.ChooseCpuAction(1, new DeterministicRandom(100001))));
        }

        private static GameResult PlayToEnd(IGame game, int seed)
        {
            DeterministicRandom rng = new DeterministicRandom(seed);
            for (int step = 0; step < 1000 && !game.IsTerminal; step++)
            {
                TrumpLab.Action action = game.ChooseCpuAction(game.CurrentPlayer, rng);
                Assert.That(game.LegalActions(), Does.Contain(action));
                game.Apply(action);
            }
            Assert.That(game.IsTerminal, Is.True, game.GameId + " did not finish within fixed test bound.");
            return game.Result();
        }

        private static T Field<T>(object source, string name)
        {
            FieldInfo? field = source.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field!.GetValue(source)!;
        }

        private static void SetField(object source, string name, object value)
        {
            FieldInfo? field = source.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field!.SetValue(source, value);
        }
    }
}
