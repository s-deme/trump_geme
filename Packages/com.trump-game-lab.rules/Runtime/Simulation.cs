using System;
using System.Collections.Generic;
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

    public static class Simulator
    {
        public static GameResult RunGame(IGame game, long policySeed, int turnLimit = 50000)
        {
            var rng = new DeterministicRandom(policySeed);
            while (!game.IsTerminal)
            {
                if (game.TurnCount >= turnLimit)
                    throw new InvalidOperationException("External turn limit " + turnLimit);
                IReadOnlyList<Action> actions = game.LegalActions();
                if (actions.Count == 0)
                    throw new InvalidOperationException(
                        "Non-terminal state has no action: player=" + game.CurrentPlayer +
                        " state=" + game.View(game.CurrentPlayer).Replace('\n', ' '));
                Action action = game.ChooseCpuAction(game.CurrentPlayer, rng);
                if (!actions.Contains(action))
                    throw new InvalidOperationException("CPU chose illegal action: " + action);
                game.Apply(action);
            }
            return game.Result();
        }

        public static SimulationReport Simulate(string gameId, int games, int? players = null,
            long seed = 1, IReadOnlyDictionary<string, string>? options = null)
        {
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
                    GameResult result = RunGame(game, seed * 100003 + index);
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
    }
}
