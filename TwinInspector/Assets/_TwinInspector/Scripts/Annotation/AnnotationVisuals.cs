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
        public Candidate HoveredCandidate {get;private set;}
        public AnnotationMarker Hovered => HoveredCandidate==null?hovered:null;
        public void Initialize(AnnotationStore annotations, TwinRoot twin, DesignTokens design)
        { store = annotations; root = twin; tokens = design; store.Changed += Refresh; L.Changed+=Refresh; Refresh(); }
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
        void OnDestroy() { if (store != null) store.Changed -= Refresh; L.Changed-=Refresh; }
        void LateUpdate()
        {
            var app=AppBootstrap.Instance; if(!app || !app.Ready) return;
            AnnotationMarker best=null; Candidate bestCandidate=null; float bestScore=float.MaxValue;
            foreach(var marker in markers.Values)
            {
                bool show=app.Style.PlacementVisible(marker.AnchorPosition);marker.gameObject.SetActive(show);if(!show)continue;
                marker.Present(app.Viewer);
                Rank(marker,null);
            }
            var candidates=app.GetComponent<CandidateVisuals>();
            if(candidates)foreach(var item in candidates.Items)if(app.Diff.Visible&&!app.Diff.Paused&&app.Style.PlacementVisible(item.Candidate.CentroidWorld(root))) {item.Marker.Present(app.Viewer);Rank(item.Marker,item.Candidate);}
            void Rank(AnnotationMarker marker,Candidate candidate)
            {
                if(!app.Input.Valid || app.Input.OverUI || app.Manual.Active || app.Roam.Moving) return;
                var delta=marker.AnchorPosition-app.Input.Aim.origin; float distance=delta.magnitude;
                if(Vector3.Dot(delta,app.Input.Aim.direction)<=0) return;
                float angle=Vector3.Angle(delta,app.Input.Aim.direction);
                float radius=Mathf.Atan2(marker.SelectionDiameter*.5f,distance)*Mathf.Rad2Deg;
                if(angle>radius) return;
                if(Physics.Raycast(app.Input.Aim.origin,delta.normalized,out var obstruction,distance-.025f,InputRouter.BuildingMask,QueryTriggerInteraction.Ignore)) return;
                float score=angle-(marker==hovered?.12f:0);
                if(score<bestScore-.0001f || Mathf.Abs(score-bestScore)<.0001f && (best==null || string.CompareOrdinal(marker.Id,best.Id)<0)) { best=marker; bestScore=score;bestCandidate=candidate; }
            }
            HoveredCandidate=bestCandidate;
            if(hovered!=best)
            { if(hovered) hovered.Hover(false); hovered=best; if(hovered) { hovered.Hover(true); app.Feedback.Hover(); } }
            if(hovered && !app.Placer.Active && app.Input.ConsumePress()) { if(bestCandidate!=null)app.Diff.Select(bestCandidate);else store.Select(hovered.Id); app.Feedback.Confirm(); app.UI.Show("picker"); }
        }
    }
}
