using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class NineteenthRuleAuditTests
    {
        [Test]
        [Category("BroadSimulation")]
        public void Unit19FixedSeedAudit() => RuleAuditTestSupport.AssertFixedSeedBatch(
            1901, "italian_whist", "gooseberry_fool", "briscola_bugiarda");

        [Test]
        public void ItalianWhistJokerOwnerChoosesAmbiguousSuitAndUnusedRank()
        {
            bool sawSuitChoice = false;
            bool sawRankChoice = false;
            bool sawOccupiedRankExcluded = false;
            for (int seed = 1910; seed < 1940; seed++)
            {
                IGame game = BuiltInGames.Registry.Create("italian_whist", 3, seed,
                    new Dictionary<string, string> { ["deals"] = "1" });
                var random = new DeterministicRandom(seed * 100);
                while (!game.IsTerminal)
                {
                    IReadOnlyList<TrumpLab.Action> actions = game.LegalActions();
                    Assert.That(actions, Is.Not.Empty);
                    if (actions.All(action => action.Kind == "choose_joker_suit"))
                    {
                        sawSuitChoice = true;
                        Assert.That(actions.Count, Is.EqualTo(2));
                    }
                    if (actions.All(action => action.Kind == "choose_joker_rank"))
                    {
                        sawRankChoice = true;
                        Assert.That(actions.Select(action => int.Parse(action.Value!)),
                            Is.Unique.And.All.InRange(1, 13));
                        if (actions.Count < 13) sawOccupiedRankExcluded = true;
                    }
                    TrumpLab.Action selected = game.ChooseCpuAction(game.CurrentPlayer, random);
                    Assert.That(actions, Does.Contain(selected));
                    game.Apply(selected);
                }
            }
            Assert.That(sawSuitChoice, Is.True);
            Assert.That(sawRankChoice, Is.True);
            Assert.That(sawOccupiedRankExcluded, Is.True);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("italian_whist", 1941);
        }

        [Test]
        public void GooseberryFoolUsesTheAuthorTieAndMedianWinnerRules()
        {
            for (int first = 0; first <= 11; first++)
            for (int second = 0; second <= 11 - first; second++)
            {
                int third = 11 - first - second;
                int[] dealScores = { first + 2 * third, second + 2 * first, third + 2 * second };
                Assert.That(dealScores.Distinct().Count(), Is.EqualTo(3),
                    $"tricks={first},{second},{third}");
            }

            IGame tied = BuiltInGames.Registry.Create("gooseberry_fool", seed: 1950);
            SetScoresAndFinish(tied, 105, 105, 99);
            Assert.That(tied.Result().Winners, Is.EqualTo(new[] { 2 }));

            IGame distinct = BuiltInGames.Registry.Create("gooseberry_fool", seed: 1951);
            SetScoresAndFinish(distinct, 101, 110, 109);
            Assert.That(distinct.Result().Winners, Is.EqualTo(new[] { 2 }));
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("gooseberry_fool", 1952);
        }

        [Test]
        public void BriscolaBugiardaExplicitSoloStartsPublicNoTrumpOneAgainstFour()
        {
            IGame game = BuiltInGames.Registry.Create("briscola_bugiarda", seed: 1960);
            Assert.That(game.LegalActions().Count(action => action.Kind == "bid_solo"), Is.EqualTo(1));
            int declarer = game.CurrentPlayer;
            game.Apply(new TrumpLab.Action("bid_solo"));
            for (int viewer = 0; viewer < 5; viewer++)
            {
                Assert.That(game.View(viewer), Does.Contain("phase=play"));
                Assert.That(game.View(viewer), Does.Contain("trump=none"));
                Assert.That(game.View(viewer), Does.Contain("declarer=P" + declarer));
                Assert.That(game.View(viewer), Does.Contain("partner=solo"));
            }
            var random = new DeterministicRandom(196000);
            while (!game.IsTerminal)
            {
                TrumpLab.Action action = game.ChooseCpuAction(game.CurrentPlayer, random);
                Assert.That(game.LegalActions(), Does.Contain(action));
                game.Apply(action);
            }
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands(
                "briscola_bugiarda", 1961);
        }

        private static void SetScoresAndFinish(IGame game, params int[] values)
        {
            int[] scores = (int[])Field(game, "scores").GetValue(game)!;
            Array.Copy(values, scores, values.Length);
            Field(game, "finished").SetValue(game, true);
        }

        private static FieldInfo Field(object source, string name) => source.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("Missing field " + name);
    }
}
