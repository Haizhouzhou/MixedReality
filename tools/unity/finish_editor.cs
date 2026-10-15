if(UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Stop simulation before finishing");
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_TwinInspector/Scenes/Inspector.unity");
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
if(System.IO.Directory.Exists("Assets/Resources") && System.IO.Directory.GetFileSystemEntries("Assets/Resources").Length==0)
    UnityEditor.AssetDatabase.DeleteAsset("Assets/Resources");
UnityEditor.AssetDatabase.SaveAssets();
return "Inspector saved; temporary test resource folder cleaned if empty";
