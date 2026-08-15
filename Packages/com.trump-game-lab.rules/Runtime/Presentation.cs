using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab
{
    public enum CardZoneVisibility
    {
        FaceUp,
        FaceDown,
        CountOnly
    }

    public enum PresentationValueKind
    {
        Text,
        Number,
        Boolean,
        Suit,
        Player,
        Card
    }

    public sealed class PresentationValue
    {
        public PresentationValueKind Kind { get; }
        public string? TextValue { get; }
        public double? NumberValue { get; }
        public bool? BooleanValue { get; }
        public Suit? SuitValue { get; }
        public int? PlayerValue { get; }
        public Card? CardValue { get; }

        private PresentationValue(PresentationValueKind kind, string? textValue = null,
            double? numberValue = null, bool? booleanValue = null, Suit? suitValue = null,
            int? playerValue = null, Card? cardValue = null)
        {
            Kind = kind;
            TextValue = textValue;
            NumberValue = numberValue;
            BooleanValue = booleanValue;
            SuitValue = suitValue;
            PlayerValue = playerValue;
            CardValue = cardValue;
        }

        public static PresentationValue FromText(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return new PresentationValue(PresentationValueKind.Text, textValue: value);
        }

        public static PresentationValue FromNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Number must be finite.");
            return new PresentationValue(PresentationValueKind.Number, numberValue: value);
        }

        public static PresentationValue FromBoolean(bool value) =>
            new PresentationValue(PresentationValueKind.Boolean, booleanValue: value);

        public static PresentationValue FromSuit(Suit value) =>
            new PresentationValue(PresentationValueKind.Suit, suitValue: value);

        public static PresentationValue FromPlayer(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            return new PresentationValue(PresentationValueKind.Player, playerValue: value);
        }

        public static PresentationValue FromCard(Card value) =>
            new PresentationValue(PresentationValueKind.Card, cardValue: value);
    }

    public sealed class PlayerPresentation
    {
        public int PlayerIndex { get; }
        public bool IsCurrent { get; }
        public bool IsViewer { get; }

        public PlayerPresentation(int playerIndex, bool isCurrent, bool isViewer)
        {
            if (playerIndex < 0) throw new ArgumentOutOfRangeException(nameof(playerIndex));
            PlayerIndex = playerIndex;
            IsCurrent = isCurrent;
            IsViewer = isViewer;
        }
    }

    public sealed class CardZonePresentation
    {
        public string Id { get; }
        public string Role { get; }
        public int? OwnerPlayer { get; }
        public CardZoneVisibility Visibility { get; }
        public int Count { get; }
        public IReadOnlyList<Card> Cards { get; }

        public CardZonePresentation(string id, string role, int? ownerPlayer,
            CardZoneVisibility visibility, int count, IEnumerable<Card>? cards = null)
        {
            Id = PresentationGuard.Identifier(id, nameof(id));
            Role = PresentationGuard.Identifier(role, nameof(role));
            if (ownerPlayer < 0) throw new ArgumentOutOfRangeException(nameof(ownerPlayer));
            if (!Enum.IsDefined(typeof(CardZoneVisibility), visibility))
                throw new ArgumentOutOfRangeException(nameof(visibility));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            Card[] copiedCards = cards?.ToArray() ?? Array.Empty<Card>();
            if (visibility == CardZoneVisibility.FaceUp && copiedCards.Length != count)
                throw new ArgumentException("Face-up zones must provide every card.", nameof(cards));
            if (visibility != CardZoneVisibility.FaceUp && copiedCards.Length != 0)
                throw new ArgumentException("Hidden zones cannot expose card values.", nameof(cards));

            OwnerPlayer = ownerPlayer;
            Visibility = visibility;
            Count = count;
            Cards = Array.AsReadOnly(copiedCards);
        }
    }

    public sealed class GameFieldPresentation
    {
        public string Id { get; }
        public PresentationValue Value { get; }

        public GameFieldPresentation(string id, PresentationValue value)
        {
            Id = PresentationGuard.Identifier(id, nameof(id));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public sealed class ActionPresentation
    {
        public string Id { get; }
        public Action Action { get; }
        public string LabelKey { get; }

        public ActionPresentation(string id, Action action, string labelKey)
        {
            Id = PresentationGuard.Identifier(id, nameof(id));
            if (string.IsNullOrWhiteSpace(action.Kind))
                throw new ArgumentException("Action kind cannot be empty.", nameof(action));
            if (string.IsNullOrWhiteSpace(labelKey))
                throw new ArgumentException("Label key cannot be empty.", nameof(labelKey));
            Action = action;
            LabelKey = labelKey;
        }
    }

    public sealed class GameResultPresentation
    {
        public IReadOnlyList<int> Winners { get; }
        public IReadOnlyList<double> Scores { get; }
        public string Reason { get; }
        public int Turns { get; }

        public GameResultPresentation(IEnumerable<int> winners, IEnumerable<double> scores,
            string reason, int turns)
        {
            if (winners == null) throw new ArgumentNullException(nameof(winners));
            if (scores == null) throw new ArgumentNullException(nameof(scores));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Result reason cannot be empty.", nameof(reason));
            if (turns < 0) throw new ArgumentOutOfRangeException(nameof(turns));

            int[] copiedWinners = winners.ToArray();
            double[] copiedScores = scores.ToArray();
            if (copiedWinners.Any(winner => winner < 0))
                throw new ArgumentOutOfRangeException(nameof(winners));
            if (copiedScores.Any(score => double.IsNaN(score) || double.IsInfinity(score)))
                throw new ArgumentOutOfRangeException(nameof(scores), "Scores must be finite.");

            Winners = Array.AsReadOnly(copiedWinners);
            Scores = Array.AsReadOnly(copiedScores);
            Reason = reason;
            Turns = turns;
        }
    }

    public sealed class GamePresentation
    {
        public string GameId { get; }
        public string Phase { get; }
        public int Viewer { get; }
        public int CurrentPlayer { get; }
        public int TurnCount { get; }
        public bool IsTerminal { get; }
        public IReadOnlyList<PlayerPresentation> Players { get; }
        public IReadOnlyList<CardZonePresentation> CardZones { get; }
        public IReadOnlyList<GameFieldPresentation> Fields { get; }
        public IReadOnlyList<ActionPresentation> Actions { get; }
        public GameResultPresentation? Result { get; }

        public GamePresentation(string gameId, string phase, int viewer, int currentPlayer,
            int turnCount, bool isTerminal, IEnumerable<PlayerPresentation> players,
            IEnumerable<CardZonePresentation> cardZones,
            IEnumerable<GameFieldPresentation> fields, IEnumerable<ActionPresentation> actions,
            GameResultPresentation? result = null)
        {
            GameId = PresentationGuard.Identifier(gameId, nameof(gameId));
            Phase = PresentationGuard.Identifier(phase, nameof(phase));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (cardZones == null) throw new ArgumentNullException(nameof(cardZones));
            if (fields == null) throw new ArgumentNullException(nameof(fields));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (turnCount < 0) throw new ArgumentOutOfRangeException(nameof(turnCount));

            PlayerPresentation[] copiedPlayers = players.ToArray();
            CardZonePresentation[] copiedZones = cardZones.ToArray();
            GameFieldPresentation[] copiedFields = fields.ToArray();
            ActionPresentation[] copiedActions = actions.ToArray();

            if (copiedPlayers.Length == 0)
                throw new ArgumentException("At least one player is required.", nameof(players));
            if (viewer < 0 || viewer >= copiedPlayers.Length)
                throw new ArgumentOutOfRangeException(nameof(viewer));
            if (currentPlayer < 0 || currentPlayer >= copiedPlayers.Length)
                throw new ArgumentOutOfRangeException(nameof(currentPlayer));

            for (int index = 0; index < copiedPlayers.Length; index++)
            {
                PlayerPresentation player = copiedPlayers[index] ??
                    throw new ArgumentException("Players cannot contain null.", nameof(players));
                if (player.PlayerIndex != index)
                    throw new ArgumentException("Players must be ordered by contiguous index.", nameof(players));
                if (player.IsCurrent != (index == currentPlayer))
                    throw new ArgumentException("Player current flags do not match CurrentPlayer.", nameof(players));
                if (player.IsViewer != (index == viewer))
                    throw new ArgumentException("Player viewer flags do not match Viewer.", nameof(players));
            }

            ValidateZones(copiedZones, copiedPlayers.Length);
            ValidateFields(copiedFields, copiedPlayers.Length);
            PresentationGuard.UniqueIds(copiedActions.Select(action =>
                (action ?? throw new ArgumentException("Actions cannot contain null.", nameof(actions))).Id),
                nameof(actions));

            if ((isTerminal || viewer != currentPlayer) && copiedActions.Length != 0)
                throw new ArgumentException(
                    "Terminal and non-current viewers cannot receive actions.", nameof(actions));
            if (isTerminal != (result != null))
                throw new ArgumentException("Result must exist exactly when the game is terminal.", nameof(result));
            if (result != null)
            {
                if (result.Scores.Count != copiedPlayers.Length)
                    throw new ArgumentException("Result score count must match player count.", nameof(result));
                if (result.Winners.Any(winner => winner >= copiedPlayers.Length))
                    throw new ArgumentException("Result winner is outside the player range.", nameof(result));
            }

            Viewer = viewer;
            CurrentPlayer = currentPlayer;
            TurnCount = turnCount;
            IsTerminal = isTerminal;
            Players = Array.AsReadOnly(copiedPlayers);
            CardZones = Array.AsReadOnly(copiedZones);
            Fields = Array.AsReadOnly(copiedFields);
            Actions = Array.AsReadOnly(copiedActions);
            Result = result;
        }

        private static void ValidateZones(IEnumerable<CardZonePresentation> zones, int players)
        {
            CardZonePresentation[] copiedZones = zones.ToArray();
            PresentationGuard.UniqueIds(copiedZones.Select(zone =>
                (zone ?? throw new ArgumentException("Card zones cannot contain null.", nameof(zones))).Id),
                nameof(zones));
            if (copiedZones.Any(zone => zone.OwnerPlayer >= players))
                throw new ArgumentException("Card zone owner is outside the player range.", nameof(zones));
        }

        private static void ValidateFields(IEnumerable<GameFieldPresentation> fields, int players)
        {
            GameFieldPresentation[] copiedFields = fields.ToArray();
            PresentationGuard.UniqueIds(copiedFields.Select(field =>
                (field ?? throw new ArgumentException("Fields cannot contain null.", nameof(fields))).Id),
                nameof(fields));
            if (copiedFields.Any(field => field.Value.Kind == PresentationValueKind.Player &&
                field.Value.PlayerValue >= players))
                throw new ArgumentException("Player field is outside the player range.", nameof(fields));
        }
    }

    public interface IGamePresentationProvider
    {
        GamePresentation Present(int? viewer = null);
    }

    internal static class PresentationGuard
    {
        public static string Identifier(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Identifier cannot be empty.", parameterName);
            if (value.Any(character => !IsIdentifierCharacter(character)) ||
                value[0] < 'a' || value[0] > 'z')
                throw new ArgumentException(
                    "Identifier must use lowercase ASCII letters, digits, and underscores, and start with a letter.",
                    parameterName);
            return value;
        }

        public static void UniqueIds(IEnumerable<string> ids, string parameterName)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
                if (!seen.Add(id))
                    throw new ArgumentException("Duplicate identifier: " + id, parameterName);
        }

        private static bool IsIdentifierCharacter(char value) =>
            value >= 'a' && value <= 'z' || value >= '0' && value <= '9' || value == '_';
    }
}
