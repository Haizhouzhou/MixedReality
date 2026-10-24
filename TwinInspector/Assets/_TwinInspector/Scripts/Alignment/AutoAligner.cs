using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Four-degree-of-freedom registration of the headset's scene mesh to the twin, in world (Y-up) coordinates.
    /// Gravity is shared, so only yaw and translation are solved: floor height first, then yaw candidates from the
    /// dominant wall directions, a floor-plan search for position, and point-to-plane ICP with robust weights.</summary>
    /// <remarks>Pure math on plain arrays: safe on a worker thread (no Unity object or native math calls).</remarks>
    public static class AutoAligner
    {
        public sealed class Cloud
        {
            public Vector3[] Points, Normals;
            public int Count => Points?.Length ?? 0;
        }

        /// <summary>Maps scene points onto the twin: p' = Rotate(p, Yaw) + Translation (Yaw in radians about +Y).</summary>
        public struct Result
        {
            public bool Found;
            public float Yaw;
            public Vector3 Translation;
            public float Inliers, Rms, Ambiguity, SceneFloor;
            /// <summary>How well the matched walls fix horizontal position: the weaker over the stronger direction of their normals
            /// (0 = only parallel walls, so the scan can slide along them; 1 = walls in all directions).</summary>
            public float Constraint;
            /// <summary>The fit near the current alignment was kept although a distant one scored up to 15% higher.</summary>
            public bool KeptCurrent;
            public int Candidates;
            public string Reason;
            /// <summary>Every refined candidate (yaw, translation, inlier fraction, RMS), for logs and review.</summary>
            public (float yaw, Vector3 t, float inliers, float rms)[] All;
            public bool Slides => Found && Constraint < MinConstraint;
            /// <summary>Thresholds for presenting a result as reliable rather than to be checked carefully.</summary>
            public bool Reliable => Found && Inliers >= .45f && Rms <= .04f && Ambiguity >= 1.15f && !Slides;
        }

        /// <remarks>Walls count from 0.8 m above the floor: below that, desks, chairs and boxes that the twin may not have dominate.</remarks>
        public const float Voxel = .05f, GridCell = .2f, WallBandLow = .8f, WallBandHigh = 2.2f, MinConstraint = .12f;
        /// <summary>A fit this close to the current alignment is "near" it: kept unless a distant fit scores clearly higher.</summary>
        const float NearYaw = 15 * Mathf.Deg2Rad, NearDistance = 1.5f, NearShare = .85f;
        const int MaxIcpPoints = 6000, TopPerYaw = 3, IcpCandidates = 6;

        /// <summary>Same convention as Quaternion.Euler(0, degrees, 0) applied to a vector.</summary>
        public static Vector3 Rotate(Vector3 p, float yaw)
        {
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            return new Vector3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z);
        }

        /// <summary>Voxel average of points and normals; keeps one representative per occupied voxel.</summary>
        public static Cloud Downsample(Cloud cloud, float voxel = Voxel)
        {
            var sums = new Accumulator(voxel);
            for (int i = 0; i < cloud.Count; i++) sums.Add(cloud.Points[i], cloud.Normals[i]);
            return sums.ToCloud();
        }

        /// <summary>Merges points per voxel, averaging position and normal, so dense and sparse sources weigh alike.</summary>
        public sealed class Accumulator
        {
            readonly Dictionary<long, (Vector3 p, Vector3 n, int k)> sums = new Dictionary<long, (Vector3, Vector3, int)>();
            readonly float voxel;
            public Accumulator(float voxelSize = Voxel) => voxel = voxelSize;
            public int Count => sums.Count;
            public void Add(Vector3 p, Vector3 n)
            {
                long key = Key(Mathf.FloorToInt(p.x / voxel), Mathf.FloorToInt(p.y / voxel), Mathf.FloorToInt(p.z / voxel));
                sums.TryGetValue(key, out var s);
                sums[key] = (s.p + p, s.n + n, s.k + 1);
            }
            public Cloud ToCloud()
            {
                var points = new Vector3[sums.Count]; var normals = new Vector3[sums.Count]; int j = 0;
                foreach (var s in sums.Values) { points[j] = s.p / s.k; normals[j] = s.n.sqrMagnitude > 1e-8f ? s.n.normalized : Vector3.up; j++; }
                return new Cloud { Points = points, Normals = normals };
            }
        }

        /// <summary>Samples triangles every <paramref name="spacing"/> metres after <paramref name="matrix"/>, with face normals turned
        /// to agree with the vertex normals; <paramref name="keep"/> filters samples (null keeps all).</summary>
        /// <remarks>Twin walls are often two large triangles, so vertices alone would leave them nearly empty.</remarks>
        public static void SampleSurface(Accumulator into, Vector3[] vertices, Vector3[] normals, int[] triangles, Matrix4x4 matrix, float spacing, Func<Vector3, bool> keep = null)
        {
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                Vector3 a = matrix.MultiplyPoint3x4(vertices[ia]), b = matrix.MultiplyPoint3x4(vertices[ib]), c = matrix.MultiplyPoint3x4(vertices[ic]);
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) continue;
                n.Normalize();
                if (normals != null && normals.Length == vertices.Length && Vector3.Dot(n, matrix.MultiplyVector(normals[ia] + normals[ib] + normals[ic])) < 0) n = -n;
                float edge = Mathf.Max((b - a).magnitude, Mathf.Max((c - b).magnitude, (a - c).magnitude));
                int k = Mathf.Clamp(Mathf.CeilToInt(edge / spacing), 1, 2000);
                for (int u = 0; u <= k; u++)
                    for (int v = 0; u + v <= k; v++)
                    {
                        var p = a + (b - a) * (u / (float)k) + (c - a) * (v / (float)k);
                        if (keep == null || keep(p)) into.Add(p, n);
                    }
            }
        }

        /// <summary>Floor height: the tracked floor <paramref name="hint"/> when enough upward surface lies within 25 cm of it, otherwise the
        /// lowest broad upward surface between 0.4 and 2.4 m below the head; NaN if too little floor was seen.</summary>
        /// <remarks>Lowest, not largest: a headset looking ahead scans desk tops more than the floor.</remarks>
        public static float EstimateFloor(Cloud scene, float headY, float hint = float.NaN)
        {
            const float bin = .02f, low = 2.4f, high = .4f;
            int bins = Mathf.CeilToInt((low - high) / bin); var counts = new int[bins];
            for (int i = 0; i < scene.Count; i++)
            {
                if (scene.Normals[i].y < .9f) continue;
                int b = Mathf.FloorToInt((scene.Points[i].y - (headY - low)) / bin);
                if (b >= 0 && b < bins) counts[b]++;
            }
            int peak = 0; for (int b = 1; b < bins; b++) if (counts[b] > counts[peak]) peak = b;
            if (counts[peak] < 40) return float.NaN;
            int best = -1;
            if (!float.IsNaN(hint))
                for (int b = 0; b < bins; b++)
                    if (Mathf.Abs(headY - low + (b + .5f) * bin - hint) <= .25f && counts[b] >= 40 && (best < 0 || counts[b] > counts[best])) best = b;
            if (best < 0)
                for (int b = 0; b < bins && best < 0; b++) if (counts[b] >= Math.Max(40, counts[peak] / 4)) best = b;
            float centre = headY - low + (best + .5f) * bin, sum = 0; int n = 0;
            for (int i = 0; i < scene.Count; i++)
                if (scene.Normals[i].y >= .9f && Mathf.Abs(scene.Points[i].y - centre) <= .05f) { sum += scene.Points[i].y; n++; }
            return n > 0 ? sum / n : centre;
        }

        static bool Wall(Vector3 p, Vector3 n, float floor) => Mathf.Abs(n.y) < .3f && p.y > floor + WallBandLow && p.y < floor + WallBandHigh;

        /// <summary>Dominant wall heading modulo 90° (4θ circular mean) and its strength in [0, 1].</summary>
        public static (float heading, float strength, int walls) WallDirection(Cloud cloud, float floor)
        {
            double c = 0, s = 0; int n = 0;
            for (int i = 0; i < cloud.Count; i++)
            {
                var normal = cloud.Normals[i];
                if (!Wall(cloud.Points[i], normal, floor)) continue;
                double a = 4 * Math.Atan2(normal.x, normal.z); c += Math.Cos(a); s += Math.Sin(a); n++;
            }
            return n == 0 ? (0, 0, 0) : ((float)(Math.Atan2(s, c) / 4), (float)(Math.Sqrt(c * c + s * s) / n), n);
        }

        /// <param name="floorHint">Tracked floor height in world, or NaN.</param>
        /// <remarks>The current alignment is identity here: it is always refined as a candidate and preferred when nearly as good.</remarks>
        public static Result Solve(Cloud twin, Cloud scene, float twinFloor, float headY, float floorHint = float.NaN)
        {
            var result = new Result { Reason = "" };
            if (twin.Count < 100 || scene.Count < 100) { result.Reason = "too few points"; return result; }
            float sceneFloor = EstimateFloor(scene, headY, floorHint);
            bool floorKnown = !float.IsNaN(sceneFloor);
            if (!floorKnown) sceneFloor = headY - 1.6f;
            result.SceneFloor = sceneFloor;
            float dy = twinFloor - sceneFloor;

            var (twinHeading, twinStrength, twinWalls) = WallDirection(twin, twinFloor);
            var (sceneHeading, sceneStrength, sceneWalls) = WallDirection(scene, sceneFloor);
            if (sceneWalls < 200 || twinWalls < 200) { result.Reason = "too little wall surface"; return result; }
            var yaws = new List<float>();
            if (twinStrength > .25f && sceneStrength > .25f)
                for (int k = 0; k < 4; k++) yaws.Add(twinHeading - sceneHeading + k * Mathf.PI / 2);
            else
                for (int k = 0; k < 36; k++) yaws.Add(k * Mathf.PI / 18);

            var grid = new TwinGrid(twin, twinFloor);
            var starts = new List<(float yaw, Vector3 t, float score)>();
            foreach (float yaw in yaws) starts.AddRange(grid.Search(scene, sceneFloor, yaw, dy, TopPerYaw));
            starts.Sort((a, b) => b.score.CompareTo(a.score));
            if (starts.Count == 0) { result.Reason = "no floor-plan overlap"; return result; }

            var index = new SpatialIndex(twin, .25f);
            var sample = Subsample(Structure(scene, sceneFloor), MaxIcpPoints);
            var refined = new List<(float yaw, Vector3 t, float inliers, float rms)>();
            // The current alignment always competes, so a nearly right twin is refined rather than replaced by a look-alike.
            var tries = new List<(float yaw, Vector3 t)> { (0, new Vector3(0, dy, 0)) };
            for (int i = 0; i < Math.Min(IcpCandidates, starts.Count); i++) tries.Add((starts[i].yaw, starts[i].t));
            foreach (var (startYaw, startT) in tries)
            {
                var (yaw, t) = Icp(index, sample, startYaw, startT, !floorKnown);
                var (inliers, rms) = Evaluate(index, sample, yaw, t, sceneFloor);
                refined.Add((yaw, t, inliers, rms));
            }
            result.Candidates = refined.Count; result.All = refined.ToArray();
            float bestScore = 0; foreach (var r in refined) bestScore = Math.Max(bestScore, r.inliers);
            if (bestScore < .2f) { result.Reason = "no candidate matched"; return result; }

            // A fit near the current alignment wins unless a distant one is clearly better: symmetric halls have look-alikes,
            // and the user's rough placement (or a previous alignment) says which one is meant.
            int chosen = -1; float cost = float.MaxValue;
            for (int i = 0; i < refined.Count; i++)
                if (Near(refined[i].yaw, refined[i].t) && refined[i].inliers >= NearShare * bestScore && (chosen < 0 || refined[i].inliers > refined[chosen].inliers)) chosen = i;
            bool kept = chosen >= 0;
            if (!kept)
                // Among near-equal fits, prefer the smallest change from the current alignment.
                for (int i = 0; i < refined.Count; i++)
                {
                    if (refined[i].inliers < .97f * bestScore) continue;
                    float c = Mathf.Abs(Mathf.DeltaAngle(0, refined[i].yaw * Mathf.Rad2Deg)) / 10 + new Vector2(refined[i].t.x, refined[i].t.z).magnitude;
                    if (c < cost) { cost = c; chosen = i; }
                }
            var pick = refined[chosen];
            // Distant look-alikes do not make a kept-near fit ambiguous; other near fits (sliding along a wall) still do.
            float runnerUp = 0;
            foreach (var r in refined)
                if (Distinct(r.yaw, r.t, pick.yaw, pick.t) && (!kept || Near(r.yaw, r.t))) runnerUp = Math.Max(runnerUp, r.inliers);
            result.Found = true; result.Yaw = Mathf.Repeat(pick.yaw + Mathf.PI, 2 * Mathf.PI) - Mathf.PI; result.Translation = pick.t;
            result.Inliers = pick.inliers; result.Rms = pick.rms; result.Ambiguity = runnerUp > 0 ? pick.inliers / runnerUp : 10;
            result.KeptCurrent = kept && pick.inliers < bestScore - 1e-4f;
            result.Constraint = Constraint(index, sample, pick.yaw, pick.t, sceneFloor);
            return result;
        }

        static bool Near(float yaw, Vector3 t) => Mathf.Abs(Mathf.DeltaAngle(0, yaw * Mathf.Rad2Deg)) <= NearYaw * Mathf.Rad2Deg && new Vector2(t.x, t.z).magnitude <= NearDistance;

        static bool Structural(Vector3 p, Vector3 n, float floor) => Mathf.Abs(n.y) < .3f && p.y > floor + WallBandLow;

        /// <summary>Drops low vertical surfaces (furniture, people's legs, boxes); keeps floors and walls above the band.</summary>
        static Cloud Structure(Cloud scene, float floor)
        {
            var p = new List<Vector3>(); var q = new List<Vector3>();
            for (int i = 0; i < scene.Count; i++)
                if (Mathf.Abs(scene.Normals[i].y) >= .3f || scene.Points[i].y > floor + WallBandLow) { p.Add(scene.Points[i]); q.Add(scene.Normals[i]); }
            return new Cloud { Points = p.ToArray(), Normals = q.ToArray() };
        }

        /// <summary>Weaker over stronger eigenvalue of Σ n·nᵀ (horizontal parts) over matched wall points.</summary>
        public static float Constraint(SpatialIndex twin, Cloud scene, float yaw, Vector3 t, float sceneFloor)
        {
            double a = 0, b = 0, c = 0;
            for (int i = 0; i < scene.Count; i++)
            {
                if (!Structural(scene.Points[i], scene.Normals[i], sceneFloor)) continue;
                var p = Rotate(scene.Points[i], yaw) + t;
                if (!twin.Nearest(p, .1f, out var q, out var n) || Mathf.Abs(Vector3.Dot(n, p - q)) >= .05f || Mathf.Abs(n.y) >= .3f) continue;
                a += n.x * n.x; b += n.x * n.z; c += n.z * n.z;
            }
            double mean = (a + c) / 2, spread = Math.Sqrt((a - c) * (a - c) / 4 + b * b);
            return mean + spread > 0 ? (float)((mean - spread) / (mean + spread)) : 0;
        }

        static bool Distinct(float yawA, Vector3 tA, float yawB, Vector3 tB)
            => Mathf.Abs(Mathf.DeltaAngle(yawA * Mathf.Rad2Deg, yawB * Mathf.Rad2Deg)) > 3 || Vector3.Distance(tA, tB) > .5f;

        static Cloud Subsample(Cloud cloud, int max)
        {
            if (cloud.Count <= max) return cloud;
            int step = Mathf.CeilToInt(cloud.Count / (float)max), n = (cloud.Count + step - 1) / step;
            var p = new Vector3[n]; var q = new Vector3[n];
            for (int i = 0, j = 0; i < cloud.Count && j < n; i += step, j++) { p[j] = cloud.Points[i]; q[j] = cloud.Normals[i]; }
            return new Cloud { Points = p, Normals = q };
        }

        /// <summary>Point-to-plane ICP over (yaw, x, y, z); height is held unless the scene floor was not seen.</summary>
        static (float yaw, Vector3 t) Icp(SpatialIndex twin, Cloud scene, float yaw, Vector3 t, bool solveHeight)
        {
            foreach (var (radius, iterations) in new[] { (.5f, 8), (.25f, 8), (.1f, 8) })
                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    var moved = new Vector3[scene.Count]; var centre = Vector3.zero;
                    for (int i = 0; i < scene.Count; i++) { moved[i] = Rotate(scene.Points[i], yaw) + t; centre += moved[i]; }
                    centre /= scene.Count;
                    var a = new double[4, 4]; var b = new double[4]; int used = 0;
                    for (int i = 0; i < scene.Count; i++)
                    {
                        if (!twin.Nearest(moved[i], radius, out var q, out var n)) continue;
                        var rel = moved[i] - centre;
                        double r = Vector3.Dot(n, moved[i] - q);
                        double w = Math.Abs(r) < .03 ? 1 : .03 / Math.Abs(r);
                        // d(moved)/d(yaw) for a rotation about +Y through the centre: (rel.z, 0, -rel.x).
                        var j = new[] { (double)(n.x * rel.z - n.z * rel.x), n.x, solveHeight ? n.y : 0, n.z };
                        for (int u = 0; u < 4; u++) { b[u] -= w * j[u] * r; for (int v = 0; v < 4; v++) a[u, v] += w * j[u] * j[v]; }
                        used++;
                    }
                    if (used < 50) break;
                    if (!solveHeight) { a[2, 2] = 1; b[2] = 0; }
                    // Light damping: a direction no surface constrains (along parallel walls) stays put instead of making the system singular.
                    double trace = a[0, 0] + a[1, 1] + a[2, 2] + a[3, 3];
                    for (int u = 0; u < 4; u++) a[u, u] += 1e-3 * trace / 4 + 1e-9;
                    var x = SolveLinear(a, b); if (x == null) break;
                    float dYaw = (float)x[0]; var dt = new Vector3((float)x[1], (float)x[2], (float)x[3]);
                    yaw += dYaw; t = Rotate(t - centre, dYaw) + centre + dt;
                    if (Math.Abs(dYaw) < 1e-5 && dt.sqrMagnitude < 1e-8f) break;
                }
            return (yaw, t);
        }

        /// <summary>Fraction of scene wall points within 5 cm of a twin surface (10 cm search), and their RMS distance.</summary>
        /// <remarks>Walls above the furniture band only: floor points fit anywhere on a floor and would hide a placement slid along a wall.</remarks>
        public static (float inliers, float rms) Evaluate(SpatialIndex twin, Cloud scene, float yaw, Vector3 t, float sceneFloor)
        {
            int walls = 0, inliers = 0; double sum = 0;
            for (int i = 0; i < scene.Count; i++)
            {
                if (!Structural(scene.Points[i], scene.Normals[i], sceneFloor)) continue;
                walls++;
                var p = Rotate(scene.Points[i], yaw) + t;
                if (!twin.Nearest(p, .1f, out var q, out var n)) continue;
                float r = Mathf.Abs(Vector3.Dot(n, p - q));
                if (r < .05f) { inliers++; sum += r * r; }
            }
            return (walls > 0 ? inliers / (float)walls : 0, inliers > 0 ? (float)Math.Sqrt(sum / inliers) : float.PositiveInfinity);
        }

        static double[] SolveLinear(double[,] a, double[] b)
        {
            int n = b.Length; var m = new double[n, n + 1];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) m[i, j] = a[i, j]; m[i, n] = b[i]; }
            for (int c = 0; c < n; c++)
            {
                int pivot = c; for (int r = c + 1; r < n; r++) if (Math.Abs(m[r, c]) > Math.Abs(m[pivot, c])) pivot = r;
                if (Math.Abs(m[pivot, c]) < 1e-9) return null;
                for (int j = 0; j <= n; j++) (m[c, j], m[pivot, j]) = (m[pivot, j], m[c, j]);
                for (int r = 0; r < n; r++)
                {
                    if (r == c) continue; double f = m[r, c] / m[c, c];
                    for (int j = c; j <= n; j++) m[r, j] -= f * m[c, j];
                }
            }
            var x = new double[n]; for (int i = 0; i < n; i++) x[i] = m[i, n] / m[i, i];
            return x;
        }

        static long Key(int x, int y, int z) => ((long)(x + (1 << 20)) << 42) | ((long)(y + (1 << 20)) << 21) | (long)(z + (1 << 20));

        /// <summary>Hash grid for nearest twin point queries.</summary>
        public sealed class SpatialIndex
        {
            readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
            readonly Cloud cloud; readonly float size;
            public SpatialIndex(Cloud points, float cell)
            {
                cloud = points; size = cell;
                for (int i = 0; i < points.Count; i++)
                {
                    var p = points.Points[i]; long key = Key(Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.y / size), Mathf.FloorToInt(p.z / size));
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<int>();
                    list.Add(i);
                }
            }
            public bool Nearest(Vector3 p, float radius, out Vector3 point, out Vector3 normal)
            {
                int reach = Mathf.CeilToInt(radius / size), cx = Mathf.FloorToInt(p.x / size), cy = Mathf.FloorToInt(p.y / size), cz = Mathf.FloorToInt(p.z / size);
                float best = radius * radius; int found = -1;
                for (int x = -reach; x <= reach; x++) for (int y = -reach; y <= reach; y++) for (int z = -reach; z <= reach; z++)
                {
                    if (!cells.TryGetValue(Key(cx + x, cy + y, cz + z), out var list)) continue;
                    foreach (int i in list) { float d = (cloud.Points[i] - p).sqrMagnitude; if (d < best) { best = d; found = i; } }
                }
                point = found >= 0 ? cloud.Points[found] : default; normal = found >= 0 ? cloud.Normals[found] : default;
                return found >= 0;
            }
        }

        /// <summary>Floor-plan of twin walls (occupied 1, neighbour 0.5) for placing the scene's wall cells.</summary>
        sealed class TwinGrid
        {
            readonly float[,] score; readonly float x0, z0; readonly int width, depth;
            public TwinGrid(Cloud twin, float floor)
            {
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                for (int i = 0; i < twin.Count; i++)
                    if (Wall(twin.Points[i], twin.Normals[i], floor))
                    { var p = twin.Points[i]; minX = Math.Min(minX, p.x); minZ = Math.Min(minZ, p.z); maxX = Math.Max(maxX, p.x); maxZ = Math.Max(maxZ, p.z); }
                x0 = minX - 1; z0 = minZ - 1;
                width = Math.Max(1, Mathf.CeilToInt((maxX - x0 + 1) / GridCell) + 1); depth = Math.Max(1, Mathf.CeilToInt((maxZ - z0 + 1) / GridCell) + 1);
                var occupied = new bool[width, depth];
                for (int i = 0; i < twin.Count; i++)
                    if (Wall(twin.Points[i], twin.Normals[i], floor))
                        occupied[(int)((twin.Points[i].x - x0) / GridCell), (int)((twin.Points[i].z - z0) / GridCell)] = true;
                score = new float[width, depth];
                for (int x = 0; x < width; x++) for (int z = 0; z < depth; z++)
                {
                    if (occupied[x, z]) { score[x, z] = 1; continue; }
                    for (int dx = -1; dx <= 1 && score[x, z] == 0; dx++) for (int dz = -1; dz <= 1; dz++)
                    { int u = x + dx, v = z + dz; if (u >= 0 && v >= 0 && u < width && v < depth && occupied[u, v]) { score[x, z] = .5f; break; } }
                }
            }

            /// <summary>Best non-overlapping placements (1 m suppression) of the rotated scene wall cells.</summary>
            public List<(float yaw, Vector3 t, float score)> Search(Cloud scene, float sceneFloor, float yaw, float dy, int keep)
            {
                var cells = new HashSet<(int, int)>(); int minI = int.MaxValue, minJ = int.MaxValue;
                for (int i = 0; i < scene.Count; i++)
                {
                    if (!Wall(scene.Points[i], scene.Normals[i], sceneFloor)) continue;
                    var p = Rotate(scene.Points[i], yaw);
                    var c = (Mathf.FloorToInt(p.x / GridCell), Mathf.FloorToInt(p.z / GridCell));
                    cells.Add(c); minI = Math.Min(minI, c.Item1); minJ = Math.Min(minJ, c.Item2);
                }
                var found = new List<(float yaw, Vector3 t, float score)>();
                if (cells.Count < 20) return found;
                var offsets = new List<(int, int)>(); int spanI = 0, spanJ = 0;
                foreach (var (i, j) in cells) { offsets.Add((i - minI, j - minJ)); spanI = Math.Max(spanI, i - minI); spanJ = Math.Max(spanJ, j - minJ); }
                int lowU = -spanI / 2, highU = width - spanI / 2, lowV = -spanJ / 2, highV = depth - spanJ / 2;
                var map = new float[highU - lowU + 1, highV - lowV + 1];
                for (int u = lowU; u <= highU; u++) for (int v = lowV; v <= highV; v++)
                {
                    float sum = 0;
                    foreach (var (i, j) in offsets) { int x = u + i, z = v + j; if (x >= 0 && z >= 0 && x < width && z < depth) sum += score[x, z]; }
                    map[u - lowU, v - lowV] = sum / offsets.Count;
                }
                int suppress = Mathf.CeilToInt(1 / GridCell);
                for (int k = 0; k < keep; k++)
                {
                    float best = 0; int bu = 0, bv = 0;
                    for (int u = 0; u < map.GetLength(0); u++) for (int v = 0; v < map.GetLength(1); v++) if (map[u, v] > best) { best = map[u, v]; bu = u; bv = v; }
                    if (best <= 0) break;
                    // Scene cell (minI + i) lands on twin cell (u + i): translation = x0 + u·cell − minI·cell.
                    float tx = x0 + (bu + lowU) * GridCell - minI * GridCell, tz = z0 + (bv + lowV) * GridCell - minJ * GridCell;
                    found.Add((yaw, new Vector3(tx, dy, tz), best));
                    for (int u = Math.Max(0, bu - suppress); u <= Math.Min(map.GetLength(0) - 1, bu + suppress); u++)
                        for (int v = Math.Max(0, bv - suppress); v <= Math.Min(map.GetLength(1) - 1, bv + suppress); v++) map[u, v] = 0;
                }
                return found;
            }
        }
    }
}
