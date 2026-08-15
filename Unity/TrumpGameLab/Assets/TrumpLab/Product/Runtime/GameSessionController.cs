#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Product
{
    public enum MatchSessionState
    {
        Starting,
        AwaitingHuman,
        Applying,
        WaitingForCpu,
        Finished,
        Faulted
    }

    public sealed class GameSessionController
    {
        private const int HumanPlayer = 0;
        private readonly IGame game;
        private readonly IGamePresentationProvider provider;
        private readonly DeterministicRandom cpuRandom;
        private readonly int difficulty;
        private GamePresentation? snapshot;

        public GameSessionController(long seed, int wildRank = 8, int difficulty = 1)
            : this(
                BuiltInGames.Registry.Create(
                    "crazy_eights",
                    players: 2,
                    seed: seed,
                    options: new Dictionary<string, string>
                    {
                        ["wild_rank"] = wildRank.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    }),
                new DeterministicRandom(seed + 99991),
                difficulty)
        {
        }

        public GameSessionController(IGame configuredGame,
            DeterministicRandom configuredCpuRandom, int difficulty = 1)
        {
            game = configuredGame ?? throw new ArgumentNullException(nameof(configuredGame));
            provider = configuredGame as IGamePresentationProvider ??
                throw new ArgumentException(
                    "Game must provide structured presentation.", nameof(configuredGame));
            cpuRandom = configuredCpuRandom ?? throw new ArgumentNullException(nameof(configuredCpuRandom));
            if (game.GameId != "crazy_eights" || game.Players != 2)
                throw new ArgumentException(
                    "The product session requires a two-player Crazy Eights game.",
                    nameof(configuredGame));
            if (difficulty < 1) throw new ArgumentOutOfRangeException(nameof(difficulty));
            this.difficulty = difficulty;
        }

        public IGame Game => game;
        public MatchSessionState State { get; private set; } = MatchSessionState.Starting;
        public GamePresentation Snapshot => snapshot ?? throw new InvalidOperationException(
            "The session has not produced its first snapshot.");
        public string? FaultMessage { get; private set; }

        public event System.Action<GamePresentation>? SnapshotChanged;
        public event System.Action<GameResultPresentation>? Finished;
        public event System.Action<string>? Faulted;

        public void Begin()
        {
            if (State != MatchSessionState.Starting)
                throw new InvalidOperationException("Session can only begin once.");
            TryRefresh();
        }

        public bool TryApplyHumanAction(string actionId)
        {
            if (State != MatchSessionState.AwaitingHuman || string.IsNullOrWhiteSpace(actionId))
                return false;
            ActionPresentation? selected = Snapshot.Actions.SingleOrDefault(action =>
                string.Equals(action.Id, actionId, StringComparison.Ordinal));
            if (selected == null) return false;
            State = MatchSessionState.Applying;
            return TryApply(selected.Action);
        }

        public bool TryApplyCpuAction()
        {
            if (State != MatchSessionState.WaitingForCpu) return false;
            try
            {
                int player = game.CurrentPlayer;
                if (player == HumanPlayer)
                    throw new InvalidOperationException("CPU turn cannot run for the human player.");
                IReadOnlyList<TrumpLab.Action> legalActions = game.LegalActions(player);
                TrumpLab.Action selected = game.ChooseCpuAction(player, cpuRandom, difficulty);
                if (!legalActions.Contains(selected))
                    throw new InvalidOperationException("CPU selected an action outside LegalActions().");
                State = MatchSessionState.Applying;
                game.Apply(selected);
                return TryRefresh();
            }
            catch (Exception exception)
            {
                return Fail(exception);
            }
        }

        private bool TryApply(TrumpLab.Action action)
        {
            try
            {
                game.Apply(action);
                return TryRefresh();
            }
            catch (Exception exception)
            {
                return Fail(exception);
            }
        }

        private bool TryRefresh()
        {
            try
            {
                GamePresentation next = provider.Present(HumanPlayer);
                if (next.CurrentPlayer != game.CurrentPlayer || next.IsTerminal != game.IsTerminal)
                    throw new InvalidOperationException(
                        "Structured snapshot does not match the active game state.");
                snapshot = next;
                if (next.IsTerminal)
                {
                    State = MatchSessionState.Finished;
                    SnapshotChanged?.Invoke(next);
                    Finished?.Invoke(next.Result ?? throw new InvalidOperationException(
                        "Terminal snapshot is missing its result."));
                }
                else
                {
                    State = next.CurrentPlayer == HumanPlayer
                        ? MatchSessionState.AwaitingHuman
                        : MatchSessionState.WaitingForCpu;
                    SnapshotChanged?.Invoke(next);
                }
                return true;
            }
            catch (Exception exception)
            {
                return Fail(exception);
            }
        }

        private bool Fail(Exception exception)
        {
            if (State == MatchSessionState.Faulted) return false;
            State = MatchSessionState.Faulted;
            FaultMessage = exception.GetType().Name + ": " + exception.Message;
            Faulted?.Invoke(FaultMessage);
            return false;
        }
    }
}
