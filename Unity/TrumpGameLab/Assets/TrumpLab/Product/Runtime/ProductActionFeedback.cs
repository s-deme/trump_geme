#nullable enable

using System;
using System.Collections.Generic;

namespace TrumpLab.Product
{
    public static class ProductActionFeedback
    {
        private static readonly IReadOnlyList<ProductFeedbackKind> SubmitSequence =
            new[] { ProductFeedbackKind.Submit };
        private static readonly IReadOnlyList<ProductFeedbackKind> DrawSequence =
            new[] { ProductFeedbackKind.Draw };
        private static readonly IReadOnlyList<ProductFeedbackKind> CardPlaySequence =
            new[] { ProductFeedbackKind.CardPlay };
        private static readonly IReadOnlyList<ProductFeedbackKind> WildPlaySequence =
            new[] { ProductFeedbackKind.CardPlay, ProductFeedbackKind.WildSuit };
        private static readonly IReadOnlyList<ProductFeedbackKind> WildSuitSequence =
            new[] { ProductFeedbackKind.WildSuit };

        public static ProductFeedbackKind Classify(SessionActionRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (record.Actor != 0) return ProductFeedbackKind.CpuTurn;

            IReadOnlyList<ProductFeedbackKind> sequence = ClassifyActionSequence(record);
            return sequence[sequence.Count - 1];
        }

        public static IReadOnlyList<ProductFeedbackKind> ClassifyActionSequence(
            SessionActionRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            TrumpLab.Action action = record.Action;
            switch (action.Kind)
            {
                case "draw":
                    return DrawSequence;
                case "choose_starter_suit":
                    return WildSuitSequence;
                case "play":
                case "play_last_card":
                    return action.Value != null
                        ? WildPlaySequence
                        : CardPlaySequence;
                case "pass":
                    return SubmitSequence;
                default:
                    throw new ArgumentOutOfRangeException(nameof(record), action.Kind,
                        "The session action kind has no product feedback classification.");
            }
        }
    }
}
