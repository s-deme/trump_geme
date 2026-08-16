#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Product
{
    public enum TutorialLesson
    {
        Intro = 1,
        MatchingPlay = 2,
        Draw = 3,
        WildSuit = 4,
        GuidedPlay = 5,
        Win = 6
    }

    public enum TutorialSessionState
    {
        NotStarted,
        AwaitingIntro,
        AwaitingHuman,
        WaitingForCpu,
        AwaitingResultConfirmation,
        Finished,
        Cancelled,
        Faulted
    }

    public sealed class TutorialTraceEntry
    {
        public int Actor { get; }
        public TrumpLab.Action Action { get; }
        public TutorialLesson? Lesson { get; }

        public TutorialTraceEntry(
            int actor, TrumpLab.Action action, TutorialLesson? lesson = null)
        {
            if (actor < 0 || actor > 1) throw new ArgumentOutOfRangeException(nameof(actor));
            if (string.IsNullOrWhiteSpace(action.Kind))
                throw new ArgumentException("Tutorial action kind cannot be empty.", nameof(action));
            Actor = actor;
            Action = action;
            Lesson = lesson;
        }
    }

    public sealed class TutorialDefinition
    {
        private static readonly TutorialDefinition BasicDefinition = CreateBasic();

        public string Id { get; }
        public int Version { get; }
        public long Seed { get; }
        public int WildRank { get; }
        public int Difficulty { get; }
        public IReadOnlyList<TutorialTraceEntry> Trace { get; }

        public static TutorialDefinition CrazyEightsBasic => BasicDefinition;

        private TutorialDefinition(string id, int version, long seed, int wildRank,
            int difficulty, IEnumerable<TutorialTraceEntry> trace)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Tutorial ID cannot be empty.", nameof(id));
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (wildRank < 1 || wildRank > 13)
                throw new ArgumentOutOfRangeException(nameof(wildRank));
            BuiltInGames.Registry.ValidateCpuDifficulty("crazy_eights", difficulty);
            TutorialTraceEntry[] copied = trace?.ToArray() ??
                throw new ArgumentNullException(nameof(trace));
            if (copied.Length == 0 || copied.Any(entry => entry == null))
                throw new ArgumentException("Tutorial trace cannot be empty.", nameof(trace));
            TutorialLesson[] lessons = copied.Where(entry => entry.Lesson.HasValue)
                .Select(entry => entry.Lesson!.Value).Distinct().ToArray();
            if (!lessons.OrderBy(lesson => lesson).SequenceEqual(new[]
                {
                    TutorialLesson.MatchingPlay,
                    TutorialLesson.Draw,
                    TutorialLesson.WildSuit,
                    TutorialLesson.GuidedPlay,
                    TutorialLesson.Win
                }))
                throw new ArgumentException(
                    "Tutorial trace must cover every action lesson.", nameof(trace));

            Id = id;
            Version = version;
            Seed = seed;
            WildRank = wildRank;
            Difficulty = difficulty;
            Trace = Array.AsReadOnly(copied);
        }

        private static TutorialDefinition CreateBasic() => new TutorialDefinition(
            "crazy_eights_basic_v1",
            version: 1,
            seed: 29,
            wildRank: 8,
            difficulty: CpuDifficulties.Standard,
            trace: new[]
            {
                Human("play", "3H", TutorialLesson.MatchingPlay),
                Cpu("play", "8H", value: "S"),
                Human("draw", lesson: TutorialLesson.Draw),
                Cpu("play", "2S"),
                Human("play", "8C", TutorialLesson.WildSuit, value: "C"),
                Cpu("play", "KC"),
                Human("play", "5C", TutorialLesson.GuidedPlay),
                Cpu("play", "5S"),
                Human("play", "JS", TutorialLesson.GuidedPlay),
                Cpu("play", "7S"),
                Human("play", "9S", TutorialLesson.GuidedPlay),
                Cpu("play_last_card", "KS"),
                Human("play", "8S", TutorialLesson.GuidedPlay, value: "H"),
                Cpu("draw"),
                Human("play_last_card", "4H", TutorialLesson.GuidedPlay),
                Cpu("draw"),
                Human("play", "2H", TutorialLesson.Win)
            });

        private static TutorialTraceEntry Human(string kind, string? card = null,
            TutorialLesson? lesson = null, string? value = null) =>
            Entry(0, kind, card, lesson, value);

        private static TutorialTraceEntry Cpu(
            string kind, string? card = null, string? value = null) =>
            Entry(1, kind, card, lesson: null, value: value);

        private static TutorialTraceEntry Entry(int actor, string kind, string? card,
            TutorialLesson? lesson, string? value) => new TutorialTraceEntry(
                actor,
                new TrumpLab.Action(kind,
                    card == null ? (Card?)null : Card.Parse(card),
                    value: value),
                lesson);
    }

    public sealed class TutorialSessionController
    {
        private const int HumanPlayer = 0;
        private readonly TutorialDefinition definition;
        private readonly SessionRecorder recorder;
        private readonly IGamePresentationProvider provider;
        private GamePresentation? snapshot;
        private int traceIndex;

        public TutorialDefinition Definition => definition;
        public TutorialSessionState State { get; private set; } =
            TutorialSessionState.NotStarted;
        public TutorialLesson Lesson { get; private set; } = TutorialLesson.Intro;
        public int StepNumber => (int)Lesson;
        public int TotalSteps => 6;
        public int AppliedActions => traceIndex;
        public string InstructionKey => "tutorial." + LessonKey(Lesson);
        public string? FeedbackKey { get; private set; }
        public string? ExpectedActionId { get; private set; }
        public string? FaultMessage { get; private set; }
        public IGame Game => recorder.Game;
        public SessionArchive Archive => recorder.Archive;
        public GamePresentation Snapshot => snapshot ?? throw new InvalidOperationException(
            "Tutorial has not produced its first snapshot.");

        public event System.Action? Changed;
        public event System.Action? Completed;
        public event System.Action<string>? Faulted;

        public TutorialSessionController(TutorialDefinition? configuredDefinition = null)
        {
            definition = configuredDefinition ?? TutorialDefinition.CrazyEightsBasic;
            var configuration = new SessionConfiguration(
                "crazy_eights",
                players: 2,
                seed: definition.Seed,
                difficulty: definition.Difficulty,
                humanPlayers: new[] { HumanPlayer },
                options: new Dictionary<string, string>
                {
                    ["wild_rank"] = definition.WildRank.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)
                });
            recorder = new SessionRecorder(configuration);
            provider = recorder.Game as IGamePresentationProvider ??
                throw new InvalidOperationException(
                    "Tutorial game must provide structured presentation.");
        }

        public void Begin()
        {
            if (State != TutorialSessionState.NotStarted)
                throw new InvalidOperationException("Tutorial can only begin once.");
            snapshot = provider.Present(HumanPlayer);
            State = TutorialSessionState.AwaitingIntro;
            Changed?.Invoke();
        }

        public bool AcknowledgeIntro()
        {
            if (State != TutorialSessionState.AwaitingIntro) return false;
            FeedbackKey = null;
            return RefreshFromTrace();
        }

        public bool TryApplyHumanAction(string actionId)
        {
            if (State != TutorialSessionState.AwaitingHuman ||
                string.IsNullOrWhiteSpace(actionId)) return false;
            ActionPresentation? selected = Snapshot.Actions.SingleOrDefault(action =>
                string.Equals(action.Id, actionId, StringComparison.Ordinal));
            if (selected == null)
            {
                FeedbackKey = "tutorial.feedback.stale_action";
                Changed?.Invoke();
                return false;
            }
            TutorialTraceEntry expected = definition.Trace[traceIndex];
            if (selected.Action != expected.Action)
            {
                FeedbackKey = "tutorial.feedback.expected_" + LessonKey(Lesson);
                Changed?.Invoke();
                return false;
            }

            try
            {
                recorder.ApplyHumanAction(HumanPlayer, selected.Action);
                traceIndex++;
                FeedbackKey = null;
                return RefreshFromTrace();
            }
            catch (Exception exception)
            {
                return Fail(exception);
            }
        }

        public bool TryApplyCpuAction()
        {
            if (State != TutorialSessionState.WaitingForCpu) return false;
            try
            {
                TutorialTraceEntry expected = definition.Trace[traceIndex];
                TrumpLab.Action selected = recorder.ApplyCpuAction();
                if (selected != expected.Action)
                    throw new InvalidOperationException(
                        "Tutorial CPU trace diverged at action " + traceIndex + ".");
                traceIndex++;
                FeedbackKey = null;
                return RefreshFromTrace();
            }
            catch (Exception exception)
            {
                return Fail(exception);
            }
        }

        public bool ConfirmResult()
        {
            if (State != TutorialSessionState.AwaitingResultConfirmation) return false;
            State = TutorialSessionState.Finished;
            Changed?.Invoke();
            Completed?.Invoke();
            return true;
        }

        public bool Cancel()
        {
            if (State == TutorialSessionState.Finished ||
                State == TutorialSessionState.Cancelled ||
                State == TutorialSessionState.Faulted) return false;
            State = TutorialSessionState.Cancelled;
            ExpectedActionId = null;
            Changed?.Invoke();
            return true;
        }

        private bool RefreshFromTrace()
        {
            try
            {
                snapshot = provider.Present(HumanPlayer);
                ExpectedActionId = null;
                if (traceIndex == definition.Trace.Count)
                {
                    if (!Game.IsTerminal)
                        throw new InvalidOperationException(
                            "Tutorial trace ended before the game result.");
                    GameResult result = Game.Result();
                    if (!result.Winners.SequenceEqual(new[] { HumanPlayer }) ||
                        result.Reason != "empty hand")
                        throw new InvalidOperationException(
                            "Tutorial trace ended with an unexpected result.");
                    Lesson = TutorialLesson.Win;
                    State = TutorialSessionState.AwaitingResultConfirmation;
                    Changed?.Invoke();
                    return true;
                }
                if (Game.IsTerminal)
                    throw new InvalidOperationException(
                        "Tutorial game ended before the trace.");

                TutorialTraceEntry expected = definition.Trace[traceIndex];
                if (Game.CurrentPlayer != expected.Actor)
                    throw new InvalidOperationException(
                        "Tutorial actor diverged at action " + traceIndex + ".");
                Lesson = NextHumanLesson(traceIndex);
                if (expected.Actor == HumanPlayer)
                {
                    ActionPresentation[] matching = Snapshot.Actions
                        .Where(action => action.Action == expected.Action).ToArray();
                    if (matching.Length != 1)
                        throw new InvalidOperationException(
                            "Tutorial expected action is not uniquely legal at action " +
                            traceIndex + ".");
                    ExpectedActionId = matching[0].Id;
                    State = TutorialSessionState.AwaitingHuman;
                }
                else
                {
                    State = TutorialSessionState.WaitingForCpu;
                }
                Changed?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception);
            }
        }

        private TutorialLesson NextHumanLesson(int startIndex)
        {
            for (int index = startIndex; index < definition.Trace.Count; index++)
            {
                TutorialTraceEntry entry = definition.Trace[index];
                if (entry.Actor == HumanPlayer && entry.Lesson.HasValue)
                    return entry.Lesson.Value;
            }
            return TutorialLesson.Win;
        }

        private bool Fail(Exception exception)
        {
            if (State == TutorialSessionState.Faulted) return false;
            State = TutorialSessionState.Faulted;
            ExpectedActionId = null;
            FaultMessage = exception.GetType().Name + ": " + exception.Message;
            Changed?.Invoke();
            Faulted?.Invoke(FaultMessage);
            return false;
        }

        private static string LessonKey(TutorialLesson lesson)
        {
            switch (lesson)
            {
                case TutorialLesson.Intro: return "intro";
                case TutorialLesson.MatchingPlay: return "matching_play";
                case TutorialLesson.Draw: return "draw";
                case TutorialLesson.WildSuit: return "wild_suit";
                case TutorialLesson.GuidedPlay: return "guided_play";
                case TutorialLesson.Win: return "win";
                default: throw new ArgumentOutOfRangeException(nameof(lesson));
            }
        }
    }
}
