using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TrumpLab
{
    public sealed class SimulationReport
    {
        public string GameId { get; }
        public int Games { get; }
        public int Completed { get; }
        public double AverageTurns { get; }
        public IReadOnlyDictionary<int, int> WinnerCounts { get; }
        public int Draws { get; }
        public IReadOnlyList<string> Failures { get; }

        public SimulationReport(string gameId, int games, int completed, double averageTurns,
            IReadOnlyDictionary<int, int> winnerCounts, int draws, IReadOnlyList<string> failures)
        {
            GameId = gameId; Games = games; Completed = completed;
            AverageTurns = averageTurns; WinnerCounts = winnerCounts;
            Draws = draws; Failures = failures;
        }
    }

    public sealed class ComparisonRow
    {
        public const double PerformanceTargetMillisecondsPerHundredGames = 60000;

        public SimulationReport Simulation { get; }
        public double ElapsedMilliseconds { get; }
        public double MillisecondsPerHundredGames { get; }
        public bool MeetsPerformanceTarget { get; }

        public ComparisonRow(SimulationReport simulation, double elapsedMilliseconds)
        {
            Simulation = simulation;
            ElapsedMilliseconds = elapsedMilliseconds;
            MillisecondsPerHundredGames = simulation.Games == 0 ? 0 :
                elapsedMilliseconds * 100 / simulation.Games;
            MeetsPerformanceTarget = MillisecondsPerHundredGames <=
                PerformanceTargetMillisecondsPerHundredGames;
        }
    }

    public static class Simulator
    {
        public const int SupportedDifficulty = CpuDifficulties.Standard;

        public static void ValidateDifficulty(int difficulty)
        {
            if (difficulty != SupportedDifficulty)
                throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty,
                    "Only CPU difficulty 1 is currently supported.");
        }

        public static CpuDifficultyInfo ValidateDifficulty(string gameId, int difficulty) =>
            BuiltInGames.Registry.ValidateCpuDifficulty(gameId, difficulty);

        public static GameResult RunGame(IGame game, long policySeed, int turnLimit = 50000,
            int difficulty = SupportedDifficulty)
        {
            if (BuiltInGames.Registry.Contains(game.GameId))
                ValidateDifficulty(game.GameId, difficulty);
            else
                CpuDifficulties.Get(difficulty);
            var rng = new DeterministicRandom(policySeed);
            while (!game.IsTerminal)
            {
                if (game.TurnCount >= turnLimit)
                    throw new InvalidOperationException("External turn limit " + turnLimit +
                        ": player=" + game.CurrentPlayer + " state=" +
                        game.View(game.CurrentPlayer).Replace('\n', ' '));
                IReadOnlyList<Action> actions = game.LegalActions();
                if (actions.Count == 0)
                    throw new InvalidOperationException(
                        "Non-terminal state has no action: player=" + game.CurrentPlayer +
                        " state=" + game.View(game.CurrentPlayer).Replace('\n', ' '));
                Action action = game.ChooseCpuAction(game.CurrentPlayer, rng, difficulty);
                if (!actions.Contains(action))
                    throw new InvalidOperationException("CPU chose illegal action: " + action);
                game.Apply(action);
            }
            return game.Result();
        }

        public static SimulationReport Simulate(string gameId, int games, int? players = null,
            long seed = 1, IReadOnlyDictionary<string, string>? options = null,
            int difficulty = SupportedDifficulty)
        {
            ValidateDifficulty(gameId, difficulty);
            if (games <= 0) throw new ArgumentOutOfRangeException(nameof(games));
            var winners = new Dictionary<int, int>();
            var failures = new List<string>();
            long turns = 0;
            int draws = 0;
            int completed = 0;
            for (int index = 0; index < games; index++)
            {
                try
                {
                    IGame game = BuiltInGames.Registry.Create(
                        gameId, players, seed + index, options);
                    GameResult result = RunGame(game, seed * 100003 + index,
                        difficulty: difficulty);
                    completed++;
                    turns += result.Turns;
                    if (result.Winners.Count == 0) draws++;
                    foreach (int winner in result.Winners)
                        winners[winner] = winners.TryGetValue(winner, out int value) ? value + 1 : 1;
                }
                catch (Exception exception)
                {
                    failures.Add($"seed={seed + index}: {exception.GetType().Name}: {exception.Message}");
                }
            }
            return new SimulationReport(gameId, games, completed,
                completed == 0 ? 0 : (double)turns / completed,
                winners, draws, failures);
        }

        public static IReadOnlyList<ComparisonRow> Compare(IEnumerable<string> gameIds,
            int games = 100, long seed = 1, int difficulty = SupportedDifficulty)
        {
            if (gameIds == null) throw new ArgumentNullException(nameof(gameIds));
            var rows = new List<ComparisonRow>();
            foreach (string gameId in gameIds.Distinct(StringComparer.Ordinal))
            {
                ValidateDifficulty(gameId, difficulty);
                var stopwatch = Stopwatch.StartNew();
                SimulationReport simulation = Simulate(gameId, games, seed: seed,
                    difficulty: difficulty);
                stopwatch.Stop();
                rows.Add(new ComparisonRow(simulation, stopwatch.Elapsed.TotalMilliseconds));
            }
            return rows;
        }
    }
}
