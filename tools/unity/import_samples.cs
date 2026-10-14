UnityEditor.AssetDatabase.StartAssetEditing();
try {
    var requested = new string[][] {
        new [] { "com.unity.xr.interaction.toolkit", "3.6.1", "Starter Assets", "Hands Interaction Demo", "XR Device Simulator" },
        new [] { "com.unity.xr.hands", "1.9.0", "HandVisualizer" },
        new [] { "com.google.xr.extensions", "1.4.0", "Image Tracking", "Composition Layer Passthrough" }
    };
    foreach (var group in requested)
        foreach (var sample in UnityEditor.PackageManager.UI.Sample.FindByPackage(group[0], group[1]))
            for (int i = 2; i < group.Length; i++)
                if (sample.displayName == group[i] && !sample.isImported) sample.Import();
} finally { UnityEditor.AssetDatabase.StopAssetEditing(); }
var essential = System.IO.Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", System.IO.SearchOption.AllDirectories);
if (!System.IO.Directory.Exists("Assets/TextMesh Pro/Resources") && essential.Length > 0) UnityEditor.AssetDatabase.ImportPackage(essential[0], false);
UnityEditor.AssetDatabase.Refresh();
return "Required interaction and Android XR samples imported; awaiting compilation.";
