using System;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Mouse click, hand pinch and controller trigger share this exact placement path.</summary>
    public sealed class AnnotationPlacer : MonoBehaviour
    {
        TwinRoot root; AnnotationStore store; Camera viewer; InputRouter input;
        public bool Active { get; set; }
        public event Action Placed;
        public event Action<string> Feedback;
        public void Initialize(TwinRoot twin, AnnotationStore annotations, Camera camera, InputRouter router)
        { root=twin; store=annotations; viewer=camera; input=router; }
        void LateUpdate()
        {
            if(!Active || !input || !input.Pressed || input.OverUI) return;
            var app=AppBootstrap.Instance;
            var visuals=GetComponent<AnnotationVisuals>();
            if(visuals.HoveredCandidate!=null && input.ConsumePress()) {app.Diff.Select(visuals.HoveredCandidate);app.Feedback.Confirm();Placed?.Invoke();return;}
            var hovered=visuals.Hovered;
            if(hovered && input.ConsumePress()) { store.Select(hovered.Id); app.Feedback.Confirm(); Placed?.Invoke(); return; }
            if(!input.HasBuildingHit || !app.Style.PlacementVisible(input.BuildingHit.point)) { Feedback?.Invoke(L.Get("Point at a visible twin surface")); return; }
            if(input.ConsumePress()) Place(input.BuildingHit);
        }
        public void Place(RaycastHit hit)
        {
            if(!hit.collider || !root || store==null || !viewer) return;
            var marker=hit.collider.GetComponentInParent<AnnotationMarker>();
            if(marker) store.Select(marker.Id);
            else
            {
                var normal=Vector3.Dot(hit.normal,viewer.transform.position-hit.point)<0 ? -hit.normal : hit.normal;
                store.Add(root.WorldToTwin(hit.point),root.NormalToTwin(normal),root.WorldToTwin(viewer.transform.position),root.RotationToTwin(viewer.transform.rotation));
            }
            Placed?.Invoke();
            AppBootstrap.Instance?.Feedback?.Confirm(true);
        }
    }
}
