using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.iOS;
using UnityEngine.Rendering;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Ashen Realm > Apply iOS Player Settings: landscape, IL2CPP, ARM64, Metal, automatic signing.
    /// Never touches Apple credentials; the signing team is chosen in Xcode or Player Settings.
    /// Each setting is applied independently and reported, so a missing iOS module does not abort the rest.
    /// </summary>
    public static class IOSBuildConfigurator
    {
        const string LogPrefix = "[AshenRealm][iOS]";
        /// <summary>Default bundle identifier, only applied when the project still uses an empty or template value.</summary>
        public const string DefaultBundleId = "com.ashenrealm.prototype";
        const string MinimumIOSVersion = "15.0";

        [MenuItem("Ashen Realm/Apply iOS Player Settings", priority = 40)]
        public static void ApplyFromMenu()
        {
            var report = Apply();
            EditorUtility.DisplayDialog("Ashen Realm — iOS Settings", string.Join("\n", report), "OK");
        }

        public static List<string> Apply()
        {
            var report = new List<string>();
            var ios = NamedBuildTarget.iOS;

            void Step(string description, Action action)
            {
                try
                {
                    action();
                    report.Add("✔ " + description);
                }
                catch (Exception e)
                {
                    report.Add($"✖ {description}: {e.Message}");
                }
            }

            Step("Orientation: landscape left/right only", () =>
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
            });
            Step("Scripting backend: IL2CPP", () => PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP));
            Step("Architecture: ARM64", () => PlayerSettings.SetArchitecture(ios, 1));
            Step("Graphics API: Metal only", () =>
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            });
            Step($"Bundle identifier (kept if already customised; default {DefaultBundleId})", () =>
            {
                string current = PlayerSettings.GetApplicationIdentifier(ios);
                bool isPlaceholder = string.IsNullOrEmpty(current)
                                     || current.IndexOf("DefaultCompany", StringComparison.OrdinalIgnoreCase) >= 0
                                     || current.IndexOf("unity.template", StringComparison.OrdinalIgnoreCase) >= 0
                                     || current.StartsWith("com.Company", StringComparison.Ordinal);
                if (isPlaceholder)
                    PlayerSettings.SetApplicationIdentifier(ios, DefaultBundleId);
                report.Add("   bundle id: " + PlayerSettings.GetApplicationIdentifier(ios));
            });
            Step($"Minimum iOS version {MinimumIOSVersion}, iPhone target", () =>
            {
                PlayerSettings.iOS.targetOSVersionString = MinimumIOSVersion;
                PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            });
            Step("Automatic signing enabled (team chosen in Xcode)", () => PlayerSettings.iOS.appleEnableAutomaticSigning = true);
            Step("Full screen, hidden status bar, deferred edge gestures", () =>
            {
                PlayerSettings.statusBarHidden = true;
                PlayerSettings.iOS.requiresFullScreen = true;
                PlayerSettings.iOS.deferSystemGesturesMode = SystemGestureDeferMode.All;
            });
            Step("Prebake collision meshes, accelerometer off", () =>
            {
                PlayerSettings.bakeCollisionMeshes = true;
                PlayerSettings.accelerometerFrequency = 0;
            });
            Step("IL2CPP release configuration", () => PlayerSettings.SetIl2CppCompilerConfiguration(ios, Il2CppCompilerConfiguration.Release));

            AssetDatabase.SaveAssets();
            foreach (string line in report) Debug.Log($"{LogPrefix} {line}");
            bool iosModule = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS);
            report.Add(iosModule
                ? "iOS Build Support module: installed"
                : "iOS Build Support module: NOT installed (add it in Unity Hub to generate the Xcode project)");
            Debug.Log($"{LogPrefix} {report[report.Count - 1]}");
            return report;
        }
    }
}
