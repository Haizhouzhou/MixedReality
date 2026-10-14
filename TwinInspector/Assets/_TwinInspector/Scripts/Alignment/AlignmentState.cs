using System;

namespace TwinInspector
{
    /// <summary>JSON fallback pose plus Android persistent-anchor identifier.</summary>
    [Serializable] public sealed class AlignmentState
    {
        public string schema = "twin-alignment/1";
        public string scan, anchor_id, updated_utc;
        public float[] position_world, rotation_world_xyzw;
    }
}
