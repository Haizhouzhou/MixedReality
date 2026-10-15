var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) throw new System.Exception("Wait for readiness");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder();
 var modality=app.Origin.GetComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager>();
 var driver=app.GetComponent<TwinInspector.EditorDesktopDriver>();
 var camera=app.Viewer; var cameraPose=new UnityEngine.Pose(camera.transform.position,camera.transform.rotation); var rootPose=new UnityEngine.Pose(app.Root.transform.position,app.Root.transform.rotation);
 var rigPose=new UnityEngine.Pose(app.Origin.transform.position,app.Origin.transform.rotation);
 bool preferred=TwinInspector.InteractionPreferences.LeftHand; float originalVolume=TwinInspector.InteractionPreferences.Volume,originalBlend=app.Style.Blend,originalRadius=app.Style.LensRadius;
 var groups=new[]{modality.leftHand,modality.rightHand,modality.leftController,modality.rightController};
 var enabled=new bool[4]; for(int i=0;i<4;i++) enabled[i]=groups[i].activeSelf;
 var savedBehaviours=new System.Collections.Generic.Dictionary<UnityEngine.Behaviour,bool>();
 var rays=new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
 var sources=new System.Collections.Generic.Dictionary<TwinInspector.TrackedRayPose,UnityEngine.Transform>();
 var readers=new System.Collections.Generic.Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor,UnityEngine.XR.Interaction.Toolkit.Inputs.Readers.XRInputButtonReader[]>();
 var created=new System.Collections.Generic.List<string>();
 var aim=new UnityEngine.GameObject("Single-hand QA tracked pose").transform; aim.SetParent(app.Origin.transform,true);
 var inputSettings=UnityEngine.InputSystem.InputSystem.settings;var background=inputSettings.backgroundBehavior;var editorInput=inputSettings.editorInputBehaviorInPlayMode;
 inputSettings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;inputSettings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
 var hmd=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.XR.XRHMD>();UnityEngine.InputSystem.InputSystem.EnableDevice(hmd);
 var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();
 var alignmentPath=TwinInspector.TwinPaths.SaveFile("alignment",app.Loader.Manifest.scan);
 var alignmentBytes=System.IO.File.Exists(alignmentPath)?System.IO.File.ReadAllBytes(alignmentPath):null;
 System.Action<bool,string> check=(ok,message)=>{ if(!ok) throw new System.Exception(message); log.AppendLine("PASS: "+message);System.IO.File.WriteAllText("docs/evidence/single-hand-v12.txt",log.ToString()); };
 System.Func<int,System.Threading.Tasks.Task> wait=ms=>System.Threading.Tasks.Task.Delay(ms);
 System.Func<string,UnityEngine.UI.Button> find=name=>
 { foreach(var b in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(UnityEngine.FindObjectsSortMode.None)) if(b.name==name) return b; throw new System.Exception("Missing button "+name); };
 try
 {
  System.IO.File.WriteAllText("docs/evidence/single-hand-v12.txt","RUNNING synthetic XRI workflow\n");
  driver.enabled=false; modality.enabled=false;
  foreach(var group in groups) foreach(var behaviour in group.GetComponentsInChildren<UnityEngine.Behaviour>(true))
  {
   string name=behaviour.GetType().Name;
   if(name=="XRPokeInteractor" || name=="TrackedPoseDriver" || name=="XRHandTrackingEvents" || name=="PokeGestureDetector" || name=="MetaSystemGestureDetector" || name=="XRHandMeshController" || name=="XRHandSkeletonDriver") { savedBehaviours[behaviour]=behaviour.enabled; behaviour.enabled=false; }
  }
  foreach(var ray in app.Input.Rays) if(ray.name=="Inspection ray")
  {
   rays.Add(ray); var source=ray.GetComponent<TwinInspector.TrackedRayPose>(); sources[source]=source.Source; source.Source=aim;
   readers[ray]=new[]{ray.selectInput,ray.uiPressInput};
   var manual=new UnityEngine.XR.Interaction.Toolkit.Inputs.Readers.XRInputButtonReader { inputSourceMode=UnityEngine.XR.Interaction.Toolkit.Inputs.Readers.XRInputButtonReader.InputSourceMode.ManualValue };
   ray.selectInput=manual; ray.uiPressInput=manual;
  }
  UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(hmd.isTracked,(byte)1);
  await wait(300); check(!app.Input.Desktop,"tracked HMD fixture selects the production XR path");
  for(int side=0;side<4;side++)
  {
   bool left=side%2==0; string label=(left?"LEFT ONLY ":"RIGHT ONLY ")+(side<2?"HAND":"CONTROLLER");
   TwinInspector.InteractionPreferences.LeftHand=!left;
   for(int i=0;i<4;i++) groups[i].SetActive(i==side);
   var ray=groups[side].transform.Find("Inspection ray").GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
   var reader=ray.selectInput;
   aim.SetPositionAndRotation(camera.transform.position+camera.transform.right*(left?-.12f:.12f)-UnityEngine.Vector3.up*.12f,camera.transform.rotation);
   app.Input.SendMessage("OnApplicationFocus",true);
   ray.GetComponent<TwinInspector.TrackedRayPose>().SendMessage("OnApplicationFocus",true);
   reader.QueueManualState(false,0); await wait(500);
   check(app.Input.Valid && app.Input.ActiveLeft==left,label+": available hand overrides absent preferred hand");
   System.Func<UnityEngine.Vector3,System.Threading.Tasks.Task> point=async target=> { aim.position=camera.transform.position+camera.transform.right*(left?-.12f:.12f)-UnityEngine.Vector3.up*.12f; aim.rotation=UnityEngine.Quaternion.LookRotation(target-aim.position,UnityEngine.Vector3.up); await wait(500); };
   System.Func<string,System.Threading.Tasks.Task> click=async name=>
   {
    var button=find(name); var rect=(UnityEngine.RectTransform)button.transform;
    await point(rect.TransformPoint(rect.rect.center));
    if(!app.Input.OverUI) { log.AppendLine("XR UI debug: enabled="+ray.enableUIInteraction+" active="+ray.isActiveAndEnabled+" model="+ray.TryGetUIModel(out var model)+" points="+model.raycastPoints?.Count+" origin="+ray.rayOriginTransform.position+" forward="+ray.rayOriginTransform.forward+" target="+rect.TransformPoint(rect.rect.center)+" raw="+aim.position+" ui="+ray.TryGetCurrentUIRaycastResult(out var uiHit)+" uiObject="+uiHit.gameObject+" held="+app.Input.Held+" pointer="+app.Input.HasPointer); }
    check(app.Input.OverUI,label+": stabilized ray hits "+name);
    reader.QueueManualState(false,.45f); await wait(45); reader.QueueManualState(true,1); await wait(110); reader.QueueManualState(false,0); await wait(320); log.AppendLine(label+": after "+name+" page="+app.UI.Page);
   };
   app.Manual.HeightUnlocked=false; app.Style.SetLens(false); app.UI.Show("setup"); strip.Recenter(true); await wait(200);
   var spatial=typeof(TwinInspector.ToolStrip).GetMethod("UpdateSpatialPose",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
   var moved=typeof(TwinInspector.ToolStrip).GetField("movedAt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
   strip.enabled=false;spatial.Invoke(strip,new object[]{false,default(UnityEngine.Pose)});spatial.Invoke(strip,new object[]{true,default(UnityEngine.Pose)});await wait(220);spatial.Invoke(strip,new object[]{true,default(UnityEngine.Pose)});
   float summoned=(float)moved.GetValue(strip);await wait(200);spatial.Invoke(strip,new object[]{true,default(UnityEngine.Pose)});
   check((float)moved.GetValue(strip)==summoned && !strip.PalmAnchored,label+": qualified palm summons once into a world dock");spatial.Invoke(strip,new object[]{false,default(UnityEngine.Pose)});strip.enabled=true;

   await click("Preferences"); await click(left?"Left hand":"Right hand"); await click("Feedback sound"); await click(TwinInspector.InteractionPreferences.Sound?"Sound: on":"Sound: muted"); await click(TwinInspector.InteractionPreferences.Sound?"Sound: on":"Sound: muted"); await click("Back"); await click("Recenter / back");
   await click("MODE"); await click(app.Style.Technical?"Technical":"Colour"); await click(app.Style.Technical?"Technical":"Colour"); await click("Reality"); await click("ALIGN"); check(app.Style.Mode==TwinInspector.ViewMode.Overlay,label+": Align from Reality enters Overlay");
   await click("Fine adjustment"); await click("X +"); await click("Back");
   // A fixed initial tracked pose, not the moving mesh hit, drives one-hand registration.
   app.UI.Panel.gameObject.SetActive(false); strip.gameObject.SetActive(false);
   UnityEngine.Vector3 surface=default; bool found=false;
   for(int y=3;y<9&&!found;y++) for(int x=1;x<9&&!found;x++)
    if(UnityEngine.Physics.Raycast(camera.ViewportPointToRay(new UnityEngine.Vector3(x/10f,y/10f,0)),out var hit,60,1<<8)) { surface=hit.point; found=true; }
   check(found,label+": real building surface available"); await point(surface);
   var before=app.Root.transform.position; reader.QueueManualState(true,1); await wait(130); aim.position+=UnityEngine.Vector3.right*.1f+UnityEngine.Vector3.up*.1f; await wait(160);
   check(UnityEngine.Mathf.Abs(app.Root.transform.position.x-before.x-.1f)<.025f && UnityEngine.Mathf.Abs(app.Root.transform.position.y-before.y)<.001f,label+": one-hand fixed-reference drag keeps height locked");
   reader.QueueManualState(false,0); await wait(120); check(!app.Manual.Dragging,label+": release ends registration");
   reader.QueueManualState(true,1);await wait(100);ray.enabled=false;await wait(100);check(!app.Input.Valid&&!app.Manual.Dragging,label+": source loss cancels manipulation");ray.enabled=true;await wait(100);check(!app.Input.Pressed&&!app.Input.Held,label+": reacquisition mid-pinch does not click");reader.QueueManualState(false,0);await wait(160);
   app.Root.transform.SetPositionAndRotation(rootPose.position,rootPose.rotation);
   strip.gameObject.SetActive(true); app.UI.Show("align"); strip.Recenter(true); await wait(200);
   await click("Height / floor"); await click("□ Height locked"); await click("Up 1 cm"); await click("Snap to floor"); await click("Back"); await click("Save alignment");
   await app.Anchors.RestoreAsync(); check(app.Anchors.Status.Contains("restored"),label+": saved alignment reloads");
   app.Root.transform.SetPositionAndRotation(rootPose.position,rootPose.rotation);
   app.Markers.SimulateFirst(); app.UI.Show("marker"); await wait(200); await click("Confirm"); check(!app.Markers.AwaitingConfirmation,label+": simulated marker confirmation accepted"); app.Root.transform.SetPositionAndRotation(rootPose.position,rootPose.rotation);
   await click("MODE"); await click("Twin"); await click("Roam");
   var forward=find("Forward"); var forwardRect=(UnityEngine.RectTransform)forward.transform; await point(forwardRect.TransformPoint(forwardRect.rect.center));
   var physical=app.Origin.transform.position; reader.QueueManualState(true,1); await wait(450); reader.QueueManualState(false,0); await wait(130);
   check(UnityEngine.Vector3.Distance(app.Origin.transform.position,physical)>.05f,label+": one-hand hold moves viewer");
   await click("Return to room"); check(UnityEngine.Vector3.Distance(app.Origin.transform.position,physical)<.002f,label+": return restores physical pose");
   await click("Turn +30°"); await click("Back"); await click("Overlay"); check(UnityEngine.Quaternion.Angle(app.Origin.transform.rotation,rigPose.rotation)<.01f,label+": leaving Twin removes roaming yaw");
   await click("Lens: off"); check(app.Style.LensActive,label+": Lens toggle accepted"); await click("Opacity / Lens");
   foreach(var slider in app.UI.Panel.GetComponentsInChildren<UnityEngine.UI.Slider>())
   { var rect=(UnityEngine.RectTransform)slider.transform; await point(rect.TransformPoint(new UnityEngine.Vector3(rect.rect.xMin+rect.rect.width*.65f,rect.rect.center.y,0))); reader.QueueManualState(true,1); await wait(150); reader.QueueManualState(false,0); await wait(120); }
   await click("Back"); await click("Lens: on");
   await click("ANNOTATE");
   await point(surface); await wait(200); int count=app.Annotations.Items.Count; reader.QueueManualState(true,1); await wait(140); reader.QueueManualState(false,0); await wait(200);
   if(app.Annotations.Items.Count>count) created.Add(app.Annotations.Selected.id);
   check(app.Annotations.Items.Count==count+1 && app.UI.Page=="picker",label+": one pinch creates one annotation");
   await click("[−] MISSING"); await click("[+] NEW"); await click("[Δ] CHANGED"); await click("[•] NOTE"); await click("List");
   for(int fixture=0;fixture<6;fixture++) {var item=app.Annotations.Add(app.Root.WorldToTwin(surface),UnityEngine.Vector3.forward,app.Root.WorldToTwin(camera.transform.position),app.Root.RotationToTwin(camera.transform.rotation));created.Add(item.id);}
   app.UI.Show("list");await wait(250);var scroll=app.UI.Panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();float beforeScroll=scroll.content.anchoredPosition.y;
   var scrollPoint=scroll.viewport.TransformPoint(scroll.viewport.rect.center+UnityEngine.Vector2.up*20);await point(scrollPoint);reader.QueueManualState(true,1);await wait(120);for(int step=1;step<=10;step++){aim.rotation=UnityEngine.Quaternion.LookRotation(scrollPoint-scroll.viewport.up*(step*.005f)-aim.position,UnityEngine.Vector3.up);await wait(60);}await wait(120);
   check(scroll.content.anchoredPosition.y<beforeScroll-10,label+": one-hand pinch drag scrolls the list");reader.QueueManualState(false,0);await wait(200);
   string rowName=null;foreach(UnityEngine.Transform row in scroll.content) {var rowRect=(UnityEngine.RectTransform)row;var center=rowRect.TransformPoint(rowRect.rect.center);if(scroll.viewport.rect.Contains(scroll.viewport.InverseTransformPoint(center)) && row.GetComponent<UnityEngine.UI.Button>()) {rowName=row.name;break;}}
   check(rowName!=null,label+": a list row remains reachable");await click(rowName);
   await click("Retype"); await click("[Δ] CHANGED"); await click("List"); await click("Export"); await click("Delete");
   foreach(var id in created) {app.Annotations.Select(id);if(app.Annotations.Selected!=null)app.Annotations.DeleteSelected();}created.Clear();
   check(app.Annotations.Items.Count==count,label+": row selection, retype, export and delete accessible");
   log.AppendLine(label+": complete synthetic aim/strength workflow passed; raw XR Hands tracking and stereo rendering still require hardware.");
  }
 }
 catch(System.Exception error) { log.AppendLine("FAIL: "+error); }
 finally
 {
  foreach(var id in created) { app.Annotations.Select(id); if(app.Annotations.Selected!=null) app.Annotations.DeleteSelected(); }
  app.Manual.HeightUnlocked=false; app.Manual.SetActive(false); app.Style.SetLens(false); app.Style.SetMode(TwinInspector.ViewMode.Overlay);
  app.Origin.transform.SetPositionAndRotation(rigPose.position,rigPose.rotation); camera.transform.SetPositionAndRotation(cameraPose.position,cameraPose.rotation); app.Root.transform.SetPositionAndRotation(rootPose.position,rootPose.rotation);
  if(alignmentBytes!=null) System.IO.File.WriteAllBytes(alignmentPath,alignmentBytes); else if(System.IO.File.Exists(alignmentPath)) System.IO.File.Delete(alignmentPath);
  foreach(var pair in readers) { pair.Key.selectInput=pair.Value[0]; pair.Key.uiPressInput=pair.Value[1]; }
  foreach(var pair in sources) { pair.Key.Source=pair.Value; pair.Key.ResetPose(); }
  for(int i=0;i<4;i++) groups[i].SetActive(enabled[i]); foreach(var pair in savedBehaviours) if(pair.Key) pair.Key.enabled=pair.Value;
  inputSettings.backgroundBehavior=background;inputSettings.editorInputBehaviorInPlayMode=editorInput;
  UnityEngine.InputSystem.InputSystem.RemoveDevice(hmd); UnityEngine.Object.Destroy(aim.gameObject); modality.enabled=true; driver.enabled=true;
  TwinInspector.InteractionPreferences.Volume=originalVolume;UnityEngine.PlayerPrefs.Save();app.Style.SetBlend(originalBlend);app.Style.SetLensRadius(originalRadius);
  TwinInspector.InteractionPreferences.LeftHand=preferred; strip.gameObject.SetActive(true); app.UI.Show("setup"); strip.Recenter(true);
  System.IO.File.WriteAllText("docs/evidence/single-hand-v12.txt",log.ToString());
 }
};
_=run();return "Synthetic one-hand XRI workflow started";
