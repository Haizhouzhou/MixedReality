var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_TwinInspector/Scenes/Inspector.unity");
var rig = UnityEngine.Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
var text = new System.Text.StringBuilder();
foreach(var t in rig.GetComponentsInChildren<UnityEngine.Transform>(true)) {
 var components=t.GetComponents<UnityEngine.Component>();
 text.AppendLine(t.name+" ["+string.Join(",",System.Array.ConvertAll(components,x=>x?x.GetType().Name:"MISSING"))+"]");
}
var m=rig.GetComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager>();
text.AppendLine("MODALITY "+(m ? $"{m.leftHand?.name} / {m.rightHand?.name} / {m.leftController?.name} / {m.rightController?.name}" : "missing"));
return text.ToString();
