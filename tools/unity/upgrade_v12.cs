if(UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Stop Play before rebuilding");
TwinInspector.Editor.ProjectBuilder.Setup();
return "v1.2 Inspector, materials, annotation prefab and modality rays rebuilt";
