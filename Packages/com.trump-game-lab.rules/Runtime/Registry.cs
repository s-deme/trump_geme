using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TrumpLab
{
    public sealed class GameInfo
    {
        public string GameId { get; }
        public string Name { get; }
        public int MinPlayers { get; }
        public int MaxPlayers { get; }
        public string Category { get; }
        public string Description { get; }
        public string Source { get; }
        public IReadOnlyDictionary<string, string> Options { get; }

        public GameInfo(string gameId, string name, int minPlayers, int maxPlayers,
            string category, string description, string source,
            IReadOnlyDictionary<string, string>? options = null)
        {
            GameId = gameId; Name = name; MinPlayers = minPlayers; MaxPlayers = maxPlayers;
            Category = category; Description = description; Source = source;
            Options = options ?? new Dictionary<string, string>();
        }
    }

    public sealed class GameRegistry
    {
        private readonly Dictionary<string, Tuple<GameInfo,
            Func<int, DeterministicRandom, IReadOnlyDictionary<string, string>, IGame>>> entries =
            new Dictionary<string, Tuple<GameInfo,
                Func<int, DeterministicRandom, IReadOnlyDictionary<string, string>, IGame>>>();

        public void Register(GameInfo info,
            Func<int, DeterministicRandom, IReadOnlyDictionary<string, string>, IGame> factory)
        {
            if (entries.ContainsKey(info.GameId))
                throw new ArgumentException("Duplicate game id: " + info.GameId);
            entries.Add(info.GameId, Tuple.Create(info, factory));
        }

        public GameInfo Info(string gameId) => entries[gameId].Item1;
        public bool Contains(string gameId) => entries.ContainsKey(gameId);
        public IReadOnlyList<GameInfo> All() =>
            entries.Values.Select(value => value.Item1).OrderBy(info => info.GameId).ToArray();

        public IGame Create(string gameId, int? players = null, long seed = 1,
            IReadOnlyDictionary<string, string>? options = null)
        {
            Tuple<GameInfo, Func<int, DeterministicRandom,
                IReadOnlyDictionary<string, string>, IGame>> entry = entries[gameId];
            int count = players ?? entry.Item1.MinPlayers;
            if (count < entry.Item1.MinPlayers || count > entry.Item1.MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(players),
                    $"{entry.Item1.Name} supports {entry.Item1.MinPlayers}..{entry.Item1.MaxPlayers} players.");
            return entry.Item2(count, new DeterministicRandom(seed),
                options ?? new Dictionary<string, string>());
        }
    }

    public static class GameOptions
    {
        public static int Integer(this IReadOnlyDictionary<string, string> options,
            string key, int defaultValue) =>
            options.TryGetValue(key, out string value)
                ? int.Parse(value, CultureInfo.InvariantCulture) : defaultValue;

        public static bool Boolean(this IReadOnlyDictionary<string, string> options,
            string key, bool defaultValue) =>
            options.TryGetValue(key, out string value)
                ? bool.Parse(value) : defaultValue;

        public static string Text(this IReadOnlyDictionary<string, string> options,
            string key, string defaultValue) =>
            options.TryGetValue(key, out string value) ? value : defaultValue;
    }

    public static class BuiltInGames
    {
        private static readonly Lazy<GameRegistry> LazyRegistry =
            new Lazy<GameRegistry>(CreateRegistry);
        public static GameRegistry Registry => LazyRegistry.Value;

        private static GameRegistry CreateRegistry()
        {
            var registry = new GameRegistry();
            Games.BlackjackGame.Register(registry);
            Games.BlackLadyGame.Register(registry);
            Games.BriscolaGame.Register(registry);
            Games.CrazyEightsGame.Register(registry);
            Games.GermanWhistGame.Register(registry);
            Games.GinRummyGame.Register(registry);
            Games.GoFishGame.Register(registry);
            Games.MinimoGame.Register(registry);
            Games.OldMaidGame.Register(registry);
            Games.MultiRoundTrickGame.RegisterGames(registry);
            Games.WarGame.Register(registry);
            Games.ClassicCandidateGames.RegisterGames(registry);
            Games.PokerAndBankingGames.RegisterGames(registry);
            Games.TrickClassicsGames.RegisterGames(registry);
            Games.RummyClassicGames.RegisterGames(registry);
            Games.SheddingAndLayoutGames.RegisterGames(registry);
            Games.SoloCandidateGames.RegisterGames(registry);
            Games.TwoPlayerCandidateGames.RegisterGames(registry);
            Games.MoreTwoPlayerGames.RegisterGames(registry);
            Games.RemainingTwoPlayerGames.RegisterGames(registry);
            Games.PiquetAndKlaberjassGames.RegisterGames(registry);
            Games.HiddenRoleTrickGames.RegisterGames(registry);
            Games.ThreePlayerRoundGames.RegisterGames(registry);
            Games.ThreePlayerBidGames.RegisterGames(registry);
            Games.SkatGame.Register(registry);
            Games.UltiGame.Register(registry);
            Games.RemainingThreePlayerGames.RegisterGames(registry);
            Games.FourPlayerFoundationGames.RegisterGames(registry);
            Games.FourPlayerClimbingGames.RegisterGames(registry);
            Games.FourPlayerSessionGames.RegisterGames(registry);
            Games.FinesseAndSchafkopfGames.RegisterGames(registry);
            Games.DoppelkopfGame.Register(registry);
            Games.VariablePlayerGames.RegisterGames(registry);
            Games.CrewWuxingSchmearGames.RegisterGames(registry);
            Games.CalledBriscolaGames.RegisterGames(registry);
            Games.GoninkanAndNapoleonGames.RegisterGames(registry);
            Games.BaohuangGame.Register(registry);
            Games.CandidateRuleGames.RegisterGames(registry);
            return registry;
        }
    }
}
