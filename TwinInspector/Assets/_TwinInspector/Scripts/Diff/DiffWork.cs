using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>One snapshot evaluated in bounded batches; jobs are never completed in their scheduling frame.</summary>
    public sealed class DiffWork : IDisposable
    {
        public readonly string Id;
        public readonly bool Missing;
        public readonly int Version;
        public readonly BatchStamp Stamp;
        public readonly Vector3[] Vertices;
        public readonly int[] Triangles;
        readonly Task<Prepared> preparation;
        Task<List<DiffClusterer.Cluster>> clustering;
        Prepared data;
        readonly float distance,observedRadius,minArea,minExtent;
        readonly int cap;
        NativeArray<RaycastCommand> rays;
        NativeArray<RaycastHit> rayHits;
        NativeArray<OverlapSphereCommand> overlaps;
        NativeArray<ColliderHit> overlapHits;
        NativeArray<byte> flags;
        JobHandle handle;
        int cursor,batchStart,batchCount,scheduledFrame=-1,reduceCursor;
        bool pending,overlapPhase;
        readonly Stopwatch jobClock=new Stopwatch();
        public double JobMs { get; private set; }
        public int Samples=>data?.Points.Length??0;
        public int ScheduledRays { get; private set; }
        public int ScheduledOverlaps { get; private set; }
        public bool Done=>clustering!=null && clustering.IsCompleted;
        public List<DiffClusterer.Cluster> Result=>clustering.GetAwaiter().GetResult();
        sealed class Prepared {public Vector3[] Points,Normals;public bool[] Eligible;}
        public DiffWork(string id,bool missing,int version,BatchStamp stamp,Vector3[] vertices,Vector3[] normals,int[] triangles,Matrix4x4 toWorld,Matrix4x4 worldToTwin,Bounds[] coverage,Bounds[] unknown,Settings settings)
        {
            Id=id;Missing=missing;Version=version;Stamp=stamp;Vertices=vertices;Triangles=triangles;
            distance=settings.DiffDistanceMax;observedRadius=settings.DiffObservedRadiusM;minArea=settings.DiffMinAreaM2;minExtent=settings.DiffMinExtentM;
            cap=Math.Min(settings.DiffRaycastsPerFrame,settings.DiffOverlapsPerFrame);
            bool ignore=settings.DiffIgnoreFloorCeiling;float floor=settings.DiffFloorZ;
            // Everything captured here is a value or an immutable array; the worker never accesses a Unity object.
            preparation=Task.Run(()=>
            {
                int count=missing?triangles.Length/3:vertices.Length;
                var p=new Vector3[count];var n=new Vector3[count];var eligible=new bool[count];
                var normalMatrix=toWorld.inverse.transpose;var twinNormal=worldToTwin.inverse.transpose;
                for(int i=0;i<count;i++)
                {
                    Vector3 position,normal;
                    if(missing){var a=vertices[triangles[i*3]];var b=vertices[triangles[i*3+1]];var c=vertices[triangles[i*3+2]];position=(a+b+c)/3;normal=Vector3.Cross(b-a,c-a).normalized;}
                    else {position=vertices[i];normal=normals[i];}
                    p[i]=toWorld.MultiplyPoint3x4(position);n[i]=normalMatrix.MultiplyVector(normal).normalized;
                    bool inside=missing;
                    if(!inside)foreach(var bounds in coverage)if(bounds.Contains(worldToTwin.MultiplyPoint3x4(p[i]))){inside=true;break;}
                    // Overlapping tile AABBs are not proof of complete coverage at streaming boundaries.
                    // A sample that could belong to an unloaded tile remains unknown, never New.
                    if(!missing&&inside)foreach(var bounds in unknown)if(bounds.Contains(worldToTwin.MultiplyPoint3x4(p[i]))){inside=false;break;}
                    eligible[i]=inside&&!DiffSampler.Ignore(worldToTwin.MultiplyPoint3x4(p[i]),twinNormal.MultiplyVector(n[i]).normalized,ignore,floor);
                }
                return new Prepared{Points=p,Normals=n,Eligible=eligible};
            });
        }
        public void Step(Stopwatch frame,double budget)
        {
            ScheduledRays=ScheduledOverlaps=0;
            if(data==null)
            {
                if(!preparation.IsCompleted)return;data=preparation.GetAwaiter().GetResult();
                flags=new NativeArray<byte>(data.Points.Length,Allocator.Persistent);
                rays=new NativeArray<RaycastCommand>(cap,Allocator.Persistent);rayHits=new NativeArray<RaycastHit>(cap,Allocator.Persistent);
                overlaps=new NativeArray<OverlapSphereCommand>(cap,Allocator.Persistent);overlapHits=new NativeArray<ColliderHit>(cap,Allocator.Persistent);
                if(frame.Elapsed.TotalMilliseconds>=budget)return;
            }
            if(clustering!=null)return;
            if(pending)
            {
                if(Time.frameCount<=scheduledFrame || !handle.IsCompleted)return;
                handle.Complete();jobClock.Stop();JobMs+=jobClock.Elapsed.TotalMilliseconds;
                while(reduceCursor<batchCount)
                {
                    int j=batchStart+reduceCursor;
                    if(overlapPhase) flags[j]=(byte)(data.Eligible[j]&&overlapHits[reduceCursor].instanceID!=0?1:0);
                    else flags[j]=(byte)(data.Eligible[j]&&(!Missing||flags[j]!=0)&&!rayHits[reduceCursor].collider?1:0);
                    reduceCursor++;
                    if((reduceCursor&63)==0 && frame.Elapsed.TotalMilliseconds>=budget)return;
                }
                pending=false;reduceCursor=0;
                if(overlapPhase){overlapPhase=false;Schedule(false);return;}
                cursor+=batchCount;
            }
            if(frame.Elapsed.TotalMilliseconds>=budget)return;
            if(cursor<data.Points.Length)
            {
                batchStart=cursor;batchCount=Math.Min(cap,data.Points.Length-cursor);overlapPhase=Missing;
                Schedule(Missing);return;
            }
            var unexplained=new bool[Triangles.Length/3];
            for(int i=0;i<unexplained.Length;i++) unexplained[i]=Missing?flags[i]!=0:flags[Triangles[i*3]]!=0&&flags[Triangles[i*3+1]]!=0&&flags[Triangles[i*3+2]]!=0;
            clustering=Task.Run(()=>DiffClusterer.Build(Vertices,Triangles,unexplained,minArea,minExtent));
        }
        void Schedule(bool observed)
        {
            // Ineligible samples use a zero mask; max command counts include those commands too.
            for(int i=0;i<batchCount;i++)
            {
                int j=batchStart+i;
                if(observed) overlaps[i]=data.Eligible[j]?DiffSampler.Observed(data.Points[j],observedRadius):new OverlapSphereCommand(Vector3.zero,0,new QueryParameters(0));
                else rays[i]=DiffSampler.Ray(data.Points[j],data.Normals[j],distance,data.Eligible[j]&&(!Missing||flags[j]!=0)?(Missing?1<<9:1<<8):0);
            }
            scheduledFrame=Time.frameCount;pending=true;jobClock.Restart();
            if(observed){handle=OverlapSphereCommand.ScheduleBatch(overlaps.GetSubArray(0,batchCount),overlapHits.GetSubArray(0,batchCount),32,1);ScheduledOverlaps=batchCount;}
            else {handle=RaycastCommand.ScheduleBatch(rays.GetSubArray(0,batchCount),rayHits.GetSubArray(0,batchCount),32,1);ScheduledRays=batchCount;}
        }
        public void Dispose()
        {
            // Disposal is used for cancellation/shutdown as well; await native job ownership before freeing buffers.
            if(pending)handle.Complete();
            if(flags.IsCreated)flags.Dispose();if(rays.IsCreated)rays.Dispose();if(rayHits.IsCreated)rayHits.Dispose();if(overlaps.IsCreated)overlaps.Dispose();if(overlapHits.IsCreated)overlapHits.Dispose();
        }
        public bool CanDispose=>!pending||handle.IsCompleted;
    }
}
