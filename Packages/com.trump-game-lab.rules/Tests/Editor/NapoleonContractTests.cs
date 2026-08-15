using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TrumpLab.Games;

namespace TrumpLab.Tests
{
    public sealed class NapoleonContractTests
    {
        [Test]
        public void NapoleonUsesOneUnique53CardDeckAndDocumentedPlayerDeals()
        {
            IReadOnlyDictionary<string, int> profile = NapoleonGame.DeckComposition();
            Assert.That(NapoleonGame.DeckSize, Is.EqualTo(53));
            Assert.That(profile, Has.Count.EqualTo(53));
            Assert.That(profile.Values, Is.All.EqualTo(1));
            Assert.That(profile["JOKER"], Is.EqualTo(1));
            Assert.That(profile.Keys.Count(IsHonor), Is.EqualTo(20));

            int[] expectedHands = { 12, 10, 8, 7 };
            int[] expectedWidows = { 5, 3, 5, 4 };
            for (int players = 4; players <= 7; players++)
            {
                IGame game = BuiltInGames.Registry.Create("napoleon", players, 40 + players);
                string[] dealt = Enumerable.Range(0, players).SelectMany(player => Hand(game, player)).ToArray();
                Assert.That(HandCounts(game), Is.All.EqualTo(expectedHands[players - 4]));
                Assert.That(dealt, Has.Length.EqualTo(players * expectedHands[players - 4]));
                Assert.That(dealt.Distinct().Count(), Is.EqualTo(dealt.Length));
                Assert.That(profile.Keys.Except(dealt).Count(), Is.EqualTo(expectedWidows[players - 4]));
                Assert.That(NapoleonGame.WidowSizeFor(players), Is.EqualTo(expectedWidows[players - 4]));
            }
        }

        [Test]
        public void NapoleonSoftPassAuctionOvercallsBySuitAndEndsAfterOtherPlayersPass()
        {
            IGame game = BuiltInGames.Registry.Create("napoleon", 5, 11);
            Assert.That(game.CurrentPlayer, Is.EqualTo(1), "P0 is the initial dealer; P1 opens.");
            Assert.That(game.LegalActions(), Does.Contain(new TrumpLab.Action("bid", value: "12:C")));
            game.Apply(new TrumpLab.Action("bid", value: "12:C"));
            game.Apply(new TrumpLab.Action("pass"));
            game.Apply(new TrumpLab.Action("bid", value: "12:D"));
            game.Apply(new TrumpLab.Action("pass"));
            game.Apply(new TrumpLab.Action("pass"));
            game.Apply(new TrumpLab.Action("pass"));
            Assert.That(game.CurrentPlayer, Is.EqualTo(2), "A prior pass does not remove P2.");
            Assert.That(game.LegalActions(), Does.Contain(new TrumpLab.Action("bid", value: "12:H")));
            game.Apply(new TrumpLab.Action("bid", value: "12:H"));
            for (int count = 0; count < 4; count++) game.Apply(new TrumpLab.Action("pass"));
            Assert.That(game.View(), Does.Contain("phase=call_adjutant"));
            Assert.That(game.View(), Does.Contain("napoleon=P2"));
            Assert.That(game.View(), Does.Contain("goal=12 trump=H"));
        }

        [Test]
        public void NapoleonRejectsWeakOrMalformedBidsAndSupportsTheTwentySpadeCeiling()
        {
            IGame game = BuiltInGames.Registry.Create("napoleon", 5, 12);
            Assert.Throws<ArgumentException>(() => game.Apply(
                new TrumpLab.Action("bid", value: "11:S")));
            game.Apply(new TrumpLab.Action("bid", value: "20:S"));
            Assert.That(game.LegalActions().Select(action => action.Kind), Is.EqualTo(new[] { "pass" }));
            for (int count = 0; count < 4; count++) game.Apply(new TrumpLab.Action("pass"));
            Assert.That(game.View(), Does.Contain("goal=20 trump=S"));
        }

        [Test]
        public void NapoleonAllPassRedealsWithTheNextDealer()
        {
            IGame game = BuiltInGames.Registry.Create("napoleon", 5, 13);
            for (int count = 0; count < 5; count++) game.Apply(new TrumpLab.Action("pass"));
            Assert.That(game.View(), Does.Contain("phase=bid deal=2 dealer=P1"));
            Assert.That(game.CurrentPlayer, Is.EqualTo(2));
            Assert.That(HandCounts(game), Is.All.EqualTo(10));
        }

