using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR;

namespace TwinInspector
{
    /// <summary>Tracking origin mode for lifecycle logs. Unbounded space was tried against the safety boundary:
    /// the boundary fade still applied and the origin moved off the floor, so the app keeps the floor space.</summary>
    public static class TrackingSpace
    {
        static XRInputSubsystem Input()
        {
            var subsystems=new List<XRInputSubsystem>(); SubsystemManager.GetSubsystems(subsystems);
            foreach(var subsystem in subsystems) if(subsystem.running) return subsystem;
            return subsystems.Count>0?subsystems[0]:null;
        }
        /// <summary>World height of the system floor when the origin is floor-based, otherwise NaN.</summary>
        public static float FloorHeight(Unity.XR.CoreUtils.XROrigin origin)
            => origin && origin.CurrentTrackingOriginMode == TrackingOriginModeFlags.Floor ? origin.Origin.transform.position.y : float.NaN;
#if UNITY_ANDROID && !UNITY_EDITOR
        // The same native call the OpenXR package uses; its feature callback would need a project-wide feature for one value.
        [DllImport("UnityOpenXR",EntryPoint="session_GetSessionState")] static extern void GetSessionState(out int oldState,out int newState);
#endif
        /// <summary>False while the system is in front (home menu): the OpenXR session is not FOCUSED, even when Unity reports no focus change.</summary>
        public static bool SessionFocused
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { GetSessionState(out _,out int state); return state==0 || state==5; } // XR_SESSION_STATE_UNKNOWN, _FOCUSED
                catch(System.Exception) { return true; }
#else
                return true;
#endif
            }
        }
        public static string Describe()
        {
            var input=Input();
            return input==null?"tracking=none":"tracking="+input.GetTrackingOriginMode()+" supported="+input.GetSupportedTrackingOriginModes();
        }
    }
}
