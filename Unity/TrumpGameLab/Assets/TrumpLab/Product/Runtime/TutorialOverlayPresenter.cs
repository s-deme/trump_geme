#nullable enable

using System;
using System.Linq;

namespace TrumpLab.Product
{
    public sealed class TutorialOverlayViewModel
    {
        public string InstructionKey { get; }
        public string Progress { get; }
        public string Heading { get; }
        public string Instruction { get; }
        public string Guidance { get; }
        public string? ExpectedActionId { get; }
        public bool ContinueVisible { get; }
        public string ContinueLabel { get; }

        public TutorialOverlayViewModel(string instructionKey, string progress,
            string heading, string instruction, string guidance, string? expectedActionId,
            bool continueVisible, string continueLabel)
        {
            InstructionKey = Required(instructionKey, nameof(instructionKey));
            Progress = Required(progress, nameof(progress));
            Heading = Required(heading, nameof(heading));
            Instruction = Required(instruction, nameof(instruction));
            Guidance = Required(guidance, nameof(guidance));
            ExpectedActionId = expectedActionId;
            ContinueVisible = continueVisible;
            ContinueLabel = Required(continueLabel, nameof(continueLabel));
        }

        private static string Required(string value, string name) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Tutorial overlay text cannot be empty.", name)
                : value;
    }

    public static class TutorialOverlayPresenter
    {
        public static TutorialOverlayViewModel Create(TutorialSessionController tutorial)
        {
            if (tutorial == null) throw new ArgumentNullException(nameof(tutorial));
            string heading;
            string instruction;
            switch (tutorial.Lesson)
            {
                case TutorialLesson.Intro:
                    heading = "Meet the table";
                    instruction = "Find your hand, the CPU card count, stock, discard, and turn label.";
                    break;
                case TutorialLesson.MatchingPlay:
                    heading = "Match the discard";
                    instruction = "Choose the highlighted non-wild card that matches suit or rank.";
                    break;
                case TutorialLesson.Draw:
                    heading = "Draw and end the turn";
                    instruction = "Choose the highlighted Draw action. Drawing is allowed even with a play.";
                    break;
                case TutorialLesson.WildSuit:
                    heading = "Play an 8 and call a suit";
                    instruction = "Choose the highlighted 8 action. Its label includes the called suit.";
                    break;
                case TutorialLesson.GuidedPlay:
                    heading = "Read the legal actions";
                    instruction = "Follow the highlighted action and its reason to finish the hand.";
                    break;
                case TutorialLesson.Win:
                    heading = "You emptied your hand";
                    instruction = "Review the winner, score, reason, and turn count, then finish.";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tutorial));
            }

            string guidance = Guidance(tutorial);
            bool canContinue = tutorial.State == TutorialSessionState.AwaitingIntro ||
                tutorial.State == TutorialSessionState.AwaitingResultConfirmation;
            string continueLabel = tutorial.State == TutorialSessionState.AwaitingResultConfirmation
                ? "Finish tutorial"
                : "Start guided match";
            return new TutorialOverlayViewModel(
                tutorial.InstructionKey,
                "Step " + tutorial.StepNumber + " / " + tutorial.TotalSteps,
                heading,
                instruction,
                guidance,
                tutorial.ExpectedActionId,
                canContinue,
                continueLabel);
        }

        private static string Guidance(TutorialSessionController tutorial)
        {
            if (tutorial.FeedbackKey == "tutorial.feedback.stale_action")
                return "That action belongs to an older step. Use the currently highlighted action.";
            if (tutorial.FeedbackKey != null &&
                tutorial.FeedbackKey.StartsWith("tutorial.feedback.expected_",
                    StringComparison.Ordinal))
                return "That action is legal, but this step practices the highlighted action.";
            if (tutorial.State == TutorialSessionState.AwaitingResultConfirmation)
                return CrazyEightsResultPresenter.Create(tutorial.Snapshot.Result ??
                    throw new InvalidOperationException("Tutorial result is missing.")).Summary;
            if (tutorial.State == TutorialSessionState.WaitingForCpu)
                return "The CPU uses only its observation and the public table. Please wait.";
            if (tutorial.ExpectedActionId != null)
            {
                MatchViewModel match = CrazyEightsMatchPresenter.Create(
                    tutorial.Snapshot, inputEnabled: true);
                return match.Actions.Single(action => action.Id == tutorial.ExpectedActionId).Reason;
            }
            return "This guide uses a normal Crazy Eights game with a fixed seed.";
        }
    }
}
