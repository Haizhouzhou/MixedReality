var app=TwinInspector.AppBootstrap.Instance;
var previousBackfaces=UnityEngine.Physics.queriesHitBackfaces;
var colliders=UnityEngine.Object.FindObjectsByType<UnityEngine.MeshCollider>(UnityEngine.FindObjectsSortMode.None);
var result=new System.Text.StringBuilder(); result.AppendLine("Mesh colliders: "+colliders.Length);
for(int i=0;i<System.Math.Min(4,colliders.Length);i++) { var c=colliders[i]; result.AppendLine(c.name+" active="+c.gameObject.activeInHierarchy+" layer="+c.gameObject.layer+" bounds="+c.bounds+" readable="+c.sharedMesh.isReadable); }
foreach(bool backfaces in new[]{false,true})
{
    UnityEngine.Physics.queriesHitBackfaces=backfaces; int hits=0;
    for(int pitch=-60;pitch<=60;pitch+=30) for(int yaw=0;yaw<360;yaw+=15)
        if(UnityEngine.Physics.Raycast(app.Viewer.transform.position,UnityEngine.Quaternion.Euler(pitch,yaw,0)*UnityEngine.Vector3.forward,out var hit,80,1<<8)) { hits++; if(hits==1) result.AppendLine("First hit: "+hit.point+" normal="+hit.normal); }
    result.AppendLine("Backfaces "+backfaces+": "+hits+" /120 rays hit");
}
UnityEngine.Physics.queriesHitBackfaces=previousBackfaces;
return result.ToString();
