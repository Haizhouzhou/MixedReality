using NUnit.Framework;
using UnityEngine;

namespace TwinInspector.Tests
{
    public class RecenterKeeperTests
    {
        static void AssertPose(Pose expected, Pose actual)
        {
            Assert.That(Vector3.Distance(expected.position, actual.position), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(expected.rotation, actual.rotation), Is.LessThan(.01f));
        }

        [Test] public void RelativeAndComposeRoundTrip()
        {
            var anchor = new Pose(new Vector3(1.3f, 1.6f, -2.1f), Quaternion.Euler(0, 37, 0));
            var twin = new Pose(new Vector3(-4.2f, -.3f, 7.9f), Quaternion.Euler(0, -112, 0));
            AssertPose(twin, RecenterKeeper.Compose(anchor, RecenterKeeper.Relative(anchor, twin)));
        }

        /// <summary>A recenter moves every world-locked pose by the same yaw and translation; the twin must follow it.</summary>
        [Test] public void TwinFollowsARecenterOfTheTrackingFrame()
        {
            var anchor = new Pose(new Vector3(.4f, 1.7f, .2f), Quaternion.Euler(0, 10, 0));
            var twin = new Pose(new Vector3(-6f, 0f, 12f), Quaternion.Euler(0, 75, 0));
            var offset = RecenterKeeper.Relative(anchor, twin);
            var turn = Quaternion.Euler(0, -63, 0); var shift = new Vector3(-2.5f, 0, 1.25f);
            Pose Recenter(Pose p) => new Pose(turn * p.position + shift, turn * p.rotation);
            AssertPose(Recenter(twin), RecenterKeeper.Compose(Recenter(anchor), offset));
        }
    }
}
