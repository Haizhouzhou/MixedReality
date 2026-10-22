using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Transactional annotation edits in the original Z-up twin frame.</summary>
    public sealed class AnnotationStore
    {
        readonly string path;
        readonly AnnotationDocument document;
        public IReadOnlyList<Annotation> Items => document.items;
        public Annotation Selected { get; private set; }
        public event Action Changed;

        public AnnotationStore(string scan, string filePath = null)
        {
            path = filePath ?? TwinPaths.SaveFile("annotations", scan);
            document = JsonStore.Read<AnnotationDocument>(path) ?? new AnnotationDocument { scan = scan };
            if ((document.schema != "twin-annotations/1" && document.schema != "twin-annotations/2") || document.scan != scan) throw new InvalidDataException("Annotation scan mismatch");
            foreach (var item in document.items) document.next_index = Math.Max(document.next_index, item.index + 1);
        }
        public Annotation Add(Vector3 position, Vector3 normal, Vector3 devicePosition, float[] deviceRotation)
            => Add(position, normal, devicePosition, deviceRotation, AnnotationType.Note, null);
        public Annotation Add(Vector3 position, Vector3 normal, Vector3 devicePosition, float[] deviceRotation, AnnotationType type, AnnotationRegion region)
        {
            int number = document.next_index++;
            var item = new Annotation { id = "a-" + number.ToString("D6"), index = number, type = "note", label = "Note", position_twin = TwinRoot.Array(position), normal_twin = TwinRoot.Array(normal.normalized), created_utc = DateTime.UtcNow.ToString("O"), device_pose_twin = new Annotation.DevicePose { position = TwinRoot.Array(devicePosition), rotation_quat_xyzw = deviceRotation } };
            item.type = type.ToString().ToLowerInvariant(); item.label = type.ToString(); item.region = region;
            document.items.Add(item); Selected = item; Save(); return item;
        }
        public void SetRegion(Annotation item, AnnotationRegion region)
        { if (item == null || !document.items.Contains(item)) return; item.region = region; Save(); }
        public void SetRegion(string id, AnnotationRegion region) => SetRegion(document.items.Find(item => item.id == id), region);
        public void Select(string id) { Selected = document.items.Find(item => item.id == id); Changed?.Invoke(); }
        public void Retype(AnnotationType type)
        {
            if (Selected == null) return;
            Selected.type = type.ToString().ToLowerInvariant(); Selected.label = type.ToString(); Save();
        }
        public void DeleteSelected()
        { if (Selected == null) return; document.items.Remove(Selected); Selected = null; Save(); }
        public AnnotationDocument Snapshot()
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<AnnotationDocument>(Newtonsoft.Json.JsonConvert.SerializeObject(document));
        }
        void Save() { document.schema = "twin-annotations/2"; JsonStore.Write(path, document); Changed?.Invoke(); }
    }
}
