var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) throw new System.Exception("Application not ready");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var camera=app.Viewer;var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;
 var driver=app.GetComponent<TwinInspector.EditorDesktopDriver>();bool driverEnabled=driver.enabled;
 var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();string id=null;
 try
 {
  driver.enabled=false;app.Style.SetMode(TwinInspector.ViewMode.Twin);app.Style.SetTechnical(false);app.UI.BeginAnnotation();
  UnityEngine.RaycastHit selected=default;bool found=false;
  for(int y=3;y<8 && !found;y++) for(int x=2;x<8 && !found;x++)
   if(UnityEngine.Physics.Raycast(camera.ViewportPointToRay(new UnityEngine.Vector3(x/10f,y/10f,0)),out var hit,25,1<<8) && UnityEngine.Mathf.Abs(hit.normal.y)<.4f){selected=hit;found=true;}
  if(!found) throw new System.Exception("No wall surface available");
  app.Placer.Place(selected);id=app.Annotations.Selected.id;
  app.UI.Panel.gameObject.SetActive(false);strip.gameObject.SetActive(false);
  var normal=UnityEngine.Vector3.Dot(selected.normal,cameraPosition-selected.point)<0 ? -selected.normal : selected.normal;
  camera.transform.SetPositionAndRotation(selected.point+normal*.65f+UnityEngine.Vector3.up*.05f,UnityEngine.Quaternion.LookRotation(-normal,UnityEngine.Vector3.up));
  await System.Threading.Tasks.Task.Delay(300);
  var oldTarget=camera.targetTexture;var oldActive=UnityEngine.RenderTexture.active;
  var target=new UnityEngine.RenderTexture(1200,800,24);var picture=new UnityEngine.Texture2D(1200,800,UnityEngine.TextureFormat.RGB24,false);
  try {camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;picture.ReadPixels(new UnityEngine.Rect(0,0,1200,800),0,0);picture.Apply();System.IO.File.WriteAllBytes("docs/screenshots/annotation-surface.png",picture.EncodeToPNG());}
  finally {camera.targetTexture=oldTarget;UnityEngine.RenderTexture.active=oldActive;target.Release();UnityEngine.Object.Destroy(picture);UnityEngine.Object.Destroy(target);}
 }
 catch(System.Exception error){UnityEngine.Debug.LogException(error);}
 finally
 {
  if(id!=null){app.Annotations.Select(id);app.Annotations.DeleteSelected();}
  camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);driver.enabled=driverEnabled;strip.gameObject.SetActive(true);app.UI.Show("setup");
 }
};
_=run();return "Surface annotation capture started; temporary annotation will be removed";
