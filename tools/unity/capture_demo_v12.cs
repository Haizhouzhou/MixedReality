var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready || !app.Loader.Manifest.demo) throw new System.Exception("Packaged demo must be ready");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder();var camera=app.Viewer;var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();
 var mouse=UnityEngine.InputSystem.Mouse.current;var settings=UnityEngine.InputSystem.InputSystem.settings;
 var background=settings.backgroundBehavior;var editorInput=settings.editorInputBehaviorInPlayMode;
 System.Action<bool,string> check=(ok,message)=>{if(!ok)throw new System.Exception(message);log.AppendLine("PASS: "+message);};
 System.Action<string> capture=name=>
 {
  var target=new UnityEngine.RenderTexture(1600,1000,24){antiAliasing=4};var picture=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false);
  var oldTarget=camera.targetTexture;var oldActive=UnityEngine.RenderTexture.active;
  try {camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;picture.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0);picture.Apply();System.IO.File.WriteAllBytes("docs/screenshots/v12-demo-"+name+".png",picture.EncodeToPNG());}
  finally {camera.targetTexture=oldTarget;UnityEngine.RenderTexture.active=oldActive;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(picture);}
 };
 try
 {
  check(app.Loader.Failures==0 && app.Loader.LoadedTiles==32,"all 32 packaged tiles load");
  var modality=app.Origin.GetComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager>();
  foreach(var group in new[]{modality.leftController,modality.rightController}) check(group.GetComponentsInChildren<UnityEngine.MeshRenderer>(true).Length==0 && group.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true).Length==0,"no controller mesh in "+group.name);
  int filters=0;foreach(var source in app.Origin.GetComponentsInChildren<TwinInspector.TrackedRayPose>(true)) {filters++;check(source.Source && source.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>().rayOriginTransform==source.transform,"derived filtered ray owns the actual hit-test transform");}
  check(filters==4,"asset rebuild retains exactly four pointing filters");
  foreach(var beam in app.Origin.GetComponentsInChildren<UnityEngine.LineRenderer>(true)) check(!beam.enabled || beam.forceRenderingOff,"sample beam disabled");
  foreach(var font in new[]{app.Tokens.Regular,app.Tokens.Medium,app.Tokens.Semibold})
  {
   float advance=font.characterLookupTable['0'].glyph.metrics.horizontalAdvance;
   for(uint digit='0';digit<='9';digit++) check(UnityEngine.Mathf.Abs(font.characterLookupTable[digit].glyph.metrics.horizontalAdvance-advance)<.001f,font.name+" tabular digit "+(char)digit);
   foreach(char symbol in "Δ•−") check(font.HasCharacter(symbol),font.name+" contains "+symbol);
   check(!new UnityEditor.SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild").boolValue,"font metrics retained in player build");
  }
  foreach(string name in new[]{"hover","confirm"})
  {
   var clip=(UnityEngine.AudioClip)typeof(TwinInspector.MicroFeedback).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(app.Feedback);
   var samples=new float[clip.samples];clip.GetData(samples,0);check(samples[0]==0 && samples[samples.Length-1]==0,"cached "+name+" audio has zero-ended envelope");
  }
  app.Style.SetMode(TwinInspector.ViewMode.Twin);app.UI.Panel.gameObject.SetActive(false);strip.gameObject.SetActive(false);
  app.Style.SetTechnical(false);await System.Threading.Tasks.Task.Delay(200);capture("colour");
  app.Style.SetTechnical(true);await System.Threading.Tasks.Task.Delay(200);capture("technical");
  log.AppendLine("Editor rendered batches="+UnityEditor.UnityStats.batches+"; not a headset measurement.");
  settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  UnityEngine.InputSystem.InputSystem.EnableDevice(mouse);app.Input.SendMessage("OnApplicationFocus",true);app.UI.BeginAnnotation();
  bool found=false;UnityEngine.Vector2 pixel=default;
  for(int y=5;y<15&&!found;y++) for(int x=5;x<15&&!found;x++) {var p=new UnityEngine.Vector2(camera.pixelWidth*x/20f,camera.pixelHeight*y/20f);if(UnityEngine.Physics.Raycast(camera.ScreenPointToRay(p),out var hit,30,1<<8) && hit.distance>5) {pixel=p;found=true;}}
  check(found,"reticle uses a real hall surface");UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState{position=pixel});await System.Threading.Tasks.Task.Delay(200);var reticle=UnityEngine.GameObject.Find("Swiss surface reticle");log.AppendLine("Reticle active="+(reticle!=null)+" placer="+app.Placer.Active+" position="+(reticle?reticle.transform.position:UnityEngine.Vector3.zero)+" normal="+app.Input.PointerNormal+" pointer="+app.Input.PointerPosition+" distance="+app.Input.BuildingHit.distance);capture("reticle");
  check(app.Input.HasPointer && app.Input.Valid,"reticle follows the active building hit");
  var counts=new object[]{0,0,0};typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType").Invoke(null,counts);check((int)counts[0]==0 && (int)counts[1]==0,"fresh Editor runtime has zero console errors and warnings");
 }
 catch(System.Exception error) {log.AppendLine("FAIL: "+error);}
 finally {settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editorInput;app.Placer.Active=false;app.Style.SetMode(TwinInspector.ViewMode.Overlay);strip.gameObject.SetActive(true);app.UI.Show("setup");System.IO.File.WriteAllText("docs/evidence/demo-render-v12.txt",log.ToString());}
};
_=run();return "Packaged scene, font, audio and rendered-output checks started";