        [Test]
        public void NapoleonCallsAnyUniqueCardAndKeepsTheAdjutantSecretUntilPlayed()
        {
            IGame game = BuiltInGames.Registry.Create("napoleon", 5, 14,
                new Dictionary<string, string> { ["target_score"] = "1" });
            MakeNapoleon(game);
            string called = Hand(game, 2).First(card => card != "JOKER");
            game.Apply(Call(called));
            Assert.That(game.View(0), Does.Contain("called=" + called));
            Assert.That(game.View(0), Does.Contain("adjutant=hidden"));
            Assert.That(game.View(1), Does.Contain("adjutant=hidden"),
                "Napoleon knows the called card, not its holder.");
            Assert.That(game.View(2), Does.Contain("adjutant=P2"));
            Assert.That(game.View(2), Does.Contain("your_role=adjutant"));
            Assert.That(game.View(3), Does.Contain("your_role=opposition"));

            var policy = new DeterministicRandom(1400);
            while (game.View().Contains("phase=discard_widow", StringComparison.Ordinal))
                game.Apply(game.ChooseCpuAction(game.CurrentPlayer, policy));
            bool revealed = false;
            while (!game.IsTerminal && !revealed)
            {
                TrumpLab.Action action = game.CurrentPlayer == 2
                    ? game.LegalActions().FirstOrDefault(candidate => candidate.Value == called)
                    : default;
                if (action.Kind == null) action = game.ChooseCpuAction(game.CurrentPlayer, policy);
                bool isReveal = game.CurrentPlayer == 2 && action.Value == called;
                if (isReveal) Assert.That(game.View(0), Does.Contain("adjutant=hidden"));
                game.Apply(action);
                if (isReveal)
                {
                    Assert.That(game.View(0), Does.Contain("adjutant=P2"));
                    revealed = true;
                }
            }
            Assert.That(revealed, Is.True);
        }

        [Test]
        public void NapoleonSelfCallAndWidowCallArePrivateSoloDeals()
        {
            IGame self = BuiltInGames.Registry.Create("napoleon", 5, 15);
            MakeNapoleon(self);
            string own = Hand(self, self.CurrentPlayer)[0];
            self.Apply(Call(own));
            Assert.That(self.View(1), Does.Contain("adjutant=solo"));
            Assert.That(self.View(0), Does.Contain("adjutant=hidden"));

            IGame widow = BuiltInGames.Registry.Create("napoleon", 5, 16);
            MakeNapoleon(widow);
            var dealt = new HashSet<string>(Enumerable.Range(0, 5).SelectMany(player => Hand(widow, player)));
            string hiddenWidowCard = NapoleonGame.DeckComposition().Keys.First(card => !dealt.Contains(card));
            widow.Apply(Call(hiddenWidowCard));
            Assert.That(widow.View(1), Does.Contain("adjutant=solo"));
            Assert.That(widow.View(0), Does.Contain("adjutant=hidden"));
        }

        [Test]
        public void NapoleonTakesTheWidowAndPublishesOnlyDiscardedHonors()
        {
            IGame game = BuiltInGames.Registry.Create("napoleon", 5, 17);
            MakeNapoleon(game);
            game.Apply(Call(Hand(game, game.CurrentPlayer)[0]));
            Assert.That(Hand(game, 1), Has.Length.EqualTo(13));
            string honor = Hand(game, 1).First(IsHonor);
            game.Apply(PlayNamed("discard_widow", honor));
            while (game.View().Contains("phase=discard_widow", StringComparison.Ordinal))
                game.Apply(game.ChooseCpuAction(game.CurrentPlayer, new DeterministicRandom(100)));
            Assert.That(Hand(game, 1), Has.Length.EqualTo(10));
            Assert.That(game.View(0), Does.Contain("discarded_honors=[" + honor));
            Assert.That(game.View(0), Does.Contain("your_discard=[hidden]"));
            Assert.That(game.View(1), Does.Contain("your_discard=["));
            Assert.That(game.CurrentPlayer, Is.EqualTo(1));
        }

        [Test]
        public void NapoleonMustFollowPrintedSuitWhileJokerRemainsAFreeResponse()
        {
            bool found = false;
            for (int seed = 1; seed <= 200 && !found; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("napoleon", 5, seed,
                    new Dictionary<string, string> { ["target_score"] = "1" });
                ReachPlay(game);
                int leader = game.CurrentPlayer;
                int follower = (leader + 1) % 5;
                string[] followerHand = Hand(game, follower);
                TrumpLab.Action? lead = game.LegalActions().FirstOrDefault(action =>
                    action.Card.HasValue
                    && followerHand.Any(card => card != "JOKER" && Card.Parse(card).Suit == action.Card.Value.Suit)
                    && followerHand.Any(card => card != "JOKER" && Card.Parse(card).Suit != action.Card.Value.Suit));
                if (!lead.HasValue || lead.Value.Kind == null) continue;
                Suit suit = lead.Value.Card!.Value.Suit;
                game.Apply(lead.Value);
                IReadOnlyList<TrumpLab.Action> legal = game.LegalActions();
                Assert.That(legal.Where(action => action.Value != "JOKER")
                    .All(action => action.Card!.Value.Suit == suit), Is.True);
                if (followerHand.Contains("JOKER"))
                    Assert.That(legal.Any(action => action.Value == "JOKER"), Is.True);
                found = true;
            }
            Assert.That(found, Is.True);
        }

