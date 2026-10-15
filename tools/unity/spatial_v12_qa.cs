var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) throw new System.Exception("Wait for readiness");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder();var camera=app.Viewer;var rig=app.Origin.transform;var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();
 var initialRig=new UnityEngine.Pose(rig.position,rig.rotation);var initialRoot=new UnityEngine.Pose(app.Root.transform.position,app.Root.transform.rotation);var initialCamera=new UnityEngine.Pose(camera.transform.position,camera.transform.rotation);
 var mouse=UnityEngine.InputSystem.Mouse.current;var inputSettings=UnityEngine.InputSystem.InputSystem.settings;var background=inputSettings.backgroundBehavior;var editorInput=inputSettings.editorInputBehaviorInPlayMode;
 inputSettings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus; inputSettings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; UnityEngine.InputSystem.InputSystem.EnableDevice(mouse);
 System.Action<bool,string> check=(ok,message)=>{if(!ok) throw new System.Exception(message);log.AppendLine("PASS: "+message);};
 System.Func<int,System.Threading.Tasks.Task> wait=ms=>System.Threading.Tasks.Task.Delay(ms);
 System.Action<UnityEngine.Vector2,bool> point=(p,down)=>UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=p}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left,down));
 System.Action<string> capture=name=>
 {
  var oldTarget=camera.targetTexture;var oldActive=UnityEngine.RenderTexture.active;var target=new UnityEngine.RenderTexture(1600,1000,24);var image=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false);
  try {camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;image.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0);image.Apply();System.IO.File.WriteAllBytes("docs/screenshots/v12-"+name+".png",image.EncodeToPNG());}
  finally {camera.targetTexture=oldTarget;UnityEngine.RenderTexture.active=oldActive;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);}
 };
 var driver=app.GetComponent<TwinInspector.EditorDesktopDriver>();
 try
 {
  point(new UnityEngine.Vector2(-100,-100),false);await wait(200);
  app.Style.SetMode(TwinInspector.ViewMode.Twin);app.UI.Show("mode");strip.Recenter(true);await wait(200);
  UnityEngine.UI.Button overlay=null;foreach(var b in app.UI.Panel.GetComponentsInChildren<UnityEngine.UI.Button>()) if(b.name=="Overlay") overlay=b;
  var rect=(UnityEngine.RectTransform)overlay.transform;var pixel=(UnityEngine.Vector2)camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
  point(pixel,false);await wait(160);capture("controls-hover");point(pixel,true);await wait(100);capture("controls-pressed");point(pixel,false);await wait(200);
  check(app.Style.Mode==TwinInspector.ViewMode.Overlay,"actual pointer release accepts Overlay");
  app.Style.SetLens(true);app.Style.SetBlend(.8f);app.UI.Panel.gameObject.SetActive(false);strip.gameObject.SetActive(false);
  UnityEngine.Vector2 hitPixel=default,missPixel=default;bool foundHit=false,foundMiss=false;
  for(int y=-10;y<=30;y++) for(int x=-10;x<=30;x++)
  {
   var p=new UnityEngine.Vector2(camera.pixelWidth*x/20f,camera.pixelHeight*y/20f);bool hit=UnityEngine.Physics.Raycast(camera.ScreenPointToRay(p),out var surface,80,TwinInspector.InputRouter.BuildingMask);
   if(hit && x>4&&x<16&&y>4&&y<16 && !foundHit) {hitPixel=p;foundHit=true;}
   if(!hit && !foundMiss) {missPixel=p;foundMiss=true;}
  }
  check(foundHit,"real hall supplies a building target");
  point(hitPixel,false);await wait(250);check(app.Style.LensVisibility>.99f,"Lens acquires stabilized building hit");var center=app.Style.LensCenter;
  strip.gameObject.SetActive(true);app.UI.Show("display");strip.Recenter(true);await wait(200);
  var slider=app.UI.Panel.GetComponentInChildren<UnityEngine.UI.Slider>();rect=(UnityEngine.RectTransform)slider.transform;pixel=camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));point(pixel,false);await wait(200);
  check(app.Input.OverUI && UnityEngine.Vector3.Distance(center,app.Style.LensCenter)<.002f && app.Style.LensVisibility>.99f,"UI holds Lens centre without moving its target");
  app.UI.Panel.gameObject.SetActive(false);strip.gameObject.SetActive(false);driver.enabled=false;camera.transform.position=initialCamera.position+UnityEngine.Vector3.up*100;camera.transform.rotation=UnityEngine.Quaternion.LookRotation(UnityEngine.Vector3.up,UnityEngine.Vector3.forward);point(new UnityEngine.Vector2(camera.pixelWidth*.5f,camera.pixelHeight*.5f),false);await wait(70);check(!app.Input.HasBuildingHit,"outside-hall pointer has no building hit");check(app.Style.LensVisibility>.8f,"Lens briefly holds through no-hit");await wait(340);
  check(app.Style.LensVisibility<.001f && app.Style.LensCenter==center,"no-hit fades Lens to zero without jumping to origin");
  camera.transform.SetPositionAndRotation(initialCamera.position,initialCamera.rotation);driver.enabled=true;await wait(3500);app.Style.SetLens(false);app.Style.SetMode(TwinInspector.ViewMode.Twin);app.UI.Panel.gameObject.SetActive(false);strip.gameObject.SetActive(false);
  var tiles=(System.Collections.Generic.List<TwinInspector.TileHandle>)typeof(TwinInspector.TwinLoader).GetField("tiles",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(app.Loader);
  System.Func<string> lods=()=> {int a=0,b=0,c=0;foreach(var tile in tiles) if(tile.Visual) {if(tile.Lod==0)a++;else if(tile.Lod==1)b++;else c++;}return a+"/"+b+"/"+c;};
  string before=lods();rig.position+=camera.transform.forward*25;await wait(5000);string after=lods();
  check(app.Loader.Failures==0 && app.Loader.Triangles<=1200000,"streaming after movement respects source triangle budget");log.AppendLine("LOD0/1/2 visible tile counts before="+before+" after="+after);
  rig.SetPositionAndRotation(initialRig.position,initialRig.rotation);await wait(5000);
  app.Manual.SetActive(true);app.Manual.Nudge(.12f,-.08f,0,7);await wait(250);
  var pTwin=new UnityEngine.Vector3(20,10,1.5f);var restored=UnityEngine.Shader.GetGlobalMatrix("_WorldToTwin").MultiplyPoint3x4(app.Root.TwinToWorld(pTwin));
  check(UnityEngine.Vector3.Distance(pTwin,restored)<.0001f,"rendered coordinate matrix follows live alignment"); capture("aligned-contours");
  app.Root.transform.SetPositionAndRotation(initialRoot.position,initialRoot.rotation);app.Manual.SetActive(false);
  driver.enabled=false;camera.transform.position=app.Root.TwinToWorld(new UnityEngine.Vector3(20,10,8));camera.transform.rotation=UnityEngine.Quaternion.LookRotation(app.Root.TwinToWorld(new UnityEngine.Vector3(16,10,0))-camera.transform.position,UnityEngine.Vector3.forward);await wait(200);capture("datum-in-hall");
  app.Loader.SetVisible(false);await wait(100);capture("datum-grid");app.Loader.SetVisible(true);
  camera.transform.SetPositionAndRotation(initialCamera.position,initialCamera.rotation);driver.enabled=true;
  var counts=new object[]{0,0,0};typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType").Invoke(null,counts);log.AppendLine("Editor console counts error/warning/log="+counts[0]+"/"+counts[1]+"/"+counts[2]);
 }
 catch(System.Exception error) {log.AppendLine("FAIL: "+error);}
 finally
 {
  point(new UnityEngine.Vector2(-100,-100),false);app.Style.SetLens(false);app.Style.SetMode(TwinInspector.ViewMode.Overlay);app.Style.SetBlend(.45f);app.Manual.SetActive(false);
  rig.SetPositionAndRotation(initialRig.position,initialRig.rotation);app.Root.transform.SetPositionAndRotation(initialRoot.position,initialRoot.rotation);camera.transform.SetPositionAndRotation(initialCamera.position,initialCamera.rotation);driver.enabled=true;
  inputSettings.backgroundBehavior=background;inputSettings.editorInputBehaviorInPlayMode=editorInput;strip.gameObject.SetActive(true);app.UI.Show("setup");strip.Recenter(true);
  System.IO.File.WriteAllText("docs/evidence/spatial-v12.txt",log.ToString());
 }
};
_=run();return "Spatial, Lens, LOD and rendered interaction-state checks started";
