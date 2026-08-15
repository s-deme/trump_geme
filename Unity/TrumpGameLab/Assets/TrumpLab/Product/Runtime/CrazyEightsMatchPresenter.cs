#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Product
{
    public sealed class MatchViewModel
    {
        public string Status { get; }
        public string OpponentHand { get; }
        public string Stock { get; }
        public string Discard { get; }
        public string HumanHand { get; }
        public string ActionSummary { get; }
        public IReadOnlyList<ActionPresentation> Actions { get; }

        public MatchViewModel(string status, string opponentHand, string stock,
            string discard, string humanHand, string actionSummary,
            IEnumerable<ActionPresentation> actions)
        {
            Status = Required(status, nameof(status));
            OpponentHand = Required(opponentHand, nameof(opponentHand));
            Stock = Required(stock, nameof(stock));
            Discard = Required(discard, nameof(discard));
            HumanHand = Required(humanHand, nameof(humanHand));
            ActionSummary = Required(actionSummary, nameof(actionSummary));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            Actions = Array.AsReadOnly(actions.ToArray());
        }

        private static string Required(string value, string parameterName) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Display value cannot be empty.", parameterName)
                : value;
    }

    public static class CrazyEightsMatchPresenter
    {
        public static MatchViewModel Create(GamePresentation presentation)
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
                    : humanTurn ? "Your turn" : "CPU turn";
            status += "  •  Turn " + presentation.TurnCount;

            string called = calledSuit?.Value.SuitValue is Suit suit
                ? "  •  Called " + SuitName(suit)
                : string.Empty;
            string discardText = "Discard: " + CardLabel(discard.Cards[discard.Cards.Count - 1]) + called;
            string opponentText = "CPU hand: " + string.Join(" ",
                Enumerable.Repeat("■", opponentHand.Count));
            string humanText = "Your hand: " + string.Join(" ", humanHand.Cards.Select(CardLabel));

            return new MatchViewModel(
                status,
                opponentText,
                "Stock: " + stock.Count,
                discardText,
                humanText,
                presentation.Actions.Count == 0
                    ? "No actions available"
                    : "Legal actions: " + presentation.Actions.Count,
                presentation.Actions);
        }

        public static string CardLabel(Card card)
        {
            string rank = card.Rank == 1 ? "A" : card.Rank == 11 ? "J" :
                card.Rank == 12 ? "Q" : card.Rank == 13 ? "K" : card.Rank.ToString();
            return rank + SuitSymbol(card.Suit);
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
    }
}
