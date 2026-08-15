#nullable enable

using System;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Product
{
    public sealed class ResultViewModel
    {
        public string Summary { get; }

        public ResultViewModel(string summary)
        {
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
            string outcome = result.Winners.Count == 0
                ? "Draw"
                : humanWon ? "You win!" : "CPU wins";
            string scores = string.Join("  •  ", result.Scores.Select((score, player) =>
                (player == humanPlayer ? "You" : "CPU") + ": " +
                score.ToString("0.##", CultureInfo.InvariantCulture)));
            return new ResultViewModel(
                outcome + "\n" + scores + "\nReason: " + result.Reason +
                "  •  Turns: " + result.Turns);
        }
    }
}
