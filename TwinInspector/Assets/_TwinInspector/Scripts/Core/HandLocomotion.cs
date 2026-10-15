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
        Pose physicalPose;
        bool captured;
        Vector2 direction;
        public bool Moving => dragging || direction.sqrMagnitude>0;
        public void Initialize(AppBootstrap application) => app=application;
        public void EnterTwin() { if(!captured) { physicalPose=new Pose(app.Origin.transform.position,app.Origin.transform.rotation); captured=true; } }
        public void Return()
        {
            direction=Vector2.zero; dragging=false;
            if(captured) app.Origin.transform.SetPositionAndRotation(physicalPose.position,physicalPose.rotation);
            app.UI?.Summon();
        }
        public void LeaveTwin() { Return(); captured=false; }
        public void Move(Vector2 value) { if(app && app.Style.Mode==ViewMode.Twin) { EnterTwin(); direction=Vector2.ClampMagnitude(value,1); } }
        public void Turn(float degrees)
        {
            if(!app || app.Style.Mode!=ViewMode.Twin) return;
            EnterTwin(); app.Origin.transform.RotateAround(app.Viewer.transform.position,Vector3.up,degrees); app.UI.Summon();
        }
        void LateUpdate()
        {
            if(app && app.Ready && app.Style.Mode==ViewMode.Twin)
            {
                EnterTwin();
                if(!app.Input.Valid || !app.Input.Held || !app.Input.OverUI) direction=Vector2.zero;
                if(direction.sqrMagnitude>0)
                {
                    var forward=Vector3.ProjectOnPlane(app.Viewer.transform.forward,Vector3.up).normalized;
                    var displacement=(forward*direction.y+Vector3.Cross(Vector3.up,forward)*direction.x)*(.5f*Time.unscaledDeltaTime);
                    var offset=app.Origin.transform.position+displacement-physicalPose.position;
                    app.Origin.transform.position=physicalPose.position+Vector3.ClampMagnitude(offset,50); dragging=false; return;
                }
            }
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
