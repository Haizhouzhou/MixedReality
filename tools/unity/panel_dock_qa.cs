var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) throw new System.Exception("Wait for application readiness");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder();var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();
 var camera=app.Viewer;var initial=camera.transform.rotation;var mode=app.Style.Mode;var technical=app.Style.Technical;
 var update=typeof(TwinInspector.ToolStrip).GetMethod("UpdateSpatialPose",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
 System.Action<bool,string> check=(ok,message)=>{if(!ok) throw new System.Exception(message);log.AppendLine("PASS: "+message);};
 System.Action<bool,UnityEngine.Pose> step=(palm,pose)=>update.Invoke(strip,new object[]{palm,pose});
 try
 {
  app.UI.Show("setup");strip.Recenter(true);await System.Threading.Tasks.Task.Delay(200);
  var panel=app.UI.Panel.transform;var start=panel.position;
  check(panel.GetComponent<UnityEngine.Canvas>().renderMode==UnityEngine.RenderMode.WorldSpace && !panel.IsChildOf(camera.transform),"full panel remains world-space, outside camera hierarchy");
  strip.enabled=false;
  var palmPose=new UnityEngine.Pose(camera.transform.position+camera.transform.right*-.35f+UnityEngine.Vector3.down*.3f+camera.transform.forward*.4f,UnityEngine.Quaternion.identity);
  var stripStart=strip.transform.position;
  step(true,palmPose);await System.Threading.Tasks.Task.Delay(200);step(true,palmPose);
  check(strip.PalmAnchored && UnityEngine.Vector3.Distance(stripStart,strip.transform.position)>.1f,"simulated palm transition moves the compact strip");
  check(UnityEngine.Vector3.Distance(start,panel.position)<.0001f,"full panel stays at its world anchor when palm rises");
  palmPose.position+=camera.transform.right*.2f;step(true,palmPose);
  check(UnityEngine.Vector3.Distance(start,panel.position)<.0001f,"moving the palm does not drag the full panel");
  camera.transform.rotation=initial*UnityEngine.Quaternion.Euler(0,50,0);app.UI.Show("mode");
  step(true,palmPose);await System.Threading.Tasks.Task.Delay(200);step(true,palmPose);
  check(UnityEngine.Vector3.Distance(start,panel.position)>.1f,"opening a tool from palm mode summons its panel to a reachable world anchor");
  camera.transform.rotation=initial;step(false,palmPose);await System.Threading.Tasks.Task.Delay(200);step(false,palmPose);
  var p=new UnityEngine.Vector3[4];var s=new UnityEngine.Vector3[4];
  ((UnityEngine.RectTransform)panel).GetWorldCorners(p);((UnityEngine.RectTransform)strip.transform).GetWorldCorners(s);
  var gap=(p[0]+p[3]-s[1]-s[2])*.5f;
  check(UnityEngine.Mathf.Abs(UnityEngine.Vector3.Dot(gap,strip.transform.up)-.024f)<.0001f && UnityEngine.Mathf.Abs(UnityEngine.Vector3.Dot(gap,strip.transform.forward))<.0001f,"lowering palm restores the 24 mm panel/strip gap and shared depth");
  strip.enabled=true;app.UI.Show("setup");strip.Recenter(true);app.Style.SetMode(TwinInspector.ViewMode.Twin);app.Style.SetTechnical(false);
  await System.Threading.Tasks.Task.Delay(250);
  var previousTarget=camera.targetTexture;var previousActive=UnityEngine.RenderTexture.active;
  var target=new UnityEngine.RenderTexture(1600,1000,24);var picture=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false);
  try
  {
   camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;picture.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0);picture.Apply();
   System.IO.Directory.CreateDirectory("docs/screenshots");System.IO.File.WriteAllBytes("docs/screenshots/panel-docked.png",picture.EncodeToPNG());
  }
  finally {camera.targetTexture=previousTarget;UnityEngine.RenderTexture.active=previousActive;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(picture);}
  app.UI.Notify("Panel dock verification");
  var toast=(UnityEngine.RectTransform)panel.Find("Toast");toast.GetWorldCorners(p);
  var panelCorners=new UnityEngine.Vector3[4];((UnityEngine.RectTransform)panel).GetWorldCorners(panelCorners);
  check(UnityEngine.Vector3.Dot(p[0]-panelCorners[1],panel.up)>.007f,"feedback appears above the panel, leaving tool buttons unobstructed");
  toast.gameObject.SetActive(false);
  log.AppendLine("Palm poses are simulated through the same spatial update; physical tracking still requires headset acceptance.");
 }
 catch(System.Exception error){log.AppendLine("FAIL: "+error);UnityEngine.Debug.LogException(error);}
 finally
 {
  strip.enabled=true;camera.transform.rotation=initial;app.Style.SetMode(mode);app.Style.SetTechnical(technical);app.UI.Show("setup");strip.Recenter(true);
  System.IO.Directory.CreateDirectory("docs/evidence");System.IO.File.WriteAllText("docs/evidence/panel-dock.txt",log.ToString());
 }
};
_=run();return "Panel dock verification started";
