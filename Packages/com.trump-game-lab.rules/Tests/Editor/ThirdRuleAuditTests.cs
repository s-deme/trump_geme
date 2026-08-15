using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TrumpLab.Games;

namespace TrumpLab.Tests
{
    public sealed class ThirdRuleAuditTests
    {
        [Test]
        public void BriscolaFixedSeedUsesTrumpDrawOrderAndScoresAllOneHundredTwentyPoints()
        {
            IGame game = BuiltInGames.Registry.Create("briscola", 2, 301);
            GameResult result = PlayToEnd(game, 301001);

            Assert.That(result.Turns, Is.EqualTo(40));
            Assert.That(result.Scores.Sum(), Is.EqualTo(120d));
            Assert.That(result.Reason, Is.EqualTo("card points (61 wins)"));

            IGame response = BuiltInGames.Registry.Create("briscola", 2, 302);
            List<List<Card>> hands = Field<List<List<Card>>>(response, "hands");
            hands[0].Clear(); hands[0].Add(new Card(Suit.Clubs, 2));
            hands[1].Clear(); hands[1].AddRange(new[] { new Card(Suit.Clubs, 1), new Card(Suit.Diamonds, 4) });
            response.Apply(new TrumpLab.Action("play", new Card(Suit.Clubs, 2)));
            Assert.That(response.LegalActions(), Has.Count.EqualTo(2),
                "The responder may discard off-suit; Briscola has no follow-suit duty.");
        }

