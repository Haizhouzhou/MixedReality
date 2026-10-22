var app=TwinInspector.AppBootstrap.Instance;if(!app||!app.Ready)return "WAIT app";
var log=new System.Text.StringBuilder();
app.UI.Show("diagnostics");app.UI.Summon();
foreach(var key in new[]{"button.depthProbe","button.cameraProbe"})
{
 var label=TwinInspector.L.Get(key);var button=System.Linq.Enumerable.First(app.UI.Panel.GetComponentsInChildren<UnityEngine.UI.Button>(),b=>b.name==label);button.onClick.Invoke();
 if(app.Probes.Status!=TwinInspector.L.Get("text.editorProbeUnavailable"))throw new System.Exception("Editor probe must report unavailable");
 log.AppendLine("PASS: "+key+" -> Editor: probe unavailable");
}
var manager=app.Viewer.GetComponent<UnityEngine.XR.ARFoundation.AROcclusionManager>();if(!manager||manager.enabled)throw new System.Exception("Occlusion manager must exist and remain disabled");
log.AppendLine("PASS: Camera occlusion manager exists, disabled; device descriptor and Camera2 characteristics UNVERIFIED");
System.IO.File.WriteAllText("docs/evidence/probes-v13.txt",log.ToString());return log.ToString();
