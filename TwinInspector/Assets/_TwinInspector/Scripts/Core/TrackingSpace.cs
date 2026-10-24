using System.Collections.Generic;
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
        public static string Describe()
        {
            var input=Input();
            return input==null?"tracking=none":"tracking="+input.GetTrackingOriginMode()+" supported="+input.GetSupportedTrackingOriginModes();
        }
    }
}
