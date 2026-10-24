if(UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before changing the fixture");
const string backup="../work/v12-demo-settings.txt";
var settings=UnityEditor.AssetDatabase.LoadAssetAtPath<TwinInspector.Settings>("Assets/_TwinInspector/Resources/Settings.asset");
var serialized=new UnityEditor.SerializedObject(settings);
if(System.IO.File.Exists(backup))
{
 var original=System.IO.File.ReadAllLines(backup);
 serialized.FindProperty("editorDataPath").stringValue=original[0];
 serialized.ApplyModifiedPropertiesWithoutUndo();UnityEditor.AssetDatabase.SaveAssets();
 if(settings.EditorDataPath!=original[0]) throw new System.Exception("Dataset setting restore failed");
 System.IO.File.Delete(backup);
 return "Original dataset settings restored";
}
System.IO.Directory.CreateDirectory("../work");
System.IO.File.WriteAllLines(backup,new[]{settings.EditorDataPath});
serialized.FindProperty("editorDataPath").stringValue="";
serialized.ApplyModifiedPropertiesWithoutUndo();UnityEditor.AssetDatabase.SaveAssets();
return "Packaged demo fixture selected; run this again after stopping Play mode to restore";
