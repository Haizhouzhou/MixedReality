var a=TwinInspector.AppBootstrap.Instance;var log=new System.Text.StringBuilder();
foreach(var twin in new[]{new UnityEngine.Vector3(15,10,1.6f),new UnityEngine.Vector3(25,10,1.6f),new UnityEngine.Vector3(5,10,1.6f)})
foreach(var delta in new[]{UnityEngine.Vector3.right,UnityEngine.Vector3.left,UnityEngine.Vector3.up,UnityEngine.Vector3.down})
{
 var origin=a.Root.TwinToWorld(twin);var direction=(a.Root.TwinToWorld(twin+delta)-origin).normalized;
 if(UnityEngine.Physics.Raycast(origin,direction,out var hit,20,1<<8))log.AppendLine("view="+twin+" dir="+delta+" hit="+a.Root.WorldToTwin(hit.point)+" normal="+a.Root.NormalToTwin(hit.normal));
}
return log.ToString();
