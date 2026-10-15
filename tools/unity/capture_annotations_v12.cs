var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) throw new System.Exception("Wait for app readiness");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var camera=app.Viewer;var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();
 var markers=new System.Collections.Generic.List<TwinInspector.AnnotationMarker>();
 var log=new System.Text.StringBuilder();
 var oldMode=app.Style.Mode;
 try
 {
  app.Style.SetMode(TwinInspector.ViewMode.Passthrough); app.UI.Panel.gameObject.SetActive(false); strip.gameObject.SetActive(false); camera.backgroundColor=app.Tokens.Paper2;
  for(int i=0;i<4;i++) markers.Add(UnityEngine.Object.Instantiate(app.Tokens.AnnotationPrefab,app.Root.transform).GetComponent<TwinInspector.AnnotationMarker>());
  foreach(float distance in new[]{2f,10f,20f,30f})
  {
   for(int i=0;i<4;i++)
   {
    var position=camera.transform.position+camera.transform.forward*distance+camera.transform.right*((i-1.5f)*distance*.25f);
    var item=new TwinInspector.Annotation { id="visual-fixture-"+i,index=i+1,type=((TwinInspector.AnnotationType)i).ToString().ToLowerInvariant(),position_twin=TwinInspector.TwinRoot.Array(app.Root.WorldToTwin(position)),normal_twin=TwinInspector.TwinRoot.Array(app.Root.NormalToTwin(-camera.transform.forward)),label="Visual fixture" };
    markers[i].UpdateData(item,i==2,app.Root,app.Tokens); markers[i].Present(camera);
   }
   await System.Threading.Tasks.Task.Delay(200);
   foreach(var marker in markers) marker.Present(camera);
   var oldTarget=camera.targetTexture;var oldActive=UnityEngine.RenderTexture.active;
   var target=new UnityEngine.RenderTexture(1600,1000,24);var picture=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false);
   try {camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;picture.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0);picture.Apply();System.IO.File.WriteAllBytes("docs/screenshots/v12-annotations-"+distance.ToString("F0")+"m.png",picture.EncodeToPNG());}
   finally {camera.targetTexture=oldTarget;UnityEngine.RenderTexture.active=oldActive;target.Release();UnityEngine.Object.Destroy(picture);UnityEngine.Object.Destroy(target);}
   log.AppendLine("Four real marker prefabs at forward distance "+distance+" m, selected triangle retains its geometry. Controlled presentation fixture, not a claim of surveyed surface coordinates.");
  }
  for(int state=0;state<2;state++)
  {
   for(int i=0;i<4;i++)
   {
    var position=camera.transform.position+camera.transform.forward*20+camera.transform.right*((i-1.5f)*.4f);
    var item=new TwinInspector.Annotation{id="overlap-"+i,index=i+1,type=((TwinInspector.AnnotationType)i).ToString().ToLowerInvariant(),position_twin=TwinInspector.TwinRoot.Array(app.Root.WorldToTwin(position)),normal_twin=TwinInspector.TwinRoot.Array(app.Root.NormalToTwin(-camera.transform.forward)),label="Overlap fixture"};
    markers[i].UpdateData(item,state==0&&i==2,app.Root,app.Tokens);markers[i].Hover(state==1&&i==3);markers[i].Present(camera);
   }
   await System.Threading.Tasks.Task.Delay(250);
   var oldTarget=camera.targetTexture;var oldActive=UnityEngine.RenderTexture.active;var target=new UnityEngine.RenderTexture(1600,1000,24){antiAliasing=4};var picture=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false);
   try {camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;picture.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0);picture.Apply();System.IO.File.WriteAllBytes("docs/screenshots/v12-annotations-overlap-"+(state==0?"selected":"hover")+".png",picture.EncodeToPNG());}
   finally {camera.targetTexture=oldTarget;UnityEngine.RenderTexture.active=oldActive;target.Release();UnityEngine.Object.Destroy(picture);UnityEngine.Object.Destroy(target);}
   log.AppendLine("Overlapping labels: "+(state==0?"selected triangle":"hovered note")+" has presentation priority without changing its anchor or glyph identity.");
  }
 }
 catch(System.Exception error) {log.AppendLine("FAIL: "+error);}
 finally {foreach(var marker in markers) UnityEngine.Object.Destroy(marker.gameObject);app.Style.SetMode(oldMode);strip.gameObject.SetActive(true);app.UI.Show("setup");System.IO.File.WriteAllText("docs/evidence/annotation-v12.txt",log.ToString());}
};
_=run();return "annotation distance captures started";
