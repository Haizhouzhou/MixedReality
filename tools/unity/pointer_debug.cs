var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) return "Application not ready";
var text=new System.Text.StringBuilder();
var system=UnityEngine.EventSystems.EventSystem.current;
if(!system) return "No active EventSystem";
var module=system.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
var mouse=UnityEngine.InputSystem.Mouse.current;
text.AppendLine($"focused={UnityEngine.Application.isFocused} module={system.currentInputModule} active={module && module.isActiveAndEnabled} mouse={module && module.enableMouseInput} pointer={(mouse==null ? "unavailable" : mouse.position.ReadValue().ToString())} screen={UnityEngine.Screen.width}x{UnityEngine.Screen.height}");
foreach(var button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(UnityEngine.FindObjectsSortMode.None)) if(button.name=="MODE") {
 var rect=(UnityEngine.RectTransform)button.transform;
 var pixel=(UnityEngine.Vector2)app.Viewer.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
 var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
 system.RaycastAll(new UnityEngine.EventSystems.PointerEventData(system){position=pixel},hits);
 text.AppendLine("MODE "+pixel+" hits="+hits.Count);
 foreach(var hit in hits) text.AppendLine(hit.gameObject.name+" / "+hit.module.GetType().Name+" distance="+hit.distance);
}
return text.ToString();
