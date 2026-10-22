#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace TwinInspector
{
    [Serializable] public sealed class DiffFixture
    {
        public string schema,scan,region;
        public Edit[] edits;
        [Serializable] public sealed class Edit
        {
            public string id,kind,expect;
            public float[] center_twin,from_twin,to_twin,size_m;
            public float radius_m,yaw_deg,sigma_m;
            public int seed=1303;
        }
        public static DiffFixture Load(string path,string scan)
        {
            var f=JsonConvert.DeserializeObject<DiffFixture>(File.ReadAllText(path));
            if(f.schema!="twin-reality-edits/1"||f.scan!=scan||f.edits==null) throw new InvalidDataException("Invalid diff fixture");
            return f;
        }
        public void Validate(Vector3[] vertices,int[] triangles,Settings settings,IReadOnlyList<Bounds> coverage)
        {
            var centers=new List<Vector3>();
            foreach(var edit in edits)
            {
                if(edit.kind=="jitter") continue;
                var center=TwinRoot.Vector(edit.kind=="move_object"?edit.from_twin:edit.center_twin);
                foreach(var earlier in centers) if(Vector3.Distance(earlier,center)<3) throw new InvalidDataException("Independent fixture edits must be 3m apart");
                centers.Add(center);
                if(edit.kind=="remove_sphere"||edit.kind=="move_object")
                {
                    int hit=0;
                    for(int i=0;i<triangles.Length;i+=3) {var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];var p=(a+b+c)/3;var n=Vector3.Cross(b-a,c-a).normalized;
                        bool ignore=settings.DiffIgnoreFloorCeiling&&Mathf.Abs(n.z)>Mathf.Cos(20*Mathf.Deg2Rad)&&(Mathf.Abs(p.z-settings.DiffFloorZ)<.15f||p.z>settings.DiffFloorZ+2.4f);
                        if(!ignore && Vector3.Distance(p,center)<edit.radius_m)hit++;}
                    if(hit<50)throw new InvalidDataException(edit.id+" intersects only "+hit+" retained triangles");
                    DevLog.Write("Fixture "+edit.id+" removal triangles="+hit+" center="+center);
                }
                if(edit.kind=="move_object"||edit.kind=="add_box")
                {
                    var p=TwinRoot.Vector(edit.kind=="move_object"?edit.to_twin:edit.center_twin);
                    if(edit.kind=="move_object"&&Mathf.Abs(Vector3.Distance(p,center)-.7f)>.001f)throw new InvalidDataException("Move must be 0.7m");
                    if(Mathf.Abs(p.z-edit.size_m[2]/2-settings.DiffFloorZ)>.01f)throw new InvalidDataException("Fixture box must rest on floor");
                    bool inside=false;foreach(var b in coverage)if(b.Contains(p))inside=true;
                    if(!inside)throw new InvalidDataException("Fixture box outside demo coverage");
                    foreach(var other in edits) if(other!=edit && other.kind!="jitter") {var q=TwinRoot.Vector(other.kind=="move_object"?other.from_twin:other.center_twin);if(Vector3.Distance(p,q)<3)throw new InvalidDataException("Box too close to an independent edit");}
                }
            }
        }
    }
}
#endif
