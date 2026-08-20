#nullable enable

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrumpLab.Product.Tests
{
    public sealed class ProductQualityTests
    {
        [Test]
        public void PercentilesUseDeterministicNearestRankAndInclusiveBudgets()
        {
            double[] samples = Enumerable.Range(1, 100)
                .Select(value => (double)value).ToArray();

            ProductQualityMetric passing = ProductQualityStatistics.Metric(
                "PERF-test", "passing", "milliseconds", samples,
                p95Budget: 95d, p99Budget: 99d, maximumBudget: 100d);
            ProductQualityMetric failing = ProductQualityStatistics.Metric(
                "PERF-test", "failing", "milliseconds", samples,
                p95Budget: 94d, maximumBudget: 100d);

            AssertAll(() =>
            {
                Assert.That(passing.P50, Is.EqualTo(50d));
                Assert.That(passing.P95, Is.EqualTo(95d));
                Assert.That(passing.P99, Is.EqualTo(99d));
                Assert.That(passing.Maximum, Is.EqualTo(100d));
                Assert.That(passing.Passed, Is.True);
                Assert.That(failing.Passed, Is.False);
                Assert.That(ProductQualityStatistics.Percentile(new[] { 7d }, 0d),
                    Is.EqualTo(7d));
                Assert.That(ProductQualityStatistics.Percentile(new[] { 7d }, 100d),
                    Is.EqualTo(7d));
            });
        }

        [Test]
        public void MetricsRejectEmptyNonFiniteOrInvalidInputs()
        {
            AssertAll(() =>
            {
                Assert.That(() => ProductQualityStatistics.Metric(
                        "PERF-test", "empty", "ms", Array.Empty<double>()),
                    Throws.TypeOf<ArgumentException>());
                Assert.That(() => ProductQualityStatistics.Metric(
                        "PERF-test", "nan", "ms", new[] { double.NaN }),
                    Throws.TypeOf<ArgumentException>());
                Assert.That(() => ProductQualityStatistics.Metric(
                        "PERF-test", "negative", "ms", new[] { -1d }),
                    Throws.TypeOf<ArgumentException>());
                Assert.That(() => ProductQualityStatistics.Percentile(
                        new[] { 1d }, 101d),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void ProductRuntimeContainsNoNetworkOrSteamDependency()
        {
            string runtime = Path.GetFullPath(Path.Combine(Application.dataPath,
                "TrumpLab", "Product", "Runtime"));
            string[] forbidden =
            {
                "UnityEngine.Networking",
                "System.Net",
                "HttpClient",
                "WebRequest",
                "TcpClient",
                "UdpClient",
                "Steamworks"
            };
            string[] violations = Directory.GetFiles(runtime, "*.cs",
                    SearchOption.TopDirectoryOnly)
                .SelectMany(path => forbidden
                    .Where(token => File.ReadAllText(path).Contains(token,
                        StringComparison.Ordinal))
                    .Select(token => Path.GetFileName(path) + ":" + token))
                .ToArray();

            Assert.That(violations, Is.Empty,
                "M06 Product runtime must remain offline and account-free.");
        }

        [Test]
        public void WindowsReleaseContractUsesD3D11VSyncAndSingleBootstrapScene()
        {
            GraphicsDeviceType[] graphics = PlayerSettings.GetGraphicsAPIs(
                BuildTarget.StandaloneWindows64);
            EditorBuildSettingsScene[] enabled = EditorBuildSettings.scenes
                .Where(scene => scene.enabled).ToArray();

            AssertAll(() =>
            {
                Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(
                    BuildTarget.StandaloneWindows64), Is.False);
                Assert.That(graphics, Is.EqualTo(new[] { GraphicsDeviceType.Direct3D11 }));
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
                Assert.That(enabled.Length, Is.EqualTo(1));
                Assert.That(enabled[0].path,
                    Does.EndWith("/Product/Scenes/Bootstrap.unity"));
            });
        }

        private static void AssertAll(System.Action assertions) => assertions();
    }
}
