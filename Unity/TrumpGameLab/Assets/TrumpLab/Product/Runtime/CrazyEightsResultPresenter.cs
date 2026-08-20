#nullable enable

using System;
using System.Linq;

namespace TrumpLab.Product
{
    public enum ProductResultOutcome
    {
        Win,
        Loss,
        Draw
    }

    public sealed class ResultViewModel
    {
        public ProductResultOutcome Outcome { get; }
        public string Summary { get; }

        public ResultViewModel(ProductResultOutcome outcome, string summary)
        {
            if (!Enum.IsDefined(typeof(ProductResultOutcome), outcome))
                throw new ArgumentOutOfRangeException(nameof(outcome));
            Outcome = outcome;
            Summary = string.IsNullOrWhiteSpace(summary)
                ? throw new ArgumentException("Result summary cannot be empty.", nameof(summary))
                : summary;
        }
    }

    public static class CrazyEightsResultPresenter
    {
        public static ResultViewModel Create(GameResultPresentation result, int humanPlayer = 0,
            IProductText? text = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.Scores.Count != 2)
                throw new ArgumentException("Two-player result is required.", nameof(result));
            if (humanPlayer < 0 || humanPlayer >= result.Scores.Count)
                throw new ArgumentOutOfRangeException(nameof(humanPlayer));

            bool humanWon = result.Winners.Contains(humanPlayer);
            IProductText productText = text ?? ProductTextCatalog.English;
            ProductResultOutcome outcome = result.Winners.Count == 0
                ? ProductResultOutcome.Draw
                : humanWon ? ProductResultOutcome.Win : ProductResultOutcome.Loss;
            string outcomeText = outcome == ProductResultOutcome.Draw
                ? productText.Get("result.outcome_draw")
                : outcome == ProductResultOutcome.Win
                    ? productText.Get("result.outcome_win")
                    : productText.Get("result.outcome_loss");
            string scores = productText.Get("result.scores",
                productText.Get("result.score_you", result.Scores[humanPlayer]),
                productText.Get("result.score_cpu", result.Scores
                    .Where((_, player) => player != humanPlayer).Single()));
            string reason = string.Equals(result.Reason, "empty hand",
                StringComparison.Ordinal)
                ? productText.Get("result.reason_empty_hand")
                : productText.Get("result.reason_unknown");
            return new ResultViewModel(
                outcome,
                productText.Get("result.summary", outcomeText, scores, reason, result.Turns));
        }
    }
}
