#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductActionFeedbackTests
    {
        private static IEnumerable<TestCaseData> ActionSequenceCases()
        {
            object[][] actions =
            {
                new object[] { "draw", null!, new[] { ProductFeedbackKind.Draw } },
                new object[]
                {
                    "choose_starter_suit", null!,
                    new[] { ProductFeedbackKind.WildSuit }
                },
                new object[] { "play", null!, new[] { ProductFeedbackKind.CardPlay } },
                new object[]
                {
                    "play", "H",
                    new[] { ProductFeedbackKind.CardPlay, ProductFeedbackKind.WildSuit }
                },
                new object[]
                {
                    "play_last_card", null!,
                    new[] { ProductFeedbackKind.CardPlay }
                },
                new object[]
                {
                    "play_last_card", "S",
                    new[] { ProductFeedbackKind.CardPlay, ProductFeedbackKind.WildSuit }
                },
                new object[] { "pass", null!, new[] { ProductFeedbackKind.Submit } }
            };

            foreach (int actor in new[] { 0, 1 })
            {
                foreach (object[] action in actions)
                {
                    yield return new TestCaseData(actor, action[0], action[1], action[2])
                        .SetName("ActionSequence_actor" + actor + "_" + action[0] + "_" +
                            (action[1] ?? "plain"));
                }
            }
        }

        [TestCaseSource(nameof(ActionSequenceCases))]
        public void ActionSequenceCoversEveryProductActionForHumanAndCpu(
            int actor, string kind, string? value, ProductFeedbackKind[] expected)
        {
            Assert.That(ProductActionFeedback.ClassifyActionSequence(
                Record(actor, kind, value)), Is.EqualTo(expected));
        }

        [TestCase("play")]
        [TestCase("play_last_card")]
        public void WildPlayPresentsCardBeforeSuitConfirmation(string kind)
        {
            Assert.That(ProductActionFeedback.ClassifyActionSequence(
                    Record(actor: 0, kind, value: "D")),
                Is.EqualTo(new[]
                {
                    ProductFeedbackKind.CardPlay,
                    ProductFeedbackKind.WildSuit
                }));
        }

        [TestCase("draw", null)]
        [TestCase("play", null)]
        [TestCase("play", "C")]
        [TestCase("pass", null)]
        public void CpuActionRetainsItsSemanticSequence(string kind, string? value)
        {
            SessionActionRecord human = Record(actor: 0, kind, value);
            SessionActionRecord cpu = Record(actor: 1, kind, value);

            Assert.That(ProductActionFeedback.Classify(cpu),
                Is.EqualTo(ProductFeedbackKind.CpuTurn));
            Assert.That(ProductActionFeedback.ClassifyActionSequence(cpu),
                Is.EqualTo(ProductActionFeedback.ClassifyActionSequence(human)));
        }

        [TestCase(1, "unknown_cpu_kind", null, ProductFeedbackKind.CpuTurn)]
        [TestCase(0, "draw", null, ProductFeedbackKind.Draw)]
        [TestCase(0, "choose_starter_suit", null, ProductFeedbackKind.WildSuit)]
        [TestCase(0, "play", "H", ProductFeedbackKind.WildSuit)]
        [TestCase(0, "play_last_card", "S", ProductFeedbackKind.WildSuit)]
        [TestCase(0, "play", null, ProductFeedbackKind.CardPlay)]
        [TestCase(0, "play_last_card", null, ProductFeedbackKind.CardPlay)]
        [TestCase(0, "pass", null, ProductFeedbackKind.Submit)]
        public void ClassifierMapsRecordedActorAndAction(
            int actor, string kind, string? value, ProductFeedbackKind expected)
        {
            Assert.That(ProductActionFeedback.Classify(Record(actor, kind, value)),
                Is.EqualTo(expected));
        }

        [Test]
        public void ClassifierRejectsNullAndUnknownHumanActions()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ProductActionFeedback.Classify(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ProductActionFeedback.Classify(Record(0, "unknown_human_kind")));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ActionSequenceRejectsUnknownActionsForEveryActor(int actor)
        {
            Assert.Throws<ArgumentNullException>(() =>
                ProductActionFeedback.ClassifyActionSequence(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ProductActionFeedback.ClassifyActionSequence(
                    Record(actor, "unknown_action_kind")));
        }

        [Test]
        public void GameSessionEmitsEachNewRecordBeforePresentationNotifications()
        {
            var session = new GameSessionController(seed: 1);
            var notifications = new List<string>();
            var emitted = new List<SessionActionRecord>();
            session.ActionApplied += record =>
            {
                Assert.That(session.State, Is.EqualTo(MatchSessionState.Applying));
                notifications.Add("action");
                emitted.Add(record);
            };
            session.SnapshotChanged += _ => notifications.Add("snapshot");
            session.Finished += _ => notifications.Add("finished");

            session.Begin();

            Assert.That(emitted, Is.Empty);
            Assert.That(notifications, Is.EqualTo(new[] { "snapshot" }));
            bool sawHuman = false;
            bool sawCpu = false;
            notifications.Clear();

            for (int step = 0; step < 1000 &&
                session.State != MatchSessionState.Finished; step++)
            {
                int previousRecordCount = session.Archive.Actions.Count;
                int previousEventCount = emitted.Count;
                notifications.Clear();

                bool applied;
                if (session.State == MatchSessionState.AwaitingHuman)
                {
                    Assert.That(session.TryApplyHumanAction("not_current"), Is.False);
                    Assert.That(emitted.Count, Is.EqualTo(previousEventCount));
                    Assert.That(notifications, Is.Empty);
                    applied = session.TryApplyHumanAction(session.Snapshot.Actions[0].Id);
                    sawHuman = true;
                }
                else if (session.State == MatchSessionState.WaitingForCpu)
                {
                    applied = session.TryApplyCpuAction();
                    sawCpu = true;
                }
                else
                {
                    Assert.Fail("Unexpected session state: " + session.State);
                    return;
                }

                Assert.That(applied, Is.True, session.FaultMessage);
                Assert.That(session.Archive.Actions.Count, Is.EqualTo(previousRecordCount + 1));
                Assert.That(emitted.Count, Is.EqualTo(previousEventCount + 1));
                Assert.That(emitted[emitted.Count - 1],
                    Is.SameAs(session.Archive.Actions[previousRecordCount]));
                Assert.That(notifications.Take(2),
                    Is.EqualTo(new[] { "action", "snapshot" }));
                Assert.That(notifications.Count,
                    Is.EqualTo(session.State == MatchSessionState.Finished ? 3 : 2));
                if (session.State == MatchSessionState.Finished)
                    Assert.That(notifications[2], Is.EqualTo("finished"));
            }

            Assert.That(session.State, Is.EqualTo(MatchSessionState.Finished),
                session.FaultMessage);
            Assert.That(sawHuman, Is.True);
            Assert.That(sawCpu, Is.True);
            Assert.That(emitted.Count, Is.EqualTo(session.Archive.Actions.Count));
        }

        [Test]
        public void ResumedSessionDoesNotEmitHistoricalRecordsOnBegin()
        {
            var original = new GameSessionController(seed: 23);
            original.Begin();
            ApplyCurrent(original);
            byte[] encoded = SessionArchiveCodec.Encode(original.Archive);

            var resumed = new GameSessionController(SessionArchiveCodec.Decode(encoded));
            var emitted = new List<SessionActionRecord>();
            var cues = new List<ProductFeedbackKind>();
            resumed.ActionApplied += record =>
            {
                emitted.Add(record);
                cues.AddRange(ProductActionFeedback.ClassifyActionSequence(record));
            };

            resumed.Begin();

            int historicalCount = resumed.Archive.Actions.Count;
            Assert.That(historicalCount, Is.GreaterThan(0));
            Assert.That(emitted, Is.Empty);
            Assert.That(cues, Is.Empty,
                "Resuming must not present semantic cues for historical actions.");

            ApplyCurrent(resumed);

            Assert.That(emitted, Has.Count.EqualTo(1));
            Assert.That(resumed.Archive.Actions.Count, Is.EqualTo(historicalCount + 1));
            Assert.That(emitted[0], Is.SameAs(resumed.Archive.Actions[historicalCount]));
            Assert.That(cues, Is.EqualTo(ProductActionFeedback.ClassifyActionSequence(
                resumed.Archive.Actions[historicalCount])));
        }

        [Test]
        public void PresentationSpeedChangesTimingOnlyNotSemanticOrderOrGameOutcome()
        {
            ProductPresentationSpeed[] speeds =
            {
                ProductPresentationSpeed.Reduced,
                ProductPresentationSpeed.Normal,
                ProductPresentationSpeed.Fast
            };
            SpeedScenario[] scenarios = speeds.Select(RunSpeedScenario).ToArray();
            SpeedScenario baseline = scenarios[0];

            foreach (SpeedScenario scenario in scenarios.Skip(1))
            {
                Assert.That(scenario.ActionSignatures,
                    Is.EqualTo(baseline.ActionSignatures), scenario.Speed.ToString());
                Assert.That(scenario.EncodedArchive,
                    Is.EqualTo(baseline.EncodedArchive), scenario.Speed.ToString());
                Assert.That(scenario.SemanticCues,
                    Is.EqualTo(baseline.SemanticCues), scenario.Speed.ToString());
                Assert.That(scenario.TurnCount,
                    Is.EqualTo(baseline.TurnCount), scenario.Speed.ToString());
                Assert.That(scenario.ResultWinners,
                    Is.EqualTo(baseline.ResultWinners), scenario.Speed.ToString());
                Assert.That(scenario.ResultScores,
                    Is.EqualTo(baseline.ResultScores), scenario.Speed.ToString());
                Assert.That(scenario.ResultReason,
                    Is.EqualTo(baseline.ResultReason), scenario.Speed.ToString());
                Assert.That(scenario.ResultTurns,
                    Is.EqualTo(baseline.ResultTurns), scenario.Speed.ToString());
            }

            Assert.That(scenarios.Select(scenario => scenario.PresentationSeconds)
                    .Distinct().Count(),
                Is.EqualTo(speeds.Length),
                "Each speed must exercise different presentation timing while game data stays fixed.");
        }

        [Test]
        public void TutorialEmitsEachHumanAndCpuRecordBeforeChanged()
        {
            var tutorial = new TutorialSessionController();
            var notifications = new List<string>();
            var emitted = new List<SessionActionRecord>();
            tutorial.ActionApplied += record =>
            {
                notifications.Add("action");
                emitted.Add(record);
            };
            tutorial.Changed += () => notifications.Add("changed");

            tutorial.Begin();

            Assert.That(emitted, Is.Empty);
            Assert.That(notifications, Is.EqualTo(new[] { "changed" }));
            notifications.Clear();
            Assert.That(tutorial.AcknowledgeIntro(), Is.True, tutorial.FaultMessage);
            Assert.That(emitted, Is.Empty);
            Assert.That(notifications, Is.EqualTo(new[] { "changed" }));

            for (int step = 0; step < 100 &&
                tutorial.State != TutorialSessionState.AwaitingResultConfirmation; step++)
            {
                int previousRecordCount = tutorial.Archive.Actions.Count;
                int previousEventCount = emitted.Count;
                notifications.Clear();

                bool applied;
                if (tutorial.State == TutorialSessionState.AwaitingHuman)
                    applied = tutorial.TryApplyHumanAction(tutorial.ExpectedActionId!);
                else if (tutorial.State == TutorialSessionState.WaitingForCpu)
                    applied = tutorial.TryApplyCpuAction();
                else
                {
                    Assert.Fail("Unexpected tutorial state: " + tutorial.State);
                    return;
                }

                Assert.That(applied, Is.True, tutorial.FaultMessage);
                Assert.That(tutorial.Archive.Actions.Count,
                    Is.EqualTo(previousRecordCount + 1));
                Assert.That(emitted.Count, Is.EqualTo(previousEventCount + 1));
                Assert.That(emitted[emitted.Count - 1],
                    Is.SameAs(tutorial.Archive.Actions[previousRecordCount]));
                Assert.That(notifications, Is.EqualTo(new[] { "action", "changed" }));
            }

            Assert.That(tutorial.State,
                Is.EqualTo(TutorialSessionState.AwaitingResultConfirmation),
                tutorial.FaultMessage);
            Assert.That(emitted.Count, Is.EqualTo(tutorial.Archive.Actions.Count));
            Assert.That(emitted.Count, Is.EqualTo(tutorial.Definition.Trace.Count));
            Assert.That(emitted.Select(record => record.Actor), Does.Contain(0));
            Assert.That(emitted.Select(record => record.Actor), Does.Contain(1));
        }

        private static SessionActionRecord Record(
            int actor, string kind, string? value = null) =>
            new SessionActionRecord(actor, new TrumpLab.Action(kind, value: value),
                turnAfter: 1, currentPlayerAfter: actor == 0 ? 1 : 0,
                terminalAfter: false);

        private static SpeedScenario RunSpeedScenario(ProductPresentationSpeed speed)
        {
            const long seed = 20260816L;
            ProductPresentationPolicy policy = ProductPresentationPolicy.From(
                speed, reducedMotion: false);
            var session = new GameSessionController(seed);
            var semanticCues = new List<ProductFeedbackKind>();
            float presentationSeconds = 0f;
            GameResultPresentation? result = null;
            session.ActionApplied += record =>
            {
                presentationSeconds += ConsumePresentationOnly(
                    policy,
                    ProductActionFeedback.ClassifyActionSequence(record),
                    semanticCues);
            };
            session.Finished += completed => result = completed;
            session.Begin();

            for (int step = 0; step < 2000 &&
                session.State != MatchSessionState.Finished; step++)
            {
                bool applied;
                if (session.State == MatchSessionState.AwaitingHuman)
                {
                    string selectedActionId = session.Snapshot.Actions
                        .OrderBy(action => action.Id, StringComparer.Ordinal)
                        .First().Id;
                    applied = session.TryApplyHumanAction(selectedActionId);
                }
                else if (session.State == MatchSessionState.WaitingForCpu)
                {
                    presentationSeconds += ConsumePresentationOnly(
                        policy,
                        new[] { ProductFeedbackKind.CpuTurn },
                        semanticCues);
                    applied = session.TryApplyCpuAction();
                }
                else
                {
                    Assert.Fail("Unexpected session state: " + session.State);
                    throw new AssertionException("Unexpected session state.");
                }

                Assert.That(applied, Is.True, session.FaultMessage);
            }

            Assert.That(session.State, Is.EqualTo(MatchSessionState.Finished),
                "Scenario did not finish for presentation speed " + speed + ".");
            GameResultPresentation completedResult = result ??
                throw new AssertionException("Finished session did not publish a result.");
            Assert.That(completedResult.Turns, Is.EqualTo(session.Snapshot.TurnCount));
            SessionArchive archive = session.Archive;
            return new SpeedScenario(
                speed,
                archive.Actions.Select(ActionSignature).ToArray(),
                SessionArchiveCodec.Encode(archive),
                semanticCues.ToArray(),
                session.Snapshot.TurnCount,
                completedResult.Winners.ToArray(),
                completedResult.Scores.ToArray(),
                completedResult.Reason,
                completedResult.Turns,
                presentationSeconds);
        }

        private static float ConsumePresentationOnly(ProductPresentationPolicy policy,
            IEnumerable<ProductFeedbackKind> sequence,
            ICollection<ProductFeedbackKind> semanticCues)
        {
            float seconds = 0f;
            foreach (ProductFeedbackKind cue in sequence)
            {
                semanticCues.Add(cue);
                seconds += policy.CueEnterSeconds + policy.CueHoldSeconds +
                    policy.CueExitSeconds;
            }
            return seconds;
        }

        private static string ActionSignature(SessionActionRecord record) =>
            string.Join("|", new[]
            {
                record.Actor.ToString(),
                record.Action.Kind,
                record.Action.Card?.ToString() ?? "-",
                record.Action.Target?.ToString() ?? "-",
                record.Action.Value ?? "-",
                record.TurnAfter.ToString(),
                record.CurrentPlayerAfter.ToString(),
                record.TerminalAfter.ToString()
            });

        private sealed class SpeedScenario
        {
            public ProductPresentationSpeed Speed { get; }
            public IReadOnlyList<string> ActionSignatures { get; }
            public byte[] EncodedArchive { get; }
            public IReadOnlyList<ProductFeedbackKind> SemanticCues { get; }
            public int TurnCount { get; }
            public IReadOnlyList<int> ResultWinners { get; }
            public IReadOnlyList<double> ResultScores { get; }
            public string ResultReason { get; }
            public int ResultTurns { get; }
            public float PresentationSeconds { get; }

            public SpeedScenario(ProductPresentationSpeed speed,
                IReadOnlyList<string> actionSignatures, byte[] encodedArchive,
                IReadOnlyList<ProductFeedbackKind> semanticCues, int turnCount,
                IReadOnlyList<int> resultWinners, IReadOnlyList<double> resultScores,
                string resultReason, int resultTurns, float presentationSeconds)
            {
                Speed = speed;
                ActionSignatures = actionSignatures;
                EncodedArchive = encodedArchive;
                SemanticCues = semanticCues;
                TurnCount = turnCount;
                ResultWinners = resultWinners;
                ResultScores = resultScores;
                ResultReason = resultReason;
                ResultTurns = resultTurns;
                PresentationSeconds = presentationSeconds;
            }
        }

        private static void ApplyCurrent(GameSessionController session)
        {
            bool applied;
            if (session.State == MatchSessionState.AwaitingHuman)
                applied = session.TryApplyHumanAction(session.Snapshot.Actions[0].Id);
            else if (session.State == MatchSessionState.WaitingForCpu)
                applied = session.TryApplyCpuAction();
            else
                throw new AssertionException("Unexpected session state: " + session.State);
            Assert.That(applied, Is.True, session.FaultMessage);
        }
    }
}
