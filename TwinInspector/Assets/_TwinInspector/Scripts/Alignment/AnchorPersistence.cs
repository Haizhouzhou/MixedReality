using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace TwinInspector
{
    /// <summary>Restores spatial anchors when supported, with an explicit JSON fallback.</summary>
    public sealed class AnchorPersistence : MonoBehaviour
    {
        TwinRoot root;
        ARAnchorManager manager;
        string scan, path;
        ARAnchor active;
        bool following, saving;
        AlignmentState state;
        public string Status { get; private set; } = "Not aligned";

        public void Initialize(TwinRoot twin, ARAnchorManager anchors, string scanId)
        { root = twin; manager = anchors; scan = scanId; path = TwinPaths.SaveFile("alignment", scan); }

        public async Task RestoreAsync()
        {
            state = JsonStore.Read<AlignmentState>(path);
            if (state == null || state.scan != scan || state.schema != "twin-alignment/1") return;
            ApplyFallback(state);
            Status = "JSON pose restored · check alignment";
            if (Application.isEditor || manager?.subsystem == null || !manager.descriptor.supportsLoadAnchor || !Guid.TryParse(state.anchor_id, out var id))
            { DevLog.Write(Status); return; }
            try
            {
                var result = await manager.TryLoadAnchorAsync(new SerializableGuid(id));
                if (result.status.IsSuccess() && result.value)
                {
                    active = result.value;
                    float deadline = Time.realtimeSinceStartup + 12;
                    while (active && active.trackingState != TrackingState.Tracking && Time.realtimeSinceStartup < deadline) await Task.Yield();
                    if (active && active.trackingState == TrackingState.Tracking)
                    { following = true; Follow(); Status = "Persistent anchor restored"; }
                }
            }
            catch (Exception error) { DevLog.Write("Anchor restore: " + error.Message); }
            DevLog.Write(Status);
        }

        void ApplyFallback(AlignmentState saved)
        {
            if (saved.position_world?.Length != 3 || saved.rotation_world_xyzw?.Length != 4) return;
            var q = saved.rotation_world_xyzw;
            root.transform.SetPositionAndRotation(TwinRoot.Vector(saved.position_world), Quaternion.Euler(0, new Quaternion(q[0], q[1], q[2], q[3]).eulerAngles.y, 0));
        }

        public void Detach() => following = false;

        public async Task SaveAsync()
        {
            if (saving) return;
            saving = true;
            following = false;
            try
            {
                var pose = new Pose(root.transform.position, Quaternion.Euler(0, root.transform.eulerAngles.y, 0));
                var previousId = state?.anchor_id;
                var previousAnchor = active;
                state = new AlignmentState { scan = scan, position_world = TwinRoot.Array(pose.position), rotation_world_xyzw = new[] { pose.rotation.x, pose.rotation.y, pose.rotation.z, pose.rotation.w }, updated_utc = DateTime.UtcNow.ToString("O"), anchor_id = "" };
                // Save first: permission/runtime failures still retain a usable fallback.
                JsonStore.Write(path, state);
                Status = "Alignment saved · JSON fallback";
                if (!Application.isEditor && manager?.subsystem != null && manager.descriptor.supportsSaveAnchor)
                {
                    var created = await manager.TryAddAnchorAsync(pose);
                    if (created.status.IsSuccess() && created.value)
                    {
                        active = created.value;
                        var saved = await manager.TrySaveAnchorAsync(active);
                        if (saved.status.IsSuccess())
                        {
                            state.anchor_id = saved.value.guid.ToString();
                            JsonStore.Write(path, state);
                            following = true; Status = "Persistent anchor saved";
                            if (Guid.TryParse(previousId, out var oldId) && previousId != state.anchor_id && manager.descriptor.supportsEraseAnchor)
                                await manager.TryEraseAnchorAsync(new SerializableGuid(oldId));
                        }
                        if (previousAnchor && previousAnchor != active) manager.TryRemoveAnchor(previousAnchor);
                    }
                }
                DevLog.Write(Status);
            }
            catch (Exception error) { Status = "Saved locally · anchor unavailable"; DevLog.Write(Status + ": " + error.Message); }
            finally { saving = false; }
        }

        void Follow()
        {
            if (active && active.trackingState == TrackingState.Tracking)
                root.transform.SetPositionAndRotation(active.transform.position, Quaternion.Euler(0, active.transform.eulerAngles.y, 0));
        }
        void LateUpdate() { if (following) Follow(); }
    }
}
