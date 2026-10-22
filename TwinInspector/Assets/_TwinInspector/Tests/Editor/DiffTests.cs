using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace TwinInspector.Tests
{
    public class DiffTests
    {
        [Test] public void EdgeIslandsAndAreaExtentGates()
        {
            var v=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.one,new Vector3(4,0,0),new Vector3(5,0,0),new Vector3(4,1,0)};
            var t=new[]{0,1,2,1,3,2,4,5,6};var unexplained=new[]{true,true,true};
            Assert.That(DiffClusterer.Build(v,t,unexplained,.1f,.1f).Count,Is.EqualTo(2));
            Assert.That(DiffClusterer.Build(v,t,unexplained,.75f,.1f).Count,Is.EqualTo(1));
            Assert.That(DiffClusterer.Build(v,t,unexplained,.1f,5).Count,Is.Zero);
        }
        [Test] public void SampleRayAndFloorCeilingRules()
        {
            var ray=DiffSampler.Ray(new Vector3(1,2,3),Vector3.forward,.35f,1<<8);
            Assert.That(ray.from,Is.EqualTo(new Vector3(1,2,2.65f)));Assert.That(ray.direction,Is.EqualTo(Vector3.forward));Assert.That(ray.distance,Is.EqualTo(.7f));Assert.That(ray.queryParameters.hitBackfaces,Is.True);
            Assert.That(DiffSampler.Ignore(Vector3.zero,Vector3.forward,true,0),Is.True);
            Assert.That(DiffSampler.Ignore(new Vector3(0,0,2.6f),Vector3.back,true,0),Is.True);
            Assert.That(DiffSampler.Ignore(new Vector3(0,0,1),Vector3.forward,true,0),Is.False);
            Assert.That(DiffSampler.Ignore(new Vector3(0,0,3),Vector3.right,true,0),Is.False);
        }
        static Candidate C(string id,CandidateKind kind,float x)=>new Candidate{id=id,kind=kind,centroid=new[]{x,0f,0f},area_m2=1,stable=true};
        [Test] public void PersistenceDismissalAndGreedyPairing()
        {
            var a=C("a",CandidateKind.Missing,0);CandidateRules.Observe(a,null,3,0,.15f);Assert.That(a.stable,Is.False);
            var b=C("b",CandidateKind.Missing,0);CandidateRules.Observe(b,a,3,1,.15f);Assert.That(b.stable,Is.False);
            var c=C("c",CandidateKind.Missing,0);CandidateRules.Observe(c,b,3,2,.15f);Assert.That(c.stable,Is.True);
            var sphere=new Dismissal{kind="missing",centroid_twin=new[]{0f,0f,0f},radius_m=.5f};Assert.That(sphere.Suppresses(CandidateKind.Missing,new Vector3(.3f,0,0)),Is.True);Assert.That(sphere.Suppresses(CandidateKind.New,Vector3.zero),Is.False);
            var members=new[]{c,C("m2",CandidateKind.Missing,.3f),C("n1",CandidateKind.New,.31f),C("n2",CandidateKind.New,.8f),C("far",CandidateKind.New,5)};
            var pairs=CandidateRules.Pair(members,x=>TwinRoot.Vector(x.centroid),1);
            Assert.That(pairs.Count,Is.EqualTo(2));Assert.That(pairs[0].paired_missing,Is.EqualTo("m2"));Assert.That(pairs[0].paired_new,Is.EqualTo("n1"));Assert.That(members.Count(x=>x.hiddenByPair),Is.EqualTo(4));
            CollectionAssert.AreEquivalent(new[]{pairs[0].id,"m2","n1"},CandidateRules.MemberIds(pairs[0]));
        }
        [Test] public void StampsAndCoalescingRejectStaleVersions()
        {
            var stamp=new BatchStamp(2,7,3);Assert.That(stamp.Matches(3,7,3),Is.False);Assert.That(stamp.Matches(2,7,4),Is.False);
            var go=new GameObject();var mesh=new Mesh();try{mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.triangles=new[]{0,1,2};mesh.RecalculateNormals();var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=mesh;var c=new SurfaceChunk("one",filter);var queue=new CoalescedChunks();queue.Put(c);c.Capture(filter);queue.Put(c);Assert.That(queue.Count,Is.EqualTo(1));Assert.That(queue.Take().Version,Is.EqualTo(2));Assert.That(queue.Count,Is.Zero);}finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [UnityTest] public IEnumerator BatchedMeshHitContainsTriangleIndex()
        {
            var go=new GameObject();var mesh=new Mesh();var rays=new NativeArray<RaycastCommand>(1,Allocator.Persistent);var hits=new NativeArray<RaycastHit>(1,Allocator.Persistent);
            try {go.layer=8;mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(0,1,0)};mesh.triangles=new[]{0,1,2};go.AddComponent<MeshCollider>().sharedMesh=mesh;Physics.SyncTransforms();rays[0]=DiffSampler.Ray(Vector3.zero,Vector3.forward,1,1<<8);var handle=RaycastCommand.ScheduleBatch(rays,hits,1,1);yield return null;handle.Complete();Assert.That(hits[0].collider,Is.EqualTo(go.GetComponent<MeshCollider>()));Assert.That(hits[0].triangleIndex,Is.EqualTo(0));}
            finally{rays.Dispose();hits.Dispose();UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