        [Test]
        public void BriscolaViewAndCpuIgnoreTheOtherPlayersHand()
        {
            IGame left = BuiltInGames.Registry.Create("briscola", 2, 301);
            IGame right = BuiltInGames.Registry.Create("briscola", 2, 301);
            ReplaceHiddenCard(right, "hands", 1);

            Assert.That(right.View(0), Is.EqualTo(left.View(0)));
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()));
            Assert.That(right.ChooseCpuAction(0, new DeterministicRandom(301002)),
                Is.EqualTo(left.ChooseCpuAction(0, new DeterministicRandom(301002))));
        }

        [Test]
        public void BohemianSchneiderFixedSeedScoresHonorsAndRedealsATieWithoutCarry()
        {
            IGame completed = BuiltInGames.Registry.Create("bohemian_schneider", 2, 303,
                new Dictionary<string, string> { ["target_score"] = "1" });
            GameResult result = PlayToEnd(completed, 303001);
            Assert.That(result.Scores.Max(), Is.GreaterThanOrEqualTo(1d));
            Assert.That(result.Reason, Is.EqualTo("honors with Schneider and Schwarz bonuses"));

            IGame tied = BuiltInGames.Registry.Create("bohemian_schneider", 2, 304,
                new Dictionary<string, string> { ["target_score"] = "1" });
            int[] honors = Field<int[]>(tied, "honors");
            honors[0] = 10; honors[1] = 10;
            Invoke(tied, "FinishDeal");
            Assert.That(Field<int[]>(tied, "gamePoints"), Is.EqualTo(new[] { 0, 0 }),
                "A 10-10 honor deal is redealt rather than carried into the next deal.");

            Assert.That(BohemianSchneiderGame.BeatsByOne(new Card(Suit.Hearts, 10),
                new Card(Suit.Hearts, 9)), Is.True);
            Assert.That(BohemianSchneiderGame.BeatsByOne(new Card(Suit.Hearts, 11),
                new Card(Suit.Hearts, 9)), Is.False);
        }

        [Test]
        public void BohemianSchneiderViewAndCpuIgnoreTheOtherPlayersHand()
        {
            IGame left = BuiltInGames.Registry.Create("bohemian_schneider", 2, 303);
            IGame right = BuiltInGames.Registry.Create("bohemian_schneider", 2, 303);
            ReplaceHiddenCard(right, "hands", 0);

            Assert.That(right.View(1), Is.EqualTo(left.View(1)));
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()));
            Assert.That(right.ChooseCpuAction(1, new DeterministicRandom(303002)),
                Is.EqualTo(left.ChooseCpuAction(1, new DeterministicRandom(303002))));
        }

        [Test]
        public void DurakFixedSeedCompletesAndAllowsTrumpDefenseWithoutFollowingSuit()
        {
            IGame completed = BuiltInGames.Registry.Create("durak", 2, 305);
            GameResult result = PlayToEnd(completed, 305001);
            Assert.That(result.Reason, Is.EqualTo("first player out after the talon emptied").Or.EqualTo("draw: no durak"));
            Assert.That(result.Turns, Is.GreaterThan(20));

            IGame game = BuiltInGames.Registry.Create("durak", 2, 306);
            int attacker = Field<int>(game, "attacker");
            int defender = Field<int>(game, "defender");
            Suit trump = Field<Suit>(game, "trump");
            Suit plain = Enum.GetValues(typeof(Suit)).Cast<Suit>().First(suit => suit != trump);
            List<List<Card>> hands = Field<List<List<Card>>>(game, "hands");
            hands[attacker].Clear(); hands[attacker].Add(new Card(plain, 6));
            hands[defender].Clear(); hands[defender].AddRange(new[]
            {
                new Card(trump, 7), new Card(plain, 8)
            });
            game.Apply(new TrumpLab.Action("attack", new Card(plain, 6)));
            Assert.That(game.LegalActions().Select(action => action.Kind), Does.Contain("cover"));
            Assert.That(game.LegalActions().Any(action => action.Card == new Card(trump, 7)), Is.True,
                "A trump may cover an off-suit attack even when the defender holds the led suit.");
            Assert.That(game.View(), Does.Contain("face_up_trump="));
        }

        [Test]
        public void DurakViewAndCpuIgnoreTheDefendersPrivateHand()
        {
            IGame left = BuiltInGames.Registry.Create("durak", 2, 305);
            IGame right = BuiltInGames.Registry.Create("durak", 2, 305);
            int other = 1 - left.CurrentPlayer;
            ReplaceHiddenCard(right, "hands", other);

            Assert.That(right.View(left.CurrentPlayer), Is.EqualTo(left.View(left.CurrentPlayer)));
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()));
            Assert.That(right.ChooseCpuAction(left.CurrentPlayer, new DeterministicRandom(305002)),
                Is.EqualTo(left.ChooseCpuAction(left.CurrentPlayer, new DeterministicRandom(305002))));
        }

        [Test]
        public void OfficerSkatFixedSeedPlaysAllTricksAndJacksFollowAsTrump()
        {
            IGame completed = BuiltInGames.Registry.Create("officer_skat", 2, 307);
            GameResult result = PlayToEnd(completed, 307001);
            Assert.That(result.Turns, Is.EqualTo(33));
            Assert.That(result.Scores.Sum(), Is.EqualTo(120d));
            Assert.That(result.Reason, Is.EqualTo("majority of 120 card points").Or.EqualTo("60-60 favors the defender"));

            IGame game = BuiltInGames.Registry.Create("officer_skat", 2, 308);
            game.Apply(new TrumpLab.Action("choose_trump", value: "H"));
            List<List<List<Card>>> layout = Field<List<List<List<Card>>>>(game, "layout");
            foreach (List<List<Card>> playerLayout in layout)
                foreach (List<Card> pile in playerLayout) pile.Clear();
            layout[0][0].Add(new Card(Suit.Clubs, 11));
            layout[1][0].Add(new Card(Suit.Hearts, 7));
            layout[1][1].Add(new Card(Suit.Clubs, 1));

            game.Apply(new TrumpLab.Action("play", new Card(Suit.Clubs, 11)));
            Assert.That(game.LegalActions(), Is.EquivalentTo(new[]
            {
                new TrumpLab.Action("play", new Card(Suit.Hearts, 7))
            }), "A jack has trump effective-suit and must be followed by trump.");
        }

        [Test]
        public void OfficerSkatViewAndCpuIgnoreFaceDownCards()
        {
            IGame left = BuiltInGames.Registry.Create("officer_skat", 2, 307);
            IGame right = BuiltInGames.Registry.Create("officer_skat", 2, 307);
            left.Apply(new TrumpLab.Action("choose_trump", value: "C"));
            right.Apply(new TrumpLab.Action("choose_trump", value: "C"));
            List<List<List<Card>>> layout = Field<List<List<List<Card>>>>(right, "layout");
            Card original = layout[1][0][0];
            layout[1][0][0] = new Card(original.Suit, original.Rank == 7 ? 8 : 7);

            Assert.That(right.View(0), Is.EqualTo(left.View(0)));
            Assert.That(right.LegalActions(), Is.EqualTo(left.LegalActions()));
            Assert.That(right.ChooseCpuAction(0, new DeterministicRandom(307002)),
                Is.EqualTo(left.ChooseCpuAction(0, new DeterministicRandom(307002))));
        }

        [Test]
        public void PiquetExtendsOnlyToAnEighthDealBeforeDeclaringADraw()
        {
            IGame game = BuiltInGames.Registry.Create("piquet", 2, 309);
            int[] totals = Field<int[]>(game, "totalPoints");
            int[] dealPoints = Field<int[]>(game, "dealPoints");
            int[] tricks = Field<int[]>(game, "tricks");
            totals[0] = 100; totals[1] = 100;
            dealPoints[0] = 0; dealPoints[1] = 0;
            tricks[0] = 6; tricks[1] = 6;
            SetField(game, "dealsPlayed", 5);
            Invoke(game, "FinishDeal");

            Assert.That(Field<int>(game, "dealLimit"), Is.EqualTo(8));
            Assert.That(game.IsTerminal, Is.False);

            totals = Field<int[]>(game, "totalPoints");
            dealPoints = Field<int[]>(game, "dealPoints");
            tricks = Field<int[]>(game, "tricks");
            totals[0] = 140; totals[1] = 140;
            dealPoints[0] = 0; dealPoints[1] = 0;
            tricks[0] = 6; tricks[1] = 6;
            SetField(game, "dealsPlayed", 7);
            Invoke(game, "FinishDeal");

            Assert.That(game.IsTerminal, Is.True);
            Assert.That(game.Result().Winners, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(game.Result().Reason, Is.EqualTo("eight-deal Piquet draw"));
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

        private static T Field<T>(object source, string name)
        {
            FieldInfo? field = source.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field!.GetValue(source)!;
        }

        private static void Invoke(object source, string name)
        {
            MethodInfo? method = source.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method!.Invoke(source, null);
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
