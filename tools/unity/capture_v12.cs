var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) throw new System.Exception("Application not ready");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var camera=app.Viewer; var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();
 var log=new System.Text.StringBuilder();
 var created=new System.Collections.Generic.List<string>();
 var mouse=UnityEngine.InputSystem.Mouse.current;
 var background=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
 var editorInput=UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode;
 UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
 UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
 if(mouse!=null) UnityEngine.InputSystem.InputSystem.EnableDevice(mouse); app.Input.SendMessage("OnApplicationFocus",true);
 System.Action<string> capture=name=>
 {
  var oldTarget=camera.targetTexture; var oldActive=UnityEngine.RenderTexture.active;
  var target=new UnityEngine.RenderTexture(1600,1000,24); var picture=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false);
  try { camera.targetTexture=target; camera.Render(); UnityEngine.RenderTexture.active=target; picture.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0); picture.Apply(); System.IO.File.WriteAllBytes("docs/screenshots/v12-"+name+".png",picture.EncodeToPNG()); }
  finally { camera.targetTexture=oldTarget; UnityEngine.RenderTexture.active=oldActive; target.Release(); UnityEngine.Object.Destroy(picture); UnityEngine.Object.Destroy(target); }
  log.AppendLine("Captured "+name);
 };
 try
 {
  await System.Threading.Tasks.Task.Delay(1500);
  app.Style.SetMode(TwinInspector.ViewMode.Twin); app.Style.SetLens(false); app.UI.Panel.gameObject.SetActive(false); strip.gameObject.SetActive(false);
  app.Style.SetTechnical(false); await System.Threading.Tasks.Task.Delay(300); capture("hall-colour");
  app.Style.SetTechnical(true); await System.Threading.Tasks.Task.Delay(300); capture("hall-technical");
  strip.gameObject.SetActive(true); strip.Recenter(true);
  foreach(var page in new[]{"setup","mode","display","preferences","sound","align","height","list","roam"})
  { if(page=="roam") app.Style.SetMode(TwinInspector.ViewMode.Twin); app.UI.Show(page); await System.Threading.Tasks.Task.Delay(300); capture(page); }
  for(int i=0;i<4;i++)
  {
   var position=camera.transform.position+camera.transform.forward*(8+i*7);
   var item=app.Annotations.Add(app.Root.WorldToTwin(position),app.Root.NormalToTwin(-camera.transform.forward),app.Root.WorldToTwin(camera.transform.position),app.Root.RotationToTwin(camera.transform.rotation));
   created.Add(item.id);app.Annotations.Retype((TwinInspector.AnnotationType)i);
  }
  app.UI.Show("picker");await System.Threading.Tasks.Task.Delay(250);capture("picker");
  app.UI.Show("list");await System.Threading.Tasks.Task.Delay(250);capture("list");
  foreach(var id in created) {app.Annotations.Select(id);app.Annotations.DeleteSelected();}created.Clear();
  app.Style.SetMode(TwinInspector.ViewMode.Overlay); app.Style.SetBlend(.8f); app.Style.SetLens(true);
  app.UI.Panel.gameObject.SetActive(false); strip.gameObject.SetActive(false);
  for(int band=0;band<2;band++)
  {
   float score=float.MaxValue; UnityEngine.Vector2 pixel=default; float distance=0;
   for(int y=2;y<19;y++) for(int x=1;x<19;x++)
   {
    var p=new UnityEngine.Vector2(camera.pixelWidth*x/20f,camera.pixelHeight*y/20f);
    if(UnityEngine.Physics.Raycast(camera.ScreenPointToRay(p),out var hit,60,1<<8))
    { float error=UnityEngine.Mathf.Abs(hit.distance-(band==0?4:18)); if(error<score) { score=error; pixel=p; distance=hit.distance; } }
   }
   if(mouse!=null) UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=pixel});
   app.Input.SendMessage("OnApplicationFocus",true); await System.Threading.Tasks.Task.Delay(350); capture(band==0?"lens-near":"lens-far");
   log.AppendLine("Lens target "+distance+" m; center="+app.Style.LensCenter+"; visibility="+app.Style.LensVisibility+"; input valid="+app.Input.Valid);
  }
  foreach(var name in new[]{"TwinInspector/Twin","TwinInspector/ArchitecturalEdges","TwinInspector/FloorGrid","TwinInspector/Annotation"})
  { var shader=UnityEngine.Shader.Find(name); var messages=UnityEditor.ShaderUtil.GetShaderMessages(shader); log.AppendLine(name+" shader messages="+messages.Length); foreach(var message in messages) log.AppendLine(message.severity+": "+message.message); }
  log.AppendLine("Tiles="+app.Loader.LoadedTiles+" failures="+app.Loader.Failures+" resident triangles="+app.Loader.Triangles);
 }
 catch(System.Exception error) { log.AppendLine("FAIL: "+error); }
 finally
 {
  foreach(var id in created) {app.Annotations.Select(id);if(app.Annotations.Selected!=null)app.Annotations.DeleteSelected();}
  app.Style.SetLens(false); app.Style.SetBlend(.45f); app.Style.SetMode(TwinInspector.ViewMode.Overlay); strip.gameObject.SetActive(true); app.UI.Show("setup"); strip.Recenter(true);
  UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=background; UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
  System.IO.File.WriteAllText("docs/evidence/visual-v12.txt",log.ToString());
 }
};
_=run(); return "v1.2 scene capture started";
