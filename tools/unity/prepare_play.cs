if(UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first");
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_TwinInspector/Scenes/Inspector.unity");
UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>("Assets/_TwinInspector/Scenes/Inspector.unity");
foreach(var group in new[]{UnityEditor.BuildTargetGroup.Standalone,UnityEditor.BuildTargetGroup.Android})
{
    var settings=UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(group);
    if(!settings) continue;
    foreach(var feature in settings.GetFeatures()) if(feature.GetType().Name=="XRStreamingFeature")
    {
        var serialized=new UnityEditor.SerializedObject(feature);
        serialized.FindProperty("_autoStartOnPlay").boolValue=false;
        serialized.FindProperty("_autoStopOnExit").boolValue=false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
foreach(var behaviour in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None))
    if(behaviour.GetType().Name=="XRDeviceSimulator")
    { var fields=new UnityEditor.SerializedObject(behaviour); fields.FindProperty("m_DeviceSimulatorUI").objectReferenceValue=null; fields.ApplyModifiedPropertiesWithoutUndo(); }
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEditor.AssetDatabase.SaveAssets();
typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("Clear").Invoke(null,null);
return "Streaming client automation disabled; enter Play mode for desktop verification";
