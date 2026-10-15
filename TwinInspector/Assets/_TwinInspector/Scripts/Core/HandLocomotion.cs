using UnityEngine;

namespace TwinInspector
{
    /// <summary>Two-hand world drag moves the viewer rig, never the registered twin.</summary>
    public sealed class HandLocomotion : MonoBehaviour
    {
        AppBootstrap app;
        bool dragging;
        Vector3 startMid, startSpan, startPosition;
        Quaternion startRotation;
        public void Initialize(AppBootstrap application) => app=application;
        void LateUpdate()
        {
            if(!app || !app.Ready || app.Style.Mode!=ViewMode.Twin || app.Manual.Active || app.Placer.Active || app.Input.OverUI || !app.Input.TryTwoHandSelect(out var left,out var right,true))
            { dragging=false; return; }
            var rig=app.Origin.transform;
            // Tracking-space samples avoid feedback from moving the rig itself.
            left=rig.InverseTransformPoint(left); right=rig.InverseTransformPoint(right);
            var mid=(left+right)*.5f; var span=Vector3.ProjectOnPlane(right-left,Vector3.up);
            if(span.sqrMagnitude<.0025f) { dragging=false; return; }
            if(!dragging) { dragging=true; startMid=mid; startSpan=span; startPosition=rig.position; startRotation=rig.rotation; }
            var pose=DragPose(new Pose(startPosition,startRotation),startMid,startSpan,mid,span);
            rig.SetPositionAndRotation(pose.position,pose.rotation);
        }
        public static Pose DragPose(Pose start,Vector3 initialMid,Vector3 initialSpan,Vector3 mid,Vector3 span)
        {
            var yaw=Quaternion.AngleAxis(-Vector3.SignedAngle(initialSpan,span,Vector3.up),Vector3.up);
            var rotation=start.rotation*yaw;
            var position=start.position+start.rotation*initialMid-rotation*mid;
            position.y=start.position.y;
            return new Pose(position,rotation);
        }
    }
}
