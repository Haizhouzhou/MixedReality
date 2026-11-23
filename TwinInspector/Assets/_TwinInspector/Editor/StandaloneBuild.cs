using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
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
            => BuildVariant(ApplicationId, "twininspector2", "twininspector2");

        [MenuItem("TwinInspector/Build twininspector2 HG MainHall NKSR update")]
        public static void BuildNksrMainHall()
        {
            ValidateNksrMainHallData();
            BuildVariant(ApplicationId, "twininspector2", "twininspector2-nksr-mainhall");
        }

        /// <summary>Reject missing or stale reconstruction assets before changing any build settings.</summary>
        public static void ValidateNksrData()
            => ValidateNksrDataFor(new[] { "HG_MainHall", "HG_E3_Hall", "HG_E7_Hall", "CAB_MainHall" });

        public static void ValidateNksrMainHallData()
        {
            ValidateNksrDataFor(new[] { "HG_MainHall" }, true);
            var root = Path.Combine(UnityEngine.Application.streamingAssetsPath, "twins");
            var proof = JObject.Parse(File.ReadAllText(Path.Combine(root, "nksr-trial.json")));
            if ((string)proof["schema"] != "nksr-trial-package/1" || (bool?)proof["completed"] != true ||
                !JToken.DeepEquals(proof["reconstructed_spaces"], new JArray("HG_MainHall")) ||
                (string)proof["catalog_sha256"] != (string)proof["baseline_catalog_sha256"])
                throw new InvalidDataException("Expected a completed HG MainHall-only NKSR trial.");
            RequireHash(Path.Combine(root, "spaces.json"), (string)proof["catalog_sha256"]);
            var preserved = proof["preserved_spaces"] as JObject;
            var expected = new HashSet<string> { "HG_E3_Hall", "HG_E7_Hall", "CAB_MainHall" };
            if (preserved == null || !expected.SetEquals(preserved.Properties().Select(p => p.Name)))
                throw new InvalidDataException("MainHall trial must retain the other three baseline spaces.");
            var catalog = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(root, "spaces.json")))["spaces"];
            foreach (var item in preserved.Properties())
            {
                var folder = Path.Combine(root, item.Name);
                var files = item.Value["files"] as JObject;
                var entry = catalog.First(space => (string)space["id"] == item.Name);
                if ((bool?)item.Value["present"] != Directory.Exists(folder) || files == null ||
                    (bool?)item.Value["packaged"] != (bool?)entry["packaged"])
                    throw new InvalidDataException("Changed baseline space: " + item.Name);
                var actual = Directory.Exists(folder) ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                    .Select(path => path.Substring(folder.Length + 1).Replace('\\', '/')) : Enumerable.Empty<string>();
                if (!new HashSet<string>(actual).SetEquals(files.Properties().Select(p => p.Name)))
                    throw new InvalidDataException("Changed baseline file set: " + item.Name);
                foreach (var file in files.Properties()) RequireHash(Path.Combine(folder, file.Name), (string)file.Value);
            }
            UnityEngine.Debug.Log("Validated HG MainHall NKSR update; other three spaces and catalog retained.");
        }

        static void ValidateNksrDataFor(string[] requiredSpaces, bool allowFullLods = false)
        {
            var root = Path.Combine(UnityEngine.Application.streamingAssetsPath, "twins");
            var catalog = JObject.Parse(File.ReadAllText(Path.Combine(root, "spaces.json")));
            var spaces = catalog["spaces"] as JArray;
            var expected = new HashSet<string> { "HG_MainHall", "HG_E3_Hall", "HG_E7_Hall", "CAB_MainHall" };
            if ((string)catalog["schema"] != "twin-spaces/1" || spaces == null || spaces.Count != expected.Count ||
                !expected.SetEquals(spaces.Select(space => (string)space["id"])) ||
                spaces.Any(space => requiredSpaces.Contains((string)space["id"]) && (bool?)space["packaged"] != true))
                throw new InvalidDataException("NKSR build requires the requested spaces to be packaged.");

            foreach (var space in spaces.Where(space => requiredSpaces.Contains((string)space["id"])))
            {
                var id = (string)space["id"];
                var folder = Path.Combine(root, id);
                var manifestPath = Path.Combine(folder, "manifest.json");
                var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                var proofPath = Path.Combine(folder, "nksr-provenance.json");
                if (!File.Exists(proofPath))
                    throw new InvalidDataException("Missing completed NKSR provenance for " + id);
                // Packaging writes nksr-packaged/1: method=NKSR, completed=true, scan,
                // manifest_sha256, and files={each manifest GLB relative path: SHA256}.
                // Source-cloud and reconstruction-report provenance may be included as extra fields.
                var proof = JObject.Parse(File.ReadAllText(proofPath));
                if ((string)proof["schema"] != "nksr-packaged/1" || (string)proof["method"] != "NKSR" ||
                    (bool?)proof["completed"] != true || (string)proof["scan"] != (string)space["scan"] ||
                    (string)manifest["scan"] != (string)space["scan"])
                    throw new InvalidDataException("Invalid completed NKSR provenance for " + id);
                RequireHash(manifestPath, (string)proof["manifest_sha256"]);

                var hashes = proof["files"] as JObject;
                var tiles = manifest["tiles"] as JArray;
                if (hashes == null || tiles == null || tiles.Count == 0)
                    throw new InvalidDataException("NKSR build requires nonempty tile hashes for " + id);
                var paths = new HashSet<string>();
                foreach (var tile in tiles)
                {
                    var files = tile["files"] as JObject;
                    if (files == null || !files.ContainsKey("2") || !allowFullLods && files.Count != 1 ||
                        files.Properties().Any(file => !new[] { "0", "1", "2" }.Contains(file.Name) || file.Value.Type != JTokenType.String))
                        throw new InvalidDataException("Invalid NKSR tile LOD set: " + id);
                    foreach (var file in files.Properties())
                    {
                        var relative = (string)file.Value;
                        if (string.IsNullOrWhiteSpace(relative) || relative.Contains("..") || relative.Contains(":") ||
                            relative.Contains("\\") || Path.IsPathRooted(relative) || !relative.EndsWith(".glb", StringComparison.Ordinal) ||
                            !paths.Add(relative))
                            throw new InvalidDataException("Invalid NKSR tile path for " + id);
                        RequireHash(Path.Combine(folder, relative), (string)hashes[relative]);
                    }
                }
                if (!paths.SetEquals(hashes.Properties().Select(property => property.Name)))
                    throw new InvalidDataException("NKSR provenance must match the exact packaged tile set for " + id);
            }
            UnityEngine.Debug.Log("Validated bundled NKSR datasets and file hashes: " + string.Join(", ", requiredSpaces));
        }

        static void RequireHash(string path, string expected)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("NKSR provenance SHA256 mismatch: " + path);
        }

        static void BuildVariant(string applicationId, string productName, string outputName)
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
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, applicationId);
                PlayerSettings.productName = productName;
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keyStore;
                PlayerSettings.Android.keystorePass = storePass;
                PlayerSettings.Android.keyaliasName = keyAlias;
                PlayerSettings.Android.keyaliasPass = keyPass;
                var output = Path.GetFullPath(Path.Combine("..", "work", "build-records", outputName, outputName + ".apk"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException(productName + " APK build failed: " + report.summary.result);
                UnityEngine.Debug.Log("Standalone " + productName + " APK: " + output);
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
