var settings=UnityEngine.Resources.Load<TwinInspector.Settings>("Settings");
if(settings.ObjectPickEnabled||!settings.DiffEnabled)throw new System.Exception("Release gates must enable geometry diff and disable experimental SAM");
if(UnityEditor.PlayerSettings.bundleVersion!="1.3.0"||UnityEditor.PlayerSettings.Android.bundleVersionCode!=3)throw new System.Exception("Wrong application version");
if(UnityEditor.AssetDatabase.IsValidFolder("Assets/_TwinInspector/ModelsImport"))throw new System.Exception("Temporary models must not remain in Assets");
var app=TwinInspector.AppBootstrap.Instance;if(app&&app.Picker.Available)throw new System.Exception("SAM is visible despite release gate");
var result="PASS: version 1.3.0 / code 3; geometry diff enabled; experimental SAM disabled; no temporary model imports in Assets.\n";
System.IO.File.WriteAllText("docs/evidence/release-config-v13.txt",result);return result;
