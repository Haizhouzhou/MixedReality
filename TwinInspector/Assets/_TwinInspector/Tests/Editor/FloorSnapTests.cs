using NUnit.Framework;
using UnityEngine;

namespace TwinInspector.Tests
{
    public class FloorSnapTests
    {
        /// <summary>After a floor snap the twin's datum (main floor height in twin Z) must sit on the tracked plane,
        /// including spaces whose datum is not zero (CAB: −4.8 m) and whose origin has a height.</summary>
        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(-.12f, 0f, 0f, -4.8f)]
        [TestCase(.35f, 60f, 30f, -4.8f)]
        [TestCase(-.4f, 20f, 10f, 2.5f)]
        public void DatumLandsOnTheTrackedFloor(float planeY, float originX, float originZ, float datumZ)
        {
            var go = new GameObject("root");
            try
            {
                var root = go.AddComponent<TwinRoot>();
                root.Initialize(new[] { originX, 3f, originZ });
                go.transform.rotation = Quaternion.Euler(0, 37, 0);
                go.transform.position = new Vector3(1.5f, ManualAlignment.FloorSnapHeight(planeY, originZ, datumZ), -2f);
                var datumPoint = root.TwinToWorld(new Vector3(originX + 4f, 5f, datumZ));
                Assert.That(datumPoint.y, Is.EqualTo(planeY).Within(1e-4f));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
