using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace TwinInspector
{
    /// <summary>Static adjacency, cavity and screen-expanded edge quads, owned by each loaded LOD.</summary>
    public sealed class ArchitecturalMesh : MonoBehaviour
    {
        Mesh edges;
        MeshRenderer edgeRenderer;
        public void SetVisible(bool value) { if(edgeRenderer) edgeRenderer.enabled=value; }
        struct Edge
        {
            public int A,B,Opposite;
            public Vector3 Normal;
            public float Area;
            public Edge(int a,int b,int opposite,Vector3 normal,float area) { A=a; B=b; Opposite=opposite; Normal=normal; Area=area; }
        }
        public static bool IsCrease(Vector3 a,Vector3 b) => Mathf.Abs(Vector3.Dot(a,b))<Mathf.Cos(38*Mathf.Deg2Rad);
        public static void Prepare(MeshFilter filter,Material edgeMaterial)
        {
            var owner=filter.gameObject.AddComponent<ArchitecturalMesh>();
            var mesh=filter.sharedMesh;
            var positions=mesh.vertices; var triangles=mesh.triangles;
            var canonical=new int[positions.Length]; var unique=new Dictionary<Vector3,int>(positions.Length);
            for(int i=0;i<positions.Length;i++)
            { if(!unique.TryGetValue(positions[i],out int index)) { index=i; unique.Add(positions[i],i); } canonical[i]=index; }
            var normals=new Vector3[positions.Length]; var cavity=new float[positions.Length]; var counts=new float[positions.Length];
            var adjacency=new Dictionary<ulong,Edge>(triangles.Length);
            var vertices=new List<Vector3>(); var other=new List<Vector3>(); var faceA=new List<Vector3>(); var faceB=new List<Vector4>(); var uv=new List<Vector2>(); var colours=new List<Color32>(); var indices=new List<int>();
            for(int t=0;t<triangles.Length;t+=3)
            {
                int a=canonical[triangles[t]],b=canonical[triangles[t+1]],c=canonical[triangles[t+2]];
                var cross=Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]); float area=cross.magnitude;
                if(area<1e-9f) continue; var normal=cross/area;
                normals[a]+=cross; normals[b]+=cross; normals[c]+=cross;
            }
            for(int i=0;i<normals.Length;i++) normals[i]=normals[canonical[i]].normalized;
            for(int t=0;t<triangles.Length;t+=3)
            {
                int a=canonical[triangles[t]],b=canonical[triangles[t+1]],c=canonical[triangles[t+2]];
                var cross=Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]); float area=cross.magnitude;
                if(area<1e-9f) continue; var normal=cross/area;
                Add(a,b,c,normal,area); Add(b,c,a,normal,area); Add(c,a,b,normal,area);
            }
            var data=new List<Vector2>(positions.Length);
            for(int i=0;i<positions.Length;i++) { int c=canonical[i]; normals[i]=normals[c].normalized; data.Add(new Vector2(counts[c]>0?Mathf.Clamp01(cavity[c]/counts[c]):0,0)); }
            mesh.normals=normals; mesh.SetUVs(1,data);
            // Unpaired edges are intentionally omitted: centroid tile cuts are zig-zag,
            // not exact lattice planes. Only proven two-face adjacency creates ink.
            // Smooth closed surfaces still get view-dependent silhouette edges.
            var lineMesh=new Mesh { name="Cached architectural adjacency", indexFormat=IndexFormat.UInt32 };
            lineMesh.SetVertices(vertices); lineMesh.SetNormals(faceA); lineMesh.SetTangents(faceB); lineMesh.SetUVs(0,uv); lineMesh.SetUVs(1,other); lineMesh.SetColors(colours); lineMesh.SetTriangles(indices,0); lineMesh.RecalculateBounds();
            owner.edges=lineMesh;
            var child=new GameObject("Architectural edges",typeof(MeshFilter),typeof(MeshRenderer)); child.transform.SetParent(filter.transform,false);
            child.GetComponent<MeshFilter>().sharedMesh=lineMesh;
            var renderer=child.GetComponent<MeshRenderer>(); renderer.sharedMaterial=edgeMaterial; renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            owner.edgeRenderer=renderer;
            lineMesh.UploadMeshData(true);
            void Add(int a,int b,int opposite,Vector3 normal,float area)
            {
                ulong key=((ulong)(uint)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
                if(!adjacency.TryGetValue(key,out var previous)) { adjacency.Add(key,new Edge(a,b,opposite,normal,area)); return; }
                adjacency.Remove(key);
                float dot=Mathf.Clamp(Vector3.Dot(normal,previous.Normal),-1,1);
                float angle=Mathf.Acos(dot);
                float weight=Mathf.Min(area,previous.Area);
                bool concave=Vector3.Dot(previous.Normal,positions[opposite]-positions[a])>.001f;
                cavity[a]+=concave?angle*weight:0; cavity[b]+=concave?angle*weight:0; counts[a]+=weight; counts[b]+=weight;
                float length=Vector3.Distance(positions[a],positions[b]);
                var smoothA=(normals[a]+normals[b]+normals[previous.Opposite]).normalized;
                var smoothB=(normals[a]+normals[b]+normals[opposite]).normalized;
                float support=Vector3.Dot(smoothA,smoothB);
                bool crease=IsCrease(normal,previous.Normal) && support<Mathf.Cos(8*Mathf.Deg2Rad) && length>=.08f;
                // Smoothed neighbourhood normals classify silhouettes; measured
                // endpoints stay untouched. Reject slivers and reversed coplanar faces.
                if(length<.025f || weight<.0005f || weight/(length*length)<.025f || dot<-.995f || (!crease && support>.9999f)) return;
                int start=vertices.Count;
                for(int i=0;i<4;i++)
                {
                    bool end=i>=2;
                    vertices.Add(positions[end?b:a]); other.Add(positions[end?a:b]);
                    faceA.Add(smoothA); faceB.Add(new Vector4(smoothB.x,smoothB.y,smoothB.z,0));
                    uv.Add(new Vector2((i&1)==0?-1:1,end?1:0)); colours.Add(new Color32(crease?(byte)255:(byte)0,0,0,255));
                }
                indices.Add(start); indices.Add(start+2); indices.Add(start+1); indices.Add(start+1); indices.Add(start+2); indices.Add(start+3);
            }
        }
        void OnDestroy() { if(edges) Destroy(edges); }
    }
}
