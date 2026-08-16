#nullable enable

using System;
using System.Collections.Generic;
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
        public static MatchViewModel Create(GamePresentation presentation, bool inputEnabled)
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

            bool humanTurn = presentation.CurrentPlayer == presentation.Viewer;
            string status = presentation.IsTerminal
                ? "Game finished"
                : presentation.Phase == "choose_starter_suit"
                    ? (humanTurn ? "Choose the starter suit" : "CPU is choosing the starter suit")
                    : humanTurn ? "Your turn" : "CPU is thinking…";
            status += "  •  Turn " + presentation.TurnCount;

            string called = calledSuit?.Value.SuitValue is Suit suit
                ? "  •  Called " + SuitName(suit)
                : string.Empty;
            string discardText = "Discard: " + CardLabel(discard.Cards[discard.Cards.Count - 1]) + called;
            string opponentText = "CPU hand: " + string.Join(" ",
                Enumerable.Repeat("■", opponentHand.Count));
            string humanText = "Your hand: " + string.Join(" ", humanHand.Cards.Select(CardLabel));
            MatchActionViewModel[] actions = presentation.Actions
                .Select(action => new MatchActionViewModel(
                    action.Id,
                    ActionLabel(action),
                    ActionReason(action, humanHand, discard.Cards[discard.Cards.Count - 1],
                        calledSuit?.Value.SuitValue)))
                .ToArray();
            bool canAct = inputEnabled && humanTurn && !presentation.IsTerminal;

            return new MatchViewModel(
                status,
                opponentText,
                "Stock: " + stock.Count,
                discardText,
                humanText,
                actions.Length == 0 ? "Input locked while the CPU acts." :
                    "✓ " + actions.Length + " legal action" +
                    (actions.Length == 1 ? string.Empty : "s") +
                    " shown  •  Help explains why",
                ContextHelpText(presentation, discard.Cards[discard.Cards.Count - 1],
                    calledSuit?.Value.SuitValue, actions),
                actions,
                canAct);
        }

        private static string ActionReason(ActionPresentation presentation,
            CardZonePresentation humanHand, Card discardTop, Suit? calledSuit)
        {
            TrumpLab.Action action = presentation.Action;
            switch (action.Kind)
            {
                case "draw":
                    return "Draw one card and end your turn; drawing is a legal choice.";
                case "pass":
                    return "No card can be played and no card can be drawn.";
                case "choose_starter_suit":
                    return "The opening 8 lets you set the active suit to " +
                        SuitName(SuitFromCode(action.Value)) + ".";
                case "play":
                case "play_last_card":
                    if (!action.Card.HasValue)
                        throw new ArgumentException(
                            "Play action must contain a card.", nameof(presentation));
                    Card card = action.Card.Value;
                    if (card.Rank == 8)
                        return "An 8 is wild and calls " +
                            SuitName(SuitFromCode(action.Value)) + " for the next turn.";
                    Suit activeSuit = calledSuit ?? discardTop.Suit;
                    bool suitMatch = card.Suit == activeSuit;
                    bool rankMatch = card.Rank == discardTop.Rank;
                    string match = suitMatch && rankMatch ? "same suit and rank" :
                        suitMatch ? "same " + SuitName(activeSuit) + " suit" :
                        rankMatch ? "same rank " + RankLabel(discardTop.Rank) :
                        throw new ArgumentException(
                            "Presented play action does not match the public discard.",
                            nameof(presentation));
                    if (humanHand.Count == 1) return "Your final card matches by " + match + ".";
                    if (action.Kind == "play_last_card")
                        return "Matches by " + match + " and declares your last card.";
                    return "Matches the discard by " + match + ".";
                default:
                    throw new ArgumentException(
                        "Unsupported Crazy Eights action kind: " + action.Kind,
                        nameof(presentation));
            }
        }

        private static string ContextHelpText(GamePresentation presentation,
            Card discardTop, Suit? calledSuit, IReadOnlyList<MatchActionViewModel> actions)
        {
            string activeSuit = SuitName(calledSuit ?? discardTop.Suit);
            string rule = presentation.Phase == "choose_starter_suit"
                ? "The opening card is an 8. Choose the suit that starts the match."
                : "Play the active " + activeSuit + " suit, rank " +
                    RankLabel(discardTop.Rank) + ", or any 8. You may draw one card " +
                    "and end your turn whenever Draw is shown.";
            if (actions.Count == 0)
                return rule + "\n\nThe CPU is acting. Your controls remain locked until its move ends.";
            return rule + "\n\nEvery shown action is legal:\n" +
                string.Join("\n", actions.Select(action =>
                    "✓ " + action.Label + " — " + action.Reason));
        }

        public static string CardLabel(Card card)
        {
            string rank = RankLabel(card.Rank);
            return rank + SuitSymbol(card.Suit);
        }

        private static string RankLabel(int rank) => rank == 1 ? "A" : rank == 11 ? "J" :
            rank == 12 ? "Q" : rank == 13 ? "K" : rank.ToString();

        private static string ActionLabel(ActionPresentation presentation)
        {
            TrumpLab.Action action = presentation.Action;
            switch (action.Kind)
            {
                case "draw": return "Draw";
                case "pass": return "Pass";
                case "choose_starter_suit": return "Choose " + SuitName(SuitFromCode(action.Value));
                case "play":
                case "play_last_card":
                    if (!action.Card.HasValue)
                        throw new ArgumentException("Play action must contain a card.", nameof(presentation));
                    string calledSuit = action.Value == null
                        ? string.Empty
                        : " → " + SuitName(SuitFromCode(action.Value));
                    return "Play " + CardLabel(action.Card.Value) + calledSuit;
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

        private static string SuitSymbol(Suit suit) => suit == Suit.Clubs ? "♣" :
            suit == Suit.Diamonds ? "♦" : suit == Suit.Hearts ? "♥" : "♠";

        private static string SuitName(Suit suit) => suit == Suit.Clubs ? "Clubs" :
            suit == Suit.Diamonds ? "Diamonds" : suit == Suit.Hearts ? "Hearts" : "Spades";

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
