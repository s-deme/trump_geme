#nullable enable

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrumpLab.Product.Editor
{
    public static class ProductQualityBuild
    {
        private const string BuildPathArgument = "-qualityBuildPath";
        private const string DevelopmentArgument = "-qualityDevelopment";

        public static void BuildCommandLine()
        {
            string outputPath = Path.GetFullPath(RequiredArgument(
                Environment.GetCommandLineArgs(), BuildPathArgument));
            bool development = string.Equals(OptionalArgument(
                    Environment.GetCommandLineArgs(), DevelopmentArgument, "false"),
                "true", StringComparison.OrdinalIgnoreCase);
            if (!outputPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Quality build path must end in .exe.");
            string? outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Quality build path has no parent directory.");
            Directory.CreateDirectory(outputDirectory);

            EditorBuildSettingsScene[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled).ToArray();
            if (enabledScenes.Length != 1 ||
                !enabledScenes[0].path.EndsWith("/Product/Scenes/Bootstrap.unity",
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The quality build requires only the generated Product Bootstrap scene.");

            PlayerSettings.SetUseDefaultGraphicsAPIs(
                BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D11 });
            QualitySettings.vSyncCount = 1;

            var options = new BuildPlayerOptions
            {
                scenes = enabledScenes.Select(scene => scene.path).ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded ||
                report.summary.totalErrors != 0 || !File.Exists(outputPath))
                throw new InvalidOperationException(
                    "Windows quality build failed: " + report.summary.result +
                    ", errors=" + report.summary.totalErrors + ".");

            Debug.Log("Product quality " + (development ? "development" : "release") +
                " build succeeded: " + outputPath +
                " (bytes=" + report.summary.totalSize + ", duration=" +
                report.summary.totalTime + ")");
        }

        private static string RequiredArgument(string[] arguments, string name)
        {
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(arguments[index + 1]))
                    return arguments[index + 1];
            }
            throw new ArgumentException("Missing required build argument: " + name);
        }

        private static string OptionalArgument(string[] arguments, string name,
            string fallback)
        {
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(arguments[index + 1]))
                    return arguments[index + 1];
            }
            return fallback;
        }
    }
}
