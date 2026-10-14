var app=TwinInspector.AppBootstrap.Instance;
app.UI.Panel.gameObject.SetActive(false); app.Style.SetMode(TwinInspector.ViewMode.Twin); app.Style.SetTechnical(false);
var camera=app.Viewer; var oldTarget=camera.targetTexture; var oldActive=UnityEngine.RenderTexture.active;
var target=new UnityEngine.RenderTexture(1600,1000,24); camera.targetTexture=target; camera.Render(); UnityEngine.RenderTexture.active=target;
var picture=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false); picture.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0); picture.Apply();
System.IO.Directory.CreateDirectory("docs/images"); System.IO.File.WriteAllBytes("docs/images/hall-simulator.png",picture.EncodeToPNG());
camera.targetTexture=oldTarget; UnityEngine.RenderTexture.active=oldActive; target.Release(); UnityEngine.Object.Destroy(picture); UnityEngine.Object.Destroy(target);
app.Style.SetMode(TwinInspector.ViewMode.Overlay); app.Style.SetTechnical(true); app.UI.Show("setup");
return "Hall simulator capture saved";
