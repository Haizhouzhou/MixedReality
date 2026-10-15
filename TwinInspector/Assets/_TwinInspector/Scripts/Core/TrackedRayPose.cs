using UnityEngine;

namespace TwinInspector
{
    /// <summary>Uses the sample's tracked hand aim pose without assuming any controller is active.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class TrackedRayPose : MonoBehaviour
    {
        [SerializeField] Transform source;
        public Transform Source { get => source; set => source=value; }
        void Update() { if(source && source.gameObject.activeInHierarchy) transform.SetPositionAndRotation(source.position,source.rotation); }
    }
}
