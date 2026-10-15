using System;
using System.Threading;
using GLTFast;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Owns imports until renderers/colliders have been released.</summary>
    public sealed class TileHandle : IDisposable
    {
        public TwinManifest.Tile Data;
        public Bounds LocalBounds;
        public int Lod = -1, Wanted = -1, Failures;
        public bool Loading;
        public float RetryAfter;
        public CancellationTokenSource Cancellation;
        public GltfImport VisualImport, ColliderImport;
        public GameObject Visual, Collision;
        public ArchitecturalMesh[] Architecture;
        public int ResidentTriangles => (Visual ? Data.Count(Lod) : 0) + (Collision ? Data.Count(2) : 0);
        public void ReleaseVisual()
        {
            if (Visual) { Visual.SetActive(false); UnityEngine.Object.Destroy(Visual); }
            VisualImport?.Dispose(); VisualImport = null; Visual = null; Architecture=null; Lod = -1;
        }
        public void Dispose()
        {
            Cancellation?.Cancel();
            ReleaseVisual();
            if (Collision) { Collision.SetActive(false); UnityEngine.Object.Destroy(Collision); }
            ColliderImport?.Dispose(); ColliderImport = null; Collision = null;
        }
    }
}
