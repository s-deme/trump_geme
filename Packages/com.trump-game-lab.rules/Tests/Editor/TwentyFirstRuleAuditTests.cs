using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TrumpLab.Games;

namespace TrumpLab.Tests
{
    public sealed class TwentyFirstRuleAuditTests
    {
        [Test]
        [Category("BroadSimulation")]
        public void Unit21FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            2401, "piquet", "five_hundred", "skat", "ulti", "doppelkopf", "schafkopf",
            "goninkan", "speed", "casino", "seven_bridge", "canasta", "pinochle",
            "texas_holdem", "five_card_draw");

        [Test]
        public void PiquetOffersCarteBlancheAndEachDeclarationCategoryExplicitly()
        {
            IGame game = BuiltInGames.Registry.Create("piquet", seed: 2420);
            for (int player = 0; player < 2; player++)
            {
                Assert.That(game.LegalActions(), Has.Some.Matches<TrumpLab.Action>(a => a.Kind == "sink_carte_blanche"));
                game.Apply(new TrumpLab.Action("sink_carte_blanche"));
            }
            game.Apply(game.LegalActions()[0]);
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.View(), Does.Contain("phase=declaration"));
            Assert.That(game.LegalActions(), Has.Some.Matches<TrumpLab.Action>(a =>
                a.Kind == "sink_declaration" && a.Value == "point"));
        }

        [Test]
        public void FiveHundredOrdersMisereCorrectlyAndMakesNoTrumpJokerNominationExplicit()
        {
            MethodInfo bidRank = StaticMethod(typeof(FiveHundredGame), "BidRank");
            MethodInfo bidScore = StaticMethod(typeof(FiveHundredGame), "BidScore");
            Assert.That((int)bidRank.Invoke(null, new object[] { "7N" })!, Is.LessThan((int)bidRank.Invoke(null, new object[] { "M" })!));
            Assert.That((int)bidRank.Invoke(null, new object[] { "M" })!, Is.LessThan((int)bidRank.Invoke(null, new object[] { "8N" })!));
            Assert.That((int)bidScore.Invoke(null, new object[] { "M" })!, Is.EqualTo(250));
            Assert.That((int)bidScore.Invoke(null, new object[] { "OM" })!, Is.EqualTo(500));

            bool found = false;
            for (int seed = 2430; seed < 2500 && !found; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("five_hundred", seed: seed);
                IList hands = (IList)Field(game, "hands").GetValue(game)!;
                IList hand = (IList)hands[game.CurrentPlayer]!;
                if (!hand.Cast<object>().Any(card => card.ToString() == "X")) continue;
                game.Apply(new TrumpLab.Action("bid", value: "6N"));
                game.Apply(new TrumpLab.Action("pass"));
                game.Apply(new TrumpLab.Action("pass"));
                for (int count = 0; count < 3; count++)
                    game.Apply(game.LegalActions().First(action => action.Kind == "discard_to_kitty" && action.Value != "X"));
                game.Apply(new TrumpLab.Action("finish_discard"));
                Assert.That(game.View(), Does.Contain("phase=joker_nomination"));
                Assert.That(game.LegalActions().Count, Is.EqualTo(5));
                found = true;
            }
            Assert.That(found, Is.True, "a deterministic opening with the Joker in the first bidder's hand");
        }

        [Test]
        public void SkatAndUltiExposeTheirFullContractChoiceStages()
        {
            IGame skat = BuiltInGames.Registry.Create("skat", seed: 2510,
                options: new Dictionary<string, string> { ["deals"] = "1" });
            skat.Apply(new TrumpLab.Action("bid", value: "18"));
            skat.Apply(new TrumpLab.Action("pass"));
            skat.Apply(new TrumpLab.Action("pass"));
            skat.Apply(new TrumpLab.Action("hand_game"));
            string[] contracts = skat.LegalActions().Select(action => action.Value!).ToArray();
            Assert.That(contracts, Does.Contain("G:O").And.Contain("N").And.Contain("NO"));

            IGame ulti = BuiltInGames.Registry.Create("ulti", seed: 2511,
                options: new Dictionary<string, string> { ["deals"] = "1" });
            ulti.Apply(ulti.LegalActions()[0]);
            ulti.Apply(ulti.LegalActions()[0]);
            ulti.Apply(ulti.LegalActions()[0]);
            ulti.Apply(new TrumpLab.Action("finish_discard"));
            Assert.That(ulti.View(), Does.Contain("phase=auction"));
            Assert.That(ulti.LegalActions(), Has.Some.Matches<TrumpLab.Action>(a => a.Kind == "take_talon"));
            Assert.That(ulti.LegalActions(), Has.Some.Matches<TrumpLab.Action>(a => a.Kind == "bid_without_talon"));
        }

        [Test]
        public void DoppelkopfAnnouncementsAndSchafkopfCounterRaisesArePlayableActions()
        {
            bool sawAnnouncement = false;
            for (int seed = 2520; seed < 2560 && !sawAnnouncement; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("doppelkopf", seed: seed,
                    options: new Dictionary<string, string> { ["deals"] = "1" });
                for (int player = 0; player < 4; player++) game.Apply(new TrumpLab.Action("contract", value: "normal"));
                Assert.That(game.View(), Does.Contain("phase=announce"));
                sawAnnouncement = game.LegalActions().Any(action => action.Kind == "announce_re" || action.Kind == "announce_kontra");
            }
            Assert.That(sawAnnouncement, Is.True);

            IGame schafkopf = BuiltInGames.Registry.Create("schafkopf", seed: 2561,
                options: new Dictionary<string, string> { ["deals"] = "1" });
            schafkopf.Apply(new TrumpLab.Action("bid", value: "wenz"));
            schafkopf.Apply(new TrumpLab.Action("bid", value: "solo:C"));
            for (int pass = 0; pass < 3; pass++) schafkopf.Apply(new TrumpLab.Action("pass"));
            Assert.That(schafkopf.View(), Does.Contain("phase=stoss").And.Contain("contract=solo"));
            schafkopf.Apply(new TrumpLab.Action("stoss"));
            Assert.That(schafkopf.View(), Does.Contain("multiplier=x2"));
            Assert.That(schafkopf.LegalActions(), Has.Some.Matches<TrumpLab.Action>(a => a.Kind == "gegenstoss"));
        }

        [Test]
        public void GoninkanDoubleRelationExchangeKeepsTenCardHands()
        {
            bool found = false;
            for (int seed = 2570; seed < 2700 && !found; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("goninkan", seed: seed);
                if (!game.View().Contains("phase=double_relation_exchange")) continue;
                game.Apply(game.LegalActions()[0]);
                Assert.That(game.View(), Does.Contain("phase=play"));
                IList hands = (IList)Field(game, "hands").GetValue(game)!;
                Assert.That(hands.Cast<IList>().Select(hand => hand.Count), Is.All.EqualTo(10));
                found = true;
            }
            Assert.That(found, Is.True);
        }

        [Test]
        public void SpeedReplenishesEachCenterOnlyFromItsOwnersReserve()
        {
            IGame game = BuiltInGames.Registry.Create("speed", seed: 2710);
            var layouts = (List<List<Card>>)Field(game, "layouts").GetValue(game)!;
            var reserves = (List<List<Card>>)Field(game, "reserves").GetValue(game)!;
            var centers = (Card[])Field(game, "centers").GetValue(game)!;
            layouts[0].Clear(); layouts[0].Add(new Card(Suit.Clubs, 7));
            layouts[1].Clear(); layouts[1].Add(new Card(Suit.Diamonds, 7));
            reserves[0].Clear(); reserves[1].Clear(); reserves[1].Add(new Card(Suit.Hearts, 9));
            centers[0] = new Card(Suit.Clubs, 2); centers[1] = new Card(Suit.Spades, 12);
            game.Apply(new TrumpLab.Action("pass"));
            game.Apply(new TrumpLab.Action("pass"));
            Assert.That(centers[0], Is.EqualTo(new Card(Suit.Clubs, 2)));
            Assert.That(centers[1], Is.EqualTo(new Card(Suit.Hearts, 9)));
            Assert.That(reserves.Select(reserve => reserve.Count), Is.EqualTo(new[] { 0, 0 }));
        }

        [Test]
        public void CasinoOffersOwnedBuildRaisesAndSevenBridgeRecognizesTwoCardSevenMelds()
        {
            IGame casino = BuiltInGames.Registry.Create("casino", seed: 2720);
            var hands = (List<List<Card>>)Field(casino, "hands").GetValue(casino)!;
            IList table = (IList)Field(casino, "table").GetValue(casino)!;
            hands[casino.CurrentPlayer].Clear();
            hands[casino.CurrentPlayer].AddRange(new[] { new Card(Suit.Clubs, 5), new Card(Suit.Diamonds, 9) });
            Type entryType = table[0]!.GetType(); table.Clear();
            object build = Activator.CreateInstance(entryType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { new[] { new Card(Suit.Hearts, 4) }, (int?)4, (int?)((casino.CurrentPlayer + 1) % 2) }, null)!;
            table.Add(build);
            Assert.That(casino.LegalActions(), Has.Some.Matches<TrumpLab.Action>(a => a.Kind == "raise_build" && a.Target == 9));

            Type rules = typeof(CanastaGame).Assembly.GetType("TrumpLab.Games.RummyRules")!;
            MethodInfo newMelds = rules.GetMethod("NewMelds", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
            var cards = new List<Card>
            {
                new Card(Suit.Clubs, 7), new Card(Suit.Diamonds, 7),
                new Card(Suit.Clubs, 6), new Card(Suit.Clubs, 8)
            };
            var melds = ((IEnumerable)newMelds.Invoke(null, new object[] { cards, true, false })!).Cast<int[]>().ToArray();
            Assert.That(melds, Has.Some.EqualTo(new[] { 0, 1 }));
            Assert.That(melds, Has.Some.EqualTo(new[] { 0, 2 }));
            Assert.That(melds, Has.Some.EqualTo(new[] { 0, 3 }));
        }

        [Test]
        public void CanastaPermissionIsRequesterScopedAndSeed98Completes()
        {
            IGame game = BuiltInGames.Registry.Create("canasta", seed: 98,
                options: new Dictionary<string, string> { ["target_score"] = "500" });
            Field(game, "outPermission").SetValue(game, true);
            Field(game, "outRequester").SetValue(game, game.CurrentPlayer);
            MethodInfo permission = Method(game, "HasOutPermission");
            Assert.That((bool)permission.Invoke(game, new object[] { game.CurrentPlayer })!, Is.True);
            Assert.That((bool)permission.Invoke(game, new object[] { (game.CurrentPlayer + 1) % 4 })!, Is.False);
            Assert.That(RuleAuditTestSupport.PlayWithLegalCpu(game, 273000).Winners, Is.Not.Empty);
        }

        [Test]
        public void PinochleUsesRacehorseExchangeAndScoresDix()
        {
            IGame game = BuiltInGames.Registry.Create("pinochle", seed: 2740,
                options: new Dictionary<string, string> { ["target_score"] = "1" });
            game.Apply(new TrumpLab.Action("bid", value: "20"));
            for (int pass = 0; pass < 3; pass++) game.Apply(new TrumpLab.Action("pass"));
            game.Apply(new TrumpLab.Action("name_trump", value: "H"));
            Assert.That(game.View(), Does.Contain("phase=partner_pass"));
            Assert.That(game.LegalActions().Count, Is.EqualTo(220));
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.View(), Does.Contain("phase=bidder_return"));
            Assert.That(game.LegalActions().Count, Is.EqualTo(455));
            Assert.That(PinochleGame.MeldScore(new[] { new Card(Suit.Hearts, 9) }, Suit.Hearts), Is.EqualTo(1));
        }

        [Test]
        public void PokerSessionsConserveChipsAndFiveCardDrawCarriesAnUnopenedPot()
        {
            IGame holdEm = BuiltInGames.Registry.Create("texas_holdem", players: 6, seed: 2750,
                options: new Dictionary<string, string> { ["starting_stack"] = "5" });
            GameResult holdEmResult = RuleAuditTestSupport.PlayWithLegalCpu(holdEm, 275000);
            Assert.That(holdEmResult.Scores.Sum(), Is.EqualTo(30));
            Assert.That((int)holdEmResult.Extra["hands"], Is.GreaterThanOrEqualTo(1));

            IGame draw = BuiltInGames.Registry.Create("five_card_draw", players: 4, seed: 2751,
                options: new Dictionary<string, string> { ["starting_stack"] = "5" });
            GameResult drawResult = RuleAuditTestSupport.PlayWithLegalCpu(draw, 275100);
            Assert.That(drawResult.Scores.Sum(), Is.EqualTo(20));
            Assert.That((int)drawResult.Extra["hands"], Is.GreaterThanOrEqualTo(1));

            IGame carry = BuiltInGames.Registry.Create("five_card_draw", players: 2, seed: 2752);
            carry.Apply(new TrumpLab.Action("check"));
            carry.Apply(new TrumpLab.Action("check"));
            Assert.That(carry.View(), Does.Contain("hand=2").And.Contain("pot=4"));
        }

        private static FieldInfo Field(object source, string name)
        {
            Type? type = source.GetType();
            while (type != null)
            {
                FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) return field;
                type = type.BaseType;
            }
            throw new InvalidOperationException("Missing field " + name);
        }

        private static MethodInfo Method(object source, string name) => source.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("Missing method " + name);

        private static MethodInfo StaticMethod(Type type, string name) => type
            .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("Missing static method " + name);
    }
}
