using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwinInspector
{
    /// <summary>Flat region geometry and hull ink segments in one mesh and one shared material.</summary>
    public sealed class RegionPatch : MonoBehaviour
    {
        Mesh mesh;MeshRenderer rendererComponent;
        public int TriangleCount {get;private set;}
        public static int AllTriangles {get;private set;}
        public void Build(Vector3[] vertices,int[] sourceTriangles,int[] selected,Vector3[] hull,Matrix4x4 toLocal,Material material,AnnotationType type,float alpha,int budget)
        {
            AllTriangles-=TriangleCount;TriangleCount=0;budget=Mathf.Min(budget,Mathf.Max(0,50000-AllTriangles));
            if(mesh)Destroy(mesh);gameObject.layer=12;
            var filter=GetComponent<MeshFilter>();if(!filter)filter=gameObject.AddComponent<MeshFilter>();
            rendererComponent=GetComponent<MeshRenderer>();if(!rendererComponent)rendererComponent=gameObject.AddComponent<MeshRenderer>();
            rendererComponent.sharedMaterial=material;rendererComponent.shadowCastingMode=ShadowCastingMode.Off;rendererComponent.receiveShadows=false;
            var v=new List<Vector3>();var uv=new List<Vector2>();var other=new List<Vector3>();var indices=new List<int>();
            void Vertex(Vector3 p){v.Add(toLocal.MultiplyPoint3x4(p));uv.Add(Vector2.zero);other.Add(Vector3.zero);}
            int limit=Mathf.Max(0,budget-(hull?.Length??0)*2);
            if(vertices!=null&&sourceTriangles!=null&&selected!=null)
            {
                int count=Mathf.Min(selected.Length,limit);
                for(int i=0;i<count;i++){int t=selected[i]*3;if(t<0||t+2>=sourceTriangles.Length)continue;int offset=v.Count;for(int j=0;j<3;j++)Vertex(vertices[sourceTriangles[t+j]]);indices.Add(offset);indices.Add(offset+1);indices.Add(offset+2);}
            }
            else if(hull!=null)for(int i=1;i<hull.Length-1&&i<=limit;i++){int offset=v.Count;Vertex(hull[0]);Vertex(hull[i]);Vertex(hull[i+1]);indices.Add(offset);indices.Add(offset+1);indices.Add(offset+2);}
            if(hull!=null&&budget>=hull.Length*2)for(int i=0;i<hull.Length;i++)
            {
                var a=toLocal.MultiplyPoint3x4(hull[i]);var b=toLocal.MultiplyPoint3x4(hull[(i+1)%hull.Length]);int offset=v.Count;
                v.AddRange(new[]{a,a,b,b});uv.AddRange(new[]{new Vector2(-1,1),new Vector2(1,1),new Vector2(-1,2),new Vector2(1,2)});other.AddRange(new[]{b,b,a,a});indices.AddRange(new[]{offset,offset+2,offset+1,offset+1,offset+2,offset+3});
            }
            mesh=new Mesh{name="Inspection region patch",indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetUVs(1,other);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();filter.sharedMesh=mesh;TriangleCount=indices.Count/3;
            AllTriangles+=TriangleCount;
            var colour=AnnotationTypes.Colour(type).linear;colour.a=alpha;var block=new MaterialPropertyBlock();block.SetColor("_Color",colour);rendererComponent.SetPropertyBlock(block);
        }
        void OnDestroy(){AllTriangles-=TriangleCount;if(mesh)Destroy(mesh);}
    }
}
