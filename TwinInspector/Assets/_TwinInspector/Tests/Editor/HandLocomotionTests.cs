using NUnit.Framework;
using UnityEngine;

namespace TwinInspector.Tests
{
    public class HandLocomotionTests
    {
        [Test]
        public void WorldDragKeepsPinchedPointFixedUnderTranslationAndYaw()
        {
            var start=new Pose(new Vector3(7,1,-3),Quaternion.Euler(0,37,0));
            var initialMid=new Vector3(.2f,1.3f,.6f);var initialSpan=Vector3.right*.4f;
            var mid=new Vector3(.4f,1.3f,.3f);var span=Quaternion.Euler(0,28,0)*initialSpan;
            var result=HandLocomotion.DragPose(start,initialMid,initialSpan,mid,span);
            Assert.That(Vector3.Distance(start.position+start.rotation*initialMid,result.position+result.rotation*mid),Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(result.rotation,start.rotation*Quaternion.Euler(0,-28,0)),Is.LessThan(.001f));
        }
        [Test]
        public void WorldDragDoesNotChangeFloorHeightOrAddRoll()
        {
            var start=new Pose(new Vector3(7,1,-3),Quaternion.Euler(0,37,0));
            var result=HandLocomotion.DragPose(start,new Vector3(0,1,.5f),Vector3.right,new Vector3(0,2,.5f),Vector3.forward);
            Assert.That(result.position.y,Is.EqualTo(start.position.y));
            Assert.That(Vector3.Distance(result.rotation*Vector3.up,Vector3.up),Is.LessThan(.00001f));
        }
    }
}
