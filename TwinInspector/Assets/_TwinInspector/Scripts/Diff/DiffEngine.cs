using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Scene/twin discrepancy candidates, with snapshot invalidation and bounded physics scheduling.</summary>
    public sealed class DiffEngine : MonoBehaviour
    {
        sealed class TileGeometry {public TileHandle Handle;public Vector3[] Vertices;public int[] Triangles;}
        readonly Dictionary<string,TileGeometry> tiles=new Dictionary<string,TileGeometry>();
        readonly List<Candidate> members=new List<Candidate>(),visible=new List<Candidate>(),pairs=new List<Candidate>();
        readonly CoalescedChunks queued=new CoalescedChunks();
        readonly List<Dismissal> sessionSuppression=new List<Dismissal>();
        readonly HashSet<string> evaluatedTiles=new HashSet<string>();
        readonly Stopwatch frame=new Stopwatch();
        AppBootstrap app;ISceneSurfaceSource source;DiffWork work;Settings settings;
        DismissedDocument dismissed;string dismissedPath;
        int generation,sourceVersion,lastTileVersion=-1,nextTile,serial;
        float resumeAt,lastLog;Vector3 rootPosition;Quaternion rootRotation;
        bool paused,preferChunk=true,visibleSetting;
        double mainTotal,jobTotal;int measuredFrames,jobCount;long samples,rays,overlaps;
        public IReadOnlyList<Candidate> Candidates=>visible;
        public IReadOnlyList<Candidate> Members=>members;
        public Candidate Selected {get;private set;}
        public AnnotationType PendingType {get;set;}
        public bool Visible {get=>visibleSetting;set{visibleSetting=value;Changed?.Invoke();}}
        public bool Paused=>paused;
        public bool HasPendingWork=>work!=null;
        public bool Settled=>source!=null&&source.Ready&&queued.Count==0&&evaluatedTiles.Count==tiles.Count&&members.All(c=>c.stable);
        public int LastRays {get;private set;}
        public int LastOverlaps {get;private set;}
        public double AverageMainMs {get;private set;}
        public event Action Changed;
        public void Initialize(AppBootstrap application)
        {
            app=application;settings=app.Config;visibleSetting=settings.DiffEnabled;
            dismissedPath=TwinPaths.SaveFile("diff",settings.Scan+"-dismissed");dismissed=JsonStore.Read<DismissedDocument>(dismissedPath)??new DismissedDocument{scan=settings.Scan};
            if(dismissed.schema!="twin-dismissed/1"||dismissed.scan!=settings.Scan) {DevLog.Write("Dismissal document ignored: schema/scan mismatch");dismissed=new DismissedDocument{scan=settings.Scan};}
            app.Loader.CollisionLoaded+=Loaded;app.Loader.CollisionReleasing+=Released;app.Annotations.Changed+=AnnotationChanged;
            foreach(var t in app.Loader.Tiles)if(t.Collision)Loaded(t);
            rootPosition=app.Root.transform.position;rootRotation=app.Root.transform.rotation;resumeAt=Time.unscaledTime+1;
        }
        void Loaded(TileHandle tile)
        {
            var filter=tile.Collision.GetComponentInChildren<MeshFilter>();if(!filter||!filter.sharedMesh.isReadable)return;
            var v=filter.sharedMesh.vertices;var matrix=app.Root.WorldToTwinMatrix*filter.transform.localToWorldMatrix;
            for(int i=0;i<v.Length;i++)v[i]=matrix.MultiplyPoint3x4(v[i]);
            tiles[tile.Data.id]=new TileGeometry{Handle=tile,Vertices=v,Triangles=filter.sharedMesh.triangles};
        }
        void Released(TileHandle tile){tiles.Remove(tile.Data.id);members.RemoveAll(c=>c.space==CandidateSpace.TwinLocal&&c.chunk_or_tile==tile.Data.id);RebuildVisible();}
        void SourceChanged(SurfaceChunk c){sourceVersion++;queued.Put(c);}
        void SourceRemoved(SurfaceChunk c){sourceVersion++;members.RemoveAll(x=>x.space==CandidateSpace.World&&x.chunk_or_tile==c.Id);RebuildVisible();}
        void AnnotationChanged(){if(app.Annotations.Selected!=null&&Selected!=null){Selected=null;Changed?.Invoke();}}
        public void Select(Candidate candidate){Selected=null;app.Annotations.Select(null);Selected=candidate;PendingType=candidate?.Type??AnnotationType.Note;Changed?.Invoke();}
        public void Reevaluate()
        {
            generation++;members.Clear();pairs.Clear();visible.Clear();evaluatedTiles.Clear();queued.Clear();Selected=null;
            if(source!=null)foreach(var c in source.Chunks)queued.Put(c);Changed?.Invoke();
        }
        void Update()
        {
            if(!app||!app.Ready)return;LastRays=LastOverlaps=0;frame.Restart();
            try
            {
                if(source!=app.SceneSource)
                {
                    if(source!=null){source.ChunkChanged-=SourceChanged;source.ChunkRemoved-=SourceRemoved;}
                    source=app.SceneSource;if(source!=null){source.ChunkChanged+=SourceChanged;source.ChunkRemoved+=SourceRemoved;}Reevaluate();
                }
                bool trigger=app.Manual.Active||app.Manual.Dragging||app.Markers.AwaitingConfirmation||app.Roam.Moving||app.Style.Mode==ViewMode.Twin;
                bool moved=Vector3.Distance(rootPosition,app.Root.transform.position)>.01f||Quaternion.Angle(rootRotation,app.Root.transform.rotation)>.5f;
                bool tileChange=lastTileVersion!=app.Loader.TileVersion;
                if(trigger||moved||tileChange)
                {
                    if(!paused||moved||tileChange)Reevaluate();paused=true;resumeAt=Time.unscaledTime+1;
                    rootPosition=app.Root.transform.position;rootRotation=app.Root.transform.rotation;lastTileVersion=app.Loader.TileVersion;
                }
                else paused=Time.unscaledTime<resumeAt;
                if(work!=null&&!Valid(work)){if(work.CanDispose){work.Dispose();work=null;}return;}
                if(paused||!visibleSetting||source==null||!source.Ready||tiles.Count==0)return;
                bool stabilized=false;
                foreach(var c in members)if(!c.stable&&Time.unscaledTime-c.lastEvaluation>=5){c.stable=true;c.confidence=Mathf.Min(1,c.area_m2/(4*settings.DiffMinAreaM2));stabilized=true;}
                if(stabilized)RebuildVisible();
                if(work!=null)
                {
                    work.Step(frame,settings.DiffMainThreadBudgetMs);LastRays=work.ScheduledRays;LastOverlaps=work.ScheduledOverlaps;rays+=LastRays;overlaps+=LastOverlaps;
                    if(work.Done){if(Valid(work)){Apply(work);samples+=work.Samples;jobTotal+=work.JobMs;jobCount++;}work.Dispose();work=null;}
                    return;
                }
                if(frame.Elapsed.TotalMilliseconds>=settings.DiffMainThreadBudgetMs)return;
                var chunk=preferChunk&&queued.Count>0?queued.Take():null;preferChunk=!preferChunk;
                var stamp=new BatchStamp(app.Loader.TileVersion,sourceVersion,generation);
                var coverage=tiles.Values.Select(t=>{var b=new Bounds((TwinRoot.Vector(t.Handle.Data.min)+TwinRoot.Vector(t.Handle.Data.max))/2,TwinRoot.Vector(t.Handle.Data.max)-TwinRoot.Vector(t.Handle.Data.min));b.Expand(settings.DiffDistanceMax*2);return b;}).ToArray();
                var unknown=app.Loader.Tiles.Where(t=>!t.Collision).Select(t=>{var b=new Bounds((TwinRoot.Vector(t.Data.min)+TwinRoot.Vector(t.Data.max))/2,TwinRoot.Vector(t.Data.max)-TwinRoot.Vector(t.Data.min));b.Expand(settings.DiffDistanceMax*2);return b;}).ToArray();
                if(chunk!=null)work=new DiffWork(chunk.Id,false,chunk.Version,stamp,chunk.Vertices,chunk.Normals,chunk.Triangles,Matrix4x4.identity,app.Root.WorldToTwinMatrix,coverage,unknown,settings);
                else
                {
                    var list=tiles.Keys.OrderBy(id=>id,StringComparer.Ordinal).ToArray();nextTile%=list.Length;var tile=tiles[list[nextTile++]];
                    work=new DiffWork(tile.Handle.Data.id,true,app.Loader.TileVersion,stamp,tile.Vertices,null,tile.Triangles,app.Root.WorldToTwinMatrix.inverse,app.Root.WorldToTwinMatrix,coverage,unknown,settings);
                }
            }
            catch(Exception error){DevLog.Write("Diff evaluation failed: "+error);if(work!=null&&work.CanDispose){work.Dispose();work=null;}resumeAt=Time.unscaledTime+1;}
            finally
            {
                frame.Stop();mainTotal+=frame.Elapsed.TotalMilliseconds;measuredFrames++;
                if(Time.unscaledTime-lastLog>=10){AverageMainMs=mainTotal/Math.Max(1,measuredFrames);DevLog.Write($"Diff chunks={source?.Chunks.Count??0} tiles={tiles.Count} samples={samples} rays={rays} overlaps={overlaps} visible_new={visible.Count(c=>c.kind==CandidateKind.New)} visible_missing={visible.Count(c=>c.kind==CandidateKind.Missing)} visible_changed={visible.Count(c=>c.kind==CandidateKind.Changed)} main_ms={AverageMainMs:F3} job_ms={jobTotal/Math.Max(1,jobCount):F3}");lastLog=Time.unscaledTime;mainTotal=0;measuredFrames=0;jobTotal=0;jobCount=0;}
            }
        }
        bool Valid(DiffWork w)
        {
            if(!w.Stamp.Matches(app.Loader.TileVersion,w.Stamp.SourceVersion,generation))return false;
            if(w.Missing)return tiles.ContainsKey(w.Id);
            foreach(var c in source.Chunks)if(c.Id==w.Id&&c.Version==w.Version)return true;
            return false;
        }
        void Apply(DiffWork w)
        {
            var kind=w.Missing?CandidateKind.Missing:CandidateKind.New;var previous=members.Where(c=>c.kind==kind&&c.chunk_or_tile==w.Id).ToList();
            members.RemoveAll(c=>c.kind==kind&&c.chunk_or_tile==w.Id);
            foreach(var cluster in w.Result)
            {
                var r=cluster.Region;var old=previous.OrderBy(c=>Vector3.Distance(TwinRoot.Vector(c.centroid),r.Centroid)).FirstOrDefault(c=>Vector3.Distance(TwinRoot.Vector(c.centroid),r.Centroid)<.5f);if(old!=null)previous.Remove(old);
                var candidate=new Candidate{id=old?.id??"c-"+(++serial).ToString("D6"),kind=kind,space=w.Missing?CandidateSpace.TwinLocal:CandidateSpace.World,centroid=TwinRoot.Array(r.Centroid),normal=TwinRoot.Array(r.Normal),hull=Array.ConvertAll(r.Hull,TwinRoot.Array),area_m2=r.Area,extent_m=TwinRoot.Array(r.Extent),chunk_or_tile=w.Id,chunk_or_tile_version=w.Version,triangles=cluster.Triangles};
                CandidateRules.Observe(candidate,old,settings.DiffPersistUpdates,Time.unscaledTime,settings.DiffMinAreaM2);members.Add(candidate);
            }
            if(w.Missing)evaluatedTiles.Add(w.Id);RebuildVisible();
        }
        bool Suppressed(Candidate c)=>dismissed.items.Any(d=>d.Suppresses(c.kind,c.CentroidTwin(app.Root)))||sessionSuppression.Any(d=>d.Suppresses(c.kind,c.CentroidTwin(app.Root)));
        void RebuildVisible()
        {
            if(!this||!app||!app.Root)return;
            if(!app)return;
            var eligible=members.Where(c=>!Suppressed(c)).ToList();pairs.Clear();pairs.AddRange(CandidateRules.Pair(eligible,c=>c.CentroidWorld(app.Root),settings.DiffChangedPairDistanceM));
            visible.Clear();visible.AddRange(eligible.Where(c=>c.stable&&!c.hiddenByPair));visible.AddRange(pairs.Where(c=>!Suppressed(c)));
            visible.Sort((a,b)=>b.area_m2.CompareTo(a.area_m2));if(visible.Count>200)visible.RemoveRange(200,visible.Count-200);
            if(Selected!=null)Selected=visible.Find(c=>c.id==Selected.id);Changed?.Invoke();
        }
        Dismissal Sphere(Candidate c)=>new Dismissal{kind=c.kind.ToString().ToLowerInvariant(),centroid_twin=TwinRoot.Array(c.CentroidTwin(app.Root)),radius_m=Mathf.Max(.5f,TwinRoot.Vector(c.extent_m).magnitude*.5f),dismissed_utc=DateTime.UtcNow.ToString("O")};
        void Remove(Candidate c,bool persist)
        {
            foreach(var id in CandidateRules.MemberIds(c)) {var member=id==c.id?c:members.Find(x=>x.id==id);if(member==null)continue;var sphere=Sphere(member);if(persist)dismissed.items.Add(sphere);else sessionSuppression.Add(sphere);members.RemoveAll(x=>x.id==id);}
            Selected=null;if(persist)JsonStore.Write(dismissedPath,dismissed);RebuildVisible();
        }
        public void Dismiss(){if(Selected==null)return;Remove(Selected,true);app.Feedback?.Confirm();app.UI.Notify(L.Get("toast.candidateDismissed"));}
        public Annotation Confirm()
        {
            var c=Selected;if(c==null)return null;var region=Region(c);var normal=TwinRoot.Vector(region.normal_twin);
            var item=app.Annotations.Add(c.CentroidTwin(app.Root),normal,app.Root.WorldToTwin(app.Viewer.transform.position),app.Root.RotationToTwin(app.Viewer.transform.rotation),PendingType,region);
            Remove(c,false);app.Feedback?.Confirm();app.UI.Notify(L.Format("toast.candidateConfirmedAnnotation0D2",item.index));return item;
        }
        public AnnotationRegion Region(Candidate c)
        {
            var region=new AnnotationRegion{source="diff",centroid_twin=TwinRoot.Array(c.CentroidTwin(app.Root)),normal_twin=TwinRoot.Array(c.space==CandidateSpace.World?app.Root.NormalToTwin(TwinRoot.Vector(c.normal)):TwinRoot.Vector(c.normal)),hull_twin=c.HullTwin(app.Root),area_m2=c.area_m2,extent_m=c.extent_m};
            if(c.space==CandidateSpace.TwinLocal)region.triangles=new AnnotationRegion.TriangleReference{tile=c.chunk_or_tile,indices=c.triangles};
            if(c.paired_new!=null){var paired=members.Find(x=>x.id==c.paired_new);if(paired!=null)region.paired_new_hull_twin=paired.HullTwin(app.Root);}return region;
        }
        public bool Geometry(Candidate c,out Vector3[] vertices,out int[] triangles)
        {
            if(c.space==CandidateSpace.TwinLocal&&tiles.TryGetValue(c.chunk_or_tile,out var t)){vertices=t.Vertices;triangles=t.Triangles;return true;}
            var chunk=source?.Chunks.FirstOrDefault(s=>s.Id==c.chunk_or_tile);vertices=chunk?.Vertices;triangles=chunk?.Triangles;return chunk!=null;
        }
        void OnDestroy(){if(work!=null)work.Dispose();if(source!=null){source.ChunkChanged-=SourceChanged;source.ChunkRemoved-=SourceRemoved;}if(app&&app.Loader){app.Loader.CollisionLoaded-=Loaded;app.Loader.CollisionReleasing-=Released;}if(app?.Annotations!=null)app.Annotations.Changed-=AnnotationChanged;}
        void OnDisable(){if(work!=null){work.Dispose();work=null;}}
    }
}
