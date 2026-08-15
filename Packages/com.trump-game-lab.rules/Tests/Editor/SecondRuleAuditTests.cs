using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TrumpLab.Games;

namespace TrumpLab.Tests
{
    public sealed class SecondRuleAuditTests
    {
        [Test]
        public void SonoFixedSeedRevealsAndScoresTheCompletedGridIncludingJokerClan()
        {
            IGame game = BuiltInGames.Registry.Create("sono", 2, 210);
            Assert.That(game.View(0), Does.Contain("?"), "The diagonal cards begin hidden.");

            GameResult result = PlayToEnd(game, 210001);

            Assert.That(result.Turns, Is.EqualTo(20));
            Assert.That(result.Reason, Is.EqualTo("column versus row poker and clan score"));
            Assert.That(result.Scores.Sum(), Is.GreaterThan(0d));
            Assert.That(SonoGame.LineScore(new Card?[]
            {
                new Card(Suit.Clubs, 9), new Card(Suit.Diamonds, 9),
                new Card(Suit.Hearts, 9), new Card(Suit.Spades, 9), null
            }), Is.EqualTo(13), "The joker may complete five-of-a-kind and the numeral clan.");
        }

        [Test]
        public void SonoViewAndCpuIgnoreTheOtherPlayersHiddenHand()
        {
            IGame left = BuiltInGames.Registry.Create("sono", 2, 210);
            IGame right = BuiltInGames.Registry.Create("sono", 2, 210);
            RotateHiddenHand(right, "hands", 1);

            Assert.That(right.View(0), Is.EqualTo(left.View(0)));
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()));
            Assert.That(right.ChooseCpuAction(0, new DeterministicRandom(210002)),
                Is.EqualTo(left.ChooseCpuAction(0, new DeterministicRandom(210002))));
        }

        [Test]
        public void CrispUsesMatchScoreStarterAndAllowsTripleOverQueen()
        {
            IGame game = BuiltInGames.Registry.Create("crisp", 2, 211);
            int[] points = Field<int[]>(game, "matchPoints");
            points[0] = 0; points[1] = 1;
            Invoke(game, "StartDeal");
            Assert.That(game.CurrentPlayer, Is.EqualTo(0), "The lower match score starts.");

            points[0] = 1; points[1] = 1;
            Invoke(game, "StartDeal");
            Assert.That(game.CurrentPlayer, Is.EqualTo(1), "A tie gives the start to the prior non-starter.");

            List<List<Card>> hands = Field<List<List<Card>>>(game, "hands");
            hands[0].Clear(); hands[0].AddRange(new[] { new Card(Suit.Clubs, 12), new Card(Suit.Clubs, 2) });
            hands[1].Clear(); hands[1].AddRange(new[]
            {
                new Card(Suit.Diamonds, 3), new Card(Suit.Hearts, 3), new Card(Suit.Spades, 3),
                new Card(Suit.Clubs, 4)
            });
            SetField(game, "currentCombo", null);
            SetCurrentPlayer(game, 0);
            Card queen = new Card(Suit.Clubs, 12);
            game.Apply(game.LegalActions().Single(action => action.Value == queen.ToString()));

            Assert.That(game.LegalActions().Any(action => action.Kind == "play" &&
                action.Value!.Split(',').Length == 3), Is.True,
                "A triple is the special reply permitted over a queen.");

            IGame completed = BuiltInGames.Registry.Create("crisp", 2, 212);
            GameResult result = PlayToEnd(completed, 212001);
            Assert.That(result.Scores.Max(), Is.EqualTo(3d));
            Assert.That(result.Reason, Is.EqualTo("first to three deals"));
        }

        [Test]
        public void CrispViewAndCpuIgnoreTheOtherPlayersHiddenHand()
        {
            IGame left = BuiltInGames.Registry.Create("crisp", 2, 211);
            IGame right = BuiltInGames.Registry.Create("crisp", 2, 211);
            ReplaceHiddenCard(right, "hands", 1);

            Assert.That(right.View(0), Is.EqualTo(left.View(0)));
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()));
            Assert.That(right.ChooseCpuAction(0, new DeterministicRandom(211001)),
                Is.EqualTo(left.ChooseCpuAction(0, new DeterministicRandom(211001))));
        }

        [Test]
        public void CribbageFixedSeedCoversDiscardPeggingShowAndCribFlushException()
        {
            IGame game = BuiltInGames.Registry.Create("cribbage", 2, 213);
            Assert.That(game.View(), Does.Contain("phase=discard"));

            GameResult result = PlayToEnd(game, 213001);

            Assert.That(result.Turns, Is.EqualTo(116), "The fixed seed crosses repeated discard, pegging and show cycles.");
            Assert.That(result.Reason, Is.EqualTo("first to 121"));
            Assert.That(result.Scores.Max(), Is.GreaterThanOrEqualTo(121d));
            Card[] fourFlush =
            {
                new Card(Suit.Clubs, 2), new Card(Suit.Clubs, 4),
                new Card(Suit.Clubs, 6), new Card(Suit.Clubs, 8)
            };
            Assert.That(CribbageGame.HandScore(fourFlush, new Card(Suit.Diamonds, 13), false), Is.EqualTo(4));
            Assert.That(CribbageGame.HandScore(fourFlush, new Card(Suit.Diamonds, 13), true), Is.EqualTo(0),
                "A four-card flush does not score in the crib.");
        }

        [Test]
        public void CribbageViewAndCpuIgnoreTheOtherPlayersHiddenHand()
        {
            IGame left = BuiltInGames.Registry.Create("cribbage", 2, 213);
            IGame right = BuiltInGames.Registry.Create("cribbage", 2, 213);
            ReplaceHiddenCard(right, "hands", 0);

            Assert.That(right.View(1), Is.EqualTo(left.View(1)));
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()));
            Assert.That(right.ChooseCpuAction(1, new DeterministicRandom(213002)),
                Is.EqualTo(left.ChooseCpuAction(1, new DeterministicRandom(213002))));
        }

        [Test]
        public void SuperTrumpFixedSeedUsesBothStagesAndTreatsSuperRankAsTrumpForFollowing()
        {
            IGame game = BuiltInGames.Registry.Create("super_trump", 2, 214);
            game.Apply(new TrumpLab.Action("choose_trump", value: "H"));
            game.Apply(new TrumpLab.Action("choose_super", value: "5"));
            List<List<Card>> hands = Field<List<List<Card>>>(game, "hands");
            hands[1].Clear(); hands[1].AddRange(new[] { new Card(Suit.Clubs, 5), new Card(Suit.Diamonds, 2) });
            hands[0].Clear(); hands[0].AddRange(new[] { new Card(Suit.Hearts, 7), new Card(Suit.Clubs, 9) });

            game.Apply(new TrumpLab.Action("play", new Card(Suit.Clubs, 5)));
            Assert.That(game.LegalActions(), Is.EquivalentTo(new[]
            {
                new TrumpLab.Action("play", new Card(Suit.Hearts, 7))
            }));

            IGame completed = BuiltInGames.Registry.Create("super_trump", 2, 215);
            GameResult result = PlayToEnd(completed, 215001);
            Assert.That(result.Turns, Is.EqualTo(54));
            Assert.That(result.Scores.Sum(), Is.EqualTo(39d));
            Assert.That(result.Scores.Max(), Is.GreaterThanOrEqualTo(20d));
        }

        [Test]
        public void SuperTrumpViewAndCpuIgnoreTheDealersHiddenHand()
        {
            IGame left = BuiltInGames.Registry.Create("super_trump", 2, 214);
            IGame right = BuiltInGames.Registry.Create("super_trump", 2, 214);
            ReplaceHiddenCard(right, "hands", 0);

            Assert.That(right.View(1), Is.EqualTo(left.View(1)));
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()));
            Assert.That(right.ChooseCpuAction(1, new DeterministicRandom(214001)),
                Is.EqualTo(left.ChooseCpuAction(1, new DeterministicRandom(214001))));
        }

        [Test]
        public void DaifugoFixedSeedCompletesAScoredMatchAndFourOfAKindStartsRevolution()
        {
            IGame completed = BuiltInGames.Registry.Create("daifugo_two", 2, 216,
                new Dictionary<string, string> { ["target_score"] = "1" });
            GameResult result = PlayToEnd(completed, 216001);
            Assert.That(result.Reason, Is.EqualTo("first to 1 remaining-card points"));
            Assert.That(result.Scores.Max(), Is.GreaterThanOrEqualTo(1d));

            IGame game = BuiltInGames.Registry.Create("daifugo_two", 2, 217,
                new Dictionary<string, string> { ["target_score"] = "1" });
            IList hands = Field<IList>(game, "hands");
            IList hand = (IList)hands[0]!;
            Type playingCardType = hand[0]!.GetType();
            hand.Clear();
            foreach (Suit suit in new[] { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades })
                hand.Add(Activator.CreateInstance(playingCardType, new object[] { new Card(suit, 7) })!);

            TrumpLab.Action revolution = game.LegalActions().Single(action => action.Kind == "play" &&
                action.Value!.StartsWith("G:0:", StringComparison.Ordinal) &&
                action.Value.Split(':')[2].Split(',').Length == 4);
            game.Apply(revolution);

            Assert.That(game.View(), Does.Contain("revolution=True"));
            Assert.That(game.IsTerminal, Is.True);
            Assert.That(game.Result().Scores.Max(), Is.EqualTo(16d));
        }

        [Test]
        public void DaifugoViewAndCpuIgnoreTheOtherPlayersHiddenHand()
        {
            IGame left = BuiltInGames.Registry.Create("daifugo_two", 2, 216);
            IGame right = BuiltInGames.Registry.Create("daifugo_two", 2, 216);
            RotateHiddenHand(right, "hands", 1);

            Assert.That(right.View(0), Is.EqualTo(left.View(0)));
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()));
            Assert.That(right.ChooseCpuAction(0, new DeterministicRandom(216002)),
                Is.EqualTo(left.ChooseCpuAction(0, new DeterministicRandom(216002))));
        }

        private static GameResult PlayToEnd(IGame game, int seed)
        {
            DeterministicRandom rng = new DeterministicRandom(seed);
            for (int step = 0; step < 2000 && !game.IsTerminal; step++)
            {
                TrumpLab.Action action = game.ChooseCpuAction(game.CurrentPlayer, rng);
                Assert.That(game.LegalActions(), Does.Contain(action));
                game.Apply(action);
            }
            Assert.That(game.IsTerminal, Is.True, game.GameId + " did not finish within fixed test bound.");
            return game.Result();
        }

        private static void ReplaceHiddenCard(object game, string fieldName, int player)
        {
            List<List<Card>> hands = Field<List<List<Card>>>(game, fieldName);
            Card original = hands[player][0];
            hands[player][0] = new Card(original.Suit, original.Rank == 1 ? 2 : 1);
        }

        private static void RotateHiddenHand(object game, string fieldName, int player)
        {
            IList hands = Field<IList>(game, fieldName);
            IList hand = (IList)hands[player]!;
            object first = hand[0]!;
            hand.RemoveAt(0);
            hand.Add(first);
        }

        private static T Field<T>(object source, string name)
        {
            FieldInfo? field = source.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field!.GetValue(source)!;
        }

        private static void SetField(object source, string name, object? value)
        {
            FieldInfo? field = source.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field!.SetValue(source, value);
        }

        private static void SetCurrentPlayer(IGame game, int value)
        {
            PropertyInfo? property = game.GetType().BaseType!.GetProperty("CurrentPlayer",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            property!.SetValue(game, value);
        }

        private static void Invoke(object source, string name)
        {
            MethodInfo? method = source.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method!.Invoke(source, null);
        }
    }
}
