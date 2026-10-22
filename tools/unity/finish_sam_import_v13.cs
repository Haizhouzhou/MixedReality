var folder=System.IO.Path.GetFullPath("Assets/_TwinInspector/ModelsImport");
var root=System.IO.Path.GetFullPath(UnityEngine.Application.dataPath)+System.IO.Path.DirectorySeparatorChar;
if(!folder.StartsWith(root,System.StringComparison.OrdinalIgnoreCase))throw new System.Exception("Import path outside Assets");
foreach(var name in new[]{"mobile_sam_encoder.sentis","mobile_sam_decoder.sentis"})if(!System.IO.File.Exists("../tools/sam/models/"+name))throw new System.Exception("Serialized model missing");
if(UnityEditor.AssetDatabase.IsValidFolder("Assets/_TwinInspector/ModelsImport"))UnityEditor.AssetDatabase.DeleteAsset("Assets/_TwinInspector/ModelsImport");
UnityEditor.AssetDatabase.Refresh();return "Temporary ONNX import assets removed; external serialized models retained";
