#nullable enable

using System;
using System.Collections.Generic;
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
            GameResultPresentation? result = null, IProductText? text = null)
        {
            if (presentation != null && presentation.GameId != "crazy_eights")
                throw new ArgumentException("Crazy Eights presentation is required.",
                    nameof(presentation));
            if (result == null) result = presentation?.Result;
            IProductText productText = text ?? ProductTextCatalog.English;

            HowToPlayPage[] pages =
            {
                new HowToPlayPage(
                    HowToPlayPageId.Objective,
                    "rules.crazy_eights.objective",
                    productText.Get("rules.crazy_eights.objective.title"),
                    productText.Get("rules.crazy_eights.objective")),
                new HowToPlayPage(
                    HowToPlayPageId.LegalPlay,
                    "rules.crazy_eights.legal_play",
                    productText.Get("rules.crazy_eights.legal_play.title"),
                    productText.Get("rules.crazy_eights.legal_play")),
                new HowToPlayPage(
                    HowToPlayPageId.Draw,
                    "rules.crazy_eights.draw",
                    productText.Get("rules.crazy_eights.draw.title"),
                    productText.Get("rules.crazy_eights.draw")),
                new HowToPlayPage(
                    HowToPlayPageId.WildSuit,
                    "rules.crazy_eights.wild_suit",
                    productText.Get("rules.crazy_eights.wild_suit.title"),
                    productText.Get("rules.crazy_eights.wild_suit")),
                new HowToPlayPage(
                    HowToPlayPageId.Result,
                    "rules.crazy_eights.result",
                    productText.Get("rules.crazy_eights.result.title"),
                    ResultBody(result, productText))
            };

            HowToPlayPageId initialPage = InitialPage(presentation, result);
            int initialIndex = Array.FindIndex(pages, page => page.Id == initialPage);
            return new HowToPlayViewModel(pages, initialIndex,
                Context(presentation, result, productText));
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
            GameResultPresentation? result, IProductText text)
        {
            if (result != null)
                return text.Get("rules.context_result", ResultReason(result.Reason, text));
            if (presentation == null) return text.Get("rules.context_read_only");
            string turn = presentation.CurrentPlayer == presentation.Viewer
                ? text.Get("rules.turn_human")
                : text.Get("rules.turn_cpu");
            string calledSuit = presentation.Fields
                .Where(field => field.Id == "called_suit")
                .Where(field => field.Value.SuitValue.HasValue)
                .Select(field => SuitName(field.Value.SuitValue!.Value, text))
                .FirstOrDefault() ?? text.Get("rules.called_none");
            string phase = presentation.IsTerminal
                ? text.Get("rules.phase_finished")
                : presentation.Phase == "choose_starter_suit"
                    ? text.Get("rules.phase_choose_starter")
                    : text.Get("rules.phase_play");
            return text.Get("rules.context_match", turn, phase, calledSuit);
        }

        private static string ResultBody(GameResultPresentation? result, IProductText text)
        {
            string rules = text.Get("rules.crazy_eights.result");
            if (result == null) return rules;
            if (result.Scores.Count != 2)
                throw new ArgumentException("Two-player result is required.", nameof(result));
            string outcome = result.Winners.Contains(0)
                ? text.Get("rules.outcome_you")
                : result.Winners.Count == 0
                    ? text.Get("rules.outcome_draw")
                    : text.Get("rules.outcome_cpu");
            return rules + "\n\n" + text.Get("rules.result_current", outcome,
                ResultReason(result.Reason, text), result.Scores[0], result.Scores[1],
                result.Turns);
        }

        private static string ResultReason(string reason, IProductText text) =>
            string.Equals(reason, "empty hand", StringComparison.Ordinal)
                ? text.Get("rules.reason_empty_hand")
                : text.Get("result.reason_unknown");

        private static string SuitName(Suit suit, IProductText text) => suit switch
        {
            Suit.Clubs => text.Get("suit.clubs"),
            Suit.Diamonds => text.Get("suit.diamonds"),
            Suit.Hearts => text.Get("suit.hearts"),
            Suit.Spades => text.Get("suit.spades"),
            _ => throw new ArgumentOutOfRangeException(nameof(suit))
        };
    }
}
