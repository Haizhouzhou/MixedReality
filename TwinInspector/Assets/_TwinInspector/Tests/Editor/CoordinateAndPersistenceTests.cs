using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TwinInspector.Tests
{
    public sealed class CoordinateAndPersistenceTests
    {
        [Test]
        public void GltfHandednessAndContainerRotationKeepWestNegative()
        {
            var twin=new Vector3(-17,23,4);
            var gltf=new Vector3(twin.x,twin.z,-twin.y);
            var imported=new Vector3(-gltf.x,gltf.y,gltf.z);
            var final=Quaternion.Euler(0,180,0)*imported;
            Assert.That(Vector3.Distance(final,TwinRoot.SwapYZ(twin)),Is.LessThan(.00001));
            Assert.That(final.x,Is.LessThan(0),"The west rotunda must remain west");
        }
        [Test]
        public void OriginalCoordinatesAndNormalsRoundTripAfterYawAndTranslation()
        {
            var go=new GameObject("coordinate test");
            try
            {
                var root=go.AddComponent<TwinRoot>(); root.Initialize(new[]{20f,0f,0f});
                root.transform.SetPositionAndRotation(new Vector3(7,.25f,-3),Quaternion.Euler(0,127,0));
                var p=new Vector3(-13.72f,27.31f,1.49f); var n=new Vector3(.23f,-.91f,.1f).normalized;
                Assert.That(Vector3.Distance(p,root.WorldToTwin(root.TwinToWorld(p))),Is.LessThan(.00002));
                Assert.That(Vector3.Distance(n,root.NormalToTwin(root.NormalToWorld(n))),Is.LessThan(.00001));
                var world=Quaternion.Euler(10,30,20); var q=root.RotationToTwin(world); var twinQ=new Quaternion(q[0],q[1],q[2],q[3]);
                var direction=new Vector3(.2f,.7f,.4f);
                var expected=TwinRoot.SwapYZ(Quaternion.Inverse(root.transform.rotation)*world*direction);
                Assert.That(Vector3.Distance(twinQ*TwinRoot.SwapYZ(direction),expected),Is.LessThan(.00001));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test]
        public void AnnotationEditsSurviveRestartWithoutReusingDeletedIds()
        {
            string folder=Path.Combine("Temp","TwinInspectorTests",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"annotations.json");
            try
            {
                var store=new AnnotationStore("fixture",path);
                var first=store.Add(new Vector3(1,2,3),Vector3.forward,Vector3.zero,new[]{0f,0f,0f,1f}); store.Retype(AnnotationType.Missing);
                var restored=new AnnotationStore("fixture",path);
                Assert.That(restored.Items.Count,Is.EqualTo(1)); Assert.That(restored.Items[0].type,Is.EqualTo("missing"));
                CollectionAssert.AreEqual(new[]{1f,2f,3f},restored.Items[0].position_twin);
                restored.Select(first.id); restored.DeleteSelected();
                var again=new AnnotationStore("fixture",path); var next=again.Add(Vector3.one,Vector3.up,Vector3.zero,new[]{0f,0f,0f,1f});
                Assert.That(next.index,Is.EqualTo(2)); Assert.That(next.id,Is.Not.EqualTo(first.id));
                Assert.That(File.Exists(path+".bak"),Is.True);
            }
            finally { Directory.Delete(folder,true); }
        }
        [TestCase("../secret")]
        [TestCase("C:/outside")]
        [TestCase("/absolute")]
        public void ManifestPathsCannotEscapeDataRoot(string relative)
        { Assert.Throws<InvalidDataException>(()=>TwinPaths.Url("E:/fixture",relative)); }
    }
}
