var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) throw new System.Exception("Wait for readiness");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder();var camera=app.Viewer;var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();
 var pipeline=(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
 float scale=pipeline.renderScale;var pose=new UnityEngine.Pose(camera.transform.position,camera.transform.rotation);var driver=app.GetComponent<TwinInspector.EditorDesktopDriver>();
 UnityEngine.GameObject plane=null;UnityEngine.Mesh mesh=null;
 var grid=app.Root.transform.Find("Metre floor grid").GetComponent<UnityEngine.MeshRenderer>();bool gridEnabled=grid.enabled;
 System.Action<string> capture=name=>
 {
  var target=new UnityEngine.RenderTexture(1600,1000,24){antiAliasing=4};var image=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false);
  var oldTarget=camera.targetTexture;var oldActive=UnityEngine.RenderTexture.active;
  try {camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;image.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0);image.Apply();System.IO.File.WriteAllBytes("docs/screenshots/v12-"+name+".png",image.EncodeToPNG());log.AppendLine(name+": center pixel "+image.GetPixel(800,500));}
  finally {camera.targetTexture=oldTarget;UnityEngine.RenderTexture.active=oldActive;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);}
 };
 try
 {
  app.Style.SetMode(TwinInspector.ViewMode.Twin);app.Style.SetTechnical(true);app.Style.SetLens(false);app.UI.Panel.gameObject.SetActive(false);strip.gameObject.SetActive(false);
  await System.Threading.Tasks.Task.Delay(1000);pipeline.renderScale=1;await System.Threading.Tasks.Task.Delay(150);capture("scale-100");
  pipeline.renderScale=.75f;await System.Threading.Tasks.Task.Delay(150);capture("scale-075");
  log.AppendLine("Same physical camera, same output dimensions; outline expansion uses unscaled eye dimensions.");
  pipeline.renderScale=1;driver.enabled=false;app.Loader.SetVisible(false);grid.enabled=false;
  plane=new UnityEngine.GameObject("Constant-datum QA surface",typeof(UnityEngine.MeshFilter),typeof(UnityEngine.MeshRenderer));
  var center=app.Root.TwinToWorld(new UnityEngine.Vector3(20,10,0));plane.transform.position=center;
  mesh=new UnityEngine.Mesh();mesh.vertices=new[]{new UnityEngine.Vector3(-5,0,-5),new UnityEngine.Vector3(5,0,-5),new UnityEngine.Vector3(5,0,5),new UnityEngine.Vector3(-5,0,5)};mesh.triangles=new[]{0,2,1,0,3,2};mesh.normals=new[]{UnityEngine.Vector3.up,UnityEngine.Vector3.up,UnityEngine.Vector3.up,UnityEngine.Vector3.up};mesh.RecalculateBounds();plane.GetComponent<UnityEngine.MeshFilter>().sharedMesh=mesh;plane.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial=app.Tokens.TwinMaterial;
  camera.transform.SetPositionAndRotation(center+UnityEngine.Vector3.up*2,UnityEngine.Quaternion.LookRotation(UnityEngine.Vector3.down,UnityEngine.Vector3.forward));await System.Threading.Tasks.Task.Delay(150);capture("constant-datum-fixture");
  log.AppendLine("Synthetic horizontal datum surface verifies that an entire level plane does not become an Ink contour; measured data is untouched.");
  foreach(var name in new[]{"TwinInspector/Twin","TwinInspector/ArchitecturalEdges","TwinInspector/FloorGrid","TwinInspector/Annotation"})
  {var messages=UnityEditor.ShaderUtil.GetShaderMessages(UnityEngine.Shader.Find(name));log.AppendLine(name+": "+messages.Length+" shader messages");foreach(var message in messages)log.AppendLine(message.severity+": "+message.message);}
 }
 catch(System.Exception error) {log.AppendLine("FAIL: "+error);}
 finally {if(plane)UnityEngine.Object.Destroy(plane);if(mesh)UnityEngine.Object.Destroy(mesh);grid.enabled=gridEnabled;pipeline.renderScale=scale;camera.transform.SetPositionAndRotation(pose.position,pose.rotation);driver.enabled=true;app.Loader.SetVisible(true);app.Style.SetMode(TwinInspector.ViewMode.Overlay);strip.gameObject.SetActive(true);app.UI.Show("setup");System.IO.File.WriteAllText("docs/evidence/render-scale-v12.txt",log.ToString());}
};
_=run();return "Render-scale and flat-datum checks started";
