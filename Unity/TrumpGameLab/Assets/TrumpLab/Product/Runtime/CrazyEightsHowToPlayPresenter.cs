#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Product
{
    public enum HowToPlayPageId
    {
        Objective,
        LegalPlay,
        Draw,
        WildSuit,
        Result
    }

    public sealed class HowToPlayPage
    {
        public HowToPlayPageId Id { get; }
        public string TextKey { get; }
        public string Title { get; }
        public string Body { get; }

        public HowToPlayPage(HowToPlayPageId id, string textKey, string title, string body)
        {
            Id = id;
            TextKey = Required(textKey, nameof(textKey));
            Title = Required(title, nameof(title));
            Body = Required(body, nameof(body));
        }

        private static string Required(string value, string name) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("How-to-play text cannot be empty.", name)
                : value;
    }

    public sealed class HowToPlayViewModel
    {
        public IReadOnlyList<HowToPlayPage> Pages { get; }
        public int InitialPageIndex { get; }
        public string Context { get; }

        public HowToPlayViewModel(IEnumerable<HowToPlayPage> pages,
            int initialPageIndex, string context)
        {
            HowToPlayPage[] copied = pages?.ToArray() ??
                throw new ArgumentNullException(nameof(pages));
            if (copied.Length == 0 || copied.Any(page => page == null))
                throw new ArgumentException("How-to-play pages cannot be empty.", nameof(pages));
            if (copied.Select(page => page.Id).Distinct().Count() != copied.Length ||
                copied.Select(page => page.TextKey).Distinct(StringComparer.Ordinal).Count() !=
                    copied.Length)
                throw new ArgumentException("How-to-play page IDs and keys must be unique.",
                    nameof(pages));
            if (initialPageIndex < 0 || initialPageIndex >= copied.Length)
                throw new ArgumentOutOfRangeException(nameof(initialPageIndex));
            Pages = Array.AsReadOnly(copied);
            InitialPageIndex = initialPageIndex;
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }
    }

    public static class CrazyEightsHowToPlayPresenter
    {
        public static HowToPlayViewModel Create(GamePresentation? presentation = null,
            GameResultPresentation? result = null)
        {
            if (presentation != null && presentation.GameId != "crazy_eights")
                throw new ArgumentException("Crazy Eights presentation is required.",
                    nameof(presentation));
            if (result == null) result = presentation?.Result;

            HowToPlayPage[] pages =
            {
                new HowToPlayPage(
                    HowToPlayPageId.Objective,
                    "rules.crazy_eights.objective",
                    "Objective",
                    "Be the first player to empty your hand. The screen shows your cards, " +
                    "the CPU hand count, the stock, the discard top, and whose turn it is."),
                new HowToPlayPage(
                    HowToPlayPageId.LegalPlay,
                    "rules.crazy_eights.legal_play",
                    "Discard and legal plays",
                    "Play a card that matches the discard top by suit or rank. Every action " +
                    "button shown by the game is legal; its label explains why it can be used."),
                new HowToPlayPage(
                    HowToPlayPageId.Draw,
                    "rules.crazy_eights.draw",
                    "Drawing",
                    "You may draw even when you can play. Draw takes one card when available " +
                    "and ends your turn. Pass appears only when no play or draw is possible."),
                new HowToPlayPage(
                    HowToPlayPageId.WildSuit,
                    "rules.crazy_eights.wild_suit",
                    "Eights and the called suit",
                    "An 8 is wild. Its action includes the suit you call. The called suit, " +
                    "shown beside the discard, controls suit matching until a non-wild card is played."),
                new HowToPlayPage(
                    HowToPlayPageId.Result,
                    "rules.crazy_eights.result",
                    "Winning and score details",
                    ResultBody(result))
            };

            HowToPlayPageId initialPage = InitialPage(presentation, result);
            int initialIndex = Array.FindIndex(pages, page => page.Id == initialPage);
            return new HowToPlayViewModel(pages, initialIndex, Context(presentation, result));
        }

        private static HowToPlayPageId InitialPage(GamePresentation? presentation,
            GameResultPresentation? result)
        {
            if (result != null || presentation?.IsTerminal == true)
                return HowToPlayPageId.Result;
            if (presentation == null) return HowToPlayPageId.Objective;
            if (presentation.Phase == "choose_starter_suit")
                return HowToPlayPageId.WildSuit;
            if (presentation.Actions.Count > 0 &&
                presentation.Actions.All(action => action.Action.Kind == "draw" ||
                    action.Action.Kind == "pass"))
                return HowToPlayPageId.Draw;
            return HowToPlayPageId.LegalPlay;
        }

        private static string Context(GamePresentation? presentation,
            GameResultPresentation? result)
        {
            if (result != null)
                return "Result details · Reason: " + ResultReason(result.Reason);
            if (presentation == null) return "Crazy Eights rules · Read-only guide";
            string turn = presentation.CurrentPlayer == presentation.Viewer
                ? "Your turn"
                : "CPU turn";
            string calledSuit = presentation.Fields
                .Where(field => field.Id == "called_suit")
                .Where(field => field.Value.SuitValue.HasValue)
                .Select(field => Card.SuitCode(field.Value.SuitValue!.Value))
                .FirstOrDefault() ?? "none";
            return turn + " · Phase: " + presentation.Phase +
                " · Called suit: " + calledSuit;
        }

        private static string ResultBody(GameResultPresentation? result)
        {
            const string rules = "Play your final card to win. The winner receives the total " +
                "penalty left in the opponent's hand; the opponent receives the negative value. " +
                "Eights are 50, face cards are 10, and other cards use their rank.";
            if (result == null) return rules;
            if (result.Scores.Count != 2)
                throw new ArgumentException("Two-player result is required.", nameof(result));
            string outcome = result.Winners.Contains(0) ? "You win" :
                result.Winners.Count == 0 ? "Draw" : "CPU wins";
            return rules + "\n\nCurrent result\n" + outcome + " · Reason: " +
                ResultReason(result.Reason) + "\nYou: " + Score(result.Scores[0]) +
                " · CPU: " + Score(result.Scores[1]) + " · Turns: " + result.Turns;
        }

        private static string ResultReason(string reason) => reason == "empty hand"
            ? "a player emptied their hand"
            : reason;

        private static string Score(double value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
