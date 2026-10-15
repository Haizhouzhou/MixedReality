using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace TwinInspector
{
    /// <summary>Fixed-reference single-hand translation and optional two-hand yaw, with transition rebasing.</summary>
    public sealed class ManualAlignment : MonoBehaviour
    {
        TwinRoot root;
        AnchorPersistence persistence;
        ARPlaneManager planes;
        InputRouter input;
        bool dragging;
        int handCount;
        bool dragLeft;
        public bool Dragging => dragging;
        Vector3 startMidpoint, startSpan, startPosition;
        Quaternion startRotation;
        public bool Active { get; private set; }
        public bool HeightUnlocked { get; set; }

        public void Initialize(TwinRoot twin, AnchorPersistence anchors, ARPlaneManager planeManager, InputRouter router)
        {
            root = twin; persistence = anchors; planes = planeManager; input=router;
        }
        public void SetActive(bool value) { Active = value; dragging = false; if (value) persistence.Detach(); }
        public void Nudge(float x, float z, float height, float yaw)
        {
            persistence.Detach();
            root.transform.position += new Vector3(x, HeightUnlocked ? height : 0, z);
            root.transform.RotateAround(root.transform.position, Vector3.up, yaw);
        }
        public bool SnapFloor(Camera camera)
        {
            if (planes == null || planes.subsystem == null) return false;
            ARPlane best = null; float error = float.MaxValue;
            foreach (var plane in planes.trackables)
            {
                if (plane.trackingState != TrackingState.Tracking || plane.alignment != PlaneAlignment.HorizontalUp) continue;
                float score = Mathf.Abs(plane.center.y - (camera.transform.position.y - 1.6f));
                if (score < error && plane.center.y < camera.transform.position.y - .6f) { best = plane; error = score; }
            }
            if (!best) return false;
            persistence.Detach(); var p = root.transform.position; p.y = best.center.y + root.Origin.z; root.transform.position = p; return true;
        }
        void LateUpdate()
        {
            if (!Active || !input || input.OverUI || !input.Valid) { dragging=false; return; }
            bool two=input.TryTwoHandSelect(out var left,out var right);
            bool one=input.TrySingleSelect(out var single);
            if(!one && !two) { dragging=false; return; }
            // A new drag starts only on the building, then uses fixed tracked poses.
            if(!dragging && !input.HasBuildingHit) return;
            int count=two?2:1;
            var midpoint = two ? (left + right) * .5f : single;
            var span = two ? Vector3.ProjectOnPlane(right - left, Vector3.up) : Vector3.right;
            if (span.sqrMagnitude < .0025f) { dragging = false; return; }
            if (!dragging || handCount!=count || (count==1 && dragLeft!=input.ActiveLeft))
            {
                dragging = true; startMidpoint = midpoint; startSpan = span;
                startPosition = root.transform.position; startRotation = root.transform.rotation;
                handCount=count; dragLeft=input.ActiveLeft;
            }
            var delta = two ? Quaternion.AngleAxis(Vector3.SignedAngle(startSpan, span, Vector3.up), Vector3.up) : Quaternion.identity;
            var position = midpoint + delta * (startPosition - startMidpoint);
            if (!HeightUnlocked) position.y = startPosition.y;
            root.transform.SetPositionAndRotation(position, delta * startRotation);
        }
    }
}
