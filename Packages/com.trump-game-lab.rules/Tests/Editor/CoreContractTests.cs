using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrumpLab.Games;

namespace TrumpLab.Tests
{
    public sealed class CoreContractTests
    {
        [Test]
        public void StandardDeckIsUnique()
        {
            List<Card> deck = Cards.StandardDeck();
            Assert.That(deck, Has.Count.EqualTo(52));
            Assert.That(deck.Distinct().Count(), Is.EqualTo(52));
        }

        [Test]
        public void CardTextRoundTrips()
        {
            foreach (Card card in Cards.StandardDeck())
                Assert.That(Card.Parse(card.ToString()), Is.EqualTo(card));
        }

        [Test]
        public void BlackjackAceValuesAreCorrect()
        {
            Assert.That(BlackjackGame.HandValue(new[]
            {
                new Card(Suit.Spades, 1), new Card(Suit.Clubs, 13)
            }), Is.EqualTo(21));
            Assert.That(BlackjackGame.HandValue(new[]
            {
                new Card(Suit.Spades, 1), new Card(Suit.Clubs, 1),
                new Card(Suit.Hearts, 5)
            }), Is.EqualTo(17));
        }

        [Test]
        public void BlackjackOffersInsuranceDoubleAndSplitAndKeepsHandsPrivate()
        {
            bool insurance = false, doubling = false, split = false;
            for (int seed = 1; seed <= 5000 && !(insurance && doubling && split); seed++)
            {
                IGame game = BuiltInGames.Registry.Create("blackjack", 2, seed);
                if (game.IsTerminal) continue;
                IReadOnlyList<TrumpLab.Action> actions = game.LegalActions();
                if (actions.Any(action => action.Kind == "insurance"))
                {
                    insurance = true;
                    game.Apply(actions.First(action => action.Kind == "decline_insurance"));
                    if (!game.IsTerminal && game.LegalActions().Any(action => action.Kind == "decline_insurance"))
                        game.Apply(game.LegalActions().First(action => action.Kind == "decline_insurance"));
                }
                if (game.IsTerminal) continue;
                actions = game.LegalActions();
                if (actions.Any(action => action.Kind == "double")) doubling = true;
                if (actions.Any(action => action.Kind == "split"))
                {
                    split = true;
                    game.Apply(actions.First(action => action.Kind == "split"));
                    Assert.That(game.View(0), Does.Contain("H1:"));
                }
                Assert.That(game.View(0), Does.Contain("other_card_counts="));
                Assert.That(game.View(0), Does.Not.Contain("P1:"));
            }
            Assert.That(insurance, Is.True);
            Assert.That(doubling, Is.True);
            Assert.That(split, Is.True);
        }

