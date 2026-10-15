var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) throw new System.Exception("Application not ready");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var camera=app.Viewer;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;
 var driver=app.GetComponent<TwinInspector.EditorDesktopDriver>();var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();
 var mouse=UnityEngine.InputSystem.Mouse.current ?? UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
 var background=UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
 var editorInput=UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode;
 try
 {
  driver.enabled=false;app.Style.SetMode(TwinInspector.ViewMode.Twin);app.Style.SetTechnical(false);app.UI.BeginAnnotation();strip.gameObject.SetActive(false);
  UnityEngine.RaycastHit selected=default;bool found=false;
  for(int y=3;y<8 && !found;y++) for(int x=2;x<8 && !found;x++)
   if(UnityEngine.Physics.Raycast(camera.ViewportPointToRay(new UnityEngine.Vector3(x/10f,y/10f,0)),out var hit,25,1<<8)){selected=hit;found=true;}
  if(!found) throw new System.Exception("No surface available");
  var normal=UnityEngine.Vector3.Dot(selected.normal,cameraPosition-selected.point)<0 ? -selected.normal : selected.normal;
  camera.transform.SetPositionAndRotation(selected.point+normal*.3f,UnityEngine.Quaternion.LookRotation(-normal,UnityEngine.Mathf.Abs(normal.y)>.95f?UnityEngine.Vector3.forward:UnityEngine.Vector3.up));
  UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
  UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  UnityEngine.InputSystem.InputSystem.EnableDevice(mouse);
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState {position=new UnityEngine.Vector2(camera.pixelWidth/2f,camera.pixelHeight/2f)});
  await System.Threading.Tasks.Task.Delay(350);
  if(!app.Input.HasHit) throw new System.Exception("Reticle input did not hit the surface");
  var oldTarget=camera.targetTexture;var oldActive=UnityEngine.RenderTexture.active;
  var target=new UnityEngine.RenderTexture(1200,800,24);var picture=new UnityEngine.Texture2D(1200,800,UnityEngine.TextureFormat.RGB24,false);
  try {camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;picture.ReadPixels(new UnityEngine.Rect(0,0,1200,800),0,0);picture.Apply();System.IO.File.WriteAllBytes("docs/screenshots/reticle-surface.png",picture.EncodeToPNG());}
  finally {camera.targetTexture=oldTarget;UnityEngine.RenderTexture.active=oldActive;target.Release();UnityEngine.Object.Destroy(picture);UnityEngine.Object.Destroy(target);}
 }
 catch(System.Exception error){UnityEngine.Debug.LogException(error);}
 finally
 {
  camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);driver.enabled=true;strip.gameObject.SetActive(true);app.UI.Show("setup");
  UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior=background;UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
 }
};
_=run();return "Actual mouse reticle capture started";
