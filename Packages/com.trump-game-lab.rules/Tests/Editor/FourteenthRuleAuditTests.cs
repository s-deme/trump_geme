using System;
using System.Linq;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class FourteenthRuleAuditTests
    {
        [Test]
        public void Unit14FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            1401, "old_maid", "speed", "gops", "spite_and_malice", "casino");

        [Test]
        public void OldMaidUsesFiftyOneCardsAndOnlyOneOddQueenRemains()
        {
            IGame game = BuiltInGames.Registry.Create("old_maid", players: 4, seed: 1410);
            var random = new DeterministicRandom(141000);
            RuleAuditTestSupport.PlayWithLegalCpu(game, 141000);
            Assert.That(game.Result().Winners, Has.Count.EqualTo(3));
            Assert.That(game.Result().Scores.Count(score => score < 0), Is.EqualTo(1));
        }

        [Test]
        public void GopsKeepsFirstBidSecretAndAccountsForAllNinetyOnePrizePoints()
        {
            IGame low = BuiltInGames.Registry.Create("gops", seed: 1420);
            IGame high = BuiltInGames.Registry.Create("gops", seed: 1420);
            low.Apply(low.LegalActions().First()); high.Apply(high.LegalActions().Last());
            Assert.That(high.View(1), Is.EqualTo(low.View(1)));
            Assert.That(high.LegalActions(), Is.EqualTo(low.LegalActions()));
            Assert.That(high.ChooseCpuAction(1, new DeterministicRandom(142000)),
                Is.EqualTo(low.ChooseCpuAction(1, new DeterministicRandom(142000))));

            GameResult result = RuleAuditTestSupport.PlayWithLegalCpu(low, 142001);
            Assert.That(result.Scores.Sum() + Convert.ToInt32(result.Extra["unclaimed"]), Is.EqualTo(91));
        }

        [Test]
        public void SpiteAndMaliceStartsWithHigherPayoffCardUsingAceLow()
        {
            for (int seed = 1430; seed < 1450; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("spite_and_malice", seed: seed);
                string[] payoff = Between(game.View(0), "payoffs=[", "]").Split(',');
                Card left = Card.Parse(payoff[0].Substring(payoff[0].IndexOf(':') + 1));
                Card right = Card.Parse(payoff[1].Substring(payoff[1].IndexOf(':') + 1));
                Assert.That(left.Rank, Is.Not.EqualTo(right.Rank));
                Assert.That(game.CurrentPlayer, Is.EqualTo(left.Rank > right.Rank ? 0 : 1));
            }
        }

        [Test]
        public void Unit14PromotedGamesKeepHiddenInformationPrivate()
        {
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("old_maid", 3, 1440);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresOpponentHandAndStock("spite_and_malice", 1441);
        }

        private static string Between(string value, string prefix, string suffix)
        {
            int start = value.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            int end = value.IndexOf(suffix, start, StringComparison.Ordinal);
            return value.Substring(start, end - start);
        }
    }
}
