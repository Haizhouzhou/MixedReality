#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwinInspector
{
    /// <summary>Fixed session-space reality built from real LOD2 geometry, independently of live twin alignment.</summary>
    public sealed class EditorSceneSurfaceSimulator : MonoBehaviour,ISceneSurfaceSource
    {
        readonly List<SurfaceChunk> chunks=new List<SurfaceChunk>();
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        public IReadOnlyList<SurfaceChunk> Chunks => chunks;
        public bool Ready { get; private set; }
        public string Failure { get; private set; }
        public DiffFixture Fixture { get; private set; }
        public event Action<SurfaceChunk> ChunkChanged,ChunkRemoved;
        public async Task InitializeAsync(AppBootstrap app)
        {
            try
            {
                var token=lifetime.Token; var root=app.Root;
                // Capture alignment once. Later twin movement deliberately makes reality and twin disagree.
                var fixedMatrix=root.transform.localToWorldMatrix;var origin=root.Origin;
                Fixture=DiffFixture.Load(Path.GetFullPath(Path.Combine(Application.dataPath,"../../tools/fixtures/reality_edits_a.json")),app.Space.scan);
                var groups=new List<(string id,Vector3[] v,int[] t,Vector3[] n)>();
                var allV=new List<Vector3>();var allT=new List<int>();var coverage=new List<Bounds>();
                foreach(var tile in app.Loader.Manifest.tiles)
                {
                    token.ThrowIfCancellationRequested();
                    using(var importer=new GltfImport())
                    {
                        var go=new GameObject("Simulation import");go.SetActive(false);
                        go.transform.rotation=Quaternion.Euler(0,180,0);
                        try
                        {
                            if(!await importer.Load(TwinPaths.Url(app.Loader.DataRoot,tile.files["2"]),cancellationToken:token)||!await importer.InstantiateMainSceneAsync(go.transform,token))throw new IOException("Simulation GLB failed: "+tile.id);
                            foreach(var mf in go.GetComponentsInChildren<MeshFilter>(true))
                            {
                                var mesh=mf.sharedMesh;if(mesh.normals.Length!=mesh.vertexCount)mesh.RecalculateNormals();var v=mesh.vertices;var n=mesh.normals;var t=mesh.triangles;var matrix=mf.transform.localToWorldMatrix;
                                for(int i=0;i<v.Length;i++){v[i]=TwinRoot.SwapYZ(matrix.MultiplyPoint3x4(v[i]))+origin;n[i]=TwinRoot.SwapYZ(matrix.MultiplyVector(n[i])).normalized;}
                                int offset=allV.Count;allV.AddRange(v);foreach(int i in t)allT.Add(i+offset);groups.Add((tile.id,v,t,n));
                            }
                            var bounds=new Bounds((TwinRoot.Vector(tile.min)+TwinRoot.Vector(tile.max))/2,TwinRoot.Vector(tile.max)-TwinRoot.Vector(tile.min));coverage.Add(bounds);
                        }
                        finally {Destroy(go);}
                    }
                    await Task.Yield();
                }
                Fixture.Validate(allV.ToArray(),allT.ToArray(),app.Config,coverage);
                var random=new System.Random(1303);var jitter=new Dictionary<Vector3,Vector3>();
                float sigma=0;foreach(var e in Fixture.edits)if(e.kind=="jitter")sigma=e.sigma_m;
                foreach(var group in groups)
                {
                    var kept=new List<int>();
                    for(int i=0;i<group.t.Length;i+=3)
                    {
                        var p=(group.v[group.t[i]]+group.v[group.t[i+1]]+group.v[group.t[i+2]])/3;bool remove=false;
                        foreach(var e in Fixture.edits)if(e.kind=="remove_sphere"||e.kind=="move_object")if(Vector3.Distance(p,TwinRoot.Vector(e.kind=="move_object"?e.from_twin:e.center_twin))<e.radius_m)remove=true;
                        if(!remove){kept.Add(group.t[i]);kept.Add(group.t[i+1]);kept.Add(group.t[i+2]);}
                    }
                    Publish(group.id,group.v,kept.ToArray(),group.n,fixedMatrix,origin,random,jitter,sigma);await Task.Yield();
                }
                foreach(var e in Fixture.edits)if(e.kind=="add_box"||e.kind=="move_object")
                {
                    // A subdivided, welded box allows normal-ray samples to resolve all sides.
                    Box(TwinRoot.Vector(e.kind=="move_object"?e.to_twin:e.center_twin),TwinRoot.Vector(e.size_m),e.yaw_deg,out var v,out var t,out var n);
                    Publish(e.id,v,t,n,fixedMatrix,origin,random,jitter,sigma);
                }
                Physics.SyncTransforms(); Ready=true; DevLog.Write("Editor scene simulator ready: chunks="+chunks.Count);
            }
            catch(OperationCanceledException){}
            catch(Exception e){Failure=e.ToString();DevLog.Write("Editor scene simulator failed: "+e);}
        }
        void Publish(string id,Vector3[] vertices,int[] triangles,Vector3[] normals,Matrix4x4 alignment,Vector3 origin,System.Random random,Dictionary<Vector3,Vector3> noise,float sigma)
        {
            for(int start=0;start<triangles.Length;start+=60000)
            {
                var map=new Dictionary<int,int>();var v=new List<Vector3>();var n=new List<Vector3>();int count=Math.Min(60000,triangles.Length-start);var t=new int[count];
                for(int i=0;i<count;i++)
                {
                    int old=triangles[start+i];if(!map.TryGetValue(old,out int index))
                    {
                        index=v.Count;map.Add(old,index);var p=vertices[old];
                        if(!noise.TryGetValue(p,out var delta)){delta=new Vector3(Gaussian(random),Gaussian(random),Gaussian(random))*sigma;noise.Add(p,delta);}
                        v.Add(alignment.MultiplyPoint3x4(TwinRoot.SwapYZ(p+delta-origin)));n.Add(alignment.MultiplyVector(TwinRoot.SwapYZ(normals[old])).normalized);
                    }
                    t[i]=index;
                }
                var go=new GameObject("Reality "+id+" "+start,typeof(MeshFilter),typeof(MeshCollider));go.transform.SetParent(transform,false);go.layer=9;
                var mesh=new Mesh{name="Simulated reality",indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetNormals(n);mesh.triangles=t;mesh.RecalculateBounds();
                var filter=go.GetComponent<MeshFilter>();filter.sharedMesh=mesh;go.GetComponent<MeshCollider>().sharedMesh=mesh;
                var chunk=new SurfaceChunk(id+":"+start,filter);chunks.Add(chunk);ChunkChanged?.Invoke(chunk);
            }
        }
        static float Gaussian(System.Random r) => (float)(Math.Sqrt(-2*Math.Log(Math.Max(1e-12,r.NextDouble())))*Math.Cos(2*Math.PI*r.NextDouble()));
        static void Box(Vector3 center,Vector3 size,float yaw,out Vector3[] vertices,out int[] triangles,out Vector3[] normals)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();var rotation=Quaternion.AngleAxis(yaw,Vector3.forward);const int divisions=6;
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                int u=(axis+1)%3,w=(axis+2)%3,offset=v.Count;var normal=Vector3.zero;normal[axis]=sign;
                for(int y=0;y<=divisions;y++)for(int x=0;x<=divisions;x++){var p=Vector3.zero;p[axis]=sign*size[axis]/2;p[u]=(x/(float)divisions-.5f)*size[u];p[w]=(y/(float)divisions-.5f)*size[w];v.Add(center+rotation*p);n.Add(rotation*normal);}
                for(int y=0;y<divisions;y++)for(int x=0;x<divisions;x++){int a=offset+y*(divisions+1)+x,b=a+1,c=a+divisions+1,d=c+1;if(sign>0)t.AddRange(new[]{a,b,d,a,d,c});else t.AddRange(new[]{a,d,b,a,c,d});}
            }
            vertices=v.ToArray();triangles=t.ToArray();normals=n.ToArray();
        }
        void OnDestroy(){lifetime.Cancel();lifetime.Dispose();foreach(var c in chunks){ChunkRemoved?.Invoke(c);if(c.Filter){Destroy(c.Filter.sharedMesh);Destroy(c.Filter.gameObject);}}}
    }
}
#endif
