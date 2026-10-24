using NUnit.Framework;
using UnityEngine;

namespace TwinInspector.Tests
{
    public class DockTests
    {
        [Test] public void GlancingKeepsBodyDirectionTurningBringsItAlong()
        {
            Assert.That(DockPlacement.FollowBodyYaw(0,30),Is.EqualTo(0),"looking at a right-front dock must not push it away");
            Assert.That(DockPlacement.FollowBodyYaw(0,90),Is.EqualTo(55).Within(1e-4f),"turning trails by the dead zone");
            Assert.That(Mathf.DeltaAngle(DockPlacement.FollowBodyYaw(10,-80),-45),Is.EqualTo(0).Within(1e-4f));
            Assert.That(Mathf.DeltaAngle(DockPlacement.FollowBodyYaw(170,-150),175),Is.EqualTo(0).Within(1e-4f),"wraps across 180°");
        }

        [Test] public void HomeIsRightFrontAtTheSavedOffsetAndFacesTheViewer()
        {
            var head=new Vector3(1,1.6f,2);
            var home=DockPlacement.Home(head,0,.55f,20,-.22f);
            Assert.That(home.x,Is.GreaterThan(head.x),"positive yaw is to the right");
            Assert.That(Vector3.Distance(new Vector3(home.x,0,home.z),new Vector3(head.x,0,head.z)),Is.EqualTo(.55f).Within(1e-4f));
            Assert.That(home.y,Is.EqualTo(1.38f).Within(1e-4f));
            var facing=DockPlacement.Facing(head,home)*Vector3.forward;
            Assert.That(Vector3.Angle(facing,Vector3.ProjectOnPlane(home-head,Vector3.up)),Is.LessThan(.01f));
            Assert.That(Vector3.Angle(DockPlacement.Facing(head,home)*Vector3.up,Vector3.up),Is.LessThan(.01f),"stays upright");
        }

        [Test] public void DraggedPoseRoundTripsAndIsClamped()
        {
            var head=new Vector3(0,1.5f,0);
            var dropped=DockPlacement.Home(head,90,.7f,-30,.1f);
            var (distance,yaw,height)=DockPlacement.Offsets(head,90,dropped);
            Assert.That(distance,Is.EqualTo(.7f).Within(1e-4f)); Assert.That(yaw,Is.EqualTo(-30).Within(1e-3f)); Assert.That(height,Is.EqualTo(.1f).Within(1e-4f));
            (distance,yaw,height)=DockPlacement.Offsets(head,0,new Vector3(3,-2,.1f));
            Assert.That(distance,Is.EqualTo(.9f)); Assert.That(yaw,Is.EqualTo(45)); Assert.That(height,Is.EqualTo(-.3f));
        }

        [Test] public void OnlyAnIdleUnpinnedDockHides()
        {
            Assert.That(DockPlacement.ShouldHide(false,false,false,false,2.1f),Is.True);
            Assert.That(DockPlacement.ShouldHide(false,false,false,false,1.9f),Is.False,"waits two seconds");
            Assert.That(DockPlacement.ShouldHide(true,false,false,false,10),Is.False,"pinned");
            Assert.That(DockPlacement.ShouldHide(false,true,false,false,10),Is.False,"palm still up");
            Assert.That(DockPlacement.ShouldHide(false,false,true,false,10),Is.False,"being looked at");
            Assert.That(DockPlacement.ShouldHide(false,false,false,true,10),Is.False,"in use");
            Assert.That(DockPlacement.Looking(Vector3.zero,Vector3.forward,new Vector3(.2f,0,1)),Is.True);
            Assert.That(DockPlacement.Looking(Vector3.zero,Vector3.forward,new Vector3(1,0,1)),Is.False);
        }

        [Test] public void PinCannotBeReleasedWithoutThePalmGesture()
        {
            bool palm=InteractionPreferences.PalmSummon, pinned=InteractionPreferences.DockPinnedSetting;
            try
            {
                InteractionPreferences.DockPinnedSetting=false;
                InteractionPreferences.PalmSummon=true; Assert.That(InteractionPreferences.DockPinned,Is.False);
                InteractionPreferences.PalmSummon=false; Assert.That(InteractionPreferences.DockPinned,Is.True);
            }
            finally { InteractionPreferences.PalmSummon=palm; InteractionPreferences.DockPinnedSetting=pinned; }
        }
    }
}
