using UnityEngine;
namespace TwinInspector
{
    /// <summary>Body-relative dock pose and auto-hide rules, kept free of scene state for tests.</summary>
    public static class DockPlacement
    {
        /// <summary>Head yaw may turn this far before the estimated body direction follows.</summary>
        public const float BodyDeadZone=35;
        public const float HideDelay=2, FadeSeconds=.5f, LookCone=25;

        /// <summary>Body direction trails the head: glancing at the dock leaves it in place, turning around brings it along.</summary>
        public static float FollowBodyYaw(float bodyYaw,float headYaw,float deadZone=BodyDeadZone)
        {
            float delta=Mathf.DeltaAngle(bodyYaw,headYaw);
            return Mathf.Abs(delta)<=deadZone?bodyYaw:Mathf.Repeat(headYaw-Mathf.Sign(delta)*deadZone,360);
        }

        public static Vector3 Home(Vector3 head,float bodyYaw,float distance,float yaw,float height)
            => head+Quaternion.Euler(0,bodyYaw+yaw,0)*Vector3.forward*distance+Vector3.up*height;

        /// <summary>Upright, facing the viewer horizontally.</summary>
        public static Quaternion Facing(Vector3 head,Vector3 dock)
        {
            var away=Vector3.ProjectOnPlane(dock-head,Vector3.up);
            return Quaternion.LookRotation(away.sqrMagnitude>.0001f?away.normalized:Vector3.forward,Vector3.up);
        }

        /// <summary>Placement offsets that reproduce a dragged dock pose, clamped to the Preferences ranges.</summary>
        public static (float distance,float yaw,float height) Offsets(Vector3 head,float bodyYaw,Vector3 dock)
        {
            var away=Vector3.ProjectOnPlane(dock-head,Vector3.up);
            float yaw=Mathf.DeltaAngle(bodyYaw,Mathf.Atan2(away.x,away.z)*Mathf.Rad2Deg);
            return (Mathf.Clamp(away.magnitude,.4f,.9f),Mathf.Clamp(yaw,-45,45),Mathf.Clamp(dock.y-head.y,-.3f,.2f));
        }

        /// <summary>An unpinned dock starts fading once nobody has raised a palm, looked at or used it for HideDelay seconds.</summary>
        public static bool ShouldHide(bool pinned,bool palmUp,bool looking,bool busy,float idleSeconds)
            => !pinned&&!palmUp&&!looking&&!busy&&idleSeconds>=HideDelay;

        public static bool Looking(Vector3 head,Vector3 forward,Vector3 dock,float cone=LookCone)
            => Vector3.Angle(forward,dock-head)<=cone;
    }
}
