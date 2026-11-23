using UnityEngine;
namespace TwinInspector
{
    /// <summary>Body-relative dock pose and auto-hide rules, kept free of scene state for tests.</summary>
    public static class DockPlacement
    {
        /// <summary>Head yaw may turn this far before the estimated body direction follows.</summary>
        public const float BodyDeadZone=35;
        public const float HideDelay=2, FadeSeconds=.5f, LookCone=25;
        /// <summary>Lazy follow: the dock stays put while it is within 25 cm and 15 degrees of its home (head bob, leaning in,
        /// small steps), glides there once it is clearly out of place, and stops when it has arrived.</summary>
        public const float GlideStartDistance=.25f, GlideStartAngle=15, GlideStopDistance=.01f, GlideStopAngle=1, GlideSeconds=.2f;

        /// <summary>While the viewer walks (head faster than 0.3 m/s horizontally) the dock stays fixed in the room and moves
        /// only after they have stood still for 0.35 s. A dock moving with a walking viewer jittered: the headset re-projects
        /// late frames for content that stands still in the room, not for content that moves with the head. A dock left more
        /// than 1.5 m away reappears at its place instead of sweeping across the room.</summary>
        public const float WalkSpeed=.3f, SettleSeconds=.35f, JumpDistance=1.5f;

        public static bool MayMove(float headSpeed,float stillSeconds) => headSpeed<WalkSpeed && stillSeconds>=SettleSeconds;

        public static bool ShouldGlide(bool gliding,float distance,float angle)
            => gliding ? distance>GlideStopDistance||angle>GlideStopAngle : distance>GlideStartDistance||angle>GlideStartAngle;

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

        /// <summary>An unpinned dock starts fading once nobody has raised a palm, looked at or used it for HideDelay seconds.</summary>
        public static bool ShouldHide(bool pinned,bool palmUp,bool looking,bool busy,float idleSeconds)
            => !pinned&&!palmUp&&!looking&&!busy&&idleSeconds>=HideDelay;

        public static bool Looking(Vector3 head,Vector3 forward,Vector3 dock,float cone=LookCone)
            => Vector3.Angle(forward,dock-head)<=cone;
    }
}
