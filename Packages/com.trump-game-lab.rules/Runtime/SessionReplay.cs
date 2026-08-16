using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TrumpLab
{
    public sealed class SessionConfiguration
    {
        public string GameId { get; }
        public int Players { get; }
        public long Seed { get; }
        public int Difficulty { get; }
        public IReadOnlyList<int> HumanPlayers { get; }
        public IReadOnlyDictionary<string, string> Options { get; }

        public SessionConfiguration(string gameId, int players, long seed, int difficulty,
            IEnumerable<int> humanPlayers,
            IReadOnlyDictionary<string, string>? options = null)
        {
            if (string.IsNullOrWhiteSpace(gameId))
                throw new ArgumentException("Game ID cannot be empty.", nameof(gameId));
            if (players <= 0) throw new ArgumentOutOfRangeException(nameof(players));
            if (difficulty < 1) throw new ArgumentOutOfRangeException(nameof(difficulty));
            if (humanPlayers == null) throw new ArgumentNullException(nameof(humanPlayers));

            int[] copiedHumans = humanPlayers.ToArray();
            if (copiedHumans.Any(player => player < 0 || player >= players))
                throw new ArgumentOutOfRangeException(
                    nameof(humanPlayers), "Human player is outside the player range.");
            if (copiedHumans.Distinct().Count() != copiedHumans.Length)
                throw new ArgumentException("Human players cannot contain duplicates.",
                    nameof(humanPlayers));
            Array.Sort(copiedHumans);

            var copiedOptions = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (options != null)
            {
                foreach (KeyValuePair<string, string> option in options)
                {
                    if (string.IsNullOrWhiteSpace(option.Key))
                        throw new ArgumentException("Option key cannot be empty.", nameof(options));
                    if (option.Value == null)
                        throw new ArgumentException("Option value cannot be null.", nameof(options));
                    copiedOptions.Add(option.Key, option.Value);
                }
            }

            GameId = gameId;
            Players = players;
            Seed = seed;
            Difficulty = difficulty;
            HumanPlayers = Array.AsReadOnly(copiedHumans);
            Options = new ReadOnlyDictionary<string, string>(copiedOptions);
        }
    }

    public sealed class SessionActionRecord
    {
        public int Actor { get; }
        public Action Action { get; }
        public int TurnAfter { get; }
        public int CurrentPlayerAfter { get; }
        public bool TerminalAfter { get; }

        public SessionActionRecord(int actor, Action action, int turnAfter,
            int currentPlayerAfter, bool terminalAfter)
        {
            if (actor < 0) throw new ArgumentOutOfRangeException(nameof(actor));
            if (string.IsNullOrWhiteSpace(action.Kind))
                throw new ArgumentException("Action kind cannot be empty.", nameof(action));
            if (turnAfter < 0) throw new ArgumentOutOfRangeException(nameof(turnAfter));
            if (currentPlayerAfter < 0)
                throw new ArgumentOutOfRangeException(nameof(currentPlayerAfter));
            Actor = actor;
            Action = action;
            TurnAfter = turnAfter;
            CurrentPlayerAfter = currentPlayerAfter;
            TerminalAfter = terminalAfter;
        }
    }

    public sealed class SessionArchive
    {
        public const int CurrentFormatVersion = 1;
        public const int CurrentRulesVersion = 1;

        public int FormatVersion { get; }
        public int RulesVersion { get; }
        public SessionConfiguration Configuration { get; }
        public IReadOnlyList<SessionActionRecord> Actions { get; }

        public SessionArchive(SessionConfiguration configuration,
            IEnumerable<SessionActionRecord> actions,
            int formatVersion = CurrentFormatVersion,
            int rulesVersion = CurrentRulesVersion)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if (formatVersion <= 0) throw new ArgumentOutOfRangeException(nameof(formatVersion));
            if (rulesVersion <= 0) throw new ArgumentOutOfRangeException(nameof(rulesVersion));

            SessionActionRecord[] copiedActions = actions.ToArray();
            if (copiedActions.Any(action => action == null))
                throw new ArgumentException("Actions cannot contain null.", nameof(actions));
            if (copiedActions.Any(action => action.Actor >= configuration.Players ||
                action.CurrentPlayerAfter >= configuration.Players))
                throw new ArgumentException(
                    "Action checkpoint is outside the player range.", nameof(actions));

            FormatVersion = formatVersion;
            RulesVersion = rulesVersion;
            Actions = Array.AsReadOnly(copiedActions);
        }
    }

    public sealed class ReplayCheckpoint
    {
        public int AppliedActions { get; }
        public int TurnCount { get; }
        public int CurrentPlayer { get; }
        public bool IsTerminal { get; }
        public GamePresentation? Presentation { get; }

        internal ReplayCheckpoint(int appliedActions, IGame game, int? viewer)
        {
            AppliedActions = appliedActions;
            TurnCount = game.TurnCount;
            CurrentPlayer = game.CurrentPlayer;
            IsTerminal = game.IsTerminal;
            Presentation = viewer.HasValue && game is IGamePresentationProvider provider
                ? provider.Present(viewer.Value)
                : null;
        }
    }

    public sealed class SessionReplayResult
    {
        public IGame Game { get; }
        public IReadOnlyList<ReplayCheckpoint> Checkpoints { get; }

        internal SessionReplayResult(IGame game, IEnumerable<ReplayCheckpoint> checkpoints)
        {
            Game = game;
            Checkpoints = Array.AsReadOnly(checkpoints.ToArray());
        }
    }

    public sealed class UnsupportedSessionVersionException : NotSupportedException
    {
        public UnsupportedSessionVersionException(string message) : base(message) { }
    }

    public sealed class ReplayDivergedException : InvalidOperationException
    {
        public int ActionIndex { get; }

        public ReplayDivergedException(int actionIndex, string message)
            : base("Replay diverged at action " + actionIndex + ": " + message)
        {
            ActionIndex = actionIndex;
        }
    }

    public sealed class SessionRecorder
    {
        private readonly SessionConfiguration configuration;
        private readonly IGame game;
        private readonly DeterministicRandom cpuRandom;
        private readonly HashSet<int> humanPlayers;
        private readonly List<SessionActionRecord> actions;

        public SessionRecorder(SessionConfiguration configuration, GameRegistry? registry = null)
            : this(
                configuration ?? throw new ArgumentNullException(nameof(configuration)),
                CreateGame(configuration, registry ?? BuiltInGames.Registry),
                CpuRandom(configuration),
                Array.Empty<SessionActionRecord>())
        {
        }

        internal SessionRecorder(SessionConfiguration configuration, IGame game,
            DeterministicRandom cpuRandom, IEnumerable<SessionActionRecord> existingActions)
        {
            this.configuration = configuration;
            this.game = game;
            this.cpuRandom = cpuRandom;
            humanPlayers = new HashSet<int>(configuration.HumanPlayers);
            actions = new List<SessionActionRecord>(existingActions);
        }

        public SessionConfiguration Configuration => configuration;
        public IGame Game => game;
        public SessionArchive Archive => new SessionArchive(configuration, actions);

        public void ApplyHumanAction(int actor, Action action)
        {
            if (!humanPlayers.Contains(actor))
                throw new InvalidOperationException("Player is not configured as human: " + actor);
            ApplyAndRecord(actor, action);
        }

        public Action ApplyCpuAction()
        {
            int actor = game.CurrentPlayer;
            if (humanPlayers.Contains(actor))
                throw new InvalidOperationException("Current player is configured as human: " + actor);
            IReadOnlyList<Action> legal = game.LegalActions(actor);
            Action selected = game.ChooseCpuAction(actor, cpuRandom, configuration.Difficulty);
            if (!legal.Contains(selected))
                throw new InvalidOperationException("CPU selected an action outside LegalActions().");
            ApplyAndRecord(actor, selected);
            return selected;
        }

        public static SessionRecorder Resume(SessionArchive archive, GameRegistry? registry = null)
        {
            ReplayState state = SessionReplayer.ReplayCore(
                archive, registry ?? BuiltInGames.Registry, viewer: null, captureCheckpoints: false);
            return new SessionRecorder(archive.Configuration, state.Game, state.CpuRandom,
                archive.Actions);
        }

        private void ApplyAndRecord(int actor, Action action)
        {
            if (game.IsTerminal) throw new InvalidOperationException("Game is already over.");
            if (actor != game.CurrentPlayer)
                throw new InvalidOperationException(
                    "Actor " + actor + " cannot act; current player is " + game.CurrentPlayer + ".");
            if (!game.LegalActions(actor).Contains(action))
                throw new InvalidOperationException("Action is outside LegalActions().");
            game.Apply(action);
            actions.Add(new SessionActionRecord(
                actor, action, game.TurnCount, game.CurrentPlayer, game.IsTerminal));
        }

        private static IGame CreateGame(SessionConfiguration configuration, GameRegistry registry)
        {
            if (!registry.Contains(configuration.GameId))
                throw new ArgumentException("Unknown game ID: " + configuration.GameId,
                    nameof(configuration));
            registry.ValidateCpuDifficulty(configuration.GameId, configuration.Difficulty);
            return registry.Create(configuration.GameId, configuration.Players,
                configuration.Seed, configuration.Options);
        }

        private static DeterministicRandom CpuRandom(SessionConfiguration configuration) =>
            new DeterministicRandom(unchecked(configuration.Seed + 99991L));
    }

    public static class SessionReplayer
    {
        public static SessionReplayResult Replay(SessionArchive archive, int? viewer = null,
            GameRegistry? registry = null)
        {
            ReplayState state = ReplayCore(
                archive, registry ?? BuiltInGames.Registry, viewer, captureCheckpoints: true);
            return new SessionReplayResult(state.Game, state.Checkpoints);
        }

        internal static ReplayState ReplayCore(SessionArchive archive, GameRegistry registry,
            int? viewer, bool captureCheckpoints)
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (archive.FormatVersion != SessionArchive.CurrentFormatVersion)
                throw new UnsupportedSessionVersionException(
                    "Unsupported session format version: " + archive.FormatVersion);
            if (archive.RulesVersion != SessionArchive.CurrentRulesVersion)
                throw new UnsupportedSessionVersionException(
                    "Unsupported session rules version: " + archive.RulesVersion);

            SessionConfiguration configuration = archive.Configuration;
            if (viewer.HasValue && (viewer.Value < 0 || viewer.Value >= configuration.Players))
                throw new ArgumentOutOfRangeException(nameof(viewer));
            if (!registry.Contains(configuration.GameId))
                throw new ReplayDivergedException(0, "game ID is not registered.");

            IGame game;
            try
            {
                registry.ValidateCpuDifficulty(configuration.GameId,
                    configuration.Difficulty);
                game = registry.Create(configuration.GameId, configuration.Players,
                    configuration.Seed, configuration.Options);
            }
            catch (Exception exception)
            {
                throw new ReplayDivergedException(0,
                    "initial game configuration is invalid: " + exception.Message);
            }
            var cpuRandom = new DeterministicRandom(unchecked(configuration.Seed + 99991L));
            var humans = new HashSet<int>(configuration.HumanPlayers);
            var checkpoints = new List<ReplayCheckpoint>();
            if (captureCheckpoints) checkpoints.Add(new ReplayCheckpoint(0, game, viewer));

            for (int index = 0; index < archive.Actions.Count; index++)
            {
                SessionActionRecord record = archive.Actions[index];
                if (game.IsTerminal)
                    throw new ReplayDivergedException(index, "archive continues after terminal state.");
                if (record.Actor != game.CurrentPlayer)
                    throw new ReplayDivergedException(index,
                        "recorded actor does not match CurrentPlayer.");

                IReadOnlyList<Action> legal;
                try
                {
                    legal = game.LegalActions(record.Actor);
                }
                catch (Exception exception)
                {
                    throw new ReplayDivergedException(index,
                        "LegalActions() failed: " + exception.Message);
                }
                if (!legal.Contains(record.Action))
                    throw new ReplayDivergedException(index, "recorded action is not legal.");

                if (!humans.Contains(record.Actor))
                {
                    Action selected;
                    try
                    {
                        selected = game.ChooseCpuAction(
                            record.Actor, cpuRandom, configuration.Difficulty);
                    }
                    catch (Exception exception)
                    {
                        throw new ReplayDivergedException(index,
                            "CPU selection failed: " + exception.Message);
                    }
                    if (selected != record.Action)
                        throw new ReplayDivergedException(index,
                            "CPU selection does not match the recorded action.");
                }

                try
                {
                    game.Apply(record.Action);
                }
                catch (Exception exception)
                {
                    throw new ReplayDivergedException(index,
                        "Apply() rejected the recorded action: " + exception.Message);
                }
                if (game.TurnCount != record.TurnAfter ||
                    game.CurrentPlayer != record.CurrentPlayerAfter ||
                    game.IsTerminal != record.TerminalAfter)
                    throw new ReplayDivergedException(index,
                        "post-action checkpoint does not match.");
                if (captureCheckpoints)
                    checkpoints.Add(new ReplayCheckpoint(index + 1, game, viewer));
            }

            return new ReplayState(game, cpuRandom, checkpoints);
        }
    }

    internal sealed class ReplayState
    {
        public IGame Game { get; }
        public DeterministicRandom CpuRandom { get; }
        public IReadOnlyList<ReplayCheckpoint> Checkpoints { get; }

        public ReplayState(IGame game, DeterministicRandom cpuRandom,
            IEnumerable<ReplayCheckpoint> checkpoints)
        {
            Game = game;
            CpuRandom = cpuRandom;
            Checkpoints = Array.AsReadOnly(checkpoints.ToArray());
        }
    }
}
