using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace TwinInspector
{
    public enum AutoAlignState { Idle, Scanning, Matching, Review, Failed }

    /// <summary>Scan, match and confirm: registers the headset scene mesh near the viewer to the space's LOD2 twin,
    /// moves the twin for review, and saves only on confirmation.</summary>
    public sealed class AutoAlignment : MonoBehaviour
    {
        /// <summary>Scanned surface needed before matching, in m²: walls carry yaw and position, the floor carries height, and
        /// walls across the main direction fix the position along it (parallel walls alone let the scan slide).</summary>
        public const float WallsNeeded = 6, FloorNeeded = 2, CrossNeeded = 1.5f;
        const float ScanRadius = 10, CropRadius = 20, TwinSpacing = .1f, SceneSpacing = .05f;
        const int DirectionBins = 18; // 10° bins of wall direction modulo 180°
        AppBootstrap app;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        sealed class ChunkCoverage { public int Version; public float Walls, Floor; public readonly float[] Directions = new float[DirectionBins]; }
        readonly Dictionary<string, ChunkCoverage> coverage = new Dictionary<string, ChunkCoverage>();
        readonly Dictionary<string, GameObject> scanVisuals = new Dictionary<string, GameObject>();
        Material scanMaterial;
        bool scanView;
        // Floor height for the furniture band while scanning: the tracked floor, or 1.6 m below the head at the start.
        float floorReference;
        Task<AutoAligner.Cloud> twinLocal;
        // Chunk versions at the last scan reset: those chunks count again only once the system updates them.
        Dictionary<string, int> baseline;
        Pose before;
        int attempt;
        float nextCoverage, scanStarted;
        bool noMeshLogged;
        public AutoAlignState State { get; private set; }
        public bool Active => State == AutoAlignState.Scanning || State == AutoAlignState.Matching || State == AutoAlignState.Review;
        public float Walls { get; private set; }
        public float Floor { get; private set; }
        /// <summary>Wall area within ±35° of perpendicular to the main wall direction.</summary>
        public float Cross { get; private set; }
        /// <summary>The headset has delivered no scene mesh at all several seconds into a scan (seen after the system perception service restarted).</summary>
        public bool NoMesh => State == AutoAlignState.Scanning && Time.unscaledTime - scanStarted > 8 && (app.SceneSource?.Chunks.Count ?? 0) == 0;
        public bool CanMatch => State == AutoAlignState.Scanning && Walls >= WallsNeeded && Floor >= FloorNeeded && Cross >= CrossNeeded;
        public AutoAligner.Result Result { get; private set; }
        /// <summary>Horizontal distance the twin moved at the viewer, and its turn in degrees.</summary>
        public float Moved { get; private set; }
        public float Turned { get; private set; }
        /// <summary>String key for the failure shown on the page.</summary>
        public string Failure { get; private set; }
        public event Action Changed;

        public void Initialize(AppBootstrap application) => app = application;

        /// <param name="fresh">False keeps a scan reset (Rescan); true counts the whole scene mesh again.</param>
        public void Begin(bool fresh = true)
        {
            if (State == AutoAlignState.Review) Revert();
            if (fresh) baseline = null;
            floorReference = TrackingSpace.FloorHeight(app.Origin);
            if (float.IsNaN(floorReference)) floorReference = app.Viewer.transform.position.y - 1.6f;
            coverage.Clear(); Walls = Floor = Cross = 0; scanStarted = Time.unscaledTime; noMeshLogged = false;
            attempt++; State = AutoAlignState.Scanning; Failure = null; nextCoverage = 0;
            // The twin samples load while the viewer scans.
            if (twinLocal == null || twinLocal.IsFaulted || twinLocal.IsCanceled) twinLocal = LoadTwinAsync(lifetime.Token);
            Changed?.Invoke();
        }

        /// <summary>Leaves the flow; a twin moved for review goes back to where it was.</summary>
        public void Cancel()
        {
            if (State == AutoAlignState.Idle) return;
            if (State == AutoAlignState.Review) Revert();
            attempt++; State = AutoAlignState.Idle; Changed?.Invoke();
        }

        /// <summary>Discards what was scanned so far, so a confusing earlier scan (wrong spot, drift) cannot mix into the match.</summary>
        public void ResetScan()
        {
            if (State != AutoAlignState.Scanning) return;
            baseline = new Dictionary<string, int>();
            foreach (var chunk in app.SceneSource?.Chunks ?? (IReadOnlyList<SurfaceChunk>)Array.Empty<SurfaceChunk>()) baseline[chunk.Id] = chunk.Version;
            Walls = Floor = Cross = 0; nextCoverage = Time.unscaledTime + .5f;
            foreach (var visual in scanVisuals.Values) if (visual) Destroy(visual);
            scanVisuals.Clear();
            DevLog.Write("Auto alignment scan reset: " + baseline.Count + " chunks set aside");
            Changed?.Invoke();
        }
        bool Counts(SurfaceChunk chunk) => baseline == null || !baseline.TryGetValue(chunk.Id, out int version) || chunk.Version != version;

        public async Task ConfirmAsync()
        {
            if (State != AutoAlignState.Review) return;
            State = AutoAlignState.Idle;
            await app.Anchors.SaveAsync();
            DevLog.Write("Auto alignment confirmed: " + app.Anchors.Status);
            Changed?.Invoke();
        }

        public async void Match()
        {
            if (!CanMatch) return;
            int run = ++attempt; State = AutoAlignState.Matching; Changed?.Invoke();
            try
            {
                var started = Time.realtimeSinceStartup;
                var head = app.Viewer.transform.position;
                // Chunk arrays are replaced, never edited, on update, so the snapshot can be read on a worker thread.
                var chunks = new List<SurfaceChunk>();
                foreach (var chunk in app.SceneSource.Chunks) if (Counts(chunk)) chunks.Add(chunk);
                var twin = await twinLocal;
                if (run != attempt) return;
                var rootMatrix = app.Root.transform.localToWorldMatrix;
                float twinFloor = app.Root.TwinToWorld(new Vector3(0, 0, app.Loader.Manifest.datum_z)).y, floorHint = TrackingSpace.FloorHeight(app.Origin);
                var result = await Task.Run(() => Solve(twin, chunks, rootMatrix, twinFloor, head, floorHint), lifetime.Token);
                if (run != attempt || !this) return;
                Result = result;
                DevLog.Write($"Auto alignment: found={result.Found} reliable={result.Reliable} yaw={result.Yaw * Mathf.Rad2Deg:F2}° t={result.Translation:F3} inliers={result.Inliers:P0} rms={result.Rms * 100:F1}cm ambiguity={result.Ambiguity:F2} constraint={result.Constraint:F2} keptCurrent={result.KeptCurrent} floor={result.SceneFloor:F2} floorHint={floorHint:F2} candidates={result.Candidates} reason='{result.Reason}' seconds={Time.realtimeSinceStartup - started:F1}");
                if (result.All != null) foreach (var c in result.All) DevLog.Write($"  candidate yaw={c.yaw * Mathf.Rad2Deg:F2}° t={c.t:F2} inliers={c.inliers:P0} rms={c.rms * 100:F1}cm");
                if (!result.Found) { Failure = FailureKey(result.Reason); State = AutoAlignState.Failed; Changed?.Invoke(); return; }
                Apply(result, head);
                State = AutoAlignState.Review; Changed?.Invoke();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                DevLog.Write("Auto alignment failed: " + error);
                if (run == attempt && this) { Failure = "text.autoAlign.failedError"; State = AutoAlignState.Failed; Changed?.Invoke(); }
            }
        }

        /// <summary>Tries the twin near its current placement first (fast, and keeps a nearly right alignment); the whole twin only if that is not reliable.</summary>
        /// <remarks>Both clouds are solved relative to the viewer, so "smallest change" and ambiguity distances are measured where the viewer stands.</remarks>
        static AutoAligner.Result Solve(AutoAligner.Cloud twinLocal, List<SurfaceChunk> chunks, Matrix4x4 root, float twinFloor, Vector3 head, float floorHint)
        {
            var h = new Vector3(head.x, 0, head.z);
            var sceneSamples = new AutoAligner.Accumulator();
            bool Near(Vector3 p, float radius) { float x = p.x, z = p.z; return x * x + z * z < radius * radius; }
            var toViewer = Matrix4x4.Translate(-h);
            foreach (var chunk in chunks)
                AutoAligner.SampleSurface(sceneSamples, chunk.Vertices, chunk.Normals, chunk.Triangles, toViewer, SceneSpacing, p => Near(p, ScanRadius));
            var scene = sceneSamples.ToCloud();
            var whole = new AutoAligner.Cloud { Points = new Vector3[twinLocal.Count], Normals = new Vector3[twinLocal.Count] };
            var nearPoints = new List<Vector3>(); var nearNormals = new List<Vector3>();
            for (int i = 0; i < twinLocal.Count; i++)
            {
                var p = whole.Points[i] = root.MultiplyPoint3x4(twinLocal.Points[i]) - h;
                var n = whole.Normals[i] = root.MultiplyVector(twinLocal.Normals[i]);
                if (Near(p, CropRadius)) { nearPoints.Add(p); nearNormals.Add(n); }
            }
            var near = new AutoAligner.Cloud { Points = nearPoints.ToArray(), Normals = nearNormals.ToArray() };
            var local = near.Count > 0 ? AutoAligner.Solve(near, scene, twinFloor, head.y, floorHint) : new AutoAligner.Result { Reason = "too few points" };
            var pick = local;
            if (!local.Reliable && near.Count < whole.Count)
            {
                var global = AutoAligner.Solve(whole, scene, twinFloor, head.y, floorHint);
                if (global.Reliable || !local.Found || global.Found && global.Inliers > local.Inliers * 1.05f) pick = global;
            }
            // Back to world: R(p − h) + t + h = R·p + (t + h − R·h).
            Vector3 World(Vector3 t, float yaw) => t + h - AutoAligner.Rotate(h, yaw);
            pick.Translation = World(pick.Translation, pick.Yaw);
            if (pick.All != null) for (int i = 0; i < pick.All.Length; i++) pick.All[i].t = World(pick.All[i].t, pick.All[i].yaw);
            return pick;
        }

        void Apply(AutoAligner.Result result, Vector3 head)
        {
            var root = app.Root.transform;
            before = new Pose(root.position, root.rotation);
            app.Anchors.Detach();
            // The result maps scene to twin, so the twin takes the inverse.
            root.SetPositionAndRotation(AutoAligner.Rotate(before.position - result.Translation, -result.Yaw), Quaternion.Euler(0, -result.Yaw * Mathf.Rad2Deg, 0) * before.rotation);
            var shift = AutoAligner.Rotate(head - result.Translation, -result.Yaw) - head;
            Moved = new Vector2(shift.x, shift.z).magnitude; Turned = Mathf.Abs(result.Yaw * Mathf.Rad2Deg);
        }

        void Revert() => app.Root.transform.SetPositionAndRotation(before.position, before.rotation);

        static string FailureKey(string reason)
            => reason == null ? "text.autoAlign.failedMatch"
             : reason.Contains("wall") ? "text.autoAlign.failedWalls"
             : reason.Contains("few") ? "text.autoAlign.failedPoints"
             : "text.autoAlign.failedMatch";

        /// <summary>LOD2 of every tile, sampled into the twin root's frame (Y-up metres), loaded once per space.</summary>
        async Task<AutoAligner.Cloud> LoadTwinAsync(CancellationToken token)
        {
            var started = Time.realtimeSinceStartup;
            var samples = new AutoAligner.Accumulator(TwinSpacing);
            foreach (var tile in app.Loader.Manifest.tiles)
            {
                token.ThrowIfCancellationRequested();
                if (tile.files == null || !tile.files.TryGetValue("2", out var file)) continue;
                var meshes = new List<(Vector3[] v, Vector3[] n, int[] t, Matrix4x4 m)>();
                using (var importer = new GltfImport())
                {
                    // Same container rotation as TwinLoader, at the origin, so node matrices land in the root's frame.
                    var container = new GameObject("Auto alignment import"); container.SetActive(false);
                    container.transform.rotation = Quaternion.Euler(0, 180, 0);
                    try
                    {
                        if (!await importer.Load(TwinPaths.Url(app.Loader.DataRoot, file), cancellationToken: token) || !await importer.InstantiateMainSceneAsync(container.transform, token))
                        { DevLog.Write("Auto alignment: tile " + tile.id + " LOD2 did not load"); continue; }
                        foreach (var filter in container.GetComponentsInChildren<MeshFilter>(true))
                        {
                            var mesh = filter.sharedMesh; if (!mesh || !mesh.isReadable) continue;
                            meshes.Add((mesh.vertices, mesh.normals, mesh.triangles, filter.transform.localToWorldMatrix));
                        }
                    }
                    finally { Destroy(container); }
                }
                await Task.Run(() => { foreach (var m in meshes) AutoAligner.SampleSurface(samples, m.v, m.n, m.t, m.m, TwinSpacing); }, token);
            }
            var cloud = samples.ToCloud();
            DevLog.Write($"Auto alignment twin: {cloud.Count} samples in {Time.realtimeSinceStartup - started:F1} s");
            return cloud;
        }

        void Update()
        {
            // While scanning the viewer sees the room and the scanned surface, not the twin.
            bool scanning = State == AutoAlignState.Scanning || State == AutoAlignState.Matching;
            if (scanning != scanView) { scanView = scanning; if (app.Style) app.Style.SetScanView(scanning); if (!scanning) HideScan(); }
            if (State != AutoAlignState.Scanning || Time.unscaledTime < nextCoverage) return;
            nextCoverage = Time.unscaledTime + .5f;
            bool could = CanMatch, empty = NoMesh;
            Measure();
            if (NoMesh && !noMeshLogged) { noMeshLogged = true; DevLog.Write("Auto alignment: no scene mesh after 8 s · " + TrackingSpace.Describe()); }
            if (CanMatch != could || NoMesh != empty) Changed?.Invoke();
        }

        /// <summary>Wall (above the furniture band), floor and cross-wall area seen so far, cached per chunk version;
        /// also keeps one overlay per counted chunk.</summary>
        void Measure()
        {
            float walls = 0, floor = 0; var directions = new float[DirectionBins];
            var chunks = app.SceneSource?.Chunks;
            if (chunks != null)
                foreach (var chunk in chunks)
                {
                    if (!Counts(chunk)) continue;
                    if (!coverage.TryGetValue(chunk.Id, out var c) || c.Version != chunk.Version)
                    {
                        coverage[chunk.Id] = c = new ChunkCoverage { Version = chunk.Version };
                        var v = chunk.Vertices; var t = chunk.Triangles;
                        for (int i = 0; i + 2 < t.Length; i += 3)
                        {
                            var cross = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                            float area = cross.magnitude * .5f; if (area <= 0) continue;
                            float up = Mathf.Abs(cross.y) / (2 * area), height = (v[t[i]].y + v[t[i + 1]].y + v[t[i + 2]].y) / 3 - floorReference;
                            if (up < .3f && height > AutoAligner.WallBandLow)
                            {
                                c.Walls += area;
                                float heading = Mathf.Repeat(Mathf.Atan2(cross.x, cross.z) * Mathf.Rad2Deg, 180);
                                c.Directions[Mathf.Min(DirectionBins - 1, (int)(heading / (180f / DirectionBins)))] += area;
                            }
                            else if (up > .9f && Mathf.Abs(height) < .3f) c.Floor += area;
                        }
                    }
                    walls += c.Walls; floor += c.Floor;
                    for (int b = 0; b < DirectionBins; b++) directions[b] += c.Directions[b];
                    ShowScan(chunk);
                }
            Walls = walls; Floor = floor; Cross = CrossArea(directions);
        }

        /// <summary>Area in the bins 55°–125° away from the strongest ±10° direction.</summary>
        static float CrossArea(float[] bins)
        {
            int n = bins.Length, main = 0; float best = -1;
            for (int b = 0; b < n; b++) { float s = bins[(b + n - 1) % n] + bins[b] + bins[(b + 1) % n]; if (s > best) { best = s; main = b; } }
            float cross = 0;
            for (int k = -3; k <= 3; k++) cross += bins[((main + n / 2 + k) % n + n) % n];
            return cross;
        }

        void ShowScan(SurfaceChunk chunk)
        {
            if (!chunk.Filter) return;
            if (!scanMaterial) { scanMaterial = new Material(Resources.Load<Material>("ScanSurface")); }
            scanMaterial.SetFloat("_ScanFloor", floorReference);
            if (!scanVisuals.TryGetValue(chunk.Id, out var visual) || !visual)
            {
                visual = new GameObject("Scanned surface", typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(chunk.Filter.transform, false);
                var renderer = visual.GetComponent<MeshRenderer>(); renderer.sharedMaterial = scanMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                scanVisuals[chunk.Id] = visual;
            }
            visual.GetComponent<MeshFilter>().sharedMesh = chunk.Filter.sharedMesh;
        }

        void HideScan()
        {
            foreach (var visual in scanVisuals.Values) if (visual) Destroy(visual);
            scanVisuals.Clear();
        }

        void OnDestroy()
        {
            lifetime.Cancel(); lifetime.Dispose(); HideScan();
            if (scanMaterial) Destroy(scanMaterial);
            if (scanView && app && app.Style) app.Style.SetScanView(false);
        }
    }
}
