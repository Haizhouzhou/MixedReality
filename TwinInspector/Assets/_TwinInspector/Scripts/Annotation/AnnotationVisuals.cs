using System.Collections.Generic;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Annotation visuals follow TwinRoot so re-alignment never changes saved coordinates.</summary>
    public sealed class AnnotationVisuals : MonoBehaviour
    {
        readonly Dictionary<string, AnnotationMarker> markers = new Dictionary<string, AnnotationMarker>();
        readonly List<string> removed = new List<string>();
        AnnotationStore store; TwinRoot root; DesignTokens tokens;
        public void Initialize(AnnotationStore annotations, TwinRoot twin, DesignTokens design)
        { store = annotations; root = twin; tokens = design; store.Changed += Refresh; Refresh(); }
        void Refresh()
        {
            removed.Clear(); removed.AddRange(markers.Keys);
            foreach (var item in store.Items)
            {
                if (!markers.TryGetValue(item.id, out var marker))
                {
                    marker = Instantiate(tokens.AnnotationPrefab, root.transform).GetComponent<AnnotationMarker>();
                    markers.Add(item.id, marker);
                }
                marker.UpdateData(item, store.Selected == item, root, tokens); removed.Remove(item.id);
            }
            foreach (var id in removed) { Destroy(markers[id].gameObject); markers.Remove(id); }
        }
        void OnDestroy() { if (store != null) store.Changed -= Refresh; }
    }
}
