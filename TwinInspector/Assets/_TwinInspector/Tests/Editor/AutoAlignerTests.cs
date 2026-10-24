using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TwinInspector.Tests
{
    public class AutoAlignerTests
    {
        // A 20 × 12 m hall with an inner L-wall and two columns, so the plan has no symmetry; floor at y = 0.
        static AutoAligner.Cloud Hall()
        {
            var p = new List<Vector3>(); var n = new List<Vector3>();
            void Plane(Vector3 origin, Vector3 u, Vector3 v, Vector3 normal, float lu, float lv)
            { for (float a = 0; a <= lu; a += .05f) for (float b = 0; b <= lv; b += .05f) { p.Add(origin + u * a + v * b); n.Add(normal); } }
            Plane(Vector3.zero, Vector3.right, Vector3.forward, Vector3.up, 20, 12);
            Plane(Vector3.zero, Vector3.right, Vector3.up, Vector3.forward, 20, 3);
            Plane(new Vector3(0, 0, 12), Vector3.right, Vector3.up, Vector3.back, 20, 3);
            Plane(Vector3.zero, Vector3.forward, Vector3.up, Vector3.right, 12, 3);
            Plane(new Vector3(20, 0, 0), Vector3.forward, Vector3.up, Vector3.left, 12, 3);
            Plane(new Vector3(6, 0, 4), Vector3.right, Vector3.up, Vector3.back, 5, 3);
            Plane(new Vector3(6, 0, 4), Vector3.forward, Vector3.up, Vector3.left, 4, 3);
            foreach (var c in new[] { new Vector3(14, 0, 3), new Vector3(15, 0, 8.5f) })
            {
                Plane(c, Vector3.right, Vector3.up, Vector3.back, .6f, 3); Plane(c + Vector3.forward * .6f, Vector3.right, Vector3.up, Vector3.forward, .6f, 3);
                Plane(c, Vector3.forward, Vector3.up, Vector3.left, .6f, 3); Plane(c + Vector3.right * .6f, Vector3.forward, Vector3.up, Vector3.right, .6f, 3);
            }
            return new AutoAligner.Cloud { Points = p.ToArray(), Normals = n.ToArray() };
        }

        // Walls only, floor at y = 0: a rectangle from (0, 0) to (width, depth), optionally without its two short walls (a corridor).
        static AutoAligner.Cloud Room(float width, float depth, bool ends = true)
        {
            var p = new List<Vector3>(); var n = new List<Vector3>();
            void Plane(Vector3 origin, Vector3 u, Vector3 v, Vector3 normal, float lu, float lv)
            { for (float a = 0; a <= lu; a += .05f) for (float b = 0; b <= lv; b += .05f) { p.Add(origin + u * a + v * b); n.Add(normal); } }
            Plane(Vector3.zero, Vector3.right, Vector3.forward, Vector3.up, width, depth);
            Plane(Vector3.zero, Vector3.right, Vector3.up, Vector3.forward, width, 3);
            Plane(new Vector3(0, 0, depth), Vector3.right, Vector3.up, Vector3.back, width, 3);
            if (ends) { Plane(Vector3.zero, Vector3.forward, Vector3.up, Vector3.right, depth, 3); Plane(new Vector3(width, 0, 0), Vector3.forward, Vector3.up, Vector3.left, depth, 3); }
            return new AutoAligner.Cloud { Points = p.ToArray(), Normals = n.ToArray() };
        }

        // Scene = part of the hall seen from (x, z), moved by the inverse of (yaw, t), with noise and a box that is not in the twin.
        static AutoAligner.Cloud Scan(AutoAligner.Cloud hall, Vector2 centre, float radius, float yaw, Vector3 t)
        {
            var random = new System.Random(7); var p = new List<Vector3>(); var n = new List<Vector3>();
            float Noise() => (float)(random.NextDouble() - .5) * .02f;
            void Add(Vector3 twinPoint, Vector3 twinNormal)
            { p.Add(AutoAligner.Rotate(twinPoint - t, -yaw) + new Vector3(Noise(), Noise(), Noise())); n.Add(AutoAligner.Rotate(twinNormal, -yaw)); }
            for (int i = 0; i < hall.Count; i++)
                if (Vector2.Distance(new Vector2(hall.Points[i].x, hall.Points[i].z), centre) < radius) Add(hall.Points[i], hall.Normals[i]);
            for (float a = 0; a <= 1; a += .05f) for (float b = 0; b <= 1; b += .05f) Add(new Vector3(centre.x + 1 + a, b, centre.y - 1), Vector3.back);
            return new AutoAligner.Cloud { Points = p.ToArray(), Normals = n.ToArray() };
        }

        [Test] public void RecoversYawAndTranslationFromAPartialScanWithAChange()
        {
            var hall = AutoAligner.Downsample(Hall());
            float yaw = .3f; var t = new Vector3(4, -.4f, -2);
            var scene = AutoAligner.Downsample(Scan(Hall(), new Vector2(10, 2.5f), 6, yaw, t));
            float sceneFloor = -t.y;
            var result = AutoAligner.Solve(hall, scene, 0, sceneFloor + 1.6f);
            Assert.That(result.Found, Is.True, result.Reason);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(result.Yaw * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg)), Is.LessThan(.5f), "yaw");
            Assert.That(Vector3.Distance(result.Translation, t), Is.LessThan(.03f), "translation " + result.Translation);
            Assert.That(result.Inliers, Is.GreaterThan(.8f));
            Assert.That(result.Rms, Is.LessThan(.02f));
            Assert.That(result.Reliable, Is.True);
        }

        [Test] public void LookAlikePlacementIsRecoveredButNotCalledReliable()
        {
            // From (8, 6) the scan sees the L-wall's inner corner, which a column corner elsewhere matches as well.
            var hall = AutoAligner.Downsample(Hall());
            float yaw = .3f; var t = new Vector3(4, -.4f, -2);
            var result = AutoAligner.Solve(hall, AutoAligner.Downsample(Scan(Hall(), new Vector2(8, 6), 6, yaw, t)), 0, -t.y + 1.6f);
            Assert.That(result.Found, Is.True, result.Reason);
            Assert.That(Vector3.Distance(result.Translation, t), Is.LessThan(.03f), "nearest look-alike to the current pose");
            Assert.That(result.Ambiguity, Is.LessThan(1.15f));
            Assert.That(result.Reliable, Is.False);
        }

        [Test] public void FloorIsTheLowestBroadUpwardSurfaceBelowTheHead()
        {
            var floor = new List<Vector3>(); var normals = new List<Vector3>();
            void Patch(float y, float size) { for (float x = 0; x < size; x += .05f) for (float z = 0; z < size; z += .05f) { floor.Add(new Vector3(x, y, z)); normals.Add(Vector3.up); } }
            Patch(-4.8f, 1.2f); // little floor seen
            Patch(-4.05f, 2.4f); // more desk top than floor, as when looking ahead
            Patch(-4.6f, .2f); // a step too small to count
            var cloud = new AutoAligner.Cloud { Points = floor.ToArray(), Normals = normals.ToArray() };
            Assert.That(AutoAligner.EstimateFloor(cloud, -3.2f), Is.EqualTo(-4.8f).Within(.01f), "lowest broad surface, not the largest");
            Assert.That(AutoAligner.EstimateFloor(cloud, -3.2f, -4.1f), Is.EqualTo(-4.05f).Within(.01f), "a surface at the tracked floor wins");
            Assert.That(float.IsNaN(AutoAligner.EstimateFloor(cloud, 5)), Is.True, "no floor within reach of the head");
        }

        [Test] public void ParallelWallsAloneAreFlaggedAsSliding()
        {
            var corridor = AutoAligner.Downsample(Room(20, 3, false));
            var scene = AutoAligner.Downsample(Scan(Room(20, 3, false), new Vector2(10, 1.5f), 4, .05f, new Vector3(.3f, 0, .1f)));
            var result = AutoAligner.Solve(corridor, scene, 0, 1.6f);
            Assert.That(result.Found, Is.True, result.Reason);
            Assert.That(result.Constraint, Is.LessThan(AutoAligner.MinConstraint));
            Assert.That(result.Slides, Is.True); Assert.That(result.Reliable, Is.False);
        }

        [Test] public void SymmetricRoomKeepsTheNearbyFitInsteadOfItsMirrorImage()
        {
            // A plain rectangle fits equally well turned 180°; the scan is 3° and 30 cm from the current alignment.
            var room = AutoAligner.Downsample(Room(16, 10));
            float yaw = 3 * Mathf.Deg2Rad; var t = new Vector3(.3f, 0, -.2f);
            var result = AutoAligner.Solve(room, AutoAligner.Downsample(Scan(Room(16, 10), new Vector2(8, 5), 12, yaw, t)), 0, 1.6f);
            Assert.That(result.Found, Is.True, result.Reason);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(result.Yaw * Mathf.Rad2Deg, 3)), Is.LessThan(.5f), "not the 183° look-alike");
            Assert.That(Vector3.Distance(result.Translation, t), Is.LessThan(.03f));
            Assert.That(result.Slides, Is.False);
            Assert.That(result.Reliable, Is.True, "the distant mirror image does not count against a fit near the current alignment");
        }

        [Test] public void LargeTrianglesAreSampledDenselyWithTheirOwnNormal()
        {
            // A 4 × 3 m wall as two triangles, wound so the face normal points -Z while the vertex normals say +Z.
            var v = new[] { new Vector3(0, 0, 0), new Vector3(4, 0, 0), new Vector3(4, 3, 0), new Vector3(0, 3, 0) };
            var n = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            var samples = new AutoAligner.Accumulator(.1f);
            AutoAligner.SampleSurface(samples, v, n, new[] { 0, 2, 1, 0, 3, 2 }, Matrix4x4.Translate(new Vector3(1, 0, 0)), .1f);
            var cloud = samples.ToCloud();
            Assert.That(cloud.Count, Is.InRange(1100, 1400), "about one sample per 10 cm voxel of 12 m²");
            foreach (var normal in cloud.Normals) Assert.That(Vector3.Dot(normal, Vector3.forward), Is.GreaterThan(.99f), "turned to agree with the vertex normals");
            foreach (var p in cloud.Points) Assert.That(p.x, Is.InRange(.99f, 5.01f), "matrix applied");
        }

        [Test] public void RotateMatchesUnityYawConvention()
        {
            var v = new Vector3(1, 2, 3);
            var expected = Quaternion.Euler(0, 40, 0) * v;
            Assert.That(Vector3.Distance(AutoAligner.Rotate(v, 40 * Mathf.Deg2Rad), expected), Is.LessThan(1e-5f));
        }

        [Test] public void RefusesWhenTooLittleWallWasScanned()
        {
            var hall = AutoAligner.Downsample(Hall());
            var floorOnly = new List<Vector3>(); var up = new List<Vector3>();
            for (float x = 0; x < 3; x += .05f) for (float z = 0; z < 3; z += .05f) { floorOnly.Add(new Vector3(x, 0, z)); up.Add(Vector3.up); }
            var result = AutoAligner.Solve(hall, new AutoAligner.Cloud { Points = floorOnly.ToArray(), Normals = up.ToArray() }, 0, 1.6f);
            Assert.That(result.Found, Is.False);
            Assert.That(result.Reason, Does.Contain("wall"));
        }
    }
}
