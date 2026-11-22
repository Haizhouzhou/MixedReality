using NUnit.Framework;
using UnityEngine;
namespace TwinInspector.Tests
{
    public sealed class SpatialInteractionTests
    {
        [TestCase(10f)] [TestCase(20f)] [TestCase(30f)]
        public void GlyphAndIndependentTargetMeetAngularMinimum(float distance)
        {
            foreach(AnnotationType type in System.Enum.GetValues(typeof(AnnotationType)))
            {
                float diameter=AngularPresentation.Diameter(distance,1.5f,.04f);
                float scale=diameter/AnnotationMarker.BaseGlyphDiameter(type);
                float angle=2*Mathf.Atan(AnnotationMarker.BaseGlyphDiameter(type)*scale/(2*distance))*Mathf.Rad2Deg;
                Assert.That(angle,Is.EqualTo(1.5f).Within(.0001f));
                Assert.That(AngularPresentation.Diameter(distance,2),Is.GreaterThan(diameter));
            }
        }
        [Test]
        public void ShaderFrameRestoresOriginAndSurvivesAlignment()
        {
            var go=new GameObject("shader coordinates");
            try
            {
                var root=go.AddComponent<TwinRoot>(); root.Initialize(new[]{20f,-10f,2f});
                foreach(float yaw in new[]{0,37,127})
                {
                    root.transform.SetPositionAndRotation(new Vector3(7,.25f,-3),Quaternion.Euler(0,yaw,0));
                    foreach(var p in new[]{new Vector3(30,10,0),new Vector3(29.999f,10,1.5f),new Vector3(30.001f,10,1.5f)})
                    {
                        var shader=root.WorldToTwinMatrix.MultiplyPoint3x4(root.TwinToWorld(p));
                        Assert.That(Vector3.Distance(shader,p),Is.LessThan(.00002f));
                        Assert.That(shader.z/.5f,Is.EqualTo(p.z/.5f).Within(.00004f));
                    }
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void PrePinchHasTimeoutCancellationConfirmationAndHysteresis()
        {
            var pinch=new PinchStabilization(); pinch.Reset();
            pinch.Step(.8f,false,true,0); Assert.That(pinch.State,Is.EqualTo(PinchStabilization.Phase.Normal),"Reacquisition mid-pinch is not armed");
            pinch.Step(.2f,false,true,.1f); Assert.That(pinch.Step(.41f,false,true,.2f),Is.LessThan(1));
            pinch.Step(.5f,false,true,.36f); Assert.That(pinch.State,Is.EqualTo(PinchStabilization.Phase.Recovery));
            pinch.Step(.5f,false,true,.5f); pinch.Step(.5f,false,true,.6f); Assert.That(pinch.State,Is.EqualTo(PinchStabilization.Phase.Normal));
            pinch.Step(.2f,false,true,.7f); pinch.Step(.41f,false,true,.8f); Assert.That(pinch.Step(1,true,true,.82f),Is.EqualTo(1),"Manipulation is immediately responsive");
            pinch.Step(0,false,false,.83f); Assert.That(pinch.State,Is.EqualTo(PinchStabilization.Phase.Normal));
            pinch.Step(0,false,true,1); pinch.Step(.4f,false,true,1.1f); pinch.Step(.2f,false,true,1.12f); Assert.That(pinch.State,Is.EqualTo(PinchStabilization.Phase.Recovery));
        }
        [TestCase(true)] [TestCase(false)]
        public void EitherSingleHandOwnsWholeGestureRegardlessOfPreference(bool left)
        {
            int side=left?0:1;
            foreach(bool preferLeft in new[]{true,false})
            {
                Assert.That(InputRouter.ChooseOwner(-1,left,!left,0,0,preferLeft),Is.EqualTo(side));
                Assert.That(InputRouter.ChooseOwner(side,true,true,left?.5f:1,left?1:.5f,preferLeft),Is.EqualTo(side));
                Assert.That(InputRouter.ChooseOwner(side,!left,left,0,0,preferLeft),Is.EqualTo(1-side));
                Assert.That(InputRouter.ChooseOwner(1-side,true,true,0,0,preferLeft,left,!left),Is.EqualTo(side),"the hand pointing at a panel gets the pointer");
                Assert.That(InputRouter.ChooseOwner(1-side,true,true,0,0,preferLeft,true,true),Is.EqualTo(1-side),"both point: the current one keeps it");
                Assert.That(InputRouter.ChooseOwner(1-side,true,true,left?0:.5f,left?.5f:0,preferLeft,false,false),Is.EqualTo(1-side),"a pinching hand keeps it");
                Assert.That(InputRouter.ChooseOwner(1-side,true,true,left?0:.5f,left?.5f:0,preferLeft,left,!left),Is.EqualTo(side),"but not against a hand pointing at the menu");
                Assert.That(InputRouter.ChooseOwner(side,true,true,left?0:.6f,left?.6f:0,preferLeft,left,!left),Is.EqualTo(side),"a pinch elsewhere does not take the menu hand's pointer");
            }
        }
        [Test]
        public void RelaxedGalaxyXrHandReadsAsNoPinch()
        {
            Assert.That(InputRouter.HandPinch(.28f),Is.EqualTo(0),"relaxed hand");
            Assert.That(InputRouter.HandPinch(.5f),Is.EqualTo(.5f).Within(1e-5f),"press point unchanged");
            Assert.That(InputRouter.HandPinch(.75f),Is.EqualTo(1),"full pinch");
        }
        [Test]
        public void CoplanarNoiseIsNotACreaseButCornerIs()
        {
            Assert.That(ArchitecturalMesh.IsCrease(Vector3.up,Quaternion.Euler(4,0,0)*Vector3.up),Is.False);
            Assert.That(ArchitecturalMesh.IsCrease(Vector3.up,Vector3.right),Is.True);
        }
    }
}
