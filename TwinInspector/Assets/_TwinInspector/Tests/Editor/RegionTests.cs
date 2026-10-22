using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TwinInspector.Tests
{
    public class RegionTests
    {
        [Test] public void PcaSquareAndCircleHull()
        {
            var p=new[]{new Vector3(0,0,.01f),new Vector3(1,0,-.01f),new Vector3(1,1,.01f),new Vector3(0,1,-.01f)};
            var r=RegionBuilder.Build(p,new[]{0,1,2,0,2,3});
            Assert.That(r.Hull.Length,Is.EqualTo(4)); Assert.That(Vector3.Angle(r.Normal,Vector3.forward),Is.LessThan(2));
            foreach(var v in r.Hull) Assert.That(p.Min(a=>Vector3.Distance(v,a)),Is.LessThan(.03));
            var circle=Enumerable.Range(0,500).Select(i=>new Vector3(Mathf.Cos(i*Mathf.PI*2/500),Mathf.Sin(i*Mathf.PI*2/500),0)).Concat(new[]{Vector3.zero}).ToArray();
            var indices=Enumerable.Range(0,500).SelectMany(i=>new[]{500,i,(i+1)%500}).ToArray();
            Assert.That(RegionBuilder.Build(circle,indices).Hull.Length,Is.EqualTo(64));
        }
        [Test] public void ObjIndicesAndMetadataAreInteroperable()
        {
            var writer=new StringWriter(); ObjWriter.Write(writer,new[]{new Vector3(1,2,3),Vector3.right,Vector3.up},new[]{0,1,2},"scan","a-000001","2026-10-22");
            var s=writer.ToString(); StringAssert.Contains("v 1 2 3",s); StringAssert.Contains("f 1 2 3",s); StringAssert.Contains("scan=scan annotation=a-000001",s);
            Assert.That(s.Split('\n').Count(l=>l.StartsWith("v ")),Is.EqualTo(3));
        }
        [Test] public void HullRoundTripAfterAlignmentPreservesAsymmetricCoordinates()
        {
            var go=new GameObject();try {var root=go.AddComponent<TwinRoot>();root.Initialize(new[]{20f,3f,7f});root.transform.SetPositionAndRotation(new Vector3(-2,7,3),Quaternion.Euler(0,73,0));
                foreach(var p in new[]{new Vector3(7,13,2),new Vector3(8,15,3),new Vector3(5,19,4)}) Assert.That(Vector3.Distance(p,root.WorldToTwin(root.TwinToWorld(p))),Is.LessThan(.0001f));
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
