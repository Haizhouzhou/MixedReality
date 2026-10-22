using NUnit.Framework;
using UnityEngine;

namespace TwinInspector.Tests
{
    public sealed class PickTests
    {
        [Test] public void PromptUsesTopLeftPixelsAndRejectsOutsideFrustum()
        {
            Assert.IsTrue(PickGeometry.TryPixel(new Vector3(.25f,.75f,1),out var pixel));Assert.AreEqual(new Vector2(256,256),pixel);
            Assert.IsFalse(PickGeometry.TryPixel(new Vector3(.5f,.5f,-1),out _));Assert.IsFalse(PickGeometry.TryPixel(new Vector3(1.01f,.5f,1),out _));
        }
        [Test] public void BackProjectionRejectsDepthBleedAndRequiresTwelveHits()
        {
            Assert.IsTrue(PickGeometry.AcceptDepth(4.9f,4));Assert.IsFalse(PickGeometry.AcceptDepth(5.1f,4));
            Assert.IsTrue(PickGeometry.AcceptDepth(1.4f,1));Assert.IsFalse(PickGeometry.AcceptDepth(1.6f,1));
            Assert.IsFalse(PickGeometry.EnoughHits(11));Assert.IsTrue(PickGeometry.EnoughHits(12));
            Assert.IsFalse(PickGeometry.CoherentNormal(Vector3.right,Vector3.forward));Assert.IsTrue(PickGeometry.CoherentNormal(Vector3.back,Vector3.forward));
            Assert.IsFalse(PickGeometry.CoherentNormal(new Vector3(1,0,1),Vector3.forward));
            Assert.AreEqual(1.5f,PickGeometry.Bilinear(new[]{0f,1f,2f,3f},2,2,.5f,.5f));
        }
    }
}
