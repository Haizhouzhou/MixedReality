using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace TwinInspector
{
    /// <summary>Rigid two-hand translation/yaw; no accidental building scaling.</summary>
    public sealed class ManualAlignment : MonoBehaviour
    {
        TwinRoot root;
        AnchorPersistence persistence;
        ARPlaneManager planes;
        InputRouter input;
        bool dragging;
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
            if (!Active || !input || input.OverUI || !input.TryTwoHandSelect(out var left,out var right)) { dragging=false; return; }
            var midpoint = (left + right) * .5f;
            var span = Vector3.ProjectOnPlane(right - left, Vector3.up);
            if (span.sqrMagnitude < .0025f) { dragging = false; return; }
            if (!dragging)
            {
                dragging = true; startMidpoint = midpoint; startSpan = span;
                startPosition = root.transform.position; startRotation = root.transform.rotation;
            }
            var delta = Quaternion.AngleAxis(Vector3.SignedAngle(startSpan, span, Vector3.up), Vector3.up);
            var position = midpoint + delta * (startPosition - startMidpoint);
            if (!HeightUnlocked) position.y = startPosition.y;
            root.transform.SetPositionAndRotation(position, delta * startRotation);
        }
    }
}
