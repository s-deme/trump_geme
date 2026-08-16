using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace TrumpLab.Tests
{
    public sealed class CpuDifficultyEvaluationTests
    {
        private const long FirstSeed = 44000;
        private const int SeedCount = 200;
        private const int TurnLimit = 50000;
        private const double RequiredStrongerScoreRate = 0.53;
        private const double ProductSuiteTargetMilliseconds = 15000;
        private const double DebugSuiteLimitMilliseconds = 30000;
        private const double HardP95LimitMilliseconds = 5;
        private const double HardMaximumLimitMilliseconds = 25;
        private const string ExpectedStableSignature =
            "standard>easy|games=400|seat0=193-7-0|seat1=185-15-0|turns=22082;" +
            "hard>standard|games=400|seat0=128-72-0|seat1=100-100-0|turns=11414;" +
            "failures=0";

        [Test]
        [Category("BroadSimulation")]
        [Timeout(30000)]
        public void FixedSelfPlayCorpusMeetsStrengthAndSuiteBudgets()
        {
            var stopwatch = Stopwatch.StartNew();
            EvaluationReport report = Evaluate();
            stopwatch.Stop();

            TestContext.Progress.WriteLine(report.Render(stopwatch.Elapsed.TotalMilliseconds));
            Assert.That(report.Failures, Is.Empty);
            Assert.That(report.Pairs.Sum(pair => pair.Games), Is.EqualTo(800));
            Assert.That(report.Pairs.All(pair =>
                    pair.StrongerScoreRate >= RequiredStrongerScoreRate),
                Is.True, report.StableSignature);
            Assert.That(report.StableSignature, Is.EqualTo(ExpectedStableSignature),
                "Fixed seeds, seat swaps, and policy-specific random streams must " +
                "produce the same deterministic report.");
            Assert.That(stopwatch.Elapsed.TotalMilliseconds,
                Is.LessThanOrEqualTo(DebugSuiteLimitMilliseconds),
                "CPU evaluation exceeded the shared Debug/CI hard limit.");
        }

        [Test]
        [Category("BroadSimulation")]
        [Timeout(30000)]
        public void HardPolicyMeetsPerMoveBudgetOnFixedObservations()
        {
            const int repetitionsPerSample = 20;
            IGame[] games = Enumerable.Range(0, SeedCount)
                .Select(index => BuiltInGames.Registry.Create(
                    "crazy_eights", 2, FirstSeed + 1000 + index))
                .ToArray();
            DeterministicRandom[] randoms = Enumerable.Range(0, SeedCount)
                .Select(index => new DeterministicRandom(
                    PolicySeed(FirstSeed + 1000 + index, CpuDifficulties.Hard)))
                .ToArray();

            for (int index = 0; index < games.Length; index++)
                games[index].ChooseCpuAction(
                    games[index].CurrentPlayer, randoms[index], CpuDifficulties.Hard);

            var samples = new List<double>(games.Length);
            for (int index = 0; index < games.Length; index++)
            {
                long started = Stopwatch.GetTimestamp();
                for (int repetition = 0; repetition < repetitionsPerSample; repetition++)
                    games[index].ChooseCpuAction(
                        games[index].CurrentPlayer, randoms[index], CpuDifficulties.Hard);
                long elapsed = Stopwatch.GetTimestamp() - started;
                samples.Add(elapsed * 1000.0 / Stopwatch.Frequency /
                    repetitionsPerSample);
            }

            samples.Sort();
            double p95 = samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];
            double maximum = samples[samples.Count - 1];
            TestContext.Progress.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Hard policy fixed observations: samples={0}, p95={1:F3} ms, " +
                "max={2:F3} ms, budget p95<={3:F0} ms/max<={4:F0} ms",
                samples.Count, p95, maximum, HardP95LimitMilliseconds,
                HardMaximumLimitMilliseconds));

            Assert.That(p95, Is.LessThanOrEqualTo(HardP95LimitMilliseconds));
            Assert.That(maximum, Is.LessThanOrEqualTo(HardMaximumLimitMilliseconds));
        }

        private static EvaluationReport Evaluate()
        {
            var failures = new List<string>();
            PairReport[] pairs =
            {
                EvaluatePair(
                    CpuDifficulties.Standard, CpuDifficulties.Easy, failures),
                EvaluatePair(
                    CpuDifficulties.Hard, CpuDifficulties.Standard, failures)
            };
            return new EvaluationReport(pairs, failures);
        }

        private static PairReport EvaluatePair(
            int strongerDifficulty, int weakerDifficulty, ICollection<string> failures)
        {
            var pair = new PairReport(strongerDifficulty, weakerDifficulty);
            for (long seed = FirstSeed; seed < FirstSeed + SeedCount; seed++)
            {
                RunMatch(pair, seed, strongerSeat: 0, failures);
                RunMatch(pair, seed, strongerSeat: 1, failures);
            }
            return pair;
        }

        private static void RunMatch(
            PairReport pair, long seed, int strongerSeat, ICollection<string> failures)
        {
            int weakerSeat = 1 - strongerSeat;
            int[] difficulties = new int[2];
            difficulties[strongerSeat] = pair.StrongerDifficulty;
            difficulties[weakerSeat] = pair.WeakerDifficulty;
            var randoms = new Dictionary<int, DeterministicRandom>
            {
                [pair.StrongerDifficulty] = new DeterministicRandom(
                    PolicySeed(seed, pair.StrongerDifficulty)),
                [pair.WeakerDifficulty] = new DeterministicRandom(
                    PolicySeed(seed, pair.WeakerDifficulty))
            };

            try
            {
                IGame game = BuiltInGames.Registry.Create("crazy_eights", 2, seed);
                while (!game.IsTerminal)
                {
                    if (game.TurnCount >= TurnLimit)
                        throw new InvalidOperationException("turn limit exceeded");
                    int player = game.CurrentPlayer;
                    IReadOnlyList<Action> legal = game.LegalActions(player);
                    int difficulty = difficulties[player];
                    Action selected = game.ChooseCpuAction(
                        player, randoms[difficulty], difficulty);
                    if (!legal.Contains(selected))
                        throw new InvalidOperationException(
                            "CPU chose illegal action: " + selected);
                    game.Apply(selected);
                }

                GameResult result = game.Result();
                if (result.Winners.Count > 1)
                    throw new InvalidOperationException("multiple winners are not scoreable");
                pair.Record(strongerSeat,
                    result.Winners.Count == 0 ? (int?)null : result.Winners[0],
                    result.Turns);
            }
            catch (Exception exception)
            {
                failures.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0}>{1} seed={2} strongerSeat={3}: {4}: {5}",
                    CpuDifficulties.Get(pair.StrongerDifficulty).Key,
                    CpuDifficulties.Get(pair.WeakerDifficulty).Key,
                    seed, strongerSeat, exception.GetType().Name, exception.Message));
            }
        }

        private static long PolicySeed(long gameSeed, int difficulty) =>
            unchecked(gameSeed * 100003L + difficulty * 99991L);

        private sealed class PairReport
        {
            private readonly int[] strongerWinsBySeat = new int[2];
            private readonly int[] weakerWinsByStrongerSeat = new int[2];
            private readonly int[] drawsByStrongerSeat = new int[2];

            public int StrongerDifficulty { get; }
            public int WeakerDifficulty { get; }
            public int Games => strongerWinsBySeat.Sum() +
                weakerWinsByStrongerSeat.Sum() + drawsByStrongerSeat.Sum();
            public int StrongerWins => strongerWinsBySeat.Sum();
            public int Draws => drawsByStrongerSeat.Sum();
            public int TotalTurns { get; private set; }
            public double StrongerScoreRate => Games == 0 ? 0 :
                (StrongerWins + Draws * 0.5) / Games;

            public PairReport(int strongerDifficulty, int weakerDifficulty)
            {
                StrongerDifficulty = strongerDifficulty;
                WeakerDifficulty = weakerDifficulty;
            }

            public void Record(int strongerSeat, int? winner, int turns)
            {
                if (!winner.HasValue) drawsByStrongerSeat[strongerSeat]++;
                else if (winner.Value == strongerSeat) strongerWinsBySeat[strongerSeat]++;
                else weakerWinsByStrongerSeat[strongerSeat]++;
                TotalTurns += turns;
            }

            public string StableSignature => string.Format(CultureInfo.InvariantCulture,
                "{0}>{1}|games={2}|seat0={3}-{4}-{5}|seat1={6}-{7}-{8}|turns={9}",
                CpuDifficulties.Get(StrongerDifficulty).Key,
                CpuDifficulties.Get(WeakerDifficulty).Key,
                Games,
                strongerWinsBySeat[0], weakerWinsByStrongerSeat[0],
                drawsByStrongerSeat[0],
                strongerWinsBySeat[1], weakerWinsByStrongerSeat[1],
                drawsByStrongerSeat[1],
                TotalTurns);

            public string Render() => string.Format(CultureInfo.InvariantCulture,
                "{0}>{1}: games={2}, stronger seat0 W/L/D={3}/{4}/{5}, " +
                "stronger seat1 W/L/D={6}/{7}/{8}, average turns={9:F3}, " +
                "stronger score={10:P2}",
                CpuDifficulties.Get(StrongerDifficulty).Key,
                CpuDifficulties.Get(WeakerDifficulty).Key,
                Games,
                strongerWinsBySeat[0], weakerWinsByStrongerSeat[0],
                drawsByStrongerSeat[0],
                strongerWinsBySeat[1], weakerWinsByStrongerSeat[1],
                drawsByStrongerSeat[1],
                Games == 0 ? 0 : (double)TotalTurns / Games,
                StrongerScoreRate);
        }

        private sealed class EvaluationReport
        {
            public IReadOnlyList<PairReport> Pairs { get; }
            public IReadOnlyList<string> Failures { get; }
            public string StableSignature =>
                string.Join(";", Pairs.Select(pair => pair.StableSignature)) +
                ";failures=" + Failures.Count;

            public EvaluationReport(
                IEnumerable<PairReport> pairs, IEnumerable<string> failures)
            {
                Pairs = Array.AsReadOnly(pairs.ToArray());
                Failures = Array.AsReadOnly(failures.ToArray());
            }

            public string Render(double elapsedMilliseconds)
            {
                var lines = new List<string>
                {
                    string.Format(CultureInfo.InvariantCulture,
                        "Crazy Eights CPU evaluation: seeds={0}-{1}, matches={2}, " +
                        "elapsed={3:F3} ms, product target={4}, debug limit={5}",
                        FirstSeed, FirstSeed + SeedCount - 1,
                        Pairs.Sum(pair => pair.Games), elapsedMilliseconds,
                        elapsedMilliseconds <= ProductSuiteTargetMilliseconds
                            ? "pass" : "fail",
                        elapsedMilliseconds <= DebugSuiteLimitMilliseconds
                            ? "pass" : "fail")
                };
                lines.AddRange(Pairs.Select(pair => pair.Render()));
                lines.Add("failures=" + Failures.Count);
                lines.Add("stable=" + StableSignature);
                return string.Join(Environment.NewLine, lines);
            }
        }
    }
}
