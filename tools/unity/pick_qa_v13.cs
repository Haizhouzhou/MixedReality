var app=TwinInspector.AppBootstrap.Instance;if(!app||!app.Ready||!app.Picker.Available)return "WAIT app/models";
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder();var driver=app.GetComponent<TwinInspector.EditorDesktopDriver>();bool wasDriver=driver.enabled;var camera=app.Viewer;var pose=new UnityEngine.Pose(camera.transform.position,camera.transform.rotation);var mode=app.Style.Mode;bool technical=app.Style.Technical,lens=app.Style.Lens;var created=new System.Collections.Generic.List<string>();
 System.Action<bool,string> check=(ok,message)=>{if(!ok)throw new System.Exception(message);log.AppendLine("PASS: "+message);System.IO.File.WriteAllText("docs/evidence/pick-v13.txt",log.ToString());};
 try
 {
  driver.enabled=false;app.Style.SetMode(TwinInspector.ViewMode.Twin);app.Style.SetTechnical(false);app.Style.SetLens(false);
  var views=new[]{new UnityEngine.Vector3(13.5f,8,1.6f),new UnityEngine.Vector3(25,16,1.6f)};
  var targets=new[]{new UnityEngine.Vector3(9.98f,8,1.5f),new UnityEngine.Vector3(25,20,1.6f)};
  for(int fixture=0;fixture<2;fixture++)
  {
   string label=fixture==0?"column":"wall";camera.transform.SetPositionAndRotation(app.Root.TwinToWorld(views[fixture]),UnityEngine.Quaternion.LookRotation(app.Root.TwinToWorld(targets[fixture])-app.Root.TwinToWorld(views[fixture]),UnityEngine.Vector3.up));
   await System.Threading.Tasks.Task.Delay(5000);float until=UnityEngine.Time.realtimeSinceStartup+60;while(app.Loader.PendingLoads>0&&UnityEngine.Time.realtimeSinceStartup<until)await System.Threading.Tasks.Task.Delay(100);
   UnityEngine.Physics.SyncTransforms();check(UnityEngine.Physics.Raycast(camera.transform.position,camera.transform.forward,out var hit,20,1<<8),label+" fixture hits twin surface");
   var point=app.Root.WorldToTwin(hit.point);var normal=app.Root.NormalToTwin(hit.normal);check(UnityEngine.Mathf.Abs(normal.z)<.3f,label+" fixture is a vertical surface");log.AppendLine(label+" prompt="+point+" normal="+normal);
   var item=app.Annotations.Add(point,normal,views[fixture],new float[]{0,0,0,1},TwinInspector.AnnotationType.Missing,null);created.Add(item.id);app.UI.Show("picker");
   bool picked=await app.Picker.PickAsync(item,hit);check(picked,label+" model pick returns region: "+app.Picker.LastResult);
   check(item.Kind==TwinInspector.AnnotationType.Missing,label+" preserves annotation type");var region=item.region;check(region.source=="sam"&&region.area_m2>=.1f&&region.area_m2<=20,label+" SAM area .1-20 m2: "+region.area_m2);
   var n=TwinInspector.TwinRoot.Vector(region.normal_twin);log.AppendLine(label+" region="+UnityEngine.JsonUtility.ToJson(region));float angle=UnityEngine.Mathf.Min(UnityEngine.Vector3.Angle(n,normal),UnityEngine.Vector3.Angle(-n,normal));check(angle<=30,label+" surface normal within 30 degrees: "+angle);
   var hull=System.Array.ConvertAll(region.hull_twin,TwinInspector.TwinRoot.Vector);float sign=0;bool contains=true;for(int i=0;i<hull.Length;i++){float side=UnityEngine.Vector3.Dot(UnityEngine.Vector3.Cross(hull[(i+1)%hull.Length]-hull[i],point-hull[i]),n);if(UnityEngine.Mathf.Abs(side)<.025f)continue;if(sign==0)sign=UnityEngine.Mathf.Sign(side);else if(UnityEngine.Mathf.Sign(side)!=sign)contains=false;}
   check(contains,label+" projected hull contains prompt");log.AppendLine(label+" total_ms="+app.Picker.LastMilliseconds.ToString("F1",System.Globalization.CultureInfo.InvariantCulture));
  }
  log.AppendLine("Editor only; device latency, GPU memory and physical pinch remain unverified.");System.IO.File.WriteAllText("docs/evidence/pick-v13.txt",log.ToString());
 }
 catch(System.Exception error){log.AppendLine("FAIL: "+error);System.IO.File.WriteAllText("docs/evidence/pick-v13.txt",log.ToString());UnityEngine.Debug.LogException(error);}
 finally{foreach(var id in created){app.Annotations.Select(id);app.Annotations.DeleteSelected();}camera.transform.SetPositionAndRotation(pose.position,pose.rotation);driver.enabled=wasDriver;app.Style.SetMode(mode);app.Style.SetTechnical(technical);app.Style.SetLens(lens);app.UI.Show("setup");}
};_=run();return "Column and wall SAM QA started";
