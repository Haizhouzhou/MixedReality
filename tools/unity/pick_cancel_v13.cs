var app=TwinInspector.AppBootstrap.Instance;if(!app||!app.Ready||!app.Picker.Available)return "WAIT app/models";
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder();TwinInspector.Annotation item=null;
 try
 {
  UnityEngine.RaycastHit hit=default;bool found=false;for(int y=2;y<8&&!found;y++)for(int x=2;x<8&&!found;x++)found=UnityEngine.Physics.Raycast(app.Viewer.ViewportPointToRay(new UnityEngine.Vector3(x/10f,y/10f,0)),out hit,50,1<<8);
  if(!found)throw new System.Exception("No cancellation fixture surface");
  item=app.Annotations.Add(app.Root.WorldToTwin(hit.point),app.Root.NormalToTwin(hit.normal),app.Root.WorldToTwin(app.Viewer.transform.position),new float[]{0,0,0,1});app.UI.Show("picker");
  var operation=app.Picker.PickAsync(item,hit);app.UI.Show("list");bool result=await operation;
  if(result||item.region!=null||app.Picker.Busy||app.Picker.LastResult!="cancelled")throw new System.Exception("Page-close cancellation failed");
  log.AppendLine("PASS: leaving picker cancels in-flight inference, releases busy state and preserves point annotation");
  var root=TwinInspector.TwinPaths.ModelsRoot(app.Config);await TwinInspector.SamModels.VerifyAsync(root);log.AppendLine("PASS: both external model hashes verified at runtime");
  System.IO.File.WriteAllText("docs/evidence/pick-cancel-v13.txt",log.ToString());
 }
 catch(System.Exception error){System.IO.File.WriteAllText("docs/evidence/pick-cancel-v13.txt",log+"FAIL: "+error);UnityEngine.Debug.LogException(error);}
 finally{if(item!=null){app.Annotations.Select(item.id);app.Annotations.DeleteSelected();}app.UI.Show("setup");}
};_=run();return "SAM cancellation and integrity check started";
