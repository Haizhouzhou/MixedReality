using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwinInspector
{
    /// <summary>Distance/frustum-prioritized streaming with reserved triangle budgets.</summary>
    public sealed class TwinLoader : MonoBehaviour
    {
        readonly List<TileHandle> tiles = new List<TileHandle>();
        readonly List<Candidate> candidates = new List<Candidate>();
        readonly Plane[] planes = new Plane[6];
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        Settings settings;
        TwinRoot root;
        Camera viewer;
        Material material;
        Material softMaterial,edgeMaterial;
        bool lens;
        bool technical=true;
        Transform visuals, collisions;
        float nextPlan;
        int activeLoads, reserved;
        bool initialized;
        public TwinManifest Manifest { get; private set; }
        public string DataRoot { get; private set; }
        public int PendingLoads => activeLoads;
        public int LoadedTiles { get { int count = 0; foreach (var tile in tiles) if (tile.Visual) count++; return count; } }
        public int Triangles { get { int count = 0; foreach (var tile in tiles) count += tile.ResidentTriangles; return count; } }
        public int Failures { get { int count = 0; foreach (var tile in tiles) if (tile.Failures > 0) count++; return count; } }
        struct Candidate { public TileHandle Tile; public int Lod; public float Priority; }

        public async Task InitializeAsync(Settings config, TwinRoot twin, Camera camera, Material sharedMaterial)
        {
            settings = config; root = twin; viewer = camera; material = sharedMaterial;
            softMaterial=new Material(sharedMaterial) { name="Twin Lens soft shell" }; softMaterial.SetFloat("_SoftReveal",1); softMaterial.SetFloat("_ZWrite",0);
            edgeMaterial=Resources.Load<DesignTokens>("DesignTokens").EdgeMaterial;
            DataRoot = TwinPaths.Resolve(config);
            Manifest = JsonConvert.DeserializeObject<TwinManifest>(await TwinPaths.ReadTextAsync(DataRoot, "manifest.json", lifetime.Token));
            Manifest.Validate(config.Scan);
            root.Initialize(Manifest.origin_world);
            visuals = new GameObject("Twin visuals").transform; visuals.SetParent(root.transform, false);
            collisions = new GameObject("LOD2 inspection surfaces").transform; collisions.SetParent(root.transform, false);
            foreach (var data in Manifest.tiles) tiles.Add(new TileHandle { Data = data, LocalBounds = root.LocalBounds(data) });
            initialized = true;
            DevLog.Write($"Manifest {Manifest.region.name}: {tiles.Count} tiles, demo={Manifest.demo}");
        }

        public void SetVisible(bool value) { if (visuals) visuals.gameObject.SetActive(value); }
        public void SetTechnical(bool value)
        {
            if(technical==value) return; technical=value;
            foreach(var tile in tiles) if(tile.Architecture!=null) foreach(var cached in tile.Architecture) if(cached) cached.SetVisible(value);
        }
        public void SetLens(bool value)
        {
            if(lens==value) return;
            lens=value;
            foreach(var tile in tiles) if(tile.Visual)
                foreach(var soft in tile.Visual.GetComponentsInChildren<LensShell>(true)) soft.gameObject.SetActive(value);
        }

        void Update()
        {
            if (!initialized || Time.unscaledTime < nextPlan) return;
            nextPlan = Time.unscaledTime + .35f;
            GeometryUtility.CalculateFrustumPlanes(viewer, planes);
            var localCamera = root.transform.InverseTransformPoint(viewer.transform.position);
            candidates.Clear();
            foreach (var tile in tiles)
            {
                tile.Wanted = -1;
                float distance = Vector3.Distance(tile.LocalBounds.ClosestPoint(localCamera), localCamera);
                if (distance > settings.LodDistances[2]) continue;
                int lod = tile.Data.Available(distance < settings.LodDistances[0] ? 0 : distance < settings.LodDistances[1] ? 1 : 2);
                var worldBounds = TransformBounds(tile.LocalBounds);
                candidates.Add(new Candidate { Tile = tile, Lod = lod, Priority = distance + (GeometryUtility.TestPlanesAABB(planes, worldBounds) ? 0 : 40) });
            }
            candidates.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            int planned = 0;
            foreach (var candidate in candidates)
            {
                int lod = candidate.Lod;
                while (lod >= 0 && planned + candidate.Tile.Data.Count(lod) + candidate.Tile.Data.Count(2) > settings.MaxTriangles)
                    lod = candidate.Tile.Data.Available(lod + 1);
                if (lod < 0) continue;
                candidate.Tile.Wanted = lod;
                planned += candidate.Tile.Data.Count(lod) + candidate.Tile.Data.Count(2);
            }
            foreach (var tile in tiles)
            {
                if (tile.Wanted < 0) tile.Dispose();
                else if (tile.Loading && tile.Wanted != tile.Lod) { /* Re-evaluate after the bounded current request. */ }
            }
            foreach (var candidate in candidates)
            {
                var tile = candidate.Tile;
                if (activeLoads >= settings.LoadConcurrency) break;
                if (tile.Wanted < 0 || tile.Loading || tile.Lod == tile.Wanted || Time.unscaledTime < tile.RetryAfter) continue;
                int need = tile.Data.Count(tile.Wanted) + (tile.Collision ? 0 : tile.Data.Count(2));
                if (Triangles + reserved + need > settings.MaxTriangles) tile.ReleaseVisual();
                if (Triangles + reserved + need > settings.MaxTriangles) continue;
                _ = LoadTileAsync(tile, tile.Wanted, need);
            }
        }

        Bounds TransformBounds(Bounds local)
        {
            var e = local.extents;
            var x = root.transform.TransformVector(e.x, 0, 0);
            var y = root.transform.TransformVector(0, e.y, 0);
            var z = root.transform.TransformVector(0, 0, e.z);
            var extent = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x), Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(root.transform.TransformPoint(local.center), extent * 2);
        }

        async Task LoadTileAsync(TileHandle tile, int lod, int budget)
        {
            tile.Loading = true; activeLoads++; reserved += budget;
            tile.Cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            var token = tile.Cancellation.Token;
            GltfImport visualImport = null, colliderImport = null;
            GameObject visual = null, collision = null;
            try
            {
                if (!tile.Collision)
                {
                    colliderImport = new GltfImport(materialGenerator:new TwinMaterialGenerator(material));
                    collision = await ImportAsync(colliderImport, tile, 2, collisions, token);
                    foreach (var renderer in collision.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    foreach (var filter in collision.GetComponentsInChildren<MeshFilter>())
                    {
                        var collider = filter.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = filter.sharedMesh;
                        filter.gameObject.layer = 8;
                        filter.sharedMesh.UploadMeshData(true);
                        await Task.Yield();
                        token.ThrowIfCancellationRequested();
                    }
                }
                visualImport = new GltfImport(materialGenerator:new TwinMaterialGenerator(material));
                visual = await ImportAsync(visualImport, tile, lod, visuals, token);
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
                foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
                {
                    ArchitecturalMesh.Prepare(filter,edgeMaterial);
                    var shell=new GameObject("Lens soft shell",typeof(MeshFilter),typeof(MeshRenderer),typeof(LensShell)); shell.transform.SetParent(filter.transform,false);
                    shell.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                    var shellRenderer=shell.GetComponent<MeshRenderer>(); shellRenderer.sharedMaterial=softMaterial; shellRenderer.shadowCastingMode=ShadowCastingMode.Off; shellRenderer.receiveShadows=false;
                    shell.SetActive(lens); filter.sharedMesh.UploadMeshData(true);
                    await Task.Yield(); token.ThrowIfCancellationRequested();
                }
                token.ThrowIfCancellationRequested();
                if (tile.Wanted < 0) throw new OperationCanceledException();
                tile.ReleaseVisual();
                tile.Visual = visual; tile.VisualImport = visualImport; tile.Lod = lod;
                tile.Architecture=visual.GetComponentsInChildren<ArchitecturalMesh>(true);
                foreach(var cached in tile.Architecture) cached.SetVisible(technical);
                visual.SetActive(true); visual = null; visualImport = null;
                if (collision) { tile.Collision = collision; tile.ColliderImport = colliderImport; collision.SetActive(true); collision = null; colliderImport = null; }
                tile.Failures = 0;
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                tile.Failures++; tile.RetryAfter = Time.unscaledTime + Mathf.Min(60, 2 << Mathf.Min(tile.Failures, 5));
                DevLog.Write("Tile " + tile.Data.id + " failed: " + error.Message);
            }
            finally
            {
                if (visual) Destroy(visual); if (collision) Destroy(collision);
                visualImport?.Dispose(); colliderImport?.Dispose();
                tile.Cancellation.Dispose(); tile.Cancellation = null;
                tile.Loading = false; activeLoads--; reserved -= budget;
            }
        }

        async Task<GameObject> ImportAsync(GltfImport importer, TileHandle tile, int lod, Transform parent, CancellationToken token)
        {
            var container = new GameObject(tile.Data.id + " LOD" + lod);
            container.SetActive(false); container.transform.SetParent(parent, false);
            // glTFast already reflected X. This single proper rotation produces (x,z,y).
            container.transform.localRotation = Quaternion.Euler(0, 180, 0);
            try
            {
                if (!await importer.Load(TwinPaths.Url(DataRoot, tile.Data.files[lod.ToString()]), cancellationToken: token)) throw new Exception("GLB load rejected");
                if (!await importer.InstantiateMainSceneAsync(container.transform, token)) throw new Exception("GLB scene rejected");
                return container;
            }
            catch { Destroy(container); throw; }
        }

        void OnDestroy()
        {
            lifetime.Cancel();
            foreach (var tile in tiles) tile.Dispose();
            lifetime.Dispose();
            if(softMaterial) Destroy(softMaterial);
        }
    }
}
