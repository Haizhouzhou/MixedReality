using System.Collections.Generic;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Annotation visuals follow TwinRoot so re-alignment never changes saved coordinates.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class AnnotationVisuals : MonoBehaviour
    {
        readonly Dictionary<string, AnnotationMarker> markers = new Dictionary<string, AnnotationMarker>();
        readonly List<string> removed = new List<string>();
        AnnotationStore store; TwinRoot root; DesignTokens tokens;
        AnnotationMarker hovered;
        public AnnotationMarker Hovered => hovered;
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
        void LateUpdate()
        {
            var app=AppBootstrap.Instance; if(!app || !app.Ready) return;
            AnnotationMarker best=null; float bestScore=float.MaxValue;
            foreach(var marker in markers.Values)
            {
                marker.Present(app.Viewer);
                if(!app.Input.Valid || app.Input.OverUI || app.Manual.Active || app.Roam.Moving) continue;
                var delta=marker.AnchorPosition-app.Input.Aim.origin; float distance=delta.magnitude;
                if(Vector3.Dot(delta,app.Input.Aim.direction)<=0) continue;
                float angle=Vector3.Angle(delta,app.Input.Aim.direction);
                float radius=Mathf.Atan2(marker.SelectionDiameter*.5f,distance)*Mathf.Rad2Deg;
                if(angle>radius) continue;
                if(Physics.Raycast(app.Input.Aim.origin,delta.normalized,out var obstruction,distance-.025f,InputRouter.BuildingMask,QueryTriggerInteraction.Ignore)) continue;
                float score=angle-(marker==hovered?.12f:0);
                if(score<bestScore-.0001f || Mathf.Abs(score-bestScore)<.0001f && (best==null || string.CompareOrdinal(marker.Id,best.Id)<0)) { best=marker; bestScore=score; }
            }
            if(hovered!=best)
            { if(hovered) hovered.Hover(false); hovered=best; if(hovered) { hovered.Hover(true); app.Feedback.Hover(); } }
            if(hovered && !app.Placer.Active && app.Input.ConsumePress()) { store.Select(hovered.Id); app.Feedback.Confirm(); app.UI.Show("picker"); }
        }
    }
}
