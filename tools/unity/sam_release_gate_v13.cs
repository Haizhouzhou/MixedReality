if(UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Stop Play mode before changing release settings");
var settings=UnityEngine.Resources.Load<TwinInspector.Settings>("Settings");var serialized=new UnityEditor.SerializedObject(settings);
serialized.FindProperty("objectPickEnabled").boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo();UnityEditor.EditorUtility.SetDirty(settings);UnityEditor.AssetDatabase.SaveAssets();
return "Release SAM gate disabled: cold dispatch exceeds budget; prototype remains available for explicit evaluation";
