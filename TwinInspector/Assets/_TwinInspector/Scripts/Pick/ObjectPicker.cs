using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;

namespace TwinInspector
{
    /// <summary>Consumes one prompted twin hit and attaches a mask-backed region without changing annotation type.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ObjectPicker : MonoBehaviour
    {
        AppBootstrap app;SamModels models;SamRunner runner;CancellationTokenSource operation;
        string annotationId;Vector3 viewerStart,rootStart;Quaternion rootRotation;int tileVersion;
        long peakMemory;
        public bool Available {get;private set;}
        public bool Armed {get;private set;}
        public bool Busy {get;private set;}
        public string LastResult {get;private set;}
        public double LastMilliseconds {get;private set;}
        public event Action Changed;
        public async void Initialize(AppBootstrap application)
        {
            app=application;if(!app.Config.ObjectPickEnabled)return;
            try{models=await SamModels.VerifyAsync(TwinPaths.ModelsRoot(app.Config));if(this){Available=true;Changed?.Invoke();}}
            catch(Exception error){DevLog.Write("SAM unavailable: "+error.Message);}
        }
        public void Arm()
        {
            if(!Available||Busy||app.Annotations.Selected==null)return;
            annotationId=app.Annotations.Selected.id;Armed=true;app.Placer.Active=false;
            app.UI.Notify(L.Get("toast.pointAtASurfaceAndSelect"));Changed?.Invoke();
        }
        public void Cancel(){bool armed=Armed;Armed=false;operation?.Cancel();if(armed)Changed?.Invoke();}
        void Update()
        {
            if(!app||!app.Ready)return;
            if(Busy)peakMemory=Math.Max(peakMemory,Profiler.GetTotalAllocatedMemoryLong());
            if(Armed&&(app.UI.Page!="picker"||app.Annotations.Selected?.id!=annotationId)){Cancel();return;}
            if(Busy&&(app.UI.Page!="picker"||!app.UI.Panel.gameObject.activeSelf||app.Annotations.Selected?.id!=annotationId||Vector3.Distance(app.Viewer.transform.position,viewerStart)>1||app.Loader.TileVersion!=tileVersion||Vector3.Distance(app.Root.transform.position,rootStart)>.01f||Quaternion.Angle(app.Root.transform.rotation,rootRotation)>.5f))Cancel();
            if(!Armed||!app.Input.Pressed||app.Input.OverUI||!app.Input.HasBuildingHit)return;
            var hit=app.Input.BuildingHit;
            if(!app.Style.PlacementVisible(hit.point)||!app.Input.ConsumePress())return;
            _ = PickAsync(app.Annotations.Selected,hit);
        }
        public async Task<bool> PickAsync(Annotation item,RaycastHit prompt)
        {
            if(!Available||Busy||item==null||!prompt.collider)return false;
            Armed=false;Busy=true;annotationId=item.id;LastResult="processing";
            viewerStart=app.Viewer.transform.position;rootStart=app.Root.transform.position;rootRotation=app.Root.transform.rotation;tileVersion=app.Loader.TileVersion;
            operation=new CancellationTokenSource();operation.CancelAfter(TimeSpan.FromSeconds(45));var token=operation.Token;
            long before=Profiler.GetTotalAllocatedMemoryLong();peakMemory=before;var clock=Stopwatch.StartNew();Changed?.Invoke();app.UI.Notify(L.Get("toast.processingObject"));
            try
            {
                runner??=new SamRunner(models,TwinPaths.ModelsRoot(app.Config));
                using(var capture=new TwinCapture(app.Viewer,prompt.point))
                {
                    var pixels=await capture.ReadAsync(token);
                    var mask=await runner.RunAsync(pixels,capture.Prompt,app.Config.ObjectPickFramesBudgetMs,token);
                    var region=await BackProject(capture.Camera,mask,Vector3.Distance(viewerStart,prompt.point),prompt.normal,token);
                    token.ThrowIfCancellationRequested();
                    if(region==null){LastResult="no object";app.UI.Notify(L.Get("toast.noObjectFound"));return false;}
                    app.Annotations.SetRegion(item.id,region);LastResult="picked";app.UI.Notify(L.Format("toast.objectPicked0F1M",region.area_m2));return true;
                }
            }
            catch(OperationCanceledException){LastResult="cancelled";runner?.Dispose();runner=null;return false;}
            catch(Exception error){LastResult="failed: "+error.Message;DevLog.Write("SAM pick failed: "+error);app.UI.Notify(L.Get("toast.noObjectFound"));runner?.Dispose();runner=null;return false;}
            finally
            {
                LastMilliseconds=clock.Elapsed.TotalMilliseconds;long delta=Profiler.GetTotalAllocatedMemoryLong()-before;
                DevLog.Write(FormattableString.Invariant($"SAM result={LastResult} total_ms={LastMilliseconds:F1} encoder_ms={runner?.EncoderMs:F1} max_schedule_ms={runner?.MaxScheduleMs:F2} decoder_schedule_ms={runner?.DecoderScheduleMs:F2} managed_native_delta_bytes={delta} sampled_peak_delta_bytes={peakMemory-before} source={(Application.isEditor?"Editor":"device")}"));
                operation.Dispose();operation=null;Busy=false;Changed?.Invoke();
            }
        }
        async Task<AnnotationRegion> BackProject(Camera capture,float[] mask,float promptDistance,Vector3 promptNormal,CancellationToken token)
        {
            if(mask.Length!=256*256)throw new InvalidOperationException("Unexpected SAM mask dimensions");
            int minX=256,minY=256,maxX=-1,maxY=-1;
            for(int y=0;y<256;y++)for(int x=0;x<256;x++)if(mask[y*256+x]>0){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
            if(maxX<0)return null;
            var rays=new List<Ray>(4096);var gridIds=new List<int>(4096);var grid=new int[4096];Array.Fill(grid,-1);
            var vertices=new List<Vector3>();var triangles=new List<int>();var normals=new List<Vector3>();
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float px=Mathf.Lerp(minX,maxX,(x+.5f)/64),py=Mathf.Lerp(minY,maxY,(y+.5f)/64);
                if(PickGeometry.Bilinear(mask,256,256,px,py)<=0)continue;
                rays.Add(capture.ViewportPointToRay(new Vector3((px+.5f)/256,1-(py+.5f)/256,0)));
                gridIds.Add(y*64+x);
            }
            var references=new Dictionary<TileHandle,HashSet<int>>();var colliders=new Dictionary<Collider,TileHandle>();
            foreach(var tile in app.Loader.Tiles)if(tile.Collision)foreach(var collider in tile.Collision.GetComponentsInChildren<MeshCollider>())colliders[collider]=tile;
            int accepted=0;
            for(int start=0;start<rays.Count;start+=2048)
            {
                int count=Math.Min(2048,rays.Count-start);var commands=new NativeArray<RaycastCommand>(count,Allocator.Persistent);var hits=new NativeArray<RaycastHit>(count,Allocator.Persistent);JobHandle handle=default;
                try
                {
                    for(int i=0;i<count;i++){var ray=rays[start+i];commands[i]=new RaycastCommand(ray.origin,ray.direction,new QueryParameters(1<<8,false,QueryTriggerInteraction.Ignore,true),80);}
                    handle=RaycastCommand.ScheduleBatch(commands,hits,32,1);JobHandle.ScheduleBatchedJobs();
                    do{await Awaitable.NextFrameAsync();}while(!handle.IsCompleted);
                    handle.Complete();token.ThrowIfCancellationRequested();
                    for(int i=0;i<hits.Length;i++)
                    {
                        var hit=hits[i];
                        if(!hit.collider||hit.triangleIndex<0||!PickGeometry.AcceptDepth(Vector3.Distance(capture.transform.position,hit.point),promptDistance)||!PickGeometry.CoherentNormal(hit.normal,promptNormal)||!colliders.TryGetValue(hit.collider,out var tile))continue;
                        accepted++;if(!references.TryGetValue(tile,out var ids)){ids=new HashSet<int>();references[tile]=ids;}ids.Add(hit.triangleIndex);
                        grid[gridIds[start+i]]=vertices.Count;vertices.Add(app.Root.WorldToTwin(hit.point));normals.Add(app.Root.NormalToTwin(hit.normal));
                    }
                }
                finally{handle.Complete();commands.Dispose();hits.Dispose();}
            }
            if(!PickGeometry.EnoughHits(accepted))return null;
            // Fit the actual mask samples, not entire coarse triangles extending outside the mask.
            // Adjacent accepted samples form a surface patch; skip gaps and depth discontinuities.
            void Add(int a,int b,int c)
            {
                if(a<0||b<0||c<0||(vertices[a]-vertices[b]).sqrMagnitude>.25f||(vertices[a]-vertices[c]).sqrMagnitude>.25f||(vertices[b]-vertices[c]).sqrMagnitude>.25f)return;
                if(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),normals[a]+normals[b]+normals[c])<0){int swap=b;b=c;c=swap;}
                triangles.Add(a);triangles.Add(b);triangles.Add(c);
            }
            for(int y=0;y<63;y++)for(int x=0;x<63;x++){int i=y*64+x;Add(grid[i],grid[i+1],grid[i+64]);Add(grid[i+1],grid[i+65],grid[i+64]);}
            if(triangles.Count<3)return null;
            var refs=references.Select(pair=>new AnnotationRegion.TriangleReference{tile=pair.Key.Data.id,lod=2,indices=pair.Value.OrderBy(i=>i).ToArray()}).ToList();
            var geometry=await Task.Run(()=>RegionBuilder.Build(vertices.ToArray(),triangles.ToArray()),token);
            var region=geometry.ToRegion("sam");region.triangles=refs[0];region.additional_triangles=refs.Skip(1).ToArray();return region;
        }
        void OnDestroy(){operation?.Cancel();if(!Busy){runner?.Dispose();runner=null;}else _=DisposeWhenIdle();}
        async Task DisposeWhenIdle(){while(Busy)await Task.Yield();runner?.Dispose();runner=null;}
    }
}
