using UnityEditor;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Command-line entry points, e.g.
    /// Unity.exe -batchmode -projectPath . -executeMethod AshenRealm.EditorTools.BatchTasks.GenerateAndValidate -logFile Logs/generate.log -quit
    /// Exit code: 0 = success, 1 = generation failed, 2 = validation failed.
    /// </summary>
    public static class BatchTasks
    {
        public static void GenerateAndValidate()
        {
            if (!PrototypeSceneBuilder.Generate())
            {
                EditorApplication.Exit(1);
                return;
            }
            var errors = PrototypeValidator.Run(out _);
            EditorApplication.Exit(errors.Count == 0 ? 0 : 2);
        }

        public static void Validate()
        {
            var errors = PrototypeValidator.Run(out _);
            EditorApplication.Exit(errors.Count == 0 ? 0 : 2);
        }

        /// <summary>Runs the Play-mode playtest. Do NOT pass -quit: the runner exits itself (0 ok, 3 failed).</summary>
        public static void Playtest() => PlaytestRunner.Start(true);

        /// <summary>Generates the scene and renders the menu screens to Screenshots/.</summary>
        public static void GenerateAndCaptureMenus()
        {
            if (!PrototypeSceneBuilder.Generate())
            {
                EditorApplication.Exit(1);
                return;
            }
            MenuCapture.Capture();
            EditorApplication.Exit(0);
        }

        public static void CapturePreviews()
        {
            PreviewCapture.Capture();
            EditorApplication.Exit(0);
        }

        /// <summary>Generate, validate, then build WebGL. Run with -buildTarget WebGL to skip a platform switch.</summary>
        public static void GenerateValidateAndBuildWebGL()
        {
            if (!PrototypeSceneBuilder.Generate())
            {
                EditorApplication.Exit(1);
                return;
            }
            if (PrototypeValidator.Run(out _).Count > 0)
            {
                EditorApplication.Exit(2);
                return;
            }
            EditorApplication.Exit(WebGLBuilder.Build() ? 0 : 4);
        }

        public static void BuildWebGL() => EditorApplication.Exit(WebGLBuilder.Build() ? 0 : 4);

        /// <summary>Generate, apply iOS and WebGL player settings, validate. No player build.</summary>
        public static void GenerateConfigureAndValidate()
        {
            if (!PrototypeSceneBuilder.Generate())
            {
                EditorApplication.Exit(1);
                return;
            }
            IOSBuildConfigurator.Apply();
            WebGLBuilder.ApplySettings();
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(PrototypeValidator.Run(out _).Count == 0 ? 0 : 2);
        }

        public static void ApplyIOSSettings()
        {
            IOSBuildConfigurator.Apply();
            EditorApplication.Exit(0);
        }

        public static void GenerateValidateAndConfigureIOS()
        {
            if (!PrototypeSceneBuilder.Generate())
            {
                EditorApplication.Exit(1);
                return;
            }
            IOSBuildConfigurator.Apply();
            var errors = PrototypeValidator.Run(out _);
            Debug.Log(errors.Count == 0 ? "[AshenRealm] Batch run complete." : "[AshenRealm] Batch run finished with validation errors.");
            EditorApplication.Exit(errors.Count == 0 ? 0 : 2);
        }
    }
}
