using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab
{
    public enum Suit { Clubs, Diamonds, Hearts, Spades }

    public readonly struct Card : IEquatable<Card>, IComparable<Card>
    {
        public Suit Suit { get; }
        public int Rank { get; }

        public Card(Suit suit, int rank)
        {
            if (rank < 1 || rank > 13) throw new ArgumentOutOfRangeException(nameof(rank));
            Suit = suit;
            Rank = rank;
        }

        public int CompareTo(Card other)
        {
            int suit = Suit.CompareTo(other.Suit);
            return suit != 0 ? suit : Rank.CompareTo(other.Rank);
        }

        public bool Equals(Card other) => Suit == other.Suit && Rank == other.Rank;
        public override bool Equals(object? obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => ((int)Suit * 397) ^ Rank;
        public static bool operator ==(Card left, Card right) => left.Equals(right);
        public static bool operator !=(Card left, Card right) => !left.Equals(right);

        public override string ToString()
        {
            string rank = Rank == 1 ? "A" : Rank == 11 ? "J" : Rank == 12 ? "Q" :
                Rank == 13 ? "K" : Rank.ToString(CultureInfo.InvariantCulture);
            return rank + SuitCode(Suit);
        }

        public static Card Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 2)
                throw new FormatException("Invalid card.");
            text = text.Trim().ToUpperInvariant();
            Suit suit = ParseSuit(text[text.Length - 1].ToString());
            string label = text.Substring(0, text.Length - 1);
            int rank = label == "A" ? 1 : label == "J" ? 11 : label == "Q" ? 12 :
                label == "K" ? 13 : int.Parse(label, CultureInfo.InvariantCulture);
            return new Card(suit, rank);
        }

        public static string SuitCode(Suit suit) =>
            suit == Suit.Clubs ? "C" : suit == Suit.Diamonds ? "D" :
            suit == Suit.Hearts ? "H" : "S";

        public static Suit ParseSuit(string value)
        {
            switch (value.Trim().ToUpperInvariant())
            {
                case "C": return Suit.Clubs;
                case "D": return Suit.Diamonds;
                case "H": return Suit.Hearts;
                case "S": return Suit.Spades;
                default: throw new FormatException("Invalid suit: " + value);
            }
        }
    }

    public readonly struct Action : IEquatable<Action>
    {
        public string Kind { get; }
        public Card? Card { get; }
        public int? Target { get; }
        public string? Value { get; }

        public Action(string kind, Card? card = null, int? target = null, string? value = null)
        {
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            Card = card;
            Target = target;
            Value = value;
        }

        public bool Equals(Action other) =>
            Kind == other.Kind && Nullable.Equals(Card, other.Card) &&
            Target == other.Target && Value == other.Value;
        public override bool Equals(object? obj) => obj is Action other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Kind.GetHashCode();
                hash = hash * 31 + (Card?.GetHashCode() ?? 0);
                hash = hash * 31 + (Target ?? 0);
                hash = hash * 31 + (Value?.GetHashCode() ?? 0);
                return hash;
            }
        }
        public static bool operator ==(Action left, Action right) => left.Equals(right);
        public static bool operator !=(Action left, Action right) => !left.Equals(right);
        public override string ToString() => string.Join(" ", new[]
        {
            Kind, Card?.ToString(), Target.HasValue ? "p" + Target.Value : null, Value
        }.Where(part => part != null));
    }

    public sealed class GameResult
    {
        public IReadOnlyList<int> Winners { get; }
        public IReadOnlyList<double> Scores { get; }
        public string Reason { get; }
        public int Turns { get; }
        public IReadOnlyDictionary<string, object> Extra { get; }

        public GameResult(IEnumerable<int> winners, IEnumerable<double> scores, string reason,
            int turns, IReadOnlyDictionary<string, object>? extra = null)
        {
            Winners = winners.ToArray();
            Scores = scores.ToArray();
            Reason = reason;
            Turns = turns;
            Extra = extra ?? new Dictionary<string, object>();
        }
    }

    public interface IGame
    {
        string GameId { get; }
        string Name { get; }
        int Players { get; }
        int CurrentPlayer { get; }
        int TurnCount { get; }
        IReadOnlyList<Action> LegalActions(int? player = null);
        void Apply(Action action);
        bool IsTerminal { get; }
        GameResult Result();
        string View(int? player = null);
        Action ChooseCpuAction(int player, DeterministicRandom rng, int difficulty = 1);
    }

    public abstract class GameBase : IGame
    {
        public abstract string GameId { get; }
        public abstract string Name { get; }
        public int Players { get; protected set; }
        public int CurrentPlayer { get; protected set; }
        public int TurnCount { get; protected set; }
        public abstract IReadOnlyList<Action> LegalActions(int? player = null);
        public abstract void Apply(Action action);
        public abstract bool IsTerminal { get; }
        public abstract GameResult Result();
        public abstract string View(int? player = null);

        public virtual Action ChooseCpuAction(int player, DeterministicRandom rng, int difficulty = 1)
        {
            IReadOnlyList<Action> actions = LegalActions(player);
            if (actions.Count == 0) throw new InvalidOperationException(
                GameId + ": no legal action for player " + player);
            return actions[rng.Next(actions.Count)];
        }

        protected int ValidateTurn(int? player)
        {
            int actual = player ?? CurrentPlayer;
            if (actual != CurrentPlayer)
                throw new InvalidOperationException($"Player {actual} cannot act; current player is {CurrentPlayer}.");
            if (IsTerminal) throw new InvalidOperationException("Game is already over.");
            return actual;
        }
    }

    public sealed class DeterministicRandom
    {
        private ulong state;
        public DeterministicRandom(long seed)
        {
            state = unchecked((ulong)seed) + 0x9E3779B97F4A7C15UL;
            NextUInt64();
        }

        private ulong NextUInt64()
        {
            ulong value = state;
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            state = value;
            return value * 2685821657736338717UL;
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextUInt64() % (uint)maxExclusive);
        }

        public T Choice<T>(IReadOnlyList<T> values) => values[Next(values.Count)];

        public void Shuffle<T>(IList<T> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T value = values[i];
                values[i] = values[j];
                values[j] = value;
            }
        }
    }

    public static class Cards
    {
        public static List<Card> StandardDeck(IEnumerable<int>? ranks = null, int copies = 1)
        {
            int[] selected = (ranks ?? Enumerable.Range(1, 13)).ToArray();
            var deck = new List<Card>(selected.Length * 4 * copies);
            for (int copy = 0; copy < copies; copy++)
                foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                    foreach (int rank in selected) deck.Add(new Card(suit, rank));
            return deck;
        }

        public static List<Card> Shuffled(IEnumerable<Card> cards, DeterministicRandom rng)
        {
            var result = cards.ToList();
            rng.Shuffle(result);
            return result;
        }
    }

    internal static class Guard
    {
        public static void Legal(Action action, IReadOnlyList<Action> legal)
        {
            if (!legal.Contains(action)) throw new ArgumentException("Illegal action: " + action);
        }
    }
}