        [Test]
        public void NapoleonSpecialCardPriorityMatchesTheAdoptedVariant()
        {
            Assert.That(NapoleonGame.ResolveTrickWinner(
                new[] { "10C", "KC", "2H", "AH", "3C" }, Suit.Hearts), Is.EqualTo(3));
            Assert.That(NapoleonGame.ResolveTrickWinner(
                new[] { "10C", "JD", "JH", "AH", "3C" }, Suit.Hearts), Is.EqualTo(2));
            Assert.That(NapoleonGame.ResolveTrickWinner(
                new[] { "10C", "JD", "AH", "3C", "4C" }, Suit.Hearts), Is.EqualTo(1));

            Assert.That(NapoleonGame.ResolveTrickWinner(
                new[] { "AS", "QH", "JS", "JC", "2S" }, Suit.Spades), Is.EqualTo(1),
                "Yoromeki beats Mighty and every lower special.");
            Assert.That(NapoleonGame.ResolveTrickWinner(
                new[] { "AS", "QH", "JS", "JC", "2S" }, Suit.Spades, yoromeki: false),
                Is.EqualTo(0));
            Assert.That(NapoleonGame.ResolveTrickWinner(
                new[] { "AS", "AH", "KH", "2H", "3H" }, Suit.Spades), Is.EqualTo(0),
                "Mighty remains AS even when spades are trump.");

            Assert.That(NapoleonGame.ResolveTrickWinner(
                new[] { "JOKER", "JH", "JD", "AH", "2H" }, Suit.Hearts), Is.EqualTo(0));
            Assert.That(NapoleonGame.ResolveTrickWinner(
                new[] { "JOKER", "JH", "AS", "AH", "2H" }, Suit.Hearts), Is.EqualTo(2));
            Assert.That(NapoleonGame.ResolveTrickWinner(
                new[] { "10C", "JOKER", "KC", "2C", "3C" }, Suit.Hearts), Is.EqualTo(2),
                "A following Joker is weakest and prevents Same Two.");
        }

        [Test]
        public void NapoleonSameTwoIsBelowTheThreeFixedSpecialsAndDisabledOnFirstTrick()
        {
            string[] same = { "AH", "2H", "QH", "10H", "3H" };
            Assert.That(NapoleonGame.ResolveTrickWinner(same, Suit.Clubs), Is.EqualTo(1));
            Assert.That(NapoleonGame.ResolveTrickWinner(same, Suit.Clubs, firstTrick: true),
                Is.EqualTo(0));
            Assert.That(NapoleonGame.ResolveTrickWinner(same, Suit.Clubs, sameTwo: false),
                Is.EqualTo(0));
            Assert.That(NapoleonGame.ResolveTrickWinner(
                new[] { "AH", "2H", "JH", "10H", "3H" }, Suit.Hearts), Is.EqualTo(2));
            Assert.That(NapoleonGame.ResolveTrickWinner(
                new[] { "AH", "2H", "JD", "10H", "3H" }, Suit.Hearts), Is.EqualTo(2));
        }

        [Test]
        public void NapoleonFirstTrickForbidsJokerLeadAndLaterJokerLeadRequestsTrump()
        {
            bool found = false;
            for (int seed = 1; seed <= 300 && !found; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("napoleon", 5, seed);
                ReachPlay(game, preserveJoker: true);
                if (!Hand(game, game.CurrentPlayer).Contains("JOKER")) continue;
                Assert.That(game.LegalActions().Any(action => action.Kind == "lead_joker"), Is.False);
                SetPrivateField(game, "trickNumber", 1);
                Assert.That(game.LegalActions().Any(action => action.Kind == "lead_joker"), Is.True);
                game.Apply(new TrumpLab.Action("lead_joker", value: "JOKER"));
                string trump = Value(game.View(), "trump=");
                string[] nextHand = Hand(game, game.CurrentPlayer);
                bool hasTrump = nextHand.Any(card => card != "JOKER"
                    && Card.SuitCode(Card.Parse(card).Suit) == trump);
                if (hasTrump)
                    Assert.That(game.LegalActions().All(action => action.Card.HasValue
                        && Card.SuitCode(action.Card.Value.Suit) == trump), Is.True);
                found = true;
            }
            Assert.That(found, Is.True);
        }

