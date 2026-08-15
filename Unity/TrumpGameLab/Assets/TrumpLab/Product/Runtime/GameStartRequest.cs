#nullable enable

using System;

namespace TrumpLab.Product
{
    public sealed class GameStartRequest : IEquatable<GameStartRequest>
    {
        public long Seed { get; }
        public int WildRank { get; }
        public int Difficulty { get; }

        public GameStartRequest(long seed, int wildRank, int difficulty = 1)
        {
            if (wildRank < 1 || wildRank > 13)
                throw new ArgumentOutOfRangeException(nameof(wildRank));
            if (difficulty < 1) throw new ArgumentOutOfRangeException(nameof(difficulty));
            Seed = seed;
            WildRank = wildRank;
            Difficulty = difficulty;
        }

        public bool Equals(GameStartRequest? other) => other != null &&
            Seed == other.Seed && WildRank == other.WildRank && Difficulty == other.Difficulty;
        public override bool Equals(object? obj) => Equals(obj as GameStartRequest);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Seed.GetHashCode();
                hash = hash * 31 + WildRank;
                return hash * 31 + Difficulty;
            }
        }
    }
}
