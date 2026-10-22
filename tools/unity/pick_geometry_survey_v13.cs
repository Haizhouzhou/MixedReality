var a=TwinInspector.AppBootstrap.Instance;var origin=a.Root.TwinToWorld(new UnityEngine.Vector3(13.5f,8,1.6f));var target=a.Root.TwinToWorld(new UnityEngine.Vector3(9.98f,8,1.5f));
if(!UnityEngine.Physics.Raycast(origin,(target-origin).normalized,out var hit,20,1<<8))return "no hit";
var collider=(UnityEngine.MeshCollider)hit.collider;var mesh=collider.sharedMesh;var v=mesh.vertices;var t=mesh.triangles;int j=hit.triangleIndex*3;
var p=collider.transform.TransformPoint(v[t[j]]);var q=collider.transform.TransformPoint(v[t[j+1]]);var r=collider.transform.TransformPoint(v[t[j+2]]);var normal=UnityEngine.Vector3.Cross(q-p,r-p).normalized;
return "hit="+a.Root.WorldToTwin(hit.point)+" triangle="+hit.triangleIndex+" hitNormal="+a.Root.NormalToTwin(hit.normal)+" geometricNormal="+a.Root.NormalToTwin(normal)+" p="+a.Root.WorldToTwin(p)+" q="+a.Root.WorldToTwin(q)+" r="+a.Root.WorldToTwin(r);
