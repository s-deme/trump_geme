#nullable enable

using System;
using System.Globalization;
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
        public static ResultViewModel Create(GameResultPresentation result, int humanPlayer = 0)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.Scores.Count != 2)
                throw new ArgumentException("Two-player result is required.", nameof(result));
            if (humanPlayer < 0 || humanPlayer >= result.Scores.Count)
                throw new ArgumentOutOfRangeException(nameof(humanPlayer));

            bool humanWon = result.Winners.Contains(humanPlayer);
            ProductResultOutcome outcome = result.Winners.Count == 0
                ? ProductResultOutcome.Draw
                : humanWon ? ProductResultOutcome.Win : ProductResultOutcome.Loss;
            string outcomeText = outcome == ProductResultOutcome.Draw
                ? "Draw"
                : outcome == ProductResultOutcome.Win ? "You win!" : "CPU wins";
            string scores = string.Join("  •  ", result.Scores.Select((score, player) =>
                (player == humanPlayer ? "You" : "CPU") + ": " +
                score.ToString("0.##", CultureInfo.InvariantCulture)));
            return new ResultViewModel(
                outcome,
                outcomeText + "\n" + scores + "\nReason: " + result.Reason +
                "  •  Turns: " + result.Turns);
        }
    }
}
