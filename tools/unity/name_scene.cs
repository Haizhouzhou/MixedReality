UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
var source="Assets/_TwinInspector/Scenes/Main.unity"; var dest="Assets/_TwinInspector/Scenes/Inspector.unity";
if(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(source)) { var error=UnityEditor.AssetDatabase.MoveAsset(source,dest); if(!string.IsNullOrEmpty(error)) throw new System.Exception(error); }
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(dest);
UnityEditor.EditorBuildSettings.scenes=new[]{new UnityEditor.EditorBuildSettingsScene(dest,true)};
UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(dest);
UnityEditor.AssetDatabase.SaveAssets();
return "Inspector is the sole enabled build scene";
