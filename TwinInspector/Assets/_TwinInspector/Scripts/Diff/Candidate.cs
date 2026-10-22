using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

namespace TwinInspector
{
    public enum CandidateKind { New,Missing,Changed }
    public enum CandidateSpace { World,TwinLocal }
    [Serializable,Preserve] public sealed class Candidate
    {
        public string id,chunk_or_tile,paired_new,first_seen_utc;
        public CandidateKind kind;
        public CandidateSpace space;
        public float[] centroid,normal,extent_m;
        public float[][] hull;
        public float area_m2,confidence;
        public int chunk_or_tile_version;
        public int[] triangles;
        public bool stable,hiddenByPair;
        [NonSerialized] public int observations;
        [NonSerialized] public float firstSeen,lastEvaluation;
        [NonSerialized] public string paired_missing;
        public Vector3 CentroidTwin(TwinRoot root)=>space==CandidateSpace.World?root.WorldToTwin(TwinRoot.Vector(centroid)):TwinRoot.Vector(centroid);
        public Vector3 CentroidWorld(TwinRoot root)=>space==CandidateSpace.World?TwinRoot.Vector(centroid):root.TwinToWorld(TwinRoot.Vector(centroid));
        public float[][] HullTwin(TwinRoot root)=>space==CandidateSpace.World?Array.ConvertAll(hull,p=>TwinRoot.Array(root.WorldToTwin(TwinRoot.Vector(p)))):hull;
        public AnnotationType Type=>kind==CandidateKind.New?AnnotationType.New:kind==CandidateKind.Missing?AnnotationType.Missing:AnnotationType.Changed;
    }
    [Serializable,Preserve] public sealed class DismissedDocument
    {
        public string schema="twin-dismissed/1",scan;
        public List<Dismissal> items=new List<Dismissal>();
    }
    [Serializable,Preserve] public sealed class Dismissal
    {
        public string kind,dismissed_utc;
        public float[] centroid_twin;
        public float radius_m;
        public bool Suppresses(CandidateKind type,Vector3 point)=>kind==type.ToString().ToLowerInvariant()&&Vector3.Distance(TwinRoot.Vector(centroid_twin),point)<=radius_m;
    }
    /// <summary>Pure candidate matching, persistence and pairing rules shared by runtime and tests.</summary>
    public static class CandidateRules
    {
        public static void Observe(Candidate next,Candidate previous,int required,float now,float minArea)
        {
            next.observations=(previous?.observations??0)+1;next.firstSeen=previous?.firstSeen??now;
            next.first_seen_utc=previous?.first_seen_utc??DateTime.UtcNow.ToString("O");next.lastEvaluation=now;
            next.stable=next.observations>=required || (previous!=null && previous.stable);
            next.confidence=Mathf.Min(1,next.area_m2/(4*minArea))*Mathf.Min(1,next.observations/(float)required);
        }
        public static List<Candidate> Pair(IList<Candidate> members,Func<Candidate,Vector3> position,float limit)
        {
            var distances=new List<(float distance,Candidate missing,Candidate added)>();
            foreach(var a in members){a.hiddenByPair=false;if(!a.stable||a.kind!=CandidateKind.Missing)continue;foreach(var b in members)if(b.stable&&b.kind==CandidateKind.New){float distance=Vector3.Distance(position(a),position(b));if(distance<=limit)distances.Add((distance,a,b));}}
            distances.Sort((a,b)=>a.distance!=b.distance?a.distance.CompareTo(b.distance):string.CompareOrdinal(a.missing.id+"/"+a.added.id,b.missing.id+"/"+b.added.id));
            var result=new List<Candidate>();
            foreach(var pair in distances)
            {
                var a=pair.missing;var b=pair.added;if(a.hiddenByPair||b.hiddenByPair)continue;a.hiddenByPair=b.hiddenByPair=true;
                result.Add(new Candidate{id="pair:"+a.id+":"+b.id,kind=CandidateKind.Changed,space=a.space,centroid=a.centroid,normal=a.normal,hull=a.hull,extent_m=a.extent_m,area_m2=a.area_m2,confidence=Mathf.Min(a.confidence,b.confidence),chunk_or_tile=a.chunk_or_tile,chunk_or_tile_version=a.chunk_or_tile_version,triangles=a.triangles,paired_new=b.id,paired_missing=a.id,stable=true,first_seen_utc=a.first_seen_utc});
            }
            return result;
        }
        public static string[] MemberIds(Candidate candidate)=>candidate.kind==CandidateKind.Changed?new[]{candidate.id,candidate.paired_missing,candidate.paired_new}:new[]{candidate.id};
    }
    public sealed class CoalescedChunks
    {
        readonly Queue<string> queue=new Queue<string>(); readonly Dictionary<string,SurfaceChunk> latest=new Dictionary<string,SurfaceChunk>();
        public int Count=>latest.Count;
        public void Put(SurfaceChunk chunk){if(!latest.ContainsKey(chunk.Id))queue.Enqueue(chunk.Id);latest[chunk.Id]=chunk;}
        public SurfaceChunk Take(){while(queue.Count>0){var id=queue.Dequeue();if(latest.TryGetValue(id,out var c)){latest.Remove(id);return c;}}return null;}
        public void Clear(){queue.Clear();latest.Clear();}
    }
    public readonly struct BatchStamp
    {
        public readonly int TileVersion,SourceVersion,Generation;
        public BatchStamp(int tile,int source,int generation){TileVersion=tile;SourceVersion=source;Generation=generation;}
        public bool Matches(int tile,int source,int generation)=>tile==TileVersion&&source==SourceVersion&&generation==Generation;
    }
}
