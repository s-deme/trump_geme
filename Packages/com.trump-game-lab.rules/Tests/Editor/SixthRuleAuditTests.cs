using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class SixthRuleAuditTests
    {
        [Test]
        public void Unit06FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            601, "ninety_nine", "five_hundred", "skat", "gooseberry_fool", "ulti");

        [Test]
        public void NinetyNineDefaultSessionCompletesAfterNineDeals()
        {
            IGame game = BuiltInGames.Registry.Create("ninety_nine", 3, 601);
            GameResult result = RuleAuditTestSupport.PlayWithLegalCpu(game, 60100);

            Assert.That(result.Reason, Is.EqualTo("9-deal session"));
            Assert.That(game.View(), Does.Contain("phase=finished deal=9/9"));
        }

        [Test]
        public void NinetyNinePublishesOnlySuccessfulClaims()
        {
            bool sawSuccess = false;
            bool sawFailure = false;
            for (int seed = 606; seed <= 615; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("ninety_nine", 3, seed,
                    new Dictionary<string, string> { ["deals"] = "1" });
                var bids = new int[3];
                var random = new DeterministicRandom(seed * 100);
                while (!game.IsTerminal)
                {
                    int player = game.CurrentPlayer;
                    TrumpLab.Action action = game.ChooseCpuAction(player, random);
                    Assert.That(game.LegalActions(), Does.Contain(action));
                    if (action.Kind == "set_bid_card") bids[player] += BidValue(action.Card!.Value);
                    game.Apply(action);
                }

                string view = game.View();
                int[] tricks = ParseIntegers(view, @"tricks=\[(\d+),(\d+),(\d+)\]");
                Match claims = Regex.Match(view, @"revealed_bids=\[([^,]+),([^,]+),([^\]]+)\]");
                Assert.That(claims.Success, Is.True, view);

                bool[] succeeded = Enumerable.Range(0, 3)
                    .Select(player => bids[player] == tricks[player]).ToArray();
                sawSuccess |= succeeded.Contains(true);
                sawFailure |= succeeded.Contains(false);
                for (int player = 0; player < 3; player++)
                {
                    string shown = claims.Groups[player + 1].Value;
                    Assert.That(shown, Is.EqualTo(succeeded[player] ? bids[player].ToString() : "hidden"),
                        "seed " + seed + "/P" + player);
                }
            }
            Assert.That(sawSuccess, Is.True, "fixed seeds must cover a successful claim");
            Assert.That(sawFailure, Is.True, "fixed seeds must cover a hidden failed bid");
        }

        [Test]
        public void NinetyNineUsesSuccessCountForTheNextTrump()
        {
            IGame game = BuiltInGames.Registry.Create("ninety_nine", 3, 616,
                new Dictionary<string, string> { ["deals"] = "2" });
            var random = new DeterministicRandom(61600);
            while (!game.View().Contains("deal=2/2"))
            {
                TrumpLab.Action action = game.ChooseCpuAction(game.CurrentPlayer, random);
                Assert.That(game.LegalActions(), Does.Contain(action));
                game.Apply(action);
            }

            string view = game.View();
            Match claims = Regex.Match(view, @"revealed_bids=\[([^,]+),([^,]+),([^\]]+)\]");
            int successes = Enumerable.Range(1, 3)
                .Count(index => claims.Groups[index].Value != "hidden");
            string expectedTrump = successes == 3 ? "C" : successes == 2 ? "H" : successes == 1 ? "S" : "D";
            Assert.That(view, Does.Contain("trump=" + expectedTrump));
        }

        [Test]
        public void NinetyNineRevealOvercallsDeclareButEarlierPlayerCanRaise()
        {
            IGame game = BuiltInGames.Registry.Create("ninety_nine", 3, 617,
                new Dictionary<string, string> { ["deals"] = "1" });
            for (int count = 0; count < 9; count++) game.Apply(game.LegalActions()[0]);
            int leader = game.CurrentPlayer;
            game.Apply(new TrumpLab.Action("declare"));
            game.Apply(new TrumpLab.Action("reveal"));
            game.Apply(new TrumpLab.Action("pass_premium"));
            Assert.That(game.CurrentPlayer, Is.EqualTo(leader));
            Assert.That(game.LegalActions(), Does.Contain(new TrumpLab.Action("reveal")));
            game.Apply(new TrumpLab.Action("reveal"));
            game.Apply(new TrumpLab.Action("pass_premium"));
            game.Apply(new TrumpLab.Action("pass_premium"));

            Assert.That(game.View((leader + 1) % 3), Does.Contain("phase=play"));
            Assert.That(game.View((leader + 1) % 3), Does.Contain("premium=reveal"));
            Assert.That(game.View((leader + 1) % 3), Does.Contain("open_hand_P" + leader + "=["));
        }

        [Test]
        public void NinetyNineOpponentHandsAreOutsideTheObservationBoundary() =>
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("ninety_nine", 607);

        [Test]
        public void NinetyNineTargetScoreOptionRemainsAOneGameShortcut()
        {
            IGame game = BuiltInGames.Registry.Create("ninety_nine", 3, 608,
                new Dictionary<string, string> { ["target_score"] = "1" });
            GameResult result = RuleAuditTestSupport.PlayWithLegalCpu(game, 60800);

            Assert.That(result.Reason, Is.EqualTo("first to 1 plus game bonus"));
            Assert.That(game.View(), Does.Contain("target_score=1"));
        }

        private static int[] ParseIntegers(string text, string pattern)
        {
            Match match = Regex.Match(text, pattern);
            Assert.That(match.Success, Is.True, text);
            return Enumerable.Range(1, match.Groups.Count - 1)
                .Select(index => int.Parse(match.Groups[index].Value)).ToArray();
        }

        private static int BidValue(Card card) => card.Suit == Suit.Clubs ? 3 :
            card.Suit == Suit.Hearts ? 2 : card.Suit == Suit.Spades ? 1 : 0;
    }
}
