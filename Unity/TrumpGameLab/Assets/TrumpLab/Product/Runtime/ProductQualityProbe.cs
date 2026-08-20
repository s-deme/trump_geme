#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace TrumpLab.Product
{
    internal static class ProductQualityProbeBootstrap
    {
        private const string EnabledArgument = "-trumplab-quality-probe";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateWhenRequested()
        {
            if (!Environment.GetCommandLineArgs().Any(argument =>
                    string.Equals(argument, EnabledArgument,
                        StringComparison.OrdinalIgnoreCase)))
                return;
            var host = new GameObject("ProductQualityProbe");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ProductQualityProbe>();
        }
    }

    [DisallowMultipleComponent]
    public sealed class ProductQualityProbe : MonoBehaviour
    {
        private const string ReportArgument = "-trumplab-quality-report";
        private const string ModeArgument = "-trumplab-quality-mode";
        private const string LaunchTicksArgument = "-trumplab-quality-launch-ticks";
        private const string ScreenSecondsArgument = "-trumplab-quality-screen-seconds";
        private const string SoakSecondsArgument = "-trumplab-quality-soak-seconds";
        private const int AutomatedGameCount = 100;
        private const int CpuSamplesPerDifficulty = 100;
        private const int MaximumGameActions = 10000;
        private const int MaximumArchiveActions = 10000;
        private const double StartupBudgetSeconds = 5d;
        private const long MiB = 1024L * 1024L;

        private readonly List<ProductQualityMetric> metrics =
            new List<ProductQualityMetric>();
        private readonly List<string> failures = new List<string>();
        private ProductQualityProbeReport report = new ProductQualityProbeReport();
        private string reportPath = string.Empty;
        private int errorLogs;
        private int exceptionLogs;

        private IEnumerator Start()
        {
            IEnumerator routine;
            try
            {
                Configure();
                routine = Run();
            }
            catch (Exception exception)
            {
                FailAndQuit(exception);
                yield break;
            }

            while (true)
            {
                object? current;
                try
                {
                    if (!routine.MoveNext()) yield break;
                    current = routine.Current;
                }
                catch (Exception exception)
                {
                    FailAndQuit(exception);
                    yield break;
                }
                yield return current;
            }
        }

        private void Configure()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            reportPath = RequiredArgument(arguments, ReportArgument);
            string mode = OptionalArgument(arguments, ModeArgument, "full");
            if (mode != "startup" && mode != "full" && mode != "allocation")
                throw new ArgumentException(
                    "Quality mode must be startup, full, or allocation.");

            report = new ProductQualityProbeReport
            {
                Mode = mode,
                StartedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                UnityVersion = Application.unityVersion,
                OperatingSystem = SystemInfo.operatingSystem,
                ProcessorType = SystemInfo.processorType,
                ProcessorCount = SystemInfo.processorCount,
                SystemMemoryMiB = SystemInfo.systemMemorySize,
                GraphicsDeviceName = SystemInfo.graphicsDeviceName,
                GraphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                VSyncCount = QualitySettings.vSyncCount,
                TargetFrameRate = 60,
                ScreenSampleSeconds = PositiveDouble(arguments, ScreenSecondsArgument, 60d),
                SoakSeconds = PositiveDouble(arguments, SoakSecondsArgument, 3600d)
            };
            using (Process process = Process.GetCurrentProcess())
            {
                if (!GetProcessAffinityMask(process.Handle,
                        out UIntPtr processAffinity, out _))
                    throw new InvalidOperationException(
                        "Windows processor-affinity mask is unavailable (error " +
                        Marshal.GetLastWin32Error() + ").");
                long affinity = unchecked((long)processAffinity.ToUInt64());
                if (affinity == 0L)
                    throw new InvalidOperationException(
                        "Windows processor-affinity mask is empty.");
                report.ProcessorAffinityMask = "0x" +
                    affinity.ToString("x", CultureInfo.InvariantCulture);
                report.EffectiveProcessorCount = CountSetBits(affinity);
            }
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Application.logMessageReceived += HandleLog;
            WriteReport("running");
        }

        private IEnumerator Run()
        {
            ProductAppController? controller = null;
            double deadline = Time.realtimeSinceStartupAsDouble + 15d;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                controller = FindFirstObjectByType<ProductAppController>();
                if (controller != null && controller.Router.Current == ScreenId.Title &&
                    HasUsableFocus())
                    break;
                yield return null;
            }
            if (controller == null || controller.Router.Current != ScreenId.Title ||
                !HasUsableFocus())
                throw new InvalidOperationException(
                    "Title did not become interactive within 15 seconds.");

            long launchTicks = LongArgument(Environment.GetCommandLineArgs(),
                LaunchTicksArgument, DateTime.UtcNow.Ticks);
            report.StartupSeconds = Math.Max(0d,
                TimeSpan.FromTicks(DateTime.UtcNow.Ticks - launchTicks).TotalSeconds);
            if (report.Mode != "allocation")
                AddMetric(ProductQualityStatistics.Metric("PERF-01", "title-interactive",
                    "seconds", new[] { report.StartupSeconds },
                    maximumBudget: StartupBudgetSeconds));
            WriteReport("running");

            if (report.Mode == "startup")
            {
                FinishAndQuit();
                yield break;
            }
            if (report.Mode == "allocation")
            {
                IEnumerator allocation = MeasureAllocations(controller);
                while (allocation.MoveNext()) yield return allocation.Current;
                FinishAndQuit();
                yield break;
            }

            yield return null;
            RunRuleAndStorageBenchmarks();
            WriteReport("running");

            IEnumerator input = MeasureInputLatency(controller);
            while (input.MoveNext()) yield return input.Current;

            IEnumerator screens = MeasureScreens(controller);
            while (screens.MoveNext()) yield return screens.Current;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            for (int frame = 0; frame < 60; frame++) yield return null;

            report.WarmPrivateBytes = PrivateBytes();
            report.PeakPrivateBytes = report.WarmPrivateBytes;
            report.InitialGameObjects = CountSceneGameObjects();
            report.PeakGameObjects = report.InitialGameObjects;

            IEnumerator soak = RunSoak(controller);
            while (soak.MoveNext()) yield return soak.Current;

            report.FinalGameObjects = CountSceneGameObjects();
            report.PeakGameObjects = Math.Max(report.PeakGameObjects,
                report.FinalGameObjects);
            report.PrivateByteIncrease = Math.Max(0L,
                report.PeakPrivateBytes - report.WarmPrivateBytes);
            AddMetric(ProductQualityStatistics.Metric("PERF-06", "warm-baseline",
                "MiB", new[] { report.WarmPrivateBytes / (double)MiB },
                maximumBudget: 512d));
            AddMetric(ProductQualityStatistics.Metric("PERF-06", "soak-peak",
                "MiB", new[] { report.PeakPrivateBytes / (double)MiB },
                maximumBudget: 768d));
            AddMetric(ProductQualityStatistics.Metric("PERF-06", "soak-growth",
                "MiB", new[] { report.PrivateByteIncrease / (double)MiB },
                maximumBudget: 64d));

            if (report.FinalGameObjects > report.InitialGameObjects ||
                report.PeakGameObjects > report.InitialGameObjects + 4)
                AddFailure("PERF-08 GameObject count grew during the soak.");
            if (errorLogs != 0 || exceptionLogs != 0)
                AddFailure("REL-03/PERF-08 emitted error or exception logs.");
            MatchScreen match = (MatchScreen)controller.Router.Get(ScreenId.Match);
            if (match.IsPresentationLocked || match.IsContextHelpVisible ||
                controller.PresentationController.IsTransitioning)
                AddFailure("REL-03/PERF-08 left an input or presentation lock active.");
            if (report.AutomatedGames < AutomatedGameCount || report.SoakGames <= 0 ||
                report.SoakActions <= 0)
                AddFailure("PERF-08 did not complete the required automated activity.");

            FinishAndQuit();
        }

        private IEnumerator MeasureInputLatency(ProductAppController controller)
        {
            ScreenId[] routes =
            {
                ScreenId.Title,
                ScreenId.ProductSettings,
                ScreenId.HowToPlay,
                ScreenId.Match,
                ScreenId.Result
            };
            var samples = new List<double>(100);
            for (int index = 0; index < 100; index++)
            {
                ScreenId target = routes[index % routes.Length];
                long started = Stopwatch.GetTimestamp();
                controller.Router.Show(target);
                yield return null;
                samples.Add(ElapsedMilliseconds(started));
                if (controller.Router.Current != target || !HasUsableFocus())
                    throw new InvalidOperationException(
                        "A routed input did not produce a visible focus target.");
            }
            AddMetric(ProductQualityStatistics.Metric("PERF-03", "screen-and-focus",
                "milliseconds", samples, p95Budget: 100d, maximumBudget: 200d));
            IEnumerator settle = WaitForPresentation(controller, 3d);
            while (settle.MoveNext()) yield return settle.Current;
            WriteReport("running");
        }

        private IEnumerator MeasureScreens(ProductAppController controller)
        {
            var screens = new[]
            {
                new KeyValuePair<ScreenId, string>(ScreenId.Title, "Title"),
                new KeyValuePair<ScreenId, string>(ScreenId.ProductSettings, "Settings"),
                new KeyValuePair<ScreenId, string>(ScreenId.Match, "Match"),
                new KeyValuePair<ScreenId, string>(ScreenId.HowToPlay, "HowToPlay"),
                new KeyValuePair<ScreenId, string>(ScreenId.Result, "Result")
            };

            foreach (KeyValuePair<ScreenId, string> screen in screens)
            {
                controller.Router.Show(screen.Key);
                IEnumerator settle = WaitForPresentation(controller, 3d);
                while (settle.MoveNext()) yield return settle.Current;
                for (int frame = 0; frame < 30; frame++) yield return null;

                int expectedFrames = Math.Max(256,
                    (int)Math.Ceiling(report.ScreenSampleSeconds * 120d));
                var frameMilliseconds = new List<double>(expectedFrames);
                ProfilerRecorder mainThread = ProfilerRecorder.StartNew(
                    ProfilerCategory.Internal, "CPU Main Thread Frame Time", 1);
                try
                {
                    double end = Time.realtimeSinceStartupAsDouble +
                        report.ScreenSampleSeconds;
                    while (Time.realtimeSinceStartupAsDouble < end)
                    {
                        yield return null;
                        if (mainThread.Valid && mainThread.LastValue > 0L)
                            frameMilliseconds.Add(mainThread.LastValue / 1000000d);
                    }
                }
                finally
                {
                    mainThread.Dispose();
                }

                if (frameMilliseconds.Count == 0)
                    throw new InvalidOperationException(
                        "The release Player did not expose the Main Thread profiler counter.");
                AddMetric(ProductQualityStatistics.Metric("PERF-02", screen.Value,
                    "milliseconds", frameMilliseconds, p95Budget: 16.67d,
                    p99Budget: 33.33d, maximumBudget: 99.999999d));
                WriteReport("running");
            }
        }

        private void RunRuleAndStorageBenchmarks()
        {
            var archives = new List<SessionArchive>(AutomatedGameCount);
            for (int index = 0; index < AutomatedGameCount; index++)
            {
                archives.Add(PlayCrazyEights(1000L + index,
                    CpuDifficulties.Standard, null));
            }
            report.AutomatedGames = archives.Count;

            foreach (CpuDifficultyInfo difficulty in CpuDifficulties.ProductOrder)
            {
                var cpuMilliseconds = new List<double>(CpuSamplesPerDifficulty);
                for (long seed = 1; cpuMilliseconds.Count < CpuSamplesPerDifficulty;
                     seed++)
                {
                    PlayCrazyEights(100000L * difficulty.Id + seed, difficulty.Id,
                        cpuMilliseconds);
                    if (seed > 1000)
                        throw new InvalidOperationException(
                            "Could not collect the required CPU samples.");
                }
                AddMetric(ProductQualityStatistics.Metric("PERF-04", difficulty.Key,
                    "milliseconds", cpuMilliseconds.Take(CpuSamplesPerDifficulty),
                    p95Budget: 50d, maximumBudget: 100d));
            }

            RunStorageBenchmarks(archives);
            RunLargeArchiveBenchmark();
        }

        private static SessionArchive PlayCrazyEights(long seed, int difficulty,
            List<double>? cpuMilliseconds)
        {
            var session = new GameSessionController(seed, wildRank: 8, difficulty);
            session.Begin();
            int guard = 0;
            while (session.State != MatchSessionState.Finished &&
                   guard++ < MaximumGameActions)
            {
                int beforeCount = session.Archive.Actions.Count;
                bool applied;
                if (session.State == MatchSessionState.AwaitingHuman)
                {
                    applied = session.TryApplyHumanAction(session.Snapshot.Actions[0].Id);
                }
                else if (session.State == MatchSessionState.WaitingForCpu)
                {
                    long started = Stopwatch.GetTimestamp();
                    applied = session.TryApplyCpuAction();
                    cpuMilliseconds?.Add(ElapsedMilliseconds(started));
                }
                else
                {
                    throw new InvalidOperationException(
                        "Automated game entered state " + session.State + ".");
                }
                if (!applied || session.Archive.Actions.Count != beforeCount + 1)
                    throw new InvalidOperationException(
                        "Automated action was not committed exactly once.");
            }
            if (session.State != MatchSessionState.Finished)
                throw new InvalidOperationException("Automated Crazy Eights did not finish.");
            SessionReplayer.Replay(session.Archive, viewer: 0);
            return session.Archive;
        }

        private IEnumerator MeasureAllocations(ProductAppController controller)
        {
            controller.Router.Show(ScreenId.Title);
            IEnumerator settle = WaitForPresentation(controller, 3d);
            while (settle.MoveNext()) yield return settle.Current;
            PlayCrazyEights(seed: 700000L, difficulty: 1, cpuMilliseconds: null);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            for (int frame = 0; frame < 30; frame++) yield return null;

            int expectedFrames = Math.Max(256,
                (int)Math.Ceiling(report.ScreenSampleSeconds * 120d));
            var idleBytes = new List<double>(expectedFrames);
            var actionBytes = new List<double>(100);
            var actionFrameMilliseconds = new List<double>(100);
            ProfilerRecorder allocated = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            ProfilerRecorder mainThread = ProfilerRecorder.StartNew(
                ProfilerCategory.Internal, "CPU Main Thread Frame Time", 1);
            try
            {
                if (!allocated.Valid)
                    throw new InvalidOperationException(
                        "The Development Player did not expose GC Allocated In Frame.");
                if (!mainThread.Valid)
                    throw new InvalidOperationException(
                        "The Development Player did not expose CPU Main Thread Frame Time.");
                double idleEnd = Time.realtimeSinceStartupAsDouble +
                    report.ScreenSampleSeconds;
                while (Time.realtimeSinceStartupAsDouble < idleEnd)
                {
                    yield return null;
                    idleBytes.Add(Math.Max(0L, allocated.LastValue));
                }

                var session = new GameSessionController(seed: 700001L);
                session.Begin();
                while (actionBytes.Count < 100)
                {
                    if (session.State == MatchSessionState.Finished)
                    {
                        session = new GameSessionController(
                            700001L + actionBytes.Count);
                        session.Begin();
                        yield return null;
                    }
                    int before = session.Archive.Actions.Count;
                    bool applied = session.State == MatchSessionState.AwaitingHuman
                        ? session.TryApplyHumanAction(session.Snapshot.Actions[0].Id)
                        : session.State == MatchSessionState.WaitingForCpu &&
                          session.TryApplyCpuAction();
                    if (!applied || session.Archive.Actions.Count != before + 1)
                        throw new InvalidOperationException(
                            "Allocation probe action was not committed exactly once.");
                    yield return null;
                    actionBytes.Add(Math.Max(0L, allocated.LastValue));
                    actionFrameMilliseconds.Add(Math.Max(0L,
                        mainThread.LastValue) / 1000000d);
                }
            }
            finally
            {
                allocated.Dispose();
                mainThread.Dispose();
            }

            AddMetric(ProductQualityStatistics.Metric("PERF-07", "idle-title",
                "bytes/frame", idleBytes, p95Budget: 0d));
            AddMetric(ProductQualityStatistics.Metric("PERF-07", "rule-action",
                "bytes/action", actionBytes, p95Budget: 256d * 1024d));
            AddMetric(ProductQualityStatistics.Metric("PERF-07", "action-frame-stall",
                "milliseconds", actionFrameMilliseconds, maximumBudget: 49.999999d));
        }

        private void RunStorageBenchmarks(IReadOnlyList<SessionArchive> archives)
        {
            string root = UniqueProbeRoot("storage");
            Directory.CreateDirectory(root);
            try
            {
                var store = new FileSessionStore(root);
                var ids = new List<string>(archives.Count);
                var saveMilliseconds = new List<double>(archives.Count);
                var loadMilliseconds = new List<double>(archives.Count);
                var resumeMilliseconds = new List<double>(archives.Count);
                var replayMilliseconds = new List<double>(archives.Count);
                var listMilliseconds = new List<double>(100);

                foreach (SessionArchive archive in archives)
                {
                    string id = SessionSlotIds.Create();
                    ids.Add(id);
                    long started = Stopwatch.GetTimestamp();
                    store.Save(id, archive);
                    saveMilliseconds.Add(ElapsedMilliseconds(started));
                }
                for (int index = 0; index < 100; index++)
                {
                    long started = Stopwatch.GetTimestamp();
                    IReadOnlyList<SessionSlotInfo> slots = store.List();
                    listMilliseconds.Add(ElapsedMilliseconds(started));
                    if (slots.Count != archives.Count)
                        throw new InvalidOperationException(
                            "The 100-slot listing lost a session.");
                }
                for (int index = 0; index < ids.Count; index++)
                {
                    long started = Stopwatch.GetTimestamp();
                    SessionArchive loaded = store.Load(ids[index]);
                    loadMilliseconds.Add(ElapsedMilliseconds(started));

                    started = Stopwatch.GetTimestamp();
                    var resumed = new GameSessionController(loaded);
                    resumed.Begin();
                    resumeMilliseconds.Add(ElapsedMilliseconds(started));

                    started = Stopwatch.GetTimestamp();
                    SessionReplayResult replay = SessionReplayer.Replay(loaded, viewer: 0);
                    replayMilliseconds.Add(ElapsedMilliseconds(started));
                    if (!replay.Game.IsTerminal ||
                        !SessionArchiveCodec.Encode(loaded).SequenceEqual(
                            SessionArchiveCodec.Encode(archives[index])))
                        throw new InvalidOperationException(
                            "Stored session did not round-trip deterministically.");
                }

                AddStorageMetric("atomic-save", saveMilliseconds);
                AddStorageMetric("load", loadMilliseconds);
                AddStorageMetric("resume", resumeMilliseconds);
                AddStorageMetric("replay", replayMilliseconds);
                AddStorageMetric("list-100-slots", listMilliseconds);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        private void AddStorageMetric(string context, IEnumerable<double> samples) =>
            AddMetric(ProductQualityStatistics.Metric("PERF-05", context,
                "milliseconds", samples, p95Budget: 250d, maximumBudget: 500d));

        private void RunLargeArchiveBenchmark()
        {
            GameRegistry registry = QualityRegistry();
            var configuration = new SessionConfiguration(
                "quality_long", players: 1, seed: 8080,
                difficulty: CpuDifficulties.Standard, humanPlayers: Array.Empty<int>(),
                options: new Dictionary<string, string>
                {
                    ["target"] = MaximumArchiveActions.ToString(CultureInfo.InvariantCulture)
                });
            var recorder = new SessionRecorder(configuration, registry);
            for (int index = 0; index < MaximumArchiveActions; index++)
                recorder.ApplyCpuAction();

            int low = 1;
            int high = MaximumArchiveActions;
            int bestCount = 0;
            byte[] bestBytes = Array.Empty<byte>();
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                var candidate = new SessionArchive(configuration,
                    recorder.Archive.Actions.Take(middle));
                try
                {
                    byte[] encoded = SessionArchiveCodec.Encode(candidate);
                    bestCount = middle;
                    bestBytes = encoded;
                    low = middle + 1;
                }
                catch (SessionFormatException)
                {
                    high = middle - 1;
                }
            }
            if (bestCount == 0 || bestBytes.Length <
                (int)(SessionArchiveCodec.MaximumBytes * 0.95d))
                throw new InvalidOperationException(
                    "The quality archive did not reach the 1 MiB boundary.");

            var archive = new SessionArchive(configuration,
                recorder.Archive.Actions.Take(bestCount));
            string root = UniqueProbeRoot("large");
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "maximum.tgs");
            try
            {
                long started = Stopwatch.GetTimestamp();
                byte[] encoded = SessionArchiveCodec.Encode(archive);
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(encoded, 0, encoded.Length);
                    stream.Flush(true);
                }
                double saveMilliseconds = ElapsedMilliseconds(started);

                started = Stopwatch.GetTimestamp();
                SessionArchive decoded = SessionArchiveCodec.Decode(File.ReadAllBytes(path));
                SessionReplayResult replay = SessionReplayer.Replay(decoded, registry: registry);
                double loadMilliseconds = ElapsedMilliseconds(started);
                if (replay.Checkpoints.Count != bestCount + 1)
                    throw new InvalidOperationException(
                        "The maximum archive did not replay every checkpoint.");

                report.LargeArchiveActions = bestCount;
                report.LargeArchiveBytes = encoded.Length;
                AddMetric(ProductQualityStatistics.Metric("PERF-05B", "save-encode",
                    "milliseconds", new[] { saveMilliseconds }, maximumBudget: 1000d));
                AddMetric(ProductQualityStatistics.Metric("PERF-05B", "load-full-replay",
                    "milliseconds", new[] { loadMilliseconds }, maximumBudget: 2000d));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        private IEnumerator RunSoak(ProductAppController controller)
        {
            string root = UniqueProbeRoot("soak");
            Directory.CreateDirectory(root);
            string slotId = SessionSlotIds.Create();
            var store = new FileSessionStore(root);
            var session = new GameSessionController(seed: 900000L);
            session.Begin();
            int routeIndex = 0;
            int frames = 0;
            int actionsSinceCheckpoint = 0;
            double startedAt = Time.realtimeSinceStartupAsDouble;
            double end = startedAt + report.SoakSeconds;
            double nextMemory = startedAt;
            double nextObjects = startedAt;
            double nextRoute = startedAt + 60d;
            double nextReport = startedAt + 60d;
            ScreenId[] routes =
            {
                ScreenId.Title,
                ScreenId.ProductSettings,
                ScreenId.Match,
                ScreenId.HowToPlay,
                ScreenId.Result
            };

            try
            {
                while (Time.realtimeSinceStartupAsDouble < end)
                {
                    yield return null;
                    frames++;
                    double now = Time.realtimeSinceStartupAsDouble;
                    if (now >= nextMemory)
                    {
                        report.PeakPrivateBytes = Math.Max(report.PeakPrivateBytes,
                            PrivateBytes());
                        nextMemory = now + 1d;
                    }
                    if (now >= nextObjects)
                    {
                        report.PeakGameObjects = Math.Max(report.PeakGameObjects,
                            CountSceneGameObjects());
                        nextObjects = now + 60d;
                    }
                    if (now >= nextRoute)
                    {
                        controller.Router.Show(routes[routeIndex++ % routes.Length]);
                        nextRoute = now + 60d;
                    }
                    if (now >= nextReport)
                    {
                        WriteReport("running");
                        nextReport = now + 60d;
                    }
                    if (frames % 5 != 0) continue;

                    if (session.State == MatchSessionState.Finished)
                    {
                        StoreAndVerify(store, slotId, session.Archive);
                        report.SoakGames++;
                        session = new GameSessionController(900000L + report.SoakGames);
                        session.Begin();
                        actionsSinceCheckpoint = 0;
                    }

                    int before = session.Archive.Actions.Count;
                    bool applied = session.State == MatchSessionState.AwaitingHuman
                        ? session.TryApplyHumanAction(session.Snapshot.Actions[0].Id)
                        : session.State == MatchSessionState.WaitingForCpu &&
                          session.TryApplyCpuAction();
                    if (!applied || session.Archive.Actions.Count != before + 1)
                        throw new InvalidOperationException(
                            "Soak action was not applied exactly once.");
                    report.SoakActions++;
                    actionsSinceCheckpoint++;
                    if (actionsSinceCheckpoint >= 100 ||
                        session.State == MatchSessionState.Finished)
                    {
                        StoreAndVerify(store, slotId, session.Archive);
                        actionsSinceCheckpoint = 0;
                    }
                }
                StoreAndVerify(store, slotId, session.Archive);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }

            report.SoakSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
            IEnumerator settle = WaitForPresentation(controller, 3d);
            while (settle.MoveNext()) yield return settle.Current;
        }

        private static void StoreAndVerify(FileSessionStore store, string slotId,
            SessionArchive archive)
        {
            store.Save(slotId, archive);
            SessionArchive loaded = store.Load(slotId);
            if (!SessionArchiveCodec.Encode(loaded).SequenceEqual(
                    SessionArchiveCodec.Encode(archive)))
                throw new InvalidOperationException(
                    "Soak checkpoint did not round-trip atomically.");
        }

        private static IEnumerator WaitForPresentation(ProductAppController controller,
            double timeoutSeconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (controller.PresentationController.IsTransitioning &&
                   Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            if (controller.PresentationController.IsTransitioning)
                throw new TimeoutException("A presentation transition did not finish.");
        }

        private void AddMetric(ProductQualityMetric metric)
        {
            metrics.Add(metric);
            if (!metric.Passed)
                AddFailure(metric.Id + " " + metric.Context + " exceeded its budget " +
                    "(p95=" + metric.P95.ToString("0.###", CultureInfo.InvariantCulture) +
                    ", p99=" + metric.P99.ToString("0.###", CultureInfo.InvariantCulture) +
                    ", max=" + metric.Maximum.ToString("0.###", CultureInfo.InvariantCulture) +
                    ").");
        }

        private void AddFailure(string failure)
        {
            if (!failures.Contains(failure)) failures.Add(failure);
        }

        private void FinishAndQuit()
        {
            Application.logMessageReceived -= HandleLog;
            report.CompletedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            report.ErrorLogs = errorLogs;
            report.ExceptionLogs = exceptionLogs;
            WriteReport(failures.Count == 0 ? "passed" : "failed");
            Application.Quit(failures.Count == 0 ? 0 : 1);
        }

        private void FailAndQuit(Exception exception)
        {
            Application.logMessageReceived -= HandleLog;
            AddFailure(exception.GetType().Name + ": " + exception.Message);
            report.CompletedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            report.ErrorLogs = errorLogs;
            report.ExceptionLogs = exceptionLogs;
            try { WriteReport("failed"); }
            catch { /* Preserve the original failure as the process exit code. */ }
            Application.Quit(1);
        }

        private void HandleLog(string _, string __, LogType type)
        {
            if (type == LogType.Exception) exceptionLogs++;
            else if (type == LogType.Error || type == LogType.Assert) errorLogs++;
        }

        private void WriteReport(string status)
        {
            if (string.IsNullOrWhiteSpace(reportPath)) return;
            report.Status = status;
            report.Metrics = metrics.ToArray();
            report.Failures = failures.ToArray();
            report.ErrorLogs = errorLogs;
            report.ExceptionLogs = exceptionLogs;
            string fullPath = Path.GetFullPath(reportPath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Quality report has no parent directory.");
            Directory.CreateDirectory(directory);
            string temporary = fullPath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(report, prettyPrint: true),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
            if (File.Exists(fullPath))
                File.Replace(temporary, fullPath, null, ignoreMetadataErrors: true);
            else
                File.Move(temporary, fullPath);
        }

        private static bool HasUsableFocus()
        {
            GameObject? selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) return false;
            UnityEngine.UI.Selectable? selectable =
                selected.GetComponent<UnityEngine.UI.Selectable>();
            return selectable != null && selectable.IsActive() && selectable.IsInteractable();
        }

        private static long PrivateBytes()
        {
            using (Process process = Process.GetCurrentProcess())
            {
                var counters = new ProcessMemoryCountersEx
                {
                    Size = (uint)Marshal.SizeOf<ProcessMemoryCountersEx>()
                };
                if (!GetProcessMemoryInfo(process.Handle, ref counters, counters.Size))
                    throw new InvalidOperationException(
                        "Windows private-memory counters are unavailable (error " +
                        Marshal.GetLastWin32Error() + ").");
                ulong privateBytes = counters.PrivateUsage.ToUInt64();
                if (privateBytes == 0UL || privateBytes > long.MaxValue)
                    throw new InvalidOperationException(
                        "Windows private-memory counter returned an invalid value.");
                return (long)privateBytes;
            }
        }

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr process,
            ref ProcessMemoryCountersEx counters, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessAffinityMask(IntPtr process,
            out UIntPtr processAffinityMask, out UIntPtr systemAffinityMask);

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemoryCountersEx
        {
            public uint Size;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
            public UIntPtr PrivateUsage;
        }

        private static int CountSceneGameObjects() =>
            Resources.FindObjectsOfTypeAll<GameObject>().Count(candidate =>
                candidate != null && candidate.scene.IsValid() && candidate.scene.isLoaded);

        private static double ElapsedMilliseconds(long startedTimestamp) =>
            (Stopwatch.GetTimestamp() - startedTimestamp) * 1000d / Stopwatch.Frequency;

        private static int CountSetBits(long value)
        {
            ulong remaining = unchecked((ulong)value);
            int count = 0;
            while (remaining != 0UL)
            {
                count += (int)(remaining & 1UL);
                remaining >>= 1;
            }
            return count;
        }

        private static string UniqueProbeRoot(string purpose) =>
            Path.Combine(Application.persistentDataPath, "TrumpGameLab", "QualityProbe",
                purpose + "-" + Guid.NewGuid().ToString("N"));

        private static string RequiredArgument(string[] arguments, string name)
        {
            string value = OptionalArgument(arguments, name, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Missing required quality argument: " + name);
            return value;
        }

        private static string OptionalArgument(string[] arguments, string name,
            string fallback)
        {
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }
            return fallback;
        }

        private static double PositiveDouble(string[] arguments, string name,
            double fallback)
        {
            string text = OptionalArgument(arguments, name,
                fallback.ToString(CultureInfo.InvariantCulture));
            if (!double.TryParse(text, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out double value) || value <= 0d)
                throw new ArgumentException(name + " must be a positive number.");
            return value;
        }

        private static long LongArgument(string[] arguments, string name, long fallback)
        {
            string text = OptionalArgument(arguments, name,
                fallback.ToString(CultureInfo.InvariantCulture));
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture,
                    out long value) || value <= 0L)
                throw new ArgumentException(name + " must be a positive integer.");
            return value;
        }

        private static GameRegistry QualityRegistry()
        {
            var registry = new GameRegistry();
            registry.Register(new GameInfo(
                    "quality_long", "Quality Long Game", 1, 1, "quality",
                    "Quality archive boundary probe.", "repository-owned probe"),
                (players, _, options) =>
                {
                    if (players != 1 || !options.TryGetValue("target", out string? text) ||
                        !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture,
                            out int target))
                        throw new ArgumentException("Quality long-game options are invalid.");
                    return new QualityLongGame(target);
                });
            return registry;
        }

        private sealed class QualityLongGame : GameBase
        {
            private static readonly TrumpLab.Action StepAction = new TrumpLab.Action("q");
            private static readonly IReadOnlyList<TrumpLab.Action> StepActions =
                Array.AsReadOnly(new[] { StepAction });
            private static readonly IReadOnlyList<TrumpLab.Action> NoActions =
                Array.AsReadOnly(Array.Empty<TrumpLab.Action>());
            private readonly int targetActions;

            public QualityLongGame(int targetActions)
            {
                if (targetActions <= 0 || targetActions > MaximumArchiveActions)
                    throw new ArgumentOutOfRangeException(nameof(targetActions));
                this.targetActions = targetActions;
                Players = 1;
                CurrentPlayer = 0;
            }

            public override string GameId => "quality_long";
            public override string Name => "Quality Long Game";
            public override bool IsTerminal => TurnCount >= targetActions;

            public override IReadOnlyList<TrumpLab.Action> LegalActions(int? player = null)
            {
                ValidateTurn(player);
                return IsTerminal ? NoActions : StepActions;
            }

            public override void Apply(TrumpLab.Action action)
            {
                ValidateTurn(CurrentPlayer);
                if (action != StepAction)
                    throw new InvalidOperationException("Quality action is not legal.");
                TurnCount++;
            }

            public override GameResult Result()
            {
                if (!IsTerminal)
                    throw new InvalidOperationException("Quality game is not complete.");
                return new GameResult(new[] { 0 }, new[] { 1d }, "complete", TurnCount);
            }

            public override string View(int? player = null) =>
                "quality-long " + TurnCount.ToString(CultureInfo.InvariantCulture);
        }
    }
}
