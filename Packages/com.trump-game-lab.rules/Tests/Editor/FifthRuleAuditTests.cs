using System.Linq;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class FifthRuleAuditTests
    {
        [Test]
        public void Unit05FixedSeedsCompleteAndCpuActionsAreLegal() =>
            RuleAuditTestSupport.AssertFixedSeedBatch(501,
                "hamlet", "whos_who", "farbwechsel", "sheriff", "mizerka");

        [Test]
        public void Unit05BoundaryValuesAndObservationEquivalence()
        {
            Assert.That(BuiltInGames.Registry.Create("hamlet", seed: 511).LegalActions().All(a => a.Kind == "choose_mode_card"), Is.True);
            Assert.That(BuiltInGames.Registry.Create("sheriff", seed: 512).View(), Does.Contain("phase=choose_roles"));
            Assert.That(BuiltInGames.Registry.Create("mizerka", seed: 513).LegalActions().Select(a => a.Value), Does.Contain("M"));
            Assert.That(BuiltInGames.Registry.Create("whos_who", seed: 514).View(), Does.Contain("hand_counts=[14,14,14]"));
            Assert.That(BuiltInGames.Registry.Create("farbwechsel", seed: 515).View(), Does.Contain("phase=bid"));
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("hamlet", 521);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("whos_who", 522);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("farbwechsel", 523);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("sheriff", 524);
            RuleAuditTestSupport.AssertOpeningObservationIgnoresTwoOtherHands("mizerka", 525);
        }

        [Test]
        public void SheriffJokerHolderCanLeadItAndItCannotWinTheTrick()
        {
            IGame game = BuiltInGames.Registry.Create("sheriff", seed: 526);
            int jokerHolder = game.CurrentPlayer;
            game.Apply(new TrumpLab.Action("choose_role", value: "mayor"));
            game.Apply(new TrumpLab.Action("choose_role", value: "sheriff"));
            game.Apply(new TrumpLab.Action("choose_role", value: "robber"));
            game.Apply(new TrumpLab.Action("choose_trump", value: "N"));

            Assert.That(game.CurrentPlayer, Is.EqualTo(jokerHolder));
            game.Apply(game.LegalActions().Single(action => action.Value == "X"));
            game.Apply(game.LegalActions()[0]);
            game.Apply(game.LegalActions()[0]);

            Assert.That(game.CurrentPlayer, Is.Not.EqualTo(jokerHolder));
        }

        [Test]
        public void FarbwechselRevealsEveryBidAfterTheEleventhTrick()
        {
            IGame game = BuiltInGames.Registry.Create("farbwechsel", 3, 527,
                new System.Collections.Generic.Dictionary<string, string> { ["target_score"] = "999" });
            for (int turn = 0; turn < 36; turn++) game.Apply(game.LegalActions()[0]);

            Assert.That(game.View(0), Does.Contain("phase=bid"));
            Assert.That(game.View(0), Does.Contain("revealed_bids=[0,0,0]"));
        }
    }
}
