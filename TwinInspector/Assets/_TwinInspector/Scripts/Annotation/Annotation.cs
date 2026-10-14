using System;
using System.Collections.Generic;

namespace TwinInspector
{
    public enum AnnotationType { Missing, New, Changed, Note }
    [Serializable] public sealed class Annotation
    {
        public string id, type, label, created_utc, screenshot = "", comment = "";
        public int index;
        public float[] position_twin, normal_twin;
        public DevicePose device_pose_twin;
        [Serializable] public sealed class DevicePose { public float[] position, rotation_quat_xyzw; }
        public AnnotationType Kind => Enum.TryParse(type, true, out AnnotationType value) ? value : AnnotationType.Note;
    }
    [Serializable] public sealed class AnnotationDocument
    {
        public string schema = "twin-annotations/1", scan, exported_utc;
        public int next_index = 1;
        public List<Annotation> items = new List<Annotation>();
    }
}
