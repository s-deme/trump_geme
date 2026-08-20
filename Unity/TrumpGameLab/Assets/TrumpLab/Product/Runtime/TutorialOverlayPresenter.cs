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
        public static TutorialOverlayViewModel Create(TutorialSessionController tutorial,
            IProductText? text = null)
        {
            if (tutorial == null) throw new ArgumentNullException(nameof(tutorial));
            IProductText productText = text ?? ProductTextCatalog.English;
            string instructionKey = tutorial.InstructionKey;
            string heading = productText.Get(instructionKey + ".heading");
            string instruction = productText.Get(instructionKey);

            string guidance = Guidance(tutorial, productText);
            bool canContinue = tutorial.State == TutorialSessionState.AwaitingIntro ||
                tutorial.State == TutorialSessionState.AwaitingResultConfirmation;
            string continueLabel = tutorial.State == TutorialSessionState.AwaitingResultConfirmation
                ? productText.Get("tutorial.continue_finish")
                : productText.Get("tutorial.continue_start");
            return new TutorialOverlayViewModel(
                instructionKey,
                productText.Get("tutorial.progress", tutorial.StepNumber, tutorial.TotalSteps),
                heading,
                instruction,
                guidance,
                tutorial.ExpectedActionId,
                canContinue,
                continueLabel);
        }

        private static string Guidance(TutorialSessionController tutorial, IProductText text)
        {
            if (tutorial.FeedbackKey == "tutorial.feedback.stale_action")
                return text.Get("tutorial.guidance_stale");
            if (tutorial.FeedbackKey != null &&
                tutorial.FeedbackKey.StartsWith("tutorial.feedback.expected_",
                    StringComparison.Ordinal))
                return text.Get("tutorial.guidance_expected");
            if (tutorial.State == TutorialSessionState.AwaitingResultConfirmation)
                return CrazyEightsResultPresenter.Create(tutorial.Snapshot.Result ??
                    throw new InvalidOperationException("Tutorial result is missing."),
                    text: text).Summary;
            if (tutorial.State == TutorialSessionState.WaitingForCpu)
                return text.Get("tutorial.guidance_cpu");
            if (tutorial.ExpectedActionId != null)
            {
                MatchViewModel match = CrazyEightsMatchPresenter.Create(
                    tutorial.Snapshot, inputEnabled: true, text: text);
                return match.Actions.Single(action => action.Id == tutorial.ExpectedActionId).Reason;
            }
            return text.Get("tutorial.guidance_default");
        }
    }
}