        [Test]
        public void GinRummyStartsWithTheUpcardOffer()
        {
            IGame game = BuiltInGames.Registry.Create("gin_rummy", seed: 1);
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "take_upcard", "pass_upcard" }));
            game.Apply(new TrumpLab.Action("pass_upcard"));
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "take_upcard", "pass_upcard" }));
        }

        [Test]
        public void CrazyEightsKeepsDrawingPlayersTurnUntilTheyCanPlay()
        {
            IGame? selected = null;
            for (int seed = 1; seed <= 1000; seed++)
            {
                IGame candidate = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
                if (candidate.LegalActions().Count == 1 && candidate.LegalActions()[0].Kind == "draw")
                { selected = candidate; break; }
            }
            Assert.That(selected, Is.Not.Null);
            int player = selected!.CurrentPlayer;
            selected.Apply(new TrumpLab.Action("draw"));
            Assert.That(selected.CurrentPlayer, Is.EqualTo(player));
        }

        [Test]
        public void GinRummyMeldSolverFindsSetAndRun()
        {
            Tuple<int, List<List<Card>>, List<Card>> result = GinRummyGame.BestMelds(new[]
            {
                new Card(Suit.Hearts, 3), new Card(Suit.Hearts, 4),
                new Card(Suit.Hearts, 5), new Card(Suit.Clubs, 9),
                new Card(Suit.Diamonds, 9), new Card(Suit.Spades, 9),
                new Card(Suit.Clubs, 2)
            });
            Assert.That(result.Item1, Is.EqualTo(2));
            Assert.That(result.Item2, Has.Count.EqualTo(2));
        }

        [Test]
        public void GopsUsesHiddenBidsAndAwardsNinetyOnePrizePoints()
        {
            IGame game = BuiltInGames.Registry.Create("gops", 2, 17);
            TrumpLab.Action openingBid = game.LegalActions()[0];
            game.Apply(openingBid);
            Assert.That(game.View(0), Does.Contain("your_pending=" + openingBid.Card));
            Assert.That(game.View(1), Does.Contain("your_pending=-"));
            while (!game.IsTerminal)
                game.Apply(game.ChooseCpuAction(game.CurrentPlayer, new DeterministicRandom(91)));
            GameResult result = game.Result();
            Assert.That(result.Scores.Sum() + (int)result.Extra["unclaimed"], Is.EqualTo(91));
        }

        [Test]
        public void SevensStartsFromTheFourSevensAndCompletes()
        {
            IGame game = BuiltInGames.Registry.Create("sevens", 4, 23);
            Assert.That(game.View(), Does.Contain("C:7-7 D:7-7 H:7-7 S:7-7"));
            Assert.That(game.LegalActions().Where(action => action.Kind == "play")
                .All(action => action.Card!.Value.Rank == 6 || action.Card.Value.Rank == 8), Is.True);
            int guard = 0;
            var cpu = new DeterministicRandom(24);
            while (!game.IsTerminal && guard++ < 300)
                game.Apply(game.ChooseCpuAction(game.CurrentPlayer, cpu));
            Assert.That(game.IsTerminal, Is.True);
        }

        [Test]
        public void ConcentrationShowsBothFlippedCardsBeforeResolving()
        {
            IGame game = BuiltInGames.Registry.Create("concentration", 2, 31);
            game.Apply(game.LegalActions()[0]);
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.LegalActions().Single().Kind, Is.EqualTo("continue"));
            Assert.That(game.View(), Does.Contain("phase=resolve"));
            game.Apply(new TrumpLab.Action("continue"));
            Assert.That(game.LegalActions().All(action => action.Kind == "flip"), Is.True);
        }

        [Test]
        public void HeartsPassesThreeThenOpensWithTwoOfClubs()
        {
            IGame game = BuiltInGames.Registry.Create("hearts", 4, 41,
                new Dictionary<string, string> { ["target_score"] = "1" });
            for (int player = 0; player < 4; player++)
            {
                Assert.That(game.LegalActions().All(action => action.Kind == "pass_three"), Is.True);
                game.Apply(game.LegalActions()[0]);
            }
            TrumpLab.Action opening = game.LegalActions().Single();
            Assert.That(opening.Kind, Is.EqualTo("play"));
            Assert.That(opening.Card, Is.EqualTo(new Card(Suit.Clubs, 2)));
        }

        [Test]
        public void TwentyFourSolverHandlesSolvableAndImpossiblePuzzles()
        {
            Assert.That(TwentyFourGame.CanMake24(new[] { 3d, 3d, 8d, 8d }), Is.True);
            Assert.That(TwentyFourGame.CanMake24(new[] { 1d, 1d, 1d, 1d }), Is.False);
            IGame game = BuiltInGames.Registry.Create("twenty_four", 3, 52);
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "claim_24", "no_solution" }));
        }

        [Test]
        public void PokerEvaluatorRanksWheelAndFullHouseCorrectly()
        {
            PokerRank wheel = PokerHandEvaluator.EvaluateFive(new[]
            {
                new Card(Suit.Clubs, 1), new Card(Suit.Diamonds, 2),
                new Card(Suit.Hearts, 3), new Card(Suit.Spades, 4),
                new Card(Suit.Clubs, 5)
            });
            PokerRank fullHouse = PokerHandEvaluator.EvaluateFive(new[]
            {
                new Card(Suit.Clubs, 9), new Card(Suit.Diamonds, 9),
                new Card(Suit.Hearts, 9), new Card(Suit.Spades, 4),
                new Card(Suit.Clubs, 4)
            });
            Assert.That(wheel.Category, Is.EqualTo(4));
            Assert.That(wheel.Tiebreakers[0], Is.EqualTo(5));
            Assert.That(fullHouse.Category, Is.EqualTo(6));
            Assert.That(fullHouse.CompareTo(wheel), Is.GreaterThan(0));
        }

        [Test]
        public void TexasHoldemRunsFourBettingStreetsWithoutRevealingHoleCards()
        {
            IGame game = BuiltInGames.Registry.Create("texas_holdem", 3, 61);
            Assert.That(game.View(0), Does.Not.Contain("P1["));
            var cpu = new DeterministicRandom(62);
            int guard = 0;
            while (!game.IsTerminal && guard++ < 100)
                game.Apply(game.ChooseCpuAction(game.CurrentPlayer, cpu));
            Assert.That(game.IsTerminal, Is.True);
            Assert.That(game.Result().Scores.Sum(), Is.EqualTo(60));
        }

        [Test]
        public void FiveCardDrawOffersZeroToThreeCardExchange()
        {
            IGame game = BuiltInGames.Registry.Create("five_card_draw", 2, 71);
            while (!game.IsTerminal && !game.LegalActions().Any(action => action.Kind == "draw"))
            {
                TrumpLab.Action action = game.LegalActions().First(candidate => candidate.Kind == "check");
                game.Apply(action);
            }
            Assert.That(game.IsTerminal, Is.False);
            int[] counts = game.LegalActions().Select(action =>
                string.IsNullOrEmpty(action.Value) ? 0 : action.Value!.Split(',').Length).Distinct().ToArray();
            Assert.That(counts, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void BaccaratBankerDrawingTableMatchesPuntoBanco()
        {
            Assert.That(BaccaratGame.ShouldBankerDraw(3, 7), Is.True);
            Assert.That(BaccaratGame.ShouldBankerDraw(3, 8), Is.False);
            Assert.That(BaccaratGame.ShouldBankerDraw(4, 2), Is.True);
            Assert.That(BaccaratGame.ShouldBankerDraw(4, 8), Is.False);
            Assert.That(BaccaratGame.ShouldBankerDraw(5, 4), Is.True);
            Assert.That(BaccaratGame.ShouldBankerDraw(6, 5), Is.False);
            Assert.That(BaccaratGame.ShouldBankerDraw(6, 6), Is.True);
            Assert.That(BaccaratGame.ShouldBankerDraw(5, null), Is.True);
            Assert.That(BaccaratGame.ShouldBankerDraw(6, null), Is.False);
        }

        [Test]
        public void SpadesCollectsFourBidsAndRestrictsOpeningTrump()
        {
            IGame game = BuiltInGames.Registry.Create("spades", 4, 81,
                new Dictionary<string, string> { ["target_score"] = "20" });
            for (int player = 0; player < 4; player++)
                game.Apply(new TrumpLab.Action("bid", value: "1"));
            Assert.That(game.LegalActions().All(action => action.Kind == "play"), Is.True);
            Assert.That(game.LegalActions().Any(action => action.Card!.Value.Suit != Suit.Spades), Is.True);
            Assert.That(game.LegalActions().Any(action => action.Card!.Value.Suit == Suit.Spades), Is.False);
        }

        [Test]
        public void OhHellDealerCannotMakeBidTotalEqualTrickCount()
        {
            IGame game = BuiltInGames.Registry.Create("oh_hell", 3, 82);
            game.Apply(new TrumpLab.Action("bid", value: "0"));
            game.Apply(new TrumpLab.Action("bid", value: "0"));
            Assert.That(game.LegalActions().Any(action => action.Value == "10"), Is.False);
        }

        [Test]
        public void EuchreOrdersUpThenDealerDiscardsBeforePlay()
        {
            IGame game = BuiltInGames.Registry.Create("euchre", 4, 83,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "pass", "order_up", "order_up_alone" }));
            game.Apply(new TrumpLab.Action("order_up"));
            Assert.That(game.LegalActions(), Has.Count.EqualTo(6));
            Assert.That(game.LegalActions().All(action => action.Kind == "discard"), Is.True);
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.LegalActions().All(action => action.Kind == "play"), Is.True);
        }

        [Test]
        public void PinochleMeldCountsTrumpRunAndPinochle()
        {
            int score = PinochleGame.MeldScore(new[]
            {
                new Card(Suit.Hearts, 1), new Card(Suit.Hearts, 10),
                new Card(Suit.Hearts, 13), new Card(Suit.Hearts, 12),
                new Card(Suit.Hearts, 11), new Card(Suit.Spades, 12),
                new Card(Suit.Diamonds, 11)
            }, Suit.Hearts);
            Assert.That(score, Is.EqualTo(19));
            IGame game = BuiltInGames.Registry.Create("pinochle", 4, 84,
                new Dictionary<string, string> { ["target_score"] = "20" });
            Assert.That(game.LegalActions().Any(action => action.Kind == "bid" && action.Value == "20"), Is.True);
        }

        [Test]
        public void SevenBridgeOpensPonResponsesAfterEveryDiscard()
        {
            IGame game = BuiltInGames.Registry.Create("seven_bridge", 3, 85);
            game.Apply(new TrumpLab.Action("draw_stock"));
            TrumpLab.Action discard = game.LegalActions().First(action => action.Kind == "discard");
            game.Apply(discard);
            Assert.That(game.LegalActions().Any(action => action.Kind == "pass"), Is.True);
            Assert.That(game.View(), Does.Contain("phase=claim_pon"));
        }

        [Test]
        public void Rummy500DrawsThenAllowsMeldingOrDiscarding()
        {
            IGame game = BuiltInGames.Registry.Create("rummy_500", 2, 86,
                new Dictionary<string, string> { ["target_score"] = "20" });
            Assert.That(game.LegalActions().Select(action => action.Kind), Does.Contain("draw_stock"));
            Assert.That(game.LegalActions().Select(action => action.Kind), Does.Contain("draw_discard"));
            game.Apply(new TrumpLab.Action("draw_stock"));
            Assert.That(game.LegalActions().Any(action => action.Kind == "discard"), Is.True);
        }

        [Test]
        public void CanastaDealsElevenCardsAndCompletesPartnershipScoring()
        {
            IGame game = BuiltInGames.Registry.Create("canasta", 4, 87,
                new Dictionary<string, string> { ["target_score"] = "500" });
            Assert.That(game.View(), Does.Contain("hand_counts=[11,11,11,11]"));
            Assert.That(game.LegalActions().Any(action => action.Kind == "draw_stock"), Is.True);
            GameResult result = Simulator.RunGame(game, 8701);
            Assert.That(result.Winners, Is.Not.Empty);
            Assert.That(result.Extra.ContainsKey("team_scores"), Is.True);
        }

        [Test]
        public void SpeedUsesTwoCenterPilesAndPrivateLayouts()
        {
            IGame game = BuiltInGames.Registry.Create("speed", 2, 88);
            Assert.That(game.View(0), Does.Contain("centers=["));
            Assert.That(game.View(0), Does.Contain("your layout:"));
            Assert.That(game.View(0), Does.Not.Contain("opponent"));
            Assert.That(game.LegalActions(), Is.Not.Empty);
        }

        [Test]
        public void CheatKeepsClaimedCardsHiddenDuringChallenges()
        {
            IGame game = BuiltInGames.Registry.Create("cheat", 3, 89);
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "pass", "challenge" }));
            Assert.That(game.View(), Does.Contain("last_claim=P0/1"));
            Assert.That(game.View(), Does.Not.Contain("pending="));
        }

        [Test]
        public void PageOneDealsFourCardsAndUsesFollowSuitTricks()
        {
            IGame game = BuiltInGames.Registry.Create("page_one", 3, 90);
            Assert.That(game.View(), Does.Contain("hand_counts=[4,4,4]"));
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.LegalActions(), Is.Not.Empty);
        }

        [Test]
        public void GolfRequiresTwoInitialRevealsForEveryPlayer()
        {
            IGame game = BuiltInGames.Registry.Create("golf", 4, 91);
            Assert.That(game.LegalActions(), Has.Count.EqualTo(15));
            for (int player = 0; player < 4; player++)
                game.Apply(game.LegalActions()[0]);
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "draw_stock", "draw_discard" }));
        }

        [Test]
        public void SpiteAndMaliceStartsWithTwentyCardPayoffPiles()
        {
            IGame game = BuiltInGames.Registry.Create("spite_and_malice", 2, 92);
            Assert.That(game.View(), Does.Contain("payoffs=[20:"));
            Assert.That(game.LegalActions().Any(action => action.Kind == "discard_side"), Is.True);
        }

        [Test]
        public void CasinoDealsFourTableCardsAndOffersTrailForEveryHandCard()
        {
            IGame game = BuiltInGames.Registry.Create("casino", 2, 93,
                new Dictionary<string, string> { ["target_score"] = "5" });
            Assert.That(game.View(), Does.Contain("hand_counts=[4,4]"));
            Assert.That(game.LegalActions().Count(action => action.Kind == "trail"), Is.EqualTo(4));
        }

        [Test]
        public void CardCaptureDiscardsThenDrawsToFour()
        {
            IGame game = BuiltInGames.Registry.Create("card_capture", 1, 94);
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.View(), Does.Contain("hand:"));
            Assert.That(game.LegalActions(), Is.Not.Empty);
        }

        [Test]
        public void ScoundrelCannotAvoidTwoRoomsInSuccession()
        {
            IGame game = BuiltInGames.Registry.Create("scoundrel", 1, 95);
            Assert.That(game.LegalActions().Any(action => action.Kind == "avoid"), Is.True);
            game.Apply(new TrumpLab.Action("avoid"));
            Assert.That(game.LegalActions().Any(action => action.Kind == "avoid"), Is.False);
            Assert.That(game.View(), Does.Contain("health=20"));
        }

        [Test]
        public void GosankyoOffersEachExactBidOnlyOnce()
        {
            IGame game = BuiltInGames.Registry.Create("gosankyo", 1, 96);
            Assert.That(game.LegalActions().Select(action => action.Value),
                Is.EquivalentTo(new[] { "4", "5", "6", "7" }));
            game.Apply(new TrumpLab.Action("bid", value: "4"));
            Assert.That(game.View(), Does.Contain("left=["));
            Assert.That(game.LegalActions().All(action => action.Kind == "play"), Is.True);
        }

        [Test]
        public void CrispRecognizesItsFourNormalAndTwoSpecialCombinations()
        {
            Assert.That(CrispGame.IsCombination(new[] { new Card(Suit.Clubs, 2) }), Is.True);
            Assert.That(CrispGame.IsCombination(new[]
            {
                new Card(Suit.Clubs, 4), new Card(Suit.Hearts, 4)
            }), Is.True);
            Assert.That(CrispGame.IsCombination(new[]
            {
                new Card(Suit.Clubs, 4), new Card(Suit.Hearts, 5), new Card(Suit.Spades, 6)
            }), Is.True);
            Assert.That(CrispGame.IsCombination(new[]
            {
                new Card(Suit.Clubs, 4), new Card(Suit.Hearts, 4),
                new Card(Suit.Clubs, 5), new Card(Suit.Hearts, 5)
            }), Is.True);
            Assert.That(CrispGame.IsCombination(new[]
            {
                new Card(Suit.Clubs, 8), new Card(Suit.Hearts, 8), new Card(Suit.Spades, 8)
            }), Is.True);
            Assert.That(CrispGame.IsCombination(new[]
            {
                new Card(Suit.Clubs, 8), new Card(Suit.Hearts, 8),
                new Card(Suit.Spades, 8), new Card(Suit.Diamonds, 8)
            }), Is.True);
            Assert.That(CrispGame.IsCombination(new[]
            {
                new Card(Suit.Clubs, 10), new Card(Suit.Hearts, 12), new Card(Suit.Spades, 13)
            }), Is.False);
        }

        [Test]
        public void DurakMovesFromAttackToDefenseAndAllowsTaking()
        {
            IGame game = BuiltInGames.Registry.Create("durak", 2, 97);
            Assert.That(game.LegalActions().All(action => action.Kind == "attack"), Is.True);
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.View(), Does.Contain("phase=defend"));
            Assert.That(game.LegalActions().Any(action => action.Kind == "take"), Is.True);
            game.Apply(new TrumpLab.Action("take"));
            Assert.That(game.View(), Does.Contain("taking=True"));
            Assert.That(game.LegalActions().Any(action => action.Kind == "pass"), Is.True);
        }

        [Test]
        public void SchnapsenOffersTalonCloseAndKeepsHandsPrivate()
        {
            IGame game = BuiltInGames.Registry.Create("schnapsen", 2, 98);
            Assert.That(game.View(0), Does.Contain("hand_counts=[5,5]"));
            Assert.That(game.View(0), Does.Not.Contain("P1 hand"));
            Assert.That(game.LegalActions().Any(action => action.Kind == "close_talon"), Is.True);
            game.Apply(new TrumpLab.Action("close_talon"));
            Assert.That(game.LegalActions().Any(action => action.Kind == "close_talon"), Is.False);
            game.Apply(game.LegalActions().First(action => action.Kind == "play"));
            Assert.That(game.LegalActions(), Is.Not.Empty);
        }

        [Test]
        public void CribbageScoresTheTwentyNineHandAndRunsPeggingPhases()
        {
            Assert.That(CribbageGame.HandScore(new[]
            {
                new Card(Suit.Clubs, 5), new Card(Suit.Diamonds, 5),
                new Card(Suit.Hearts, 5), new Card(Suit.Spades, 11)
            }, new Card(Suit.Spades, 5), false), Is.EqualTo(29));
            Assert.That(CribbageGame.PeggingScore(new[]
            {
                new Card(Suit.Clubs, 4), new Card(Suit.Diamonds, 2),
                new Card(Suit.Hearts, 3)
            }, 9), Is.EqualTo(3));
            IGame game = BuiltInGames.Registry.Create("cribbage", 2, 99);
            Assert.That(game.LegalActions(), Has.Count.EqualTo(15));
            game.Apply(game.LegalActions()[0]);
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.View(), Does.Contain("phase=pegging"));
            Assert.That(game.LegalActions(), Is.Not.Empty);
        }

        [Test]
        public void SonoUsesAdjacentPlacementAndPokerClanScoring()
        {
            IGame game = BuiltInGames.Registry.Create("sono", 2, 110);
            Assert.That(game.View(0), Does.Contain("?"));
            Assert.That(game.LegalActions().All(action => action.Kind == "place"), Is.True);
            Assert.That(SonoGame.LineScore(new Card?[]
            {
                new Card(Suit.Hearts, 9), new Card(Suit.Diamonds, 9),
                new Card(Suit.Hearts, 10), new Card(Suit.Diamonds, 1), null
            }), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void SuperTrumpChoosesSuitThenRankBeforeTwoStages()
        {
            IGame game = BuiltInGames.Registry.Create("super_trump", 2, 111);
            Assert.That(game.LegalActions(), Has.Count.EqualTo(4));
            game.Apply(new TrumpLab.Action("choose_trump", value: "H"));
            Assert.That(game.LegalActions(), Has.Count.EqualTo(13));
            game.Apply(new TrumpLab.Action("choose_super", value: "5"));
            Assert.That(game.View(), Does.Contain("stage=1 trump=H super=5"));
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.LegalActions(), Is.Not.Empty);
        }

        [Test]
        public void TwoPlayerDaifugoDealsSixteenAndDrawsAfterPass()
        {
            IGame game = BuiltInGames.Registry.Create("daifugo_two", 2, 112,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.View(), Does.Contain("hand_counts=[16,16]"));
            game.Apply(game.LegalActions().First(action => action.Kind == "play"));
            Assert.That(game.LegalActions().Any(action => action.Kind == "pass"), Is.True);
            int stockBefore = int.Parse(game.View().Split("stock=")[1].Split(' ')[0]);
            game.Apply(new TrumpLab.Action("pass"));
            Assert.That(game.View(), Does.Contain("stock=" + (stockBefore - 1)));
        }

        [Test]
        public void OfficerSkatRevealsPublicLayoutAfterTrumpChoice()
        {
            IGame game = BuiltInGames.Registry.Create("officer_skat", 2, 113);
            Assert.That(game.View(), Does.Contain("P1_first_row=[pending]"));
            game.Apply(new TrumpLab.Action("choose_trump", value: "C"));
            Assert.That(game.View(), Does.Contain("public layouts: P0["));
            Assert.That(game.LegalActions().All(action => action.Kind == "play"), Is.True);
        }

        [Test]
        public void BohemianSchneiderOnlyBeatsWithTheSameSuitNextRank()
        {
            Assert.That(BohemianSchneiderGame.BeatsByOne(
                new Card(Suit.Hearts, 10), new Card(Suit.Hearts, 9)), Is.True);
            Assert.That(BohemianSchneiderGame.BeatsByOne(
                new Card(Suit.Spades, 10), new Card(Suit.Hearts, 9)), Is.False);
            Assert.That(BohemianSchneiderGame.BeatsByOne(
                new Card(Suit.Hearts, 11), new Card(Suit.Hearts, 9)), Is.False);
        }

        [Test]
        public void NorwegianWhistBidsLowThenPlaysFourCardTricks()
        {
            IGame game = BuiltInGames.Registry.Create("norwegian_whist", 2, 114,
                new Dictionary<string, string> { ["target_score"] = "1" });
            game.Apply(new TrumpLab.Action("bid_low"));
            game.Apply(new TrumpLab.Action("bid_low"));
            Assert.That(game.View(), Does.Contain("contract=low"));
            var cpu = new DeterministicRandom(11401);
            for (int count = 0; count < 4; count++)
                game.Apply(game.ChooseCpuAction(game.CurrentPlayer, cpu));
            Assert.That(game.View(), Does.Contain("tricks=["));
            Assert.That(game.View(), Does.Contain("trick=[]"));
        }

        [Test]
        public void GoldmineMakesTheSecondPlayerTakeTheOtherAction()
        {
            IGame game = BuiltInGames.Registry.Create("goldmine", 2, 115,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.LegalActions().Any(action => action.Kind == "inspect"), Is.True);
            Assert.That(game.LegalActions().Any(action => action.Kind == "exchange"), Is.True);
            game.Apply(game.LegalActions().First(action => action.Kind == "inspect"));
            Assert.That(game.LegalActions().All(action => action.Kind == "exchange"), Is.True);
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.LegalActions().All(action => action.Kind == "play"), Is.True);
        }

        [Test]
        public void PiquetRequiresBothPlayersToExchangeAtLeastOneCard()
        {
            IGame game = BuiltInGames.Registry.Create("piquet", 2, 116);
            Assert.That(game.LegalActions(), Has.Count.EqualTo(1585));
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.View(), Does.Contain("phase=younger_exchange"));
            Assert.That(game.LegalActions().Any(action => action.Value == ""), Is.False);
        }

        [Test]
        public void KlaberjassTakeDealsNineThenOffersMeldDecision()
        {
            IGame game = BuiltInGames.Registry.Create("klaberjass", 2, 117,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "take", "pass" }));
            game.Apply(new TrumpLab.Action("take"));
            Assert.That(game.View(), Does.Contain("phase=meld"));
            Assert.That(game.View(), Does.Contain("hand_counts=[9,9]"));
            Assert.That(game.LegalActions().Any(action => action.Kind == "declare_meld"), Is.True);
        }

        [Test]
        public void WhosWhoDealsFourteenAndForbidsAJokerLeadWhileNaturalCardsRemain()
        {
            IGame game = BuiltInGames.Registry.Create("whos_who", 3, 118,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.View(), Does.Contain("hand_counts=[14,14,14]"));
            Assert.That(game.LegalActions().All(action => action.Kind == "play" && action.Card.HasValue),
                Is.True);
        }

        [Test]
        public void GooseberryFoolDealsElevenAndForbidsAJokerLeadWhileNaturalCardsRemain()
        {
            IGame game = BuiltInGames.Registry.Create("gooseberry_fool", 3, 119,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.View(), Does.Contain("hand_counts=[11,11,11]"));
            Assert.That(game.LegalActions().All(action => action.Kind == "play" && action.Card.HasValue),
                Is.True);
        }

        [Test]
        public void TanukiKeepsRoleSuitsPrivateAndThenDealsTwelve()
        {
            IGame game = BuiltInGames.Registry.Create("tanuki", 3, 120);
            Assert.That(game.LegalActions(), Has.Count.EqualTo(4));
            game.Apply(new TrumpLab.Action("choose_suit", value: "H"));
            game.Apply(new TrumpLab.Action("choose_suit", value: "S"));
            game.Apply(new TrumpLab.Action("choose_suit", value: "D"));
            Assert.That(game.View(), Does.Contain("hand_counts=[12,12,12]"));
            Assert.That(game.View(0), Does.Not.Contain("minus_suit="));
            Assert.That(game.LegalActions().All(action => action.Kind == "play"), Is.True);
        }

        [Test]
        public void HamletUsesThreeSecretNaturalCardsToSetTheMode()
        {
            IGame game = BuiltInGames.Registry.Create("hamlet", 3, 121,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.View(), Does.Contain("hand_counts=[11,11,11]"));
            for (int count = 0; count < 3; count++)
            {
                Assert.That(game.LegalActions().All(action => action.Kind == "choose_mode_card" && action.Card.HasValue), Is.True);
                game.Apply(game.LegalActions()[0]);
            }
            Assert.That(game.View(), Does.Contain("phase=play"));
            Assert.That(game.View(), Does.Not.Contain("trump=?"));
        }

        [Test]
        public void FarbwechselKeepsBidsPrivateAndPublishesElevenTrumpCards()
        {
            IGame game = BuiltInGames.Registry.Create("farbwechsel", 3, 122,
                new Dictionary<string, string> { ["target_score"] = "1" });
            int bidder = game.CurrentPlayer;
            game.Apply(new TrumpLab.Action("bid", value: "5"));
            Assert.That(game.View(bidder), Does.Contain("your_bid=5"));
            Assert.That(game.View((bidder + 1) % 3), Does.Contain("your_bid=-"));
            Assert.That(game.View(), Does.Contain("hand_counts=[11,11,11]"));
        }

        [Test]
        public void SheriffAssignsUniqueRolesBeforeTheMayorChoosesTrump()
        {
            IGame game = BuiltInGames.Registry.Create("sheriff", 3, 123,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.LegalActions(), Has.Count.EqualTo(3));
            game.Apply(new TrumpLab.Action("choose_role", value: "mayor"));
            game.Apply(new TrumpLab.Action("choose_role", value: "sheriff"));
            game.Apply(new TrumpLab.Action("choose_role", value: "robber"));
            Assert.That(game.LegalActions().All(action => action.Kind == "choose_trump"), Is.True);
            game.Apply(new TrumpLab.Action("choose_trump", value: "N"));
            Assert.That(game.View(), Does.Contain("hand_counts=[7,7,7]"));
        }

        [Test]
        public void MizerkaDealsSixThenThirteenAndSupportsIncrementalExchange()
        {
            IGame game = BuiltInGames.Registry.Create("mizerka", 3, 124);
            Assert.That(game.View(), Does.Contain("hand_counts=[6,6,6]"));
            Assert.That(game.View(), Does.Contain("talon=6"));
            game.Apply(game.LegalActions()[0]);
            Assert.That(game.View(), Does.Contain("hand_counts=[13,13,13]"));
            Assert.That(game.View(), Does.Contain("talon=13"));
            game.Apply(game.LegalActions().First(action => action.Kind == "discard_for_exchange"));
            game.Apply(new TrumpLab.Action("finish_exchange"));
            Assert.That(game.View(), Does.Contain("hand_counts=[13,13,13]"));
            Assert.That(game.View(), Does.Contain("talon=12"));
        }

        [Test]
        public void NinetyNineUsesThreeHandCardsAsAHiddenBid()
        {
            IGame game = BuiltInGames.Registry.Create("ninety_nine", 3, 125,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.View(), Does.Contain("hand_counts=[12,12,12]"));
            for (int count = 0; count < 9; count++) game.Apply(game.LegalActions()[0]);
            Assert.That(game.View(), Does.Contain("phase=premium"));
            for (int count = 0; count < 3; count++) game.Apply(new TrumpLab.Action("pass_premium"));
            Assert.That(game.View(), Does.Contain("phase=play"));
            Assert.That(game.View(), Does.Contain("hand_counts=[9,9,9]"));
        }

        [Test]
        public void FiveHundredAuctionWinnerTakesKittyAndReturnsThree()
        {
            IGame game = BuiltInGames.Registry.Create("five_hundred", 3, 126,
                new Dictionary<string, string> { ["target_score"] = "1" });
            Assert.That(game.View(), Does.Contain("hand_counts=[10,10,10]"));
            Assert.That(game.LegalActions().Any(action => action.Kind == "bid" && action.Value == "6S"), Is.True);
            game.Apply(new TrumpLab.Action("bid", value: "6S"));
            game.Apply(new TrumpLab.Action("pass"));
            game.Apply(new TrumpLab.Action("pass"));
            Assert.That(game.View(), Does.Contain("phase=discard"));
            for (int count = 0; count < 3; count++) game.Apply(game.LegalActions()[0]);
            game.Apply(new TrumpLab.Action("finish_discard"));
            Assert.That(game.View(), Does.Contain("phase=play"));
            Assert.That(game.View(), Does.Contain("hand_counts=[10,10,10]"));
        }

        [Test]
        public void SkatAuctionWinnerCanTakeAndReturnTheTwoSkatCards()
        {
            IGame game = BuiltInGames.Registry.Create("skat", 3, 127,
                new Dictionary<string, string> { ["deals"] = "1" });
            Assert.That(game.View(), Does.Contain("hand_counts=[10,10,10]"));
            game.Apply(new TrumpLab.Action("bid", value: "18"));
            game.Apply(new TrumpLab.Action("pass"));
            game.Apply(new TrumpLab.Action("pass"));
            Assert.That(game.LegalActions().Select(action => action.Kind),
                Is.EquivalentTo(new[] { "take_skat", "hand_game" }));
            game.Apply(new TrumpLab.Action("take_skat"));
            for (int count = 0; count < 2; count++) game.Apply(game.LegalActions()[0]);
            game.Apply(new TrumpLab.Action("finish_discard"));
            game.Apply(new TrumpLab.Action("declare_contract", value: "D"));
            Assert.That(game.View(), Does.Contain("phase=play"));
            Assert.That(game.View(), Does.Contain("hand_counts=[10,10,10]"));
        }

        [Test]
        public void UltiFirstBidderCreatesATalonBeforeTheAuctionContinues()
        {
            IGame game = BuiltInGames.Registry.Create("ulti", 3, 128,
                new Dictionary<string, string> { ["deals"] = "1" });
            Assert.That(game.View(), Does.Contain("12"));
            game.Apply(new TrumpLab.Action("bid", value: "minor_game"));
            for (int count = 0; count < 2; count++) game.Apply(game.LegalActions()[0]);
            game.Apply(new TrumpLab.Action("finish_discard"));
            for (int count = 0; count < 3; count++) game.Apply(new TrumpLab.Action("pass"));
            game.Apply(new TrumpLab.Action("choose_trump", value: "C"));
            for (int count = 0; count < 3; count++)
                game.Apply(new TrumpLab.Action("finish_marriages"));
            Assert.That(game.View(), Does.Contain("phase=play"));
            Assert.That(game.View(), Does.Contain("hand_counts=[10,10,10]"));
        }

        [Test]
        public void TrumpCrewUsesPlayerStageLimitsJokerIndicatorAndForcedDealerBid()
        {
            Assert.That(BuiltInGames.Registry.Create("trump_crew", 3, 1).View(), Does.Contain("stage=1/17"));
            Assert.That(BuiltInGames.Registry.Create("trump_crew", 4, 1).View(), Does.Contain("stage=1/13"));
            Assert.That(BuiltInGames.Registry.Create("trump_crew", 5, 1).View(), Does.Contain("stage=1/10"));

            IGame game = BuiltInGames.Registry.Create("trump_crew", 3, 19);
            Assert.That(game.View(), Does.Contain("trump=none"));
            Assert.That(game.LegalActions().Select(action => action.Value),
                Is.EquivalentTo(new[] { "weak", "middle", "strong" }));
            game.Apply(new TrumpLab.Action("announce_strength", value: "middle"));
            Assert.That(game.CurrentPlayer, Is.EqualTo(1));
            game.Apply(new TrumpLab.Action("bid", value: "0"));
            game.Apply(new TrumpLab.Action("bid", value: "0"));
            Assert.That(game.CurrentPlayer, Is.EqualTo(0));
            Assert.That(game.LegalActions().Single().Value, Is.EqualTo("1"));
            game.Apply(new TrumpLab.Action("bid", value: "1"));
            Assert.That(game.View(), Does.Contain("phase=play"));
            Assert.That(game.View(), Does.Contain("bids=[1,0,0]"));
        }

        [Test]
        public void TrumpCrewAdvancesOnlyOnExactBidsAndRetriesTheSameStage()
        {
            IGame success = ReachTrumpCrewStageTwo(42, 0);
            Assert.That(success.View(), Does.Contain("stage=2/3"));
            Assert.That(success.View(), Does.Contain("dealer=P1"));

            IGame failure = NewTrumpCrew(42, 0);
            FailTrumpCrewStageOne(failure, 1);
            Assert.That(failure.IsTerminal, Is.False);
            Assert.That(failure.View(), Does.Contain("stage=1/3"));
            Assert.That(failure.View(), Does.Contain("attempt=2/unlimited"));
            Assert.That(failure.View(), Does.Contain("dealer=P1"));
        }

        [Test]
        public void TrumpCrewMustFollowsAndSupportsJokerLeadSuitOrNoSuit()
        {
            IGame ordinaryLead = ReachTrumpCrewStageTwo(42, 0);
            BeginTrumpCrewStageTwo(ordinaryLead, new[] { 0, 0, 2 });
            ordinaryLead.Apply(ordinaryLead.LegalActions().Single(action =>
                action.Card == new Card(Suit.Hearts, 13)));
            Assert.That(ordinaryLead.LegalActions().Single().Card,
                Is.EqualTo(new Card(Suit.Hearts, 8)));

            IGame declaredJoker = ReachTrumpCrewStageTwo(42, 0);
            BeginTrumpCrewStageTwo(declaredJoker, new[] { 0, 0, 2 });
            Assert.That(declaredJoker.LegalActions().Where(action =>
                    action.Value!.StartsWith("JOKER", StringComparison.Ordinal)).Select(action => action.Value),
                Is.EquivalentTo(new[] { "JOKER", "JOKER:C", "JOKER:D", "JOKER:H", "JOKER:S" }));
            declaredJoker.Apply(new TrumpLab.Action("play", value: "JOKER:H"));
            Assert.That(declaredJoker.View(), Does.Contain("JOKER>H"));
            Assert.That(declaredJoker.LegalActions().Single().Card,
                Is.EqualTo(new Card(Suit.Hearts, 8)));
            declaredJoker.Apply(declaredJoker.LegalActions().Single());
            Assert.That(declaredJoker.LegalActions().All(action => action.Card!.Value.Suit == Suit.Hearts), Is.True);
            declaredJoker.Apply(declaredJoker.LegalActions().First());
            Assert.That(declaredJoker.CurrentPlayer, Is.EqualTo(2));
            Assert.That(declaredJoker.View(), Does.Contain("tricks=[0,0,1]"));

            IGame noSuitJoker = ReachTrumpCrewStageTwo(42, 0);
            BeginTrumpCrewStageTwo(noSuitJoker, new[] { 0, 0, 2 });
            noSuitJoker.Apply(new TrumpLab.Action("play", value: "JOKER"));
            Assert.That(noSuitJoker.View(), Does.Contain("JOKER>any"));
            Assert.That(noSuitJoker.LegalActions(), Has.Count.EqualTo(2));
        }

        [Test]
        public void TrumpCrewJokerCanBreakFollowAndAlwaysWins()
        {
            IGame game = ReachTrumpCrewStageTwo(50, 2);
            BeginTrumpCrewStageTwo(game, new[] { 0, 2, 0 });
            game.Apply(game.LegalActions().Single(action =>
                action.Card == new Card(Suit.Diamonds, 4)));
            game.Apply(game.LegalActions().Single(action =>
                action.Card == new Card(Suit.Diamonds, 6)));
            Assert.That(game.LegalActions().Select(action => action.Value),
                Is.EquivalentTo(new[] { "JOKER", "9D" }));
            game.Apply(new TrumpLab.Action("play", value: "JOKER"));
            Assert.That(game.CurrentPlayer, Is.EqualTo(1));
            Assert.That(game.View(), Does.Contain("tricks=[0,1,0]"));
        }

        [Test]
        public void TrumpCrewAttemptLimitIsExplicitAndCanBeDisabled()
        {
            int[] wrongBidders = { 1, 0, 1, 0, 1 };
            IGame limited = BuiltInGames.Registry.Create("trump_crew", 3, 42,
                new Dictionary<string, string> { ["final_stage"] = "2", ["max_attempts"] = "5" });
            foreach (int bidder in wrongBidders) FailTrumpCrewStageOne(limited, bidder);
            Assert.That(limited.IsTerminal, Is.True);
            Assert.That(limited.Result().Reason, Is.EqualTo("attempt limit reached"));

            IGame officialRetry = NewTrumpCrew(42, 0);
            foreach (int bidder in wrongBidders) FailTrumpCrewStageOne(officialRetry, bidder);
            Assert.That(officialRetry.IsTerminal, Is.False);
            Assert.That(officialRetry.View(), Does.Contain("stage=1/3"));
            Assert.That(officialRetry.View(), Does.Contain("attempt=6/unlimited"));
        }

        [Test]
        public void TrumpCrewCpuDependsOnlyOnItsObservationAndClearsACooperativeScenario()
        {
            IGame left = NewTrumpCrew(1, 5);
            IGame right = NewTrumpCrew(5, 5);
            left.Apply(new TrumpLab.Action("announce_strength", value: "middle"));
            right.Apply(new TrumpLab.Action("announce_strength", value: "middle"));
            Assert.That(left.View(1), Is.EqualTo(right.View(1)));
            Assert.That(left.View(0), Is.Not.EqualTo(right.View(0)));
            Assert.That(left.ChooseCpuAction(1, new DeterministicRandom(70)),
                Is.EqualTo(right.ChooseCpuAction(1, new DeterministicRandom(999))));

            IGame campaign = BuiltInGames.Registry.Create("trump_crew", 3, 42,
                new Dictionary<string, string> { ["final_stage"] = "2" });
            GameResult result = Simulator.RunGame(campaign, 7001);
            Assert.That(result.Winners, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(result.Scores, Is.EqualTo(new[] { 2d, 2d, 2d }));
            Assert.That(result.Reason, Is.EqualTo("all cooperative stages cleared"));
        }

        [Test]
        public void RemainingThreePlayerRulesCompleteAcrossSeeds()
        {
            foreach (string gameId in new[]
            {
                "italian_whist", "kaedama_trick", "trick_of_the_dead", "corpo"
            })
            {
                SimulationReport report = Simulator.Simulate(gameId, 20, seed: 700);
                Assert.That(report.Failures, Is.Empty, gameId);
                Assert.That(report.Completed, Is.EqualTo(20), gameId);
            }
        }

        [Test]
        public void EveryRegisteredGameCompletesAcrossSeeds()
        {
            foreach (GameInfo info in BuiltInGames.Registry.All())
            {
                SimulationReport report = Simulator.Simulate(info.GameId, 30, seed: 100);
                Assert.That(report.Failures, Is.Empty, info.GameId);
                Assert.That(report.Completed, Is.EqualTo(30), info.GameId);
            }
        }

        [Test]
        public void EveryCatalogueRuleHasExactlyOneRegisteredImplementation()
        {
            Candidate[] candidates = GameCatalogue.Candidates().ToArray();
            string[] registered = BuiltInGames.Registry.All().Select(info => info.GameId).ToArray();
            Assert.That(candidates.Length, Is.EqualTo(92));
            Assert.That(candidates.All(candidate => candidate.ImplementationId != null), Is.True);
            Assert.That(candidates.Select(candidate => candidate.ImplementationId).Distinct().Count(),
                Is.EqualTo(92));
            Assert.That(candidates.Select(candidate => candidate.ImplementationId),
                Is.EquivalentTo(registered));
        }

        [Test]
        public void CatalogueMarksOnlyCompletedRuleAuditsAsVerified()
        {
            Candidate[] candidates = GameCatalogue.Candidates().ToArray();
            Assert.That(candidates.Count(candidate => candidate.Status == CandidateStatus.RuleSpecific),
                Is.EqualTo(75));
            Assert.That(candidates.Count(candidate => candidate.Status == CandidateStatus.Prototype),
                Is.EqualTo(0));
            Assert.That(candidates.Count(candidate => candidate.Status == CandidateStatus.Verified),
                Is.EqualTo(17));
            Assert.That(candidates.Where(candidate => candidate.Status == CandidateStatus.Verified)
                .Select(candidate => candidate.ImplementationId),
                Is.EquivalentTo(new[]
                {
                    "trump_crew", "baohuang", "napoleon", "card_capture", "scoundrel",
                    "gosankyo", "german_whist", "gin_rummy", "sono", "crisp", "cribbage",
                    "super_trump", "daifugo_two", "briscola", "bohemian_schneider", "durak",
                    "officer_skat"
                }));
            Assert.That(candidates.Where(candidate => candidate.Status == CandidateStatus.RuleSpecific)
                .Select(candidate => candidate.ImplementationId),
                Is.EquivalentTo(candidates.Select(candidate => candidate.ImplementationId)
                    .Except(new[]
                    {
                        "trump_crew", "baohuang", "napoleon", "card_capture", "scoundrel",
                        "gosankyo", "german_whist", "gin_rummy", "sono", "crisp", "cribbage",
                        "super_trump", "daifugo_two", "briscola", "bohemian_schneider", "durak",
                        "officer_skat"
                    })));
            Assert.That(candidates.Select(candidate => BuiltInGames.Registry.Create(candidate.ImplementationId!, seed: 1))
                .All(game => game.GetType().Name != "RuleDrivenGame"), Is.True);
        }

        private static IGame NewTrumpCrew(int seed, int maxAttempts) =>
            BuiltInGames.Registry.Create("trump_crew", 3, seed,
                new Dictionary<string, string>
                {
                    ["final_stage"] = "3",
                    ["max_attempts"] = maxAttempts.ToString()
                });

        private static IGame ReachTrumpCrewStageTwo(int seed, int winner)
        {
            IGame game = NewTrumpCrew(seed, 0);
            game.Apply(new TrumpLab.Action("announce_strength", value: "middle"));
            for (int count = 0; count < game.Players; count++)
            {
                string bid = game.CurrentPlayer == winner ? "1" : "0";
                game.Apply(new TrumpLab.Action("bid", value: bid));
            }
            for (int count = 0; count < game.Players; count++)
                game.Apply(game.LegalActions().First());
            Assert.That(game.View(), Does.Contain("stage=2/3"));
            return game;
        }

        private static void BeginTrumpCrewStageTwo(IGame game, IReadOnlyList<int> bids)
        {
            game.Apply(new TrumpLab.Action("announce_strength", value: "middle"));
            for (int count = 0; count < game.Players; count++)
                game.Apply(new TrumpLab.Action("bid", value: bids[game.CurrentPlayer].ToString()));
            Assert.That(game.View(), Does.Contain("phase=play stage=2/3"));
        }

        private static void FailTrumpCrewStageOne(IGame game, int wrongBidder)
        {
            game.Apply(new TrumpLab.Action("announce_strength", value: "middle"));
            for (int count = 0; count < game.Players; count++)
            {
                string bid = game.CurrentPlayer == wrongBidder ? "1" : "0";
                game.Apply(new TrumpLab.Action("bid", value: bid));
            }
            for (int count = 0; count < game.Players; count++)
                game.Apply(game.LegalActions().First());
        }

        [Test]
        public void PlayerViewsDoNotRevealOpponentHands()
        {
            foreach (GameInfo info in BuiltInGames.Registry.All())
            {
                IGame game = BuiltInGames.Registry.Create(info.GameId, seed: 123);
                string view = game.View(0);
                Assert.That(view, Does.Not.Contain("opponent hand"), info.GameId);
            }
        }

        [Test]
        public void EveryPlayerBoundaryCompletes()
        {
            foreach (GameInfo info in BuiltInGames.Registry.All())
            foreach (int players in new[] { info.MinPlayers, info.MaxPlayers }.Distinct())
            {
                SimulationReport report = Simulator.Simulate(
                    info.GameId, 3, players, seed: 500);
                Assert.That(report.Failures, Is.Empty, info.GameId + "/" + players);
            }
        }

        [Test]
        public void SameSeedProducesSameResult()
        {
            foreach (GameInfo info in BuiltInGames.Registry.All())
            {
                GameResult left = Simulator.RunGame(
                    BuiltInGames.Registry.Create(info.GameId, seed: 77), 9001);
                GameResult right = Simulator.RunGame(
                    BuiltInGames.Registry.Create(info.GameId, seed: 77), 9001);
                Assert.That(right.Winners, Is.EqualTo(left.Winners), info.GameId);
                Assert.That(right.Scores, Is.EqualTo(left.Scores), info.GameId);
                Assert.That(right.Turns, Is.EqualTo(left.Turns), info.GameId);
            }
        }

        [Test]
        public void RegistryRejectsInvalidPlayerCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BuiltInGames.Registry.Create("german_whist", 3));
        }

        [Test]
        public void OptionsDoNotLeakBetweenInstances()
        {
            var custom = (CrazyEightsGame)BuiltInGames.Registry.Create(
                "crazy_eights", 2, 1,
                new Dictionary<string, string> { ["wild_rank"] = "2" });
            var normal = (CrazyEightsGame)BuiltInGames.Registry.Create(
                "crazy_eights", 2, 1);
            Assert.That(custom.WildRank, Is.EqualTo(2));
            Assert.That(normal.WildRank, Is.EqualTo(8));
        }
    }
}
