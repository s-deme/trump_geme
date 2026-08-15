using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class FourthRuleAuditTests
    {
        [Test]
        public void Unit04FixedSeedsCompleteAndCpuActionsAreLegal()
        {
            RuleAuditTestSupport.AssertFixedSeedBatch(401,
                "klaberjass", "norwegian_whist", "schnapsen", "goldmine", "knave");
        }

        [Test]
        public void Unit04BoundaryValuesAndObservationEquivalence()
        {
            IGame klaberjass = BuiltInGames.Registry.Create("klaberjass", 2, 411,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(klaberjass.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "take", "pass" }));
            Assert.That(klaberjass.View(), Does.Contain("hand_counts=[6,6]"));
            klaberjass.Apply(new TrumpLab.Action("take"));
            Assert.That(klaberjass.View(), Does.Contain("hand_counts=[9,9]"));

            IGame norwegian = BuiltInGames.Registry.Create("norwegian_whist", 2, 412,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(norwegian.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "bid_high", "bid_low" }));
            RuleAuditTestSupport.PlayWithLegalCpu(norwegian, 412001);
            Assert.That(norwegian.Result().Turns, Is.InRange(53, 54), "13 four-card tricks plus one or two bids");

            IGame schnapsen = BuiltInGames.Registry.Create("schnapsen", seed: 413);
            Assert.That(schnapsen.LegalActions().Select(action => action.Kind), Does.Contain("close_talon"));
            Assert.That(schnapsen.View(), Does.Contain("trump_card="));
            AssertSchnapsenLastTrickHasCheckOutBoundary();

            IGame goldmine = BuiltInGames.Registry.Create("goldmine", seed: 414);
            Assert.That(goldmine.LegalActions().Select(action => action.Kind), Does.Contain("inspect"));
            Assert.That(goldmine.LegalActions().Select(action => action.Kind), Does.Contain("exchange"));

            RuleAuditTestSupport.AssertOpeningObservationIgnoresOpponentHandAndStock("klaberjass", 421);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresOpponentHandAndFaceDownLayout("norwegian_whist", 422);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresOpponentHandAndStock("schnapsen", 423);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresOpponentHandAndStock("goldmine", 424);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("knave", 425);
        }

        [Test]
        public void KlabberjassMeldDeclarationsArePublicComparedAndRevealed()
        {
            IGame game = BuiltInGames.Registry.Create("klaberjass", 2, 427);
            game.Apply(new TrumpLab.Action("take"));
            List<List<Card>> hands = Field<List<List<Card>>>(game, "hands");
            int elder = Field<int>(game, "elder");
            int dealer = 1 - elder;
            hands[elder].Clear(); hands[dealer].Clear();
            hands[elder].AddRange(new[]
            {
                new Card(Suit.Hearts, 7), new Card(Suit.Hearts, 8), new Card(Suit.Hearts, 9),
                new Card(Suit.Hearts, 10), new Card(Suit.Hearts, 11)
            });
            hands[dealer].AddRange(new[] { new Card(Suit.Clubs, 7), new Card(Suit.Diamonds, 1) });
            SetField(game, "trump", (Suit?)Suit.Spades);

            Assert.That(game.LegalActions(), Does.Contain(new TrumpLab.Action("declare_meld", value: "50")),
                "5枚sequenceは4枚以上=50点として宣言する");
            game.Apply(new TrumpLab.Action("declare_meld", value: "50"));
            string beforeReply = game.View(dealer);
            string publicClaims = elder == 0 ? "meld=[P0:50,P1:-]" : "meld=[P0:-,P1:50]";
            Assert.That(beforeReply, Does.Contain(publicClaims));
            Assert.That(beforeReply, Does.Not.Contain("7H 8H 9H 10H JH"),
                "比較前に宣言者の具体的なsequenceを漏らさない");

            IGame samePublicState = BuiltInGames.Registry.Create("klaberjass", 2, 427);
            samePublicState.Apply(new TrumpLab.Action("take"));
            List<List<Card>> samePublicHands = Field<List<List<Card>>>(samePublicState, "hands");
            int samePublicElder = Field<int>(samePublicState, "elder");
            int samePublicDealer = 1 - samePublicElder;
            samePublicHands[samePublicElder].Clear(); samePublicHands[samePublicDealer].Clear();
            samePublicHands[samePublicElder].AddRange(new[]
            {
                new Card(Suit.Hearts, 9), new Card(Suit.Hearts, 10), new Card(Suit.Hearts, 11),
                new Card(Suit.Hearts, 12), new Card(Suit.Hearts, 13)
            });
            samePublicHands[samePublicDealer].AddRange(new[] { new Card(Suit.Clubs, 7), new Card(Suit.Diamonds, 1) });
            SetField(samePublicState, "trump", (Suit?)Suit.Spades);
            samePublicState.Apply(new TrumpLab.Action("declare_meld", value: "50"));
            Assert.That(samePublicState.View(samePublicDealer), Is.EqualTo(beforeReply),
                "同じ公開20/50宣言なら未公開の最高rankだけを変えても相手のViewは同一");
            Assert.That(samePublicState.ChooseCpuAction(samePublicDealer, new DeterministicRandom(427001)),
                Is.EqualTo(game.ChooseCpuAction(dealer, new DeterministicRandom(427001))),
                "CPUのmeld応答は未公開sequenceに依存しない");

            game.Apply(new TrumpLab.Action("meld_reply", value: "lose"));

            string revealed = game.View(dealer);
            Assert.That(revealed, Does.Contain($"meld_winner=P{elder}"));
            Assert.That(revealed, Does.Contain($"meld_reveals=[P{elder}:7H 8H 9H 10H JH]"));
            Assert.That(Field<int[]>(game, "dealPoints")[elder], Is.EqualTo(50));

            IGame tied = BuiltInGames.Registry.Create("klaberjass", 2, 428);
            tied.Apply(new TrumpLab.Action("take"));
            List<List<Card>> tiedHands = Field<List<List<Card>>>(tied, "hands");
            int tiedElder = Field<int>(tied, "elder");
            int tiedDealer = 1 - tiedElder;
            tiedHands[tiedElder].Clear(); tiedHands[tiedDealer].Clear();
            tiedHands[tiedElder].AddRange(new[]
            {
                new Card(Suit.Hearts, 7), new Card(Suit.Hearts, 8), new Card(Suit.Hearts, 9)
            });
            tiedHands[tiedDealer].AddRange(new[]
            {
                new Card(Suit.Diamonds, 7), new Card(Suit.Diamonds, 8), new Card(Suit.Diamonds, 9)
            });
            SetField(tied, "trump", (Suit?)Suit.Spades);

            tied.Apply(new TrumpLab.Action("declare_meld", value: "20"));
            tied.Apply(new TrumpLab.Action("meld_reply", value: "tie"));
            Assert.That(tied.LegalActions(), Is.EquivalentTo(new[] { new TrumpLab.Action("declare_meld_high", value: "9") }));
            tied.Apply(new TrumpLab.Action("declare_meld_high", value: "9"));
            tied.Apply(new TrumpLab.Action("meld_reply", value: "tie"));
            Assert.That(tied.LegalActions(), Is.EquivalentTo(new[] { new TrumpLab.Action("declare_meld_trump", value: "plain") }));
            tied.Apply(new TrumpLab.Action("declare_meld_trump", value: "plain"));
            tied.Apply(new TrumpLab.Action("meld_reply", value: "tie"));

            Assert.That(tied.View(), Does.Contain("phase=play"));
            Assert.That(tied.View(), Does.Contain("meld_winner=-"));
            Assert.That(tied.View(), Does.Contain("meld_reveals=[]"));
            Assert.That(Field<int[]>(tied, "dealPoints"), Is.EqualTo(new[] { 0, 0 }), "同一meldは双方無得点");
        }

        [Test]
        public void GoldmineUsesTheTarteActionOrderPrivateInspectionAndNoFollow()
        {
            IGame game = BuiltInGames.Registry.Create("goldmine", 2, 429);
            List<Card> stock = Field<List<Card>>(game, "stock");
            Card[] prizes = Field<Card[]>(game, "prizes");
            int dealer = Field<int>(game, "dealer");
            int firstActor = game.CurrentPlayer;
            int secondActor = 1 - firstActor;
            Card indicator = stock[0];
            Assert.That(firstActor, Is.EqualTo(1 - dealer));
            Assert.That(game.View(firstActor), Does.Contain("stock=6"));

            game.Apply(new TrumpLab.Action("inspect", target: 0));
            Assert.That(game.View(firstActor), Does.Contain($"prizes=[{prizes[0].Rank}"));
            Assert.That(game.View(secondActor), Does.Contain("prizes=[?"),
                "調査した金塊のrankは調査者以外には公開しない");
            Assert.That(game.LegalActions().Select(action => action.Kind), Is.All.EqualTo("exchange"));
            Card topDraw = stock[stock.Count - 1];
            TrumpLab.Action exchange = game.LegalActions().First();
            game.Apply(exchange);
            Assert.That(Field<List<List<Card>>>(game, "hands")[secondActor], Does.Contain(topDraw));
            Assert.That(stock[0], Is.EqualTo(indicator), "表向きtrump indicatorは最後のdrawまで残す");

            int leader = game.CurrentPlayer;
            int follower = 1 - leader;
            List<List<Card>> hands = Field<List<List<Card>>>(game, "hands");
            hands[leader].Clear(); hands[follower].Clear();
            hands[leader].Add(new Card(Suit.Hearts, 2));
            hands[follower].AddRange(new[] { new Card(Suit.Hearts, 3), new Card(Suit.Clubs, 7) });
            SetField(game, "trump", Suit.Diamonds);
            game.Apply(new TrumpLab.Action("play", new Card(Suit.Hearts, 2)));
            Assert.That(game.LegalActions(), Does.Contain(new TrumpLab.Action("play", new Card(Suit.Clubs, 7))),
                "Goldmineはmust-followを課さない");
            game.Apply(new TrumpLab.Action("play", new Card(Suit.Clubs, 7)));
            Assert.That(Field<int[]>(game, "scores")[leader], Is.EqualTo(prizes[0].Rank),
                "lead suitの高札が勝ち、現在の金塊rankを得る");
        }

        [Test]
        public void KnaveUsesTurnUpTrumpAndSuitSpecificJackPenalties()
        {
            IGame game = BuiltInGames.Registry.Create("knave", 3, 430);
            List<List<Card>> hands = Field<List<List<Card>>>(game, "Hands");
            int dealer = Field<int>(game, "Dealer");
            Assert.That(game.CurrentPlayer, Is.EqualTo((dealer + 1) % 3));
            Assert.That(Field<Suit?>(game, "Trump"), Is.Not.Null);
            Assert.That(hands.Select(hand => hand.Count), Is.EqualTo(new[] { 17, 17, 17 }));

            int leader = game.CurrentPlayer;
            int next = (leader + 1) % 3;
            int last = (next + 1) % 3;
            hands[0].Clear(); hands[1].Clear(); hands[2].Clear();
            hands[leader].Add(new Card(Suit.Hearts, 11));
            hands[next].Add(new Card(Suit.Diamonds, 11));
            hands[last].Add(new Card(Suit.Clubs, 11));
            SetField(game, "Trump", (Suit?)Suit.Hearts);
            game.Apply(new TrumpLab.Action("play", new Card(Suit.Hearts, 11)));
            game.Apply(new TrumpLab.Action("play", new Card(Suit.Diamonds, 11)));
            game.Apply(new TrumpLab.Action("play", new Card(Suit.Clubs, 11)));

            Assert.That(Field<int[]>(game, "TotalScores")[leader], Is.EqualTo(-8),
                "1 trick - JH4 - JD3 - JC2");
        }

        private static void AssertSchnapsenLastTrickHasCheckOutBoundary()
        {
            IGame game = BuiltInGames.Registry.Create("schnapsen", seed: 426);
            List<List<Card>> hands = Field<List<List<Card>>>(game, "hands");
            List<Card> stock = Field<List<Card>>(game, "stock");
            int[] cardPoints = Field<int[]>(game, "cardPoints");
            int[] gamePoints = Field<int[]>(game, "gamePoints");
            Field<Card?>(game, "trumpCard");
            int winner = game.CurrentPlayer;
            int follower = 1 - winner;
            hands[0].Clear(); hands[1].Clear(); stock.Clear();
            hands[winner].Add(new Card(Suit.Hearts, 1)); hands[follower].Add(new Card(Suit.Hearts, 10));
            cardPoints[winner] = 55; gamePoints[winner] = 6;
            SetField(game, "trumpCard", null);
            game.Apply(new TrumpLab.Action("play", new Card(Suit.Hearts, 1)));
            game.Apply(new TrumpLab.Action("play", new Card(Suit.Hearts, 10)));
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "claim_66", "settle_last_trick" }));
            game.Apply(new TrumpLab.Action("settle_last_trick"));
            Assert.That(game.IsTerminal, Is.True);
            Assert.That(game.Result().Scores[winner], Is.EqualTo(7d), "the unclaimed last trick is one game point");
            Assert.That(((int[])game.Result().Extra["card_points"])[winner], Is.EqualTo(76),
                "the adopted Schnapsen variant has no last-trick card-point bonus");
        }

        private static T Field<T>(object source, string name)
        {
            FieldInfo? field = FindField(source, name);
            Assert.That(field, Is.Not.Null, name);
            return (T)field!.GetValue(source)!;
        }

        private static void SetField(object source, string name, object? value)
        {
            FieldInfo? field = FindField(source, name);
            Assert.That(field, Is.Not.Null, name);
            field!.SetValue(source, value);
        }

        private static FieldInfo? FindField(object source, string name)
        {
            for (System.Type? type = source.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) return field;
            }
            return null;
        }
    }
}
