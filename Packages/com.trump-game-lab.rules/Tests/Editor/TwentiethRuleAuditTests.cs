using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class TwentiethRuleAuditTests
    {
        [Test]
        [Category("BroadSimulation")]
        public void Unit20FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            2001, "sasaki_44a", "toepen", "war", "blackjack", "crazy_eights",
            "cheat", "hearts", "spades", "twenty_four");

        [Test]
        public void SasakiRunExchangesTheTwoRedTensAndMakesTeamsPublic()
        {
            IGame? game = null;
            List<List<Card>>? hands = null;
            int diamondOwner = -1;
            int heartOwner = -1;
            for (int seed = 2010; seed < 2050 && game == null; seed++)
            {
                IGame candidate = BuiltInGames.Registry.Create("sasaki_44a", seed: seed,
                    options: new Dictionary<string, string> { ["target_score"] = "1" });
                var candidateHands = (List<List<Card>>)Field(candidate, "hands").GetValue(candidate)!;
                int diamond = candidateHands.FindIndex(hand => hand.Contains(new Card(Suit.Diamonds, 10)));
                int heart = candidateHands.FindIndex(hand => hand.Contains(new Card(Suit.Hearts, 10)));
                if (diamond != heart)
                { game = candidate; hands = candidateHands; diamondOwner = diamond; heartOwner = heart; }
            }
            Assert.That(game, Is.Not.Null);
            while (game!.CurrentPlayer != diamondOwner && game.CurrentPlayer != heartOwner)
                game.Apply(new TrumpLab.Action("keep_hidden"));
            game.Apply(new TrumpLab.Action("run"));
            Assert.That(hands![diamondOwner], Does.Contain(new Card(Suit.Hearts, 10)));
            Assert.That(hands[heartOwner], Does.Contain(new Card(Suit.Diamonds, 10)));
            Assert.That(game.View(0), Does.Contain("phase=stop_offer").And.Contain("P0:"));
        }

        [Test]
        public void ToepenEveryOpponentMayChallengeAnExchangeAndEveryPlayerGetsAKnockOffer()
        {
            IGame game = BuiltInGames.Registry.Create("toepen", players: 4, seed: 2060);
            int exchanger = game.CurrentPlayer;
            game.Apply(new TrumpLab.Action("exchange_hand"));
            var challengers = new List<int>();
            for (int index = 0; index < 3; index++)
            {
                challengers.Add(game.CurrentPlayer);
                Assert.That(game.View(), Does.Contain("phase=challenge"));
                game.Apply(new TrumpLab.Action("accept_exchange"));
            }
            Assert.That(challengers, Is.EqualTo(Enumerable.Range(1, 3)
                .Select(offset => (exchanger + offset) % 4)));
            while (game.View().Contains("phase=exchange")) game.Apply(new TrumpLab.Action("keep_hand"));
            var offered = new List<int>();
            for (int index = 0; index < 4; index++)
            {
                offered.Add(game.CurrentPlayer);
                Assert.That(game.View(), Does.Contain("phase=knock_offer"));
                game.Apply(new TrumpLab.Action("decline_knock"));
            }
            Assert.That(offered.Distinct().Count(), Is.EqualTo(4));
        }

        [Test]
        public void MultiplayerWarLetsANonTiedPlayerWinTheWarRound()
        {
            IGame game = BuiltInGames.Registry.Create("war", players: 4, seed: 2070);
            var piles = (List<Queue<Card>>)Field(game, "piles").GetValue(game)!;
            Card[][] cards =
            {
                new[] { new Card(Suit.Clubs, 13), new Card(Suit.Clubs, 2), new Card(Suit.Clubs, 5) },
                new[] { new Card(Suit.Diamonds, 13), new Card(Suit.Diamonds, 2), new Card(Suit.Diamonds, 6) },
                new[] { new Card(Suit.Hearts, 3), new Card(Suit.Hearts, 2), new Card(Suit.Hearts, 1) },
                new[] { new Card(Suit.Spades, 4), new Card(Suit.Spades, 2), new Card(Suit.Spades, 7) }
            };
            for (int player = 0; player < 4; player++)
            { piles[player].Clear(); foreach (Card card in cards[player]) piles[player].Enqueue(card); }
            game.Apply(new TrumpLab.Action("battle"));
            Assert.That(game.IsTerminal, Is.True);
            Assert.That(game.Result().Winners, Is.EqualTo(new[] { 2 }));
            Assert.That(game.Result().Scores[2], Is.EqualTo(12));
        }

        [Test]
        public void BlackjackAllowsAnyInitialTwoCardsToDoubleAndSplitAcesToContinue()
        {
            bool found = false;
            for (int seed = 2080; seed < 5000 && !found; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("blackjack", players: 1, seed: seed);
                if (game.IsTerminal) continue;
                while (game.View().Contains("phase=insurance"))
                    game.Apply(new TrumpLab.Action("decline_insurance"));
                if (game.IsTerminal) continue;
                List<Card> cards = ActiveBlackjackCards(game);
                if (cards.Count != 2 || cards[0].Rank != 1 || cards[1].Rank != 1) continue;
                Assert.That(TrumpLab.Games.BlackjackGame.HandValue(cards), Is.EqualTo(12));
                Assert.That(game.LegalActions(), Has.Some.Matches<TrumpLab.Action>(a => a.Kind == "double"));
                game.Apply(new TrumpLab.Action("split"));
                if (game.IsTerminal || !game.LegalActions().Any(action => action.Kind == "hit")) continue;
                Assert.That(game.View(), Does.Contain("H0:").And.Contain(" | H1:"));
                found = true;
            }
            Assert.That(found, Is.True, "a deterministic split-ace opening with a playable first hand");
        }

        [Test]
        public void CrazyEightsUsesPagatDealDrawAndStarterEightRules()
        {
            IGame game = BuiltInGames.Registry.Create("crazy_eights", players: 2, seed: 2090);
            if (game.LegalActions().All(action => action.Kind == "choose_starter_suit"))
                game.Apply(game.LegalActions()[0]);
            Assert.That(game.View(), Does.Contain("hands=[7,7]"));
            int player = game.CurrentPlayer;
            var hands = (List<List<Card>>)Field(game, "hands").GetValue(game)!;
            int before = hands[player].Count;
            Assert.That(game.LegalActions(), Has.Some.Matches<TrumpLab.Action>(a => a.Kind == "draw"));
            game.Apply(new TrumpLab.Action("draw"));
            Assert.That(hands[player].Count, Is.EqualTo(before + 1));
            Assert.That(game.CurrentPlayer, Is.EqualTo((player + 1) % 2));

            bool sawStarterEight = false;
            for (int seed = 2091; seed < 2200 && !sawStarterEight; seed++)
            {
                IGame candidate = BuiltInGames.Registry.Create("crazy_eights", players: 3, seed: seed);
                if (candidate.LegalActions().All(action => action.Kind == "choose_starter_suit"))
                {
                    Assert.That(candidate.LegalActions().Count, Is.EqualTo(4));
                    Assert.That(candidate.CurrentPlayer, Is.EqualTo(2));
                    sawStarterEight = true;
                }
            }
            Assert.That(sawStarterEight, Is.True);
        }

        [Test]
        public void CheatCanClaimAnyNonEmptyNumberOfCardsWithoutSubsetExplosion()
        {
            IGame game = BuiltInGames.Registry.Create("cheat", players: 10, seed: 2201);
            for (int count = 0; count < 5; count++)
                game.Apply(new TrumpLab.Action("select_claim_card", value: "0"));
            Assert.That(game.LegalActions(), Has.Some.Matches<TrumpLab.Action>(a => a.Kind == "finish_claim"));
            game.Apply(new TrumpLab.Action("finish_claim"));
            Assert.That(game.View(), Does.Contain("phase=challenge").And.Contain("/5"));
            Assert.That(game.LegalActions().Count, Is.EqualTo(2));
        }

        [Test]
        public void SixPlayerHeartsUsesCancellationDeckAndScoresAllFiftyTwoPenaltyPoints()
        {
            IGame game = BuiltInGames.Registry.Create("hearts", players: 6, seed: 2210,
                options: new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.View(), Does.Contain("phase=play").And.Contain("hands=[17,17,17,17,17,17]"));
            GameResult result = RuleAuditTestSupport.PlayWithLegalCpu(game, 221000);
            int[] penalties = (int[])result.Extra["penalties"];
            Assert.That(result.Turns, Is.EqualTo(102));
            Assert.That(penalties.Sum(), Is.EqualTo(52).Or.EqualTo(260));
        }

        [Test]
        public void SpadesScoresNilSeparatelyFromThePartnersContractAndBags()
        {
            IGame game = BuiltInGames.Registry.Create("spades", seed: 2220,
                options: new Dictionary<string, string> { ["target_score"] = "1" });
            int[] bids = (int[])Field(game, "bids").GetValue(game)!;
            int[] tricks = (int[])Field(game, "tricks").GetValue(game)!;
            Array.Copy(new[] { 0, 4, 4, 4 }, bids, 4);
            Array.Copy(new[] { 0, 4, 4, 5 }, tricks, 4);
            Method(game, "ScoreHand").Invoke(game, null);
            GameResult result = game.Result();
            Assert.That(result.Scores, Is.EqualTo(new[] { 140d, 81d, 140d, 81d }));
            Assert.That((int[])result.Extra["bags"], Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void TwentyFourUsesPrivateStacksAndAwardsTwoPointsAfterAWrongNoSolutionClaim()
        {
            bool sawPrivateTransfer = false;
            for (int seed = 2230; seed < 2300 && !sawPrivateTransfer; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("twenty_four", players: 2, seed: seed);
                if (!(bool)Field(game, "solvable").GetValue(game)!) continue;
                Assert.That(game.View(), Does.Contain("cards_left=18,18"));
                int claimant = game.CurrentPlayer;
                game.Apply(new TrumpLab.Action("claim_24"));
                Assert.That(game.View(), Does.Contain(claimant == 0 ? "cards_left=16,20" : "cards_left=20,16"));
                sawPrivateTransfer = true;
            }
            Assert.That(sawPrivateTransfer, Is.True);

            bool sawDoublePoint = false;
            for (int seed = 2300; seed < 2400 && !sawDoublePoint; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("twenty_four", players: 3, seed: seed,
                    options: new Dictionary<string, string> { ["target_score"] = "1" });
                if (!(bool)Field(game, "solvable").GetValue(game)!) continue;
                int first = game.CurrentPlayer;
                game.Apply(new TrumpLab.Action("no_solution"));
                int solver = game.CurrentPlayer;
                game.Apply(new TrumpLab.Action("claim_24"));
                Assert.That(solver, Is.Not.EqualTo(first));
                Assert.That(game.Result().Scores[solver], Is.EqualTo(2));
                sawDoublePoint = true;
            }
            Assert.That(sawDoublePoint, Is.True);
        }

        private static List<Card> ActiveBlackjackCards(IGame game)
        {
            var players = (IList)Field(game, "playerHands").GetValue(game)!;
            var hands = (IList)players[game.CurrentPlayer]!;
            int active = (int)Field(game, "activeHand").GetValue(game)!;
            object hand = hands[active]!;
            return (List<Card>)(hand.GetType().GetProperty("Cards")?.GetValue(hand)
                ?? throw new InvalidOperationException("Missing blackjack Cards property."));
        }

        private static FieldInfo Field(object source, string name) => source.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("Missing field " + name);

        private static MethodInfo Method(object source, string name) => source.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("Missing method " + name);
    }
}
