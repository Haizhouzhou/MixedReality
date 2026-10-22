using System;
using UnityEngine.Scripting;

namespace TwinInspector
{
    /// <summary>Optional Z-up region. Triangle indices address the original LOD2 collision mesh.</summary>
    [Serializable, Preserve] public sealed class AnnotationRegion
    {
        public string kind = "patch", source;
        public float[][] hull_twin, paired_new_hull_twin;
        public float[] centroid_twin, normal_twin, extent_m;
        public float area_m2;
        public TriangleReference triangles;
        // An object can cross tile boundaries; keep the primary field compatible with /2.
        public TriangleReference[] additional_triangles;
        [Serializable, Preserve] public sealed class TriangleReference
        {
            public string tile;
            public int lod = 2;
            public int[] indices;
        }
    }
}
