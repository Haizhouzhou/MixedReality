using UnityEngine;

namespace TwinInspector
{
    /// <summary>Shared physics-query contract; construction is independent of Unity scene objects.</summary>
    public static class DiffSampler
    {
        public static bool Ignore(Vector3 pointTwin,Vector3 normalTwin,bool enabled,float floorZ)
            => enabled && Mathf.Abs(normalTwin.z)>Mathf.Cos(20*Mathf.Deg2Rad) && (Mathf.Abs(pointTwin.z-floorZ)<.15f || pointTwin.z>floorZ+2.4f);
        public static RaycastCommand Ray(Vector3 point,Vector3 normal,float distance,int mask)
            => new RaycastCommand(point-normal*distance,normal,new QueryParameters(mask,false,QueryTriggerInteraction.Ignore,true),distance*2);
        public static OverlapSphereCommand Observed(Vector3 point,float radius)
            => new OverlapSphereCommand(point,radius,new QueryParameters(1<<9,false,QueryTriggerInteraction.Ignore,true));
    }
}
