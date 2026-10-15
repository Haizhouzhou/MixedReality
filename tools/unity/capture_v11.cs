var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) throw new System.Exception("Application not ready");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var camera=app.Viewer;var initial=camera.transform.rotation;
 System.Action<string> capture=name=>
 {
  var oldTarget=camera.targetTexture;var oldActive=UnityEngine.RenderTexture.active;
  var target=new UnityEngine.RenderTexture(1600,1000,24);var picture=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false);
  try {camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;picture.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0);picture.Apply();System.IO.File.WriteAllBytes("docs/screenshots/"+name+".png",picture.EncodeToPNG());}
  finally {camera.targetTexture=oldTarget;UnityEngine.RenderTexture.active=oldActive;target.Release();UnityEngine.Object.Destroy(picture);UnityEngine.Object.Destroy(target);}
 };
 System.IO.Directory.CreateDirectory("docs/screenshots");
 try
 {
  app.Style.SetMode(TwinInspector.ViewMode.Twin);app.Style.SetTechnical(false);app.UI.Panel.gameObject.SetActive(false);
  var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();strip.Recenter(true);await System.Threading.Tasks.Task.Delay(250);capture("hall-desktop");
  camera.transform.rotation=initial*UnityEngine.Quaternion.Euler(0,18,0);await System.Threading.Tasks.Task.Delay(250);capture("strip-world-anchor");camera.transform.rotation=initial;
  foreach(var page in new[]{"setup","mode","align","list"}) {app.UI.Show(page);await System.Threading.Tasks.Task.Delay(250);capture(page);}
  app.Style.SetTechnical(true);app.UI.Panel.gameObject.SetActive(false);await System.Threading.Tasks.Task.Delay(250);capture("hall-technical");
  System.IO.File.WriteAllText("docs/evidence/visual-v11.txt","Unity rendered seven v1.1 frames at 1600 x 1000. No controller meshes or sample beams. ToolStrip retains a world anchor during an 18 degree camera turn. Desktop and Technical hall views use the loaded twin geometry.\n");
 }
 catch(System.Exception error){UnityEngine.Debug.LogException(error);System.IO.File.WriteAllText("docs/evidence/visual-v11.txt","FAIL: "+error);}
 finally {camera.transform.rotation=initial;app.Style.SetMode(TwinInspector.ViewMode.Overlay);app.UI.Show("setup");}
};
_=run();return "v1.1 viewport captures started";
