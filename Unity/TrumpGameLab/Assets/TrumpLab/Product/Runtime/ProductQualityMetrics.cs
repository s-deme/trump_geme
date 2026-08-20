#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace TrumpLab.Product
{
    [Serializable]
    public sealed class ProductQualityMetric
    {
        public string Id = string.Empty;
        public string Context = string.Empty;
        public string Unit = string.Empty;
        public int Samples;
        public double P50;
        public double P95;
        public double P99;
        public double Maximum;
        public double P95Budget = -1d;
        public double P99Budget = -1d;
        public double MaximumBudget = -1d;
        public bool Passed;
    }

    [Serializable]
    public sealed class ProductQualityProbeReport
    {
        public string Status = "running";
        public string Mode = string.Empty;
        public string StartedUtc = string.Empty;
        public string CompletedUtc = string.Empty;
        public string UnityVersion = string.Empty;
        public string OperatingSystem = string.Empty;
        public string ProcessorType = string.Empty;
        public int ProcessorCount;
        public string ProcessorAffinityMask = string.Empty;
        public int EffectiveProcessorCount;
        public int SystemMemoryMiB;
        public string GraphicsDeviceName = string.Empty;
        public string GraphicsDeviceType = string.Empty;
        public int VSyncCount;
        public int TargetFrameRate;
        public double StartupSeconds;
        public double ScreenSampleSeconds;
        public double SoakSeconds;
        public long WarmPrivateBytes;
        public long PeakPrivateBytes;
        public long PrivateByteIncrease;
        public int InitialGameObjects;
        public int PeakGameObjects;
        public int FinalGameObjects;
        public int AutomatedGames;
        public int SoakGames;
        public int SoakActions;
        public int LargeArchiveActions;
        public int LargeArchiveBytes;
        public int ErrorLogs;
        public int ExceptionLogs;
        public ProductQualityMetric[] Metrics = Array.Empty<ProductQualityMetric>();
        public string[] Failures = Array.Empty<string>();
    }

    public static class ProductQualityStatistics
    {
        public static ProductQualityMetric Metric(string id, string context, string unit,
            IEnumerable<double> samples, double p95Budget = -1d,
            double p99Budget = -1d, double maximumBudget = -1d)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Quality metric ID cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(context))
                throw new ArgumentException("Quality metric context cannot be empty.",
                    nameof(context));
            if (string.IsNullOrWhiteSpace(unit))
                throw new ArgumentException("Quality metric unit cannot be empty.", nameof(unit));
            if (samples == null) throw new ArgumentNullException(nameof(samples));

            double[] ordered = samples.ToArray();
            if (ordered.Length == 0 || ordered.Any(value =>
                    double.IsNaN(value) || double.IsInfinity(value) || value < 0d))
                throw new ArgumentException(
                    "Quality metrics require finite non-negative samples.", nameof(samples));
            Array.Sort(ordered);

            var metric = new ProductQualityMetric
            {
                Id = id,
                Context = context,
                Unit = unit,
                Samples = ordered.Length,
                P50 = PercentileOfSorted(ordered, 50d),
                P95 = PercentileOfSorted(ordered, 95d),
                P99 = PercentileOfSorted(ordered, 99d),
                Maximum = ordered[ordered.Length - 1],
                P95Budget = p95Budget,
                P99Budget = p99Budget,
                MaximumBudget = maximumBudget
            };
            metric.Passed = (p95Budget < 0d || metric.P95 <= p95Budget) &&
                (p99Budget < 0d || metric.P99 <= p99Budget) &&
                (maximumBudget < 0d || metric.Maximum <= maximumBudget);
            return metric;
        }

        public static double Percentile(IEnumerable<double> samples, double percentile)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            double[] ordered = samples.ToArray();
            if (ordered.Length == 0)
                throw new ArgumentException("Percentile samples cannot be empty.",
                    nameof(samples));
            if (ordered.Any(value => double.IsNaN(value) || double.IsInfinity(value)))
                throw new ArgumentException("Percentile samples must be finite.",
                    nameof(samples));
            Array.Sort(ordered);
            return PercentileOfSorted(ordered, percentile);
        }

        private static double PercentileOfSorted(double[] ordered, double percentile)
        {
            if (percentile < 0d || percentile > 100d)
                throw new ArgumentOutOfRangeException(nameof(percentile));
            int index = Math.Max(0,
                (int)Math.Ceiling(percentile / 100d * ordered.Length) - 1);
            return ordered[index];
        }
    }
}
