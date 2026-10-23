var app=TwinInspector.AppBootstrap.Instance;
if(!app||!app.Ready)return "WAIT readiness";
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder();var created=new System.Collections.Generic.List<string>();
 var input=UnityEngine.InputSystem.InputSystem.settings;var background=input.backgroundBehavior;var editor=input.editorInputBehaviorInPlayMode;
 var mouse=UnityEngine.InputSystem.Mouse.current??UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
 var driver=app.GetComponent<TwinInspector.EditorDesktopDriver>();bool driverEnabled=driver.enabled,visible=app.Diff.Visible;
 System.Action<bool,string> check=(ok,message)=>{if(!ok)throw new System.Exception(message);log.AppendLine("PASS: "+message);};
 System.Action<UnityEngine.Vector2,bool> point=(p,down)=>{app.Input.SendMessage("OnApplicationFocus",true);UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=p}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left,down));};
 try
 {
  System.IO.File.WriteAllText("docs/evidence/list-refresh-v13.txt","RUNNING\n");
  input.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;input.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;driver.enabled=false;
  for(int i=0;i<6;i++)created.Add(app.Annotations.Add(new UnityEngine.Vector3(i,0,1),UnityEngine.Vector3.forward,app.Root.WorldToTwin(app.Viewer.transform.position),app.Root.RotationToTwin(app.Viewer.transform.rotation)).id);
  app.UI.Show("list");app.UI.Summon();await System.Threading.Tasks.Task.Delay(500);
  var scroll=app.UI.Panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();scroll.content.anchoredPosition=new UnityEngine.Vector2(0,56);
  var screen=(UnityEngine.Vector2)app.Viewer.WorldToScreenPoint(scroll.viewport.TransformPoint(scroll.viewport.rect.center));point(screen,false);await System.Threading.Tasks.Task.Delay(250);
  check(app.Input.OverUI,"production pointer hovers the list");
  app.Diff.Visible=!visible;await System.Threading.Tasks.Task.Delay(900);
  check(scroll&&scroll.content,"background diff event retains hovered ScrollRect");
  point(screen,true);await System.Threading.Tasks.Task.Delay(150);point(new UnityEngine.Vector2(1,1),true);await System.Threading.Tasks.Task.Delay(900);
  check(app.Input.Held&&scroll,"held pointer retains ScrollRect even outside the panel");
  point(new UnityEngine.Vector2(1,1),false);await System.Threading.Tasks.Task.Delay(100);
  scroll.StopMovement();scroll.content.anchoredPosition=new UnityEngine.Vector2(0,56);
  await System.Threading.Tasks.Task.Delay(1200);
  check(!scroll,"queued refresh applies after release and pointer leaves");
  var refreshed=app.UI.Panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
  check(refreshed&&UnityEngine.Mathf.Abs(refreshed.content.anchoredPosition.y-56)<1,"background refresh preserves scroll offset");
 }
 catch(System.Exception e){log.AppendLine("FAIL: "+e);}
 finally
 {
  point(new UnityEngine.Vector2(1,1),false);foreach(var id in created){app.Annotations.Select(id);app.Annotations.DeleteSelected();}
  app.Diff.Visible=visible;driver.enabled=driverEnabled;input.backgroundBehavior=background;input.editorInputBehaviorInPlayMode=editor;app.UI.Show("setup");
  System.IO.File.WriteAllText("docs/evidence/list-refresh-v13.txt",log.ToString());
 }
};
_=run();return "List background refresh regression started";