        [Test]
        public void NapoleonDealWinCountsCoverExactOverUnderSweepAndSoloWithoutExtraMultipliers()
        {
            Assert.That(NapoleonGame.DealWinDeltas(5, 0, 1, 12, 12),
                Is.EqualTo(new[] { 1, 1, 0, 0, 0 }));
            Assert.That(NapoleonGame.DealWinDeltas(5, 0, 1, 12, 13),
                Is.EqualTo(new[] { 1, 1, 0, 0, 0 }));
            Assert.That(NapoleonGame.DealWinDeltas(5, 0, 1, 12, 20),
                Is.EqualTo(new[] { 1, 1, 0, 0, 0 }), "A sweep has no adopted bonus.");
            Assert.That(NapoleonGame.DealWinDeltas(5, 0, 1, 12, 11),
                Is.EqualTo(new[] { 0, 0, 1, 1, 1 }));
            Assert.That(NapoleonGame.DealWinDeltas(5, 0, -1, 12, 12),
                Is.EqualTo(new[] { 1, 0, 0, 0, 0 }), "Solo has no adopted multiplier.");
            Assert.That(NapoleonGame.DealWinDeltas(5, 0, -1, 12, 11),
                Is.EqualTo(new[] { 0, 1, 1, 1, 1 }));
        }

        [Test]
        public void NapoleonOptionsAreBoundedInstanceLocalAndDeterministic()
        {
            var custom = (NapoleonGame)BuiltInGames.Registry.Create("napoleon", 5, 31,
                new Dictionary<string, string>
                {
                    ["target_score"] = "1", ["minimum_bid"] = "10",
                    ["yoromeki"] = "false", ["same_two"] = "false"
                });
            var normal = (NapoleonGame)BuiltInGames.Registry.Create("napoleon", 5, 31);
            Assert.That(custom.SessionTarget, Is.EqualTo(1));
            Assert.That(custom.MinimumBid, Is.EqualTo(10));
            Assert.That(custom.Yoromeki, Is.False);
            Assert.That(custom.SameTwo, Is.False);
            Assert.That(normal.SessionTarget, Is.EqualTo(5));
            Assert.That(normal.MinimumBid, Is.EqualTo(12));
            Assert.That(normal.Yoromeki, Is.True);
            Assert.That(normal.SameTwo, Is.True);
            Candidate catalogue = GameCatalogue.Candidates().Single(candidate =>
                candidate.ImplementationId == "napoleon");
            Assert.That(catalogue.Players, Is.EqualTo("4-7"));
            Assert.That(catalogue.Status, Is.EqualTo(CandidateStatus.Verified));
            Assert.That(BuiltInGames.Registry.Info("napoleon").MinPlayers, Is.EqualTo(4));
            Assert.That(BuiltInGames.Registry.Info("napoleon").MaxPlayers, Is.EqualTo(7));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInGames.Registry.Create("napoleon", 5, 1,
                new Dictionary<string, string> { ["target_score"] = "0" }));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInGames.Registry.Create("napoleon", 5, 1,
                new Dictionary<string, string> { ["minimum_bid"] = "21" }));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInGames.Registry.Create("napoleon", 3, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuiltInGames.Registry.Create("napoleon", 8, 1));

