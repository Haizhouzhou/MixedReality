var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) throw new System.Exception("Wait for application readiness");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder(); var created=new System.Collections.Generic.List<string>();
 var mouse=UnityEngine.InputSystem.Mouse.current ?? UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
 var keyboard=UnityEngine.InputSystem.Keyboard.current ?? UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
 var background=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
 var editorInput=UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode;
 UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
 UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
 UnityEngine.InputSystem.InputSystem.EnableDevice(mouse);UnityEngine.InputSystem.InputSystem.EnableDevice(keyboard);
 var camera=app.Viewer; var rig=app.Origin.transform; var initialPosition=rig.position; var initialRotation=camera.transform.rotation;
 System.Action<bool,string> check=(ok,message)=>{if(!ok) throw new System.Exception(message);log.AppendLine("PASS: "+message);};
 System.Func<int,System.Threading.Tasks.Task> wait=ms=>System.Threading.Tasks.Task.Delay(ms);
 System.Action<UnityEngine.Vector2,bool> point=(position,down)=>UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState {position=position}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left,down));
 System.Func<UnityEngine.UI.Button,System.Threading.Tasks.Task> click=async button=>
 {
  var rect=(UnityEngine.RectTransform)button.transform; var pixel=(UnityEngine.Vector2)camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
  check(pixel.x>=0 && pixel.x<camera.pixelWidth && pixel.y>=0 && pixel.y<camera.pixelHeight,"button visible: "+button.name+" "+pixel);
  point(pixel,false);await wait(120);point(pixel,true);await wait(120);point(pixel,false);await wait(180);
 };
 System.Func<string,UnityEngine.UI.Button> find=name=>
 {
  foreach(var button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(UnityEngine.FindObjectsSortMode.None)) if(button.name==name) return button;
  throw new System.Exception("Button missing: "+name);
 };
 try
 {
  check(app.Input.Desktop,"desktop selected with no tracked HMD");
  check(UnityEngine.Mathf.Abs(camera.transform.position.y-rig.position.y-1.6f)<.01f,"desktop eye height 1.6 m");
  var modality=rig.GetComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager>();
  check(!modality.leftController.activeInHierarchy && !modality.rightController.activeInHierarchy,"untracked controller nodes automatically inactive");
  foreach(var line in rig.GetComponentsInChildren<UnityEngine.LineRenderer>(true)) check(!line.enabled || line.forceRenderingOff,"sample beam suppressed");
  check(modality.leftController.GetComponentsInChildren<UnityEngine.MeshRenderer>(true).Length==0 && modality.rightController.GetComponentsInChildren<UnityEngine.MeshRenderer>(true).Length==0,"controller meshes stripped");
  var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();
  check(strip && strip.GetComponent<UnityEngine.Canvas>().renderMode==UnityEngine.RenderMode.WorldSpace && !strip.transform.IsChildOf(camera.transform),"ToolStrip independently anchored in world space");
  System.Action<string> checkDock=label=>
  {
   var panelCorners=new UnityEngine.Vector3[4];var stripCorners=new UnityEngine.Vector3[4];
   ((UnityEngine.RectTransform)app.UI.Panel.transform).GetWorldCorners(panelCorners);
   ((UnityEngine.RectTransform)strip.transform).GetWorldCorners(stripCorners);
   var separation=(panelCorners[0]+panelCorners[3]-stripCorners[1]-stripCorners[2])*.5f;
   check(UnityEngine.Mathf.Abs(UnityEngine.Vector3.Dot(separation,strip.transform.up)-.024f)<.0001f && UnityEngine.Mathf.Abs(UnityEngine.Vector3.Dot(separation,strip.transform.forward))<.0001f && UnityEngine.Mathf.Abs(UnityEngine.Vector3.Dot(separation,strip.transform.right))<.0001f,"panel centered 24 mm above strip at same depth: "+label);
  };
  checkDock("initial setup");
  var panelStart=app.UI.Panel.transform.position;
  var start=strip.transform.position;
  camera.transform.rotation=initialRotation*UnityEngine.Quaternion.Euler(0,15,0);await wait(300);
  check(UnityEngine.Vector3.Distance(start,strip.transform.position)<.001f,"15 degree look leaves ToolStrip stationary");
  check(UnityEngine.Vector3.Distance(panelStart,app.UI.Panel.transform.position)<.001f,"15 degree look leaves the full panel stationary");
  camera.transform.rotation=initialRotation*UnityEngine.Quaternion.Euler(0,50,0);await wait(500);
  check(UnityEngine.Vector3.Distance(start,strip.transform.position)>.15f,"50 degree turn recenters ToolStrip");
  checkDock("after shared recenter");
  camera.transform.rotation=initialRotation;strip.Recenter(true);await wait(200);
  foreach(var label in new[]{"MODE","ALIGN","LIST"})
  {
   await click(find(label));check(app.UI.Page==label.ToLowerInvariant(),"left mouse opens "+label);checkDock(label);
  }
  await click(find("MODE"));
  var slider=app.UI.Panel.GetComponentInChildren<UnityEngine.UI.Slider>();
  var sliderRect=(UnityEngine.RectTransform)slider.transform;
  var sliderPixel=(UnityEngine.Vector2)camera.WorldToScreenPoint(sliderRect.TransformPoint(new UnityEngine.Vector3(sliderRect.rect.xMin+sliderRect.rect.width*.75f,sliderRect.rect.center.y,0)));
  point(sliderPixel,false);await wait(100);point(sliderPixel,true);await wait(100);point(sliderPixel,false);await wait(100);
  check(app.Style.Blend>.65f && app.Style.Blend<.85f,"ordinary mouse pointer adjusts the world-space opacity slider");app.Style.SetBlend(.45f);
  await click(find("ANNOTATE"));check(app.Placer.Active && !app.UI.Panel.gameObject.activeSelf,"left mouse enables annotation placement");
  UnityEngine.Vector2 surfacePixel=default;bool found=false;
  for(int y=3;y<9 && !found;y++) for(int x=2;x<9 && !found;x++)
  {
   var pixel=new UnityEngine.Vector2(camera.pixelWidth*x/10f,camera.pixelHeight*y/10f);
   if(UnityEngine.Physics.Raycast(camera.ScreenPointToRay(pixel),out var hit,70,1<<8)) {surfacePixel=pixel;found=true;}
  }
  check(found,"a visible LOD2 collider accepts the screen ray");
  int count=app.Annotations.Items.Count;point(surfacePixel,false);await wait(200);
  check(app.Input.HasHit && !app.Input.OverUI,"mouse previews actual surface hit");
  point(surfacePixel,true);await wait(150);point(surfacePixel,false);await wait(200);
  if(app.Annotations.Items.Count>count) created.Add(app.Annotations.Selected.id);
  check(app.Annotations.Items.Count==count+1 && app.UI.Page=="picker","one mouse click creates one annotation and opens picker");
  checkDock("annotation type picker");
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Digit1));await wait(120);
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());await wait(120);
  check(app.UI.Page=="mode","keyboard 1 opens Mode");
  foreach(var key in new[]{UnityEngine.InputSystem.Key.Digit2,UnityEngine.InputSystem.Key.Digit3,UnityEngine.InputSystem.Key.Digit4,UnityEngine.InputSystem.Key.M,UnityEngine.InputSystem.Key.Tab})
  {
   UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(key));await wait(100);
   UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());await wait(100);
   if(key==UnityEngine.InputSystem.Key.Digit2) check(app.UI.Page=="align","keyboard 2 opens Align");
   if(key==UnityEngine.InputSystem.Key.Digit3) check(app.Placer.Active,"keyboard 3 enables Annotate");
   if(key==UnityEngine.InputSystem.Key.Digit4) check(app.UI.Page=="list" && !app.Placer.Active,"keyboard 4 opens List and ends placement");
   if(key==UnityEngine.InputSystem.Key.M) check(app.UI.Page=="marker" && app.Markers.AwaitingConfirmation,"M simulates catalog marker zero");
   if(key==UnityEngine.InputSystem.Key.Tab) check(app.UI.Panel.gameObject.activeSelf,"Tab summons the active panel");
  }
  app.Markers.Cancel();app.UI.Show("setup");
  var before=rig.position;
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState {position=new UnityEngine.Vector2(camera.pixelWidth/2,camera.pixelHeight/2)}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right));
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));await wait(350);
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState());await wait(120);
  check(UnityEngine.Vector3.Distance(before,rig.position)>.2f,"RMB + W moves the viewer rig");
  check(UnityEngine.Cursor.visible && UnityEngine.Cursor.lockState==UnityEngine.CursorLockMode.None,"releasing RMB restores the visible unlocked cursor");
  var beforeLook=camera.transform.rotation;
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState {delta=new UnityEngine.Vector2(80,-30)}.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right));await wait(120);
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState());await wait(100);
  check(UnityEngine.Quaternion.Angle(beforeLook,camera.transform.rotation)>3,"RMB delta rotates the camera");
  check(UnityEngine.Mathf.Abs(UnityEngine.Mathf.DeltaAngle(0,camera.transform.eulerAngles.z))<.01f,"flight keeps camera roll zero");
  float beforeHeight=rig.position.y;
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.E));await wait(250);
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());await wait(100);
  check(rig.position.y>beforeHeight+.2f,"E moves vertically in world space");
  log.AppendLine("Editor Input System events exercised; physical headset hands and controllers require device acceptance.");
 }
 catch(System.Exception error) {log.AppendLine("FAIL: "+error);UnityEngine.Debug.LogException(error);}
 finally
 {
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState());
  rig.position=initialPosition;camera.transform.rotation=initialRotation;
  foreach(var id in created){app.Annotations.Select(id);app.Annotations.DeleteSelected();}
  app.UI.Show("setup");app.UI.Summon();
  UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=background;
  UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
  System.IO.Directory.CreateDirectory("docs/evidence");System.IO.File.WriteAllText("docs/evidence/desktop-v11.txt",log.ToString());
 }
};
_ = run();return "Desktop interaction QA started";
