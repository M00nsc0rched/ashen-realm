using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Ashen Realm > Build WebGL: browser build of Prototype_Valley into Builds/WebGL using the project's
    /// AshenRealm web template. Gzip with decompression fallback works on any static host (no special headers).
    /// </summary>
    public static class WebGLBuilder
    {
        public const string OutputPath = "Builds/WebGL";
        const string LogPrefix = "[AshenRealm][WebGL]";

        [MenuItem("Ashen Realm/Build WebGL", priority = 41)]
        public static void BuildFromMenu()
        {
            bool ok = Build();
            EditorUtility.DisplayDialog("Ashen Realm — WebGL",
                ok ? $"Build succeeded:\n{Path.GetFullPath(OutputPath)}\n\nServe it with tools/serve-webgl.ps1 and open http://localhost:8080"
                   : "Build failed. See the Console for details.", "OK");
        }

        public static void ApplySettings()
        {
            PlayerSettings.productName = "Ashen Realm";
            PlayerSettings.WebGL.template = "PROJECT:AshenRealm";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = false;
            Debug.Log($"{LogPrefix} Player settings applied (template AshenRealm, gzip + decompression fallback).");
        }

        public static bool Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError($"{LogPrefix} WebGL Build Support is not installed (Unity Hub > Installs > Add modules).");
                return false;
            }
            if (!File.Exists(GeneratedAssets.ScenePath))
            {
                Debug.LogError($"{LogPrefix} {GeneratedAssets.ScenePath} is missing. Run Ashen Realm > Generate Prototype first.");
                return false;
            }

            ApplySettings();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { GeneratedAssets.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            int errors = report.steps.SelectMany(s => s.messages).Count(m => m.type == LogType.Error || m.type == LogType.Exception);
            Debug.Log($"{LogPrefix} Result: {summary.result} in {summary.totalTime.TotalMinutes:F1} min, " +
                      $"{summary.totalSize / (1024f * 1024f):F1} MB, {errors} error message(s) → {Path.GetFullPath(OutputPath)}");
            return summary.result == BuildResult.Succeeded;
        }
    }
}
