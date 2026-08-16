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
        private readonly SessionRecorder recorder;
        private readonly IGamePresentationProvider provider;
        private GamePresentation? snapshot;

        public GameSessionController(long seed, int wildRank = 8, int difficulty = 1)
            : this(new SessionRecorder(new SessionConfiguration(
                "crazy_eights",
                players: 2,
                seed: seed,
                difficulty: difficulty,
                humanPlayers: new[] { HumanPlayer },
                options: new Dictionary<string, string>
                {
                    ["wild_rank"] = wildRank.ToString(System.Globalization.CultureInfo.InvariantCulture)
                })))
        {
        }

        public GameSessionController(SessionArchive archive)
            : this(SessionRecorder.Resume(archive ?? throw new ArgumentNullException(nameof(archive))))
        {
        }

        public GameSessionController(SessionRecorder configuredRecorder)
        {
            recorder = configuredRecorder ?? throw new ArgumentNullException(nameof(configuredRecorder));
            IGame game = recorder.Game;
            provider = game as IGamePresentationProvider ??
                throw new ArgumentException(
                    "Game must provide structured presentation.", nameof(configuredRecorder));
            if (game.GameId != "crazy_eights" || game.Players != 2)
                throw new ArgumentException(
                    "The product session requires a two-player Crazy Eights game.",
                    nameof(configuredRecorder));
            if (!recorder.Configuration.HumanPlayers.SequenceEqual(new[] { HumanPlayer }))
                throw new ArgumentException(
                    "The product session requires player 1 to be the only human player.",
                    nameof(configuredRecorder));
        }

        public IGame Game => recorder.Game;
        public SessionArchive Archive => recorder.Archive;
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
                int player = Game.CurrentPlayer;
                if (player == HumanPlayer)
                    throw new InvalidOperationException("CPU turn cannot run for the human player.");
                State = MatchSessionState.Applying;
                recorder.ApplyCpuAction();
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
                recorder.ApplyHumanAction(HumanPlayer, action);
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
                if (next.CurrentPlayer != Game.CurrentPlayer || next.IsTerminal != Game.IsTerminal)
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