            GameResult left = Simulator.RunGame(BuiltInGames.Registry.Create("napoleon", 5, 33,
                new Dictionary<string, string> { ["target_score"] = "1" }), 70033);
            GameResult right = Simulator.RunGame(BuiltInGames.Registry.Create("napoleon", 5, 33,
                new Dictionary<string, string> { ["target_score"] = "1" }), 70033);
            Assert.That(right.Winners, Is.EqualTo(left.Winners));
            Assert.That(right.Scores, Is.EqualTo(left.Scores));
            Assert.That(right.Turns, Is.EqualTo(left.Turns));
        }

        [Test]
        public void NapoleonCpuDoesNotReadAnotherPlayersHiddenAdjutantIdentity()
        {
            IGame left = BuiltInGames.Registry.Create("napoleon", 5, 44);
            IGame right = BuiltInGames.Registry.Create("napoleon", 5, 44);
            ReachPlay(left); ReachPlay(right);
            TrumpLab.Action opening = left.LegalActions().First();
            left.Apply(opening); right.Apply(opening);
            int viewer = left.CurrentPlayer;
            int napoleon = int.Parse(Value(left.View(), "napoleon=P"));
            int[] hiddenSeats = Enumerable.Range(0, 5)
                .Where(player => player != viewer && player != napoleon).Take(2).ToArray();
            SetPrivateField(left, "adjutant", hiddenSeats[0]);
            SetPrivateField(right, "adjutant", hiddenSeats[1]);
            SetPrivateField(left, "solo", false); SetPrivateField(right, "solo", false);
            SetPrivateField(left, "adjutantRevealed", false);
            SetPrivateField(right, "adjutantRevealed", false);
            Assert.That(left.View(viewer), Is.EqualTo(right.View(viewer)));
            Assert.That(left.ChooseCpuAction(viewer, new DeterministicRandom(1)),
                Is.EqualTo(right.ChooseCpuAction(viewer, new DeterministicRandom(999))));
        }

        [Test]
        public void NapoleonCpuAlwaysReturnsLegalActionsAndAllPlayerCountsCompleteAcrossSeeds()
        {
            foreach (int players in Enumerable.Range(4, 4))
            foreach (int seed in Enumerable.Range(60, 8))
            {
                IGame game = BuiltInGames.Registry.Create("napoleon", players, seed,
                    new Dictionary<string, string> { ["target_score"] = "1" });
                var policy = new DeterministicRandom(seed + 80000);
                while (!game.IsTerminal)
                {
                    IReadOnlyList<TrumpLab.Action> legal = game.LegalActions();
                    TrumpLab.Action action = game.ChooseCpuAction(game.CurrentPlayer, policy);
                    Assert.That(legal, Does.Contain(action), "players=" + players + " seed=" + seed);
                    game.Apply(action);
                }
                Assert.That(game.Result().Extra["coalition_honors"], Is.InRange(0, 20));
            }
        }

        private static void MakeNapoleon(IGame game)
        {
            string minimum = ((NapoleonGame)game).MinimumBid.ToString();
            game.Apply(new TrumpLab.Action("bid", value: minimum + ":C"));
            while (game.View().Contains("phase=bid", StringComparison.Ordinal))
                game.Apply(new TrumpLab.Action("pass"));
        }

        private static void ReachPlay(IGame game, bool preserveJoker = false)
        {
            MakeNapoleon(game);
            string[] own = Hand(game, game.CurrentPlayer);
            string call = own.FirstOrDefault(card => card != "JOKER") ?? own[0];
            game.Apply(Call(call));
            while (game.View().Contains("phase=discard_widow", StringComparison.Ordinal))
            {
                IReadOnlyList<TrumpLab.Action> legal = game.LegalActions();
                TrumpLab.Action action = preserveJoker
                    ? legal.Where(candidate => candidate.Value != "JOKER")
                        .OrderBy(candidate => IsHonor(candidate.Value!) ? 1 : 0).First()
                    : game.ChooseCpuAction(game.CurrentPlayer, new DeterministicRandom(5050));
                game.Apply(action);
            }
        }

        private static TrumpLab.Action Call(string card) => card == "JOKER"
            ? new TrumpLab.Action("call_joker", value: "JOKER")
            : new TrumpLab.Action("call_adjutant", Card.Parse(card), value: card);

        private static TrumpLab.Action PlayNamed(string kind, string card) => card == "JOKER"
            ? new TrumpLab.Action(kind, value: card)
            : new TrumpLab.Action(kind, Card.Parse(card), value: card);

        private static string[] Hand(IGame game, int player)
        {
            string view = game.View(player);
            string marker = "your hand: ";
            int start = view.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            string text = view.Substring(start).Trim();
            return text.Length == 0 ? Array.Empty<string>()
                : text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static int[] HandCounts(IGame game)
        {
            string text = Between(game.View(), "hand_counts=[", "]");
            return text.Split(',').Select(int.Parse).ToArray();
        }

        private static string Value(string view, string marker)
        {
            int start = view.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            int end = view.IndexOf(' ', start);
            return end < 0 ? view.Substring(start) : view.Substring(start, end - start);
        }

        private static string Between(string text, string left, string right)
        {
            int start = text.IndexOf(left, StringComparison.Ordinal) + left.Length;
            int end = text.IndexOf(right, start, StringComparison.Ordinal);
            return text.Substring(start, end - start);
        }

        private static bool IsHonor(string id)
        {
            if (id == "JOKER") return false;
            int rank = Card.Parse(id).Rank;
            return rank == 1 || rank >= 10;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo? field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field!.SetValue(target, value);
        }
    }
}
