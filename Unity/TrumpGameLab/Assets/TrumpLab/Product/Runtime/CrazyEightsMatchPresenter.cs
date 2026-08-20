#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab.Product
{
    public sealed class MatchActionViewModel
    {
        public string Id { get; }
        public string Label { get; }
        public string Reason { get; }

        public MatchActionViewModel(string id, string label, string reason)
        {
            Id = Required(id, nameof(id));
            Label = Required(label, nameof(label));
            Reason = Required(reason, nameof(reason));
        }

        private static string Required(string value, string parameterName) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Action display value cannot be empty.", parameterName)
                : value;
    }

    public sealed class MatchViewModel
    {
        public string Status { get; }
        public string OpponentHand { get; }
        public string Stock { get; }
        public string Discard { get; }
        public string HumanHand { get; }
        public string ActionSummary { get; }
        public string ContextHelp { get; }
        public IReadOnlyList<MatchActionViewModel> Actions { get; }
        public bool InputEnabled { get; }

        public MatchViewModel(string status, string opponentHand, string stock,
            string discard, string humanHand, string actionSummary,
            string contextHelp, IEnumerable<MatchActionViewModel> actions,
            bool inputEnabled)
        {
            Status = Required(status, nameof(status));
            OpponentHand = Required(opponentHand, nameof(opponentHand));
            Stock = Required(stock, nameof(stock));
            Discard = Required(discard, nameof(discard));
            HumanHand = Required(humanHand, nameof(humanHand));
            ActionSummary = Required(actionSummary, nameof(actionSummary));
            ContextHelp = Required(contextHelp, nameof(contextHelp));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            Actions = Array.AsReadOnly(actions.ToArray());
            InputEnabled = inputEnabled;
        }

        private static string Required(string value, string parameterName) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Display value cannot be empty.", parameterName)
                : value;
    }

    public static class CrazyEightsMatchPresenter
    {
        public static MatchViewModel Create(GamePresentation presentation, bool inputEnabled,
            IProductText? text = null)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (presentation.GameId != "crazy_eights")
                throw new ArgumentException("Crazy Eights presentation is required.", nameof(presentation));
            if (presentation.Players.Count != 2)
                throw new ArgumentException("The product vertical slice requires two players.", nameof(presentation));

            CardZonePresentation humanHand = Hand(presentation, presentation.Viewer);
            int opponent = presentation.Players.Single(player => !player.IsViewer).PlayerIndex;
            CardZonePresentation opponentHand = Hand(presentation, opponent);
            CardZonePresentation stock = Zone(presentation, "stock");
            CardZonePresentation discard = Zone(presentation, "discard");

            RequireVisible(humanHand, "viewer hand");
            RequireHidden(opponentHand, CardZoneVisibility.FaceDown, "opponent hand");
            RequireHidden(stock, CardZoneVisibility.CountOnly, "stock");
            RequireVisible(discard, "discard");
            if (discard.Cards.Count == 0)
                throw new ArgumentException("Discard must expose its top card.", nameof(presentation));

            GameFieldPresentation? calledSuit = presentation.Fields.SingleOrDefault(field =>
                field.Id == "called_suit");
            if (calledSuit != null && calledSuit.Value.Kind != PresentationValueKind.Suit)
                throw new ArgumentException("called_suit must be a Suit value.", nameof(presentation));

            IProductText productText = text ?? ProductTextCatalog.English;
            bool humanTurn = presentation.CurrentPlayer == presentation.Viewer;
            string status = presentation.IsTerminal
                ? productText.Get("match.status_finished", presentation.TurnCount)
                : presentation.Phase == "choose_starter_suit"
                    ? productText.Get(humanTurn
                            ? "match.status_choose_human"
                            : "match.status_choose_cpu",
                        presentation.TurnCount)
                    : productText.Get(humanTurn
                            ? "match.status_human"
                            : "match.status_cpu",
                        presentation.TurnCount);

            string discardCard = CardLabel(discard.Cards[discard.Cards.Count - 1], productText);
            string discardText = calledSuit?.Value.SuitValue is Suit suit
                ? productText.Get("match.discard_called", discardCard,
                    SuitName(suit, productText))
                : productText.Get("match.discard", discardCard);
            string opponentText = productText.Get("match.opponent_hand", string.Join(" ",
                Enumerable.Repeat(productText.Get("card.hidden"), opponentHand.Count)));
            string humanText = productText.Get("match.human_hand", string.Join(" ",
                humanHand.Cards.Select(card => CardLabel(card, productText))));
            MatchActionViewModel[] actions = presentation.Actions
                .Select(action => new MatchActionViewModel(
                    action.Id,
                    ActionLabel(action, productText),
                    ActionReason(action, humanHand, discard.Cards[discard.Cards.Count - 1],
                        calledSuit?.Value.SuitValue, productText)))
                .ToArray();
            bool canAct = inputEnabled && humanTurn && !presentation.IsTerminal;

            return new MatchViewModel(
                status,
                opponentText,
                productText.Get("match.stock", stock.Count),
                discardText,
                humanText,
                actions.Length == 0
                    ? productText.Get("match.action_summary_locked")
                    : actions.Length == 1
                        ? productText.Get("match.action_summary_one")
                        : productText.Get("match.action_summary_many", actions.Length),
                ContextHelpText(presentation, discard.Cards[discard.Cards.Count - 1],
                    calledSuit?.Value.SuitValue, actions, productText),
                actions,
                canAct);
        }

        private static string ActionReason(ActionPresentation presentation,
            CardZonePresentation humanHand, Card discardTop, Suit? calledSuit,
            IProductText text)
        {
            TrumpLab.Action action = presentation.Action;
            switch (action.Kind)
            {
                case "draw":
                    return text.Get("match.reason_draw");
                case "pass":
                    return text.Get("match.reason_pass");
                case "choose_starter_suit":
                    return text.Get("match.reason_starter_suit",
                        SuitName(SuitFromCode(action.Value), text));
                case "play":
                case "play_last_card":
                    if (!action.Card.HasValue)
                        throw new ArgumentException(
                            "Play action must contain a card.", nameof(presentation));
                    Card card = action.Card.Value;
                    if (card.Rank == 8)
                        return text.Get("match.reason_wild",
                            SuitName(SuitFromCode(action.Value), text));
                    Suit activeSuit = calledSuit ?? discardTop.Suit;
                    bool suitMatch = card.Suit == activeSuit;
                    bool rankMatch = card.Rank == discardTop.Rank;
                    string match = suitMatch && rankMatch
                        ? text.Get("match.same_suit_rank")
                        : suitMatch
                            ? text.Get("match.same_suit", SuitName(activeSuit, text))
                            : rankMatch
                                ? text.Get("match.same_rank", RankLabel(discardTop.Rank, text))
                                :
                        throw new ArgumentException(
                            "Presented play action does not match the public discard.",
                            nameof(presentation));
                    if (humanHand.Count == 1) return text.Get("match.reason_final", match);
                    if (action.Kind == "play_last_card")
                        return text.Get("match.reason_last", match);
                    return text.Get("match.reason_match", match);
                default:
                    throw new ArgumentException(
                        "Unsupported Crazy Eights action kind: " + action.Kind,
                        nameof(presentation));
            }
        }

        private static string ContextHelpText(GamePresentation presentation,
            Card discardTop, Suit? calledSuit, IReadOnlyList<MatchActionViewModel> actions,
            IProductText text)
        {
            string activeSuit = SuitName(calledSuit ?? discardTop.Suit, text);
            string rule = presentation.Phase == "choose_starter_suit"
                ? text.Get("match.context_opening")
                : text.Get("match.context_rule", activeSuit,
                    RankLabel(discardTop.Rank, text));
            if (actions.Count == 0)
                return text.Get("match.context_cpu", rule);
            string actionLines = string.Join("\n", actions.Select(action =>
                text.Get("match.action_line", action.Label, action.Reason)));
            return text.Get("match.context_actions", rule, actionLines);
        }

        public static string CardLabel(Card card, IProductText? text = null)
        {
            IProductText productText = text ?? ProductTextCatalog.English;
            return productText.Get("card.label", RankLabel(card.Rank, productText),
                SuitSymbol(card.Suit, productText));
        }

        private static string RankLabel(int rank, IProductText text) => rank == 1
            ? text.Get("card.rank_ace")
            : rank == 11
                ? text.Get("card.rank_jack")
                : rank == 12
                    ? text.Get("card.rank_queen")
                    : rank == 13
                        ? text.Get("card.rank_king")
                        : rank.ToString(CultureInfo.InvariantCulture);

        private static string ActionLabel(ActionPresentation presentation, IProductText text)
        {
            TrumpLab.Action action = presentation.Action;
            switch (action.Kind)
            {
                case "draw": return text.Get("match.action_draw");
                case "pass": return text.Get("match.action_pass");
                case "choose_starter_suit":
                    return text.Get("match.action_choose_suit",
                        SuitName(SuitFromCode(action.Value), text));
                case "play":
                case "play_last_card":
                    if (!action.Card.HasValue)
                        throw new ArgumentException("Play action must contain a card.", nameof(presentation));
                    string card = CardLabel(action.Card.Value, text);
                    return action.Value == null
                        ? text.Get("match.action_play", card)
                        : text.Get("match.action_play_called", card,
                            SuitName(SuitFromCode(action.Value), text));
                default:
                    throw new ArgumentException(
                        "Unsupported Crazy Eights action kind: " + action.Kind,
                        nameof(presentation));
            }
        }

        private static CardZonePresentation Hand(GamePresentation presentation, int player) =>
            presentation.CardZones.Single(zone => zone.Role == "hand" && zone.OwnerPlayer == player);

        private static CardZonePresentation Zone(GamePresentation presentation, string id) =>
            presentation.CardZones.Single(zone => zone.Id == id);

        private static void RequireVisible(CardZonePresentation zone, string name)
        {
            if (zone.Visibility != CardZoneVisibility.FaceUp || zone.Cards.Count != zone.Count)
                throw new ArgumentException(name + " must contain every visible card.", nameof(zone));
        }

        private static void RequireHidden(CardZonePresentation zone,
            CardZoneVisibility visibility, string name)
        {
            if (zone.Visibility != visibility || zone.Cards.Count != 0)
                throw new ArgumentException(name + " must not expose card values.", nameof(zone));
        }

        private static string SuitSymbol(Suit suit, IProductText text) => suit switch
        {
            Suit.Clubs => text.Get("card.suit_clubs"),
            Suit.Diamonds => text.Get("card.suit_diamonds"),
            Suit.Hearts => text.Get("card.suit_hearts"),
            Suit.Spades => text.Get("card.suit_spades"),
            _ => throw new ArgumentOutOfRangeException(nameof(suit))
        };

        private static string SuitName(Suit suit, IProductText text) => suit switch
        {
            Suit.Clubs => text.Get("suit.clubs"),
            Suit.Diamonds => text.Get("suit.diamonds"),
            Suit.Hearts => text.Get("suit.hearts"),
            Suit.Spades => text.Get("suit.spades"),
            _ => throw new ArgumentOutOfRangeException(nameof(suit))
        };

        private static Suit SuitFromCode(string? value)
        {
            switch (value)
            {
                case "C": return Suit.Clubs;
                case "D": return Suit.Diamonds;
                case "H": return Suit.Hearts;
                case "S": return Suit.Spades;
                default: throw new ArgumentException("Action suit code is invalid.", nameof(value));
            }
        }
    }
}
