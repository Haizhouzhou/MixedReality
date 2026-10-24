var app=TwinInspector.AppBootstrap.Instance;
if(!app||!app.Ready||!app.Diff.Settled)return "WAIT engine";
bool assertPatches=System.IO.File.Exists("../work/assert-patches-v13.flag");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder();string created=null;
 var dismissalPath=TwinInspector.TwinPaths.SaveFile("diff",app.Space.scan+"-dismissed");
 var saved=System.IO.File.Exists(dismissalPath)?System.IO.File.ReadAllBytes(dismissalPath):null;
 var backup=System.IO.File.Exists(dismissalPath+".bak")?System.IO.File.ReadAllBytes(dismissalPath+".bak"):null;
 var mouse=UnityEngine.InputSystem.Mouse.current??UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
 var inputSettings=UnityEngine.InputSystem.InputSystem.settings;var oldBackground=inputSettings.backgroundBehavior;var oldEditor=inputSettings.editorInputBehaviorInPlayMode;
 inputSettings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;inputSettings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
 UnityEngine.InputSystem.InputSystem.EnableDevice(mouse);
 System.Action<bool,string> check=(ok,text)=>{if(!ok)throw new System.Exception(text);log.AppendLine("PASS: "+text);};
 System.Func<string,UnityEngine.UI.Button> button=name=>System.Linq.Enumerable.First(app.UI.Panel.GetComponentsInChildren<UnityEngine.UI.Button>(),b=>b.name==name);
 System.Action<UnityEngine.Vector2,bool> point=(p,down)=>{app.Input.SendMessage("OnApplicationFocus",true);UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=p}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left,down));};
 System.Func<UnityEngine.UI.Button,System.Threading.Tasks.Task> click=async b=>{var r=(UnityEngine.RectTransform)b.transform;var p=(UnityEngine.Vector2)app.Viewer.WorldToScreenPoint(r.TransformPoint(r.rect.center));check(p.x>=0&&p.x<app.Viewer.pixelWidth&&p.y>=0&&p.y<app.Viewer.pixelHeight,"visible control "+b.name);point(p,false);await System.Threading.Tasks.Task.Delay(100);point(p,true);await System.Threading.Tasks.Task.Delay(120);point(p,false);await System.Threading.Tasks.Task.Delay(200);};
 System.Action<string> capture=name=>
 {
  var camera=app.Viewer;var previous=camera.targetTexture;var active=UnityEngine.RenderTexture.active;var target=new UnityEngine.RenderTexture(1600,1000,24);var image=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false);
  try{camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;image.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0);image.Apply();System.IO.File.WriteAllBytes("docs/screenshots/v13-"+name+".png",image.EncodeToPNG());}
  finally{camera.targetTexture=previous;UnityEngine.RenderTexture.active=active;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);}
 };
 try
 {
  TwinInspector.L.Language="en";app.UI.Show("list");app.UI.Summon();await System.Threading.Tasks.Task.Delay(500);
  var fixture=((TwinInspector.EditorSceneSurfaceSimulator)app.SceneSource).Fixture;
  var added=System.Array.Find(fixture.edits,e=>e.id=="e2");var missing=System.Array.Find(fixture.edits,e=>e.id=="e1");
  var candidate=System.Linq.Enumerable.First(app.Diff.Candidates,c=>c.kind==TwinInspector.CandidateKind.New&&UnityEngine.Vector3.Distance(c.CentroidTwin(app.Root),TwinInspector.TwinRoot.Vector(added.center_twin))<.5f);
  check(TwinInspector.RegionPatch.AllTriangles<=50000,"all patch geometry <=50000 triangles");capture("candidates");capture("list-candidates");
  string row=TwinInspector.L.Format("?? {0}   {1:F1} m\ncandidate · {2:F1} m²",TwinInspector.AnnotationTypes.Prefix(candidate.Type),UnityEngine.Vector3.Distance(app.Viewer.transform.position,candidate.CentroidWorld(app.Root)),candidate.area_m2);
  await click(button(row));check(app.UI.Page=="picker"&&app.Diff.Selected?.id==candidate.id,"candidate selected through list row");capture("picker-candidate");
  await click(button("Confirm"));created=app.Annotations.Selected?.id;check(created!=null&&app.Annotations.Selected.region?.source=="diff"&&app.UI.Page=="list","UI Confirm creates region annotation and returns to list");
  var item=app.Annotations.Selected;
  var reloaded=new TwinInspector.AnnotationStore(app.Space.scan);check(System.Linq.Enumerable.Any(reloaded.Items,a=>a.id==created&&a.region!=null),"confirmed region survives reload");
  if(assertPatches)
  {
   string exported=TwinInspector.AnnotationExporter.Export(app.Annotations);string folder=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(exported),System.IO.Path.GetFileNameWithoutExtension(exported)+"-patches");
   string obj=System.IO.Path.Combine(folder,created+".obj");check(System.IO.File.Exists(obj),"refinement OBJ exported for confirmed New");
   var lines=System.IO.File.ReadAllLines(obj);check(System.Linq.Enumerable.Any(lines,l=>l.StartsWith("v "))&&System.Linq.Enumerable.Any(lines,l=>l.StartsWith("f ")),"OBJ has vertices and faces");
  }
  var removed=System.Linq.Enumerable.First(app.Diff.Candidates,c=>c.kind==TwinInspector.CandidateKind.Missing&&UnityEngine.Vector3.Distance(c.CentroidTwin(app.Root),TwinInspector.TwinRoot.Vector(missing.center_twin))<.5f);
  var dismissedCenter=removed.CentroidTwin(app.Root);app.Diff.Select(removed);app.UI.Show("picker");await System.Threading.Tasks.Task.Delay(300);await click(button("Dismiss"));check(app.UI.Page=="list"&&System.IO.File.Exists(dismissalPath),"UI Dismiss persists suppression");
  app.UI.Show("display");await System.Threading.Tasks.Task.Delay(300);await click(button("Diff off"));check(!app.Diff.Visible,"Diff off UI control");await click(button("Diff on"));check(app.Diff.Visible,"Diff on UI control");capture("diff-toggle");
  app.Diff.Reevaluate();float deadline=UnityEngine.Time.realtimeSinceStartup+180;
  while(!app.Diff.Settled&&UnityEngine.Time.realtimeSinceStartup<deadline)await System.Threading.Tasks.Task.Delay(500);
  check(app.Diff.Settled,"re-evaluation completes");check(!System.Linq.Enumerable.Any(app.Diff.Candidates,c=>c.kind==TwinInspector.CandidateKind.Missing&&UnityEngine.Vector3.Distance(c.CentroidTwin(app.Root),dismissedCenter)<.5f),"dismissal survives forced re-evaluation");
  log.AppendLine("Editor only; assertPatches="+assertPatches+"; physical hand/headset acceptance remains unverified.");System.IO.File.WriteAllText("docs/evidence/diff-v13.txt",log.ToString());
 }
 catch(System.Exception error){System.IO.File.WriteAllText("docs/evidence/diff-v13.txt",log+"FAIL: "+error);UnityEngine.Debug.LogException(error);}
 finally
 {
  if(created!=null){app.Annotations.Select(created);app.Annotations.DeleteSelected();}
  if(saved==null)System.IO.File.Delete(dismissalPath);else System.IO.File.WriteAllBytes(dismissalPath,saved);
  if(backup==null)System.IO.File.Delete(dismissalPath+".bak");else System.IO.File.WriteAllBytes(dismissalPath+".bak",backup);
  inputSettings.backgroundBehavior=oldBackground;inputSettings.editorInputBehaviorInPlayMode=oldEditor;app.UI.Show("setup");
 }
};_ = run();return "Candidate UI QA started; results in docs/evidence/diff-v13.txt";
