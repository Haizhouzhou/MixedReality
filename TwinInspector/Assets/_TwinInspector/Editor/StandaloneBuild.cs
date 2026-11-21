using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace TwinInspector.Editor
{
    /// <summary>Builds a separately installed twininspector2 app without changing the shared application's identity.</summary>
    public static class StandaloneBuild
    {
        public const string ApplicationId = "com.mrtwin.inspector2";

        [MenuItem("TwinInspector/Build twininspector2 APK")]
        public static void Build()
        {
            string Required(string name) => Environment.GetEnvironmentVariable(name)
                ?? throw new InvalidOperationException("Set " + name + " before building the twininspector2 app.");
            var keyStore = Required("TWIN2_KEYSTORE");
            var storePass = Required("TWIN2_KEYSTORE_PASS");
            var keyAlias = Required("TWIN2_KEY_ALIAS");
            var keyPass = Required("TWIN2_KEY_PASS");
            if (!File.Exists(keyStore)) throw new FileNotFoundException("twininspector2 signing keystore not found", keyStore);

            var originalId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            var originalName = PlayerSettings.productName;
            var originalCustom = PlayerSettings.Android.useCustomKeystore;
            var originalStore = PlayerSettings.Android.keystoreName;
            var originalStorePass = PlayerSettings.Android.keystorePass;
            var originalAlias = PlayerSettings.Android.keyaliasName;
            var originalKeyPass = PlayerSettings.Android.keyaliasPass;
            try
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
                PlayerSettings.productName = "twininspector2";
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keyStore;
                PlayerSettings.Android.keystorePass = storePass;
                PlayerSettings.Android.keyaliasName = keyAlias;
                PlayerSettings.Android.keyaliasPass = keyPass;
                var output = Path.GetFullPath(Path.Combine("..", "work", "build-records", "twininspector2", "twininspector2.apk"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("twininspector2 APK build failed: " + report.summary.result);
                UnityEngine.Debug.Log("Standalone twininspector2 APK: " + output);
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, originalId);
                PlayerSettings.productName = originalName;
                PlayerSettings.Android.useCustomKeystore = originalCustom;
                PlayerSettings.Android.keystoreName = originalStore;
                PlayerSettings.Android.keystorePass = originalStorePass;
                PlayerSettings.Android.keyaliasName = originalAlias;
                PlayerSettings.Android.keyaliasPass = originalKeyPass;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
