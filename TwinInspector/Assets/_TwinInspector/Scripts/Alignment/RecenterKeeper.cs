using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace TwinInspector
{
    /// <summary>Keeps the twin fixed to the building when the system recenters the view (Galaxy XR: touch and hold the
    /// top button). A recenter moves the whole tracking frame, so without this the twin would move with the user.
    /// The twin's pose is remembered relative to a world-locked session anchor; whenever that anchor jumps in Unity
    /// coordinates the twin is moved by the same yaw and translation. Any other change to the twin (alignment, nudges,
    /// roaming, a followed persistent anchor) simply becomes the new remembered pose. Also shows a recenter hint
    /// before the system boundary (1.5 m) fades the app.</summary>
    [DefaultExecutionOrder(1500)] // after the alignment components' LateUpdate, before TwinRoot (2000)
    public sealed class RecenterKeeper : MonoBehaviour
    {
        /// <summary>Horizontal distance from the tracking origin at which the hint appears, and at which it re-arms.</summary>
        public const float HintDistance = 1.2f, RearmDistance = .8f;
        const float SameMetres = .001f, SameDegrees = .05f, LogMetres = .05f, LogDegrees = 1f;
        AppBootstrap app;
        ARAnchorManager anchors;
        ARAnchor frame;
        bool creating, dirty = true, hintArmed = true;
        Pose offset, applied;
        readonly List<XRInputSubsystem> inputs = new List<XRInputSubsystem>();
        public int Compensations { get; private set; }
        public int OriginEvents { get; private set; }

        public void Initialize(AppBootstrap application, ARAnchorManager manager)
        {
            app = application; anchors = manager;
            if (Application.isEditor) return;
            SubsystemManager.GetSubsystems(inputs);
            foreach (var input in inputs) input.trackingOriginUpdated += OriginUpdated;
            DevLog.Write($"Recenter keeper: {inputs.Count} input subsystem(s), head local={app.Viewer.transform.localPosition:F2}");
        }

        void OriginUpdated(XRInputSubsystem _)
        {
            OriginEvents++;
            DevLog.Write($"Tracking origin updated (recenter) #{OriginEvents} head local={app.Viewer.transform.localPosition:F2}");
        }

        void LateUpdate()
        {
            if (Application.isEditor || !app || !app.Root) return;
            Hint();
            var root = app.Root.transform;
            var current = new Pose(root.position, root.rotation);
            if (!frame) { if (!creating) CreateFrameAsync(); return; }
            // Twin mode roams by moving the XR rig, and the session anchor moves with it: that is not a recenter.
            // Pause (as AnchorPersistence does) and re-check once the rig is back in the physical frame.
            if (app.Style && app.Style.Mode == ViewMode.Twin) { settledSince = -1; pending = true; return; }
            // During a system recenter the system UI is in front and the origin briefly leaves floor mode (the camera
            // offset then jumps by Unity's 1.36 m default). Those intermediate poses are ignored: the remembered
            // offset is kept and applied once, after everything has been stable for SettleSeconds.
            if (!Settled())
            {
                if (settledSince >= 0) DevLog.Write("Recenter keeper: waiting for the system (" + TrackingSpace.Describe() + ")");
                settledSince = -1; pending = true; return;
            }
            if (settledSince < 0) settledSince = Time.unscaledTime;
            var anchor = YawPose(frame.transform);
            if (pending)
            {
                if (Time.unscaledTime - settledSince < SettleSeconds) return;
                pending = false;
                if (!dirty) { Apply(root, current, Compose(anchor, offset), "after system recenter"); return; }
            }
            if (dirty || !Same(current, applied))
            {
                offset = Relative(anchor, current); dirty = false; applied = current; return;
            }
            Apply(root, current, Compose(anchor, offset), null);
        }

        const float SettleSeconds = .5f;
        float settledSince = -1;
        bool pending;

        bool Settled() => frame.trackingState == TrackingState.Tracking && Application.isFocused && TrackingSpace.SessionFocused
                          && app.Origin && app.Origin.CurrentTrackingOriginMode == TrackingOriginModeFlags.Floor;

        /// <summary>Moves the twin horizontally and in yaw only: a recenter in floor mode never changes the floor height.</summary>
        void Apply(Transform root, Pose current, Pose target, string reason)
        {
            target.position.y = current.position.y;
            if (!Same(target, current))
            {
                float metres = Vector3.Distance(target.position, current.position), degrees = Quaternion.Angle(target.rotation, current.rotation);
                if (reason != null || metres > LogMetres || degrees > LogDegrees)
                { Compensations++; DevLog.Write($"Recenter compensated #{Compensations}{(reason != null ? " " + reason : "")}: moved twin {metres:F2} m, {degrees:F1}°"); }
                root.SetPositionAndRotation(target.position, target.rotation);
                current = target;
            }
            applied = current;
        }

        void Hint()
        {
            var local = app.Viewer.transform.localPosition;
            float distance = new Vector2(local.x, local.z).magnitude;
            if (hintArmed && distance > HintDistance)
            {
                hintArmed = false;
                // With the room visible and nothing in progress, offer "Continue here"; otherwise (Twin mode is fully
                // virtual, or an alignment or annotation is underway) only warn.
                bool busy = app.AutoAlign && app.AutoAlign.Active || app.Markers && app.Markers.AwaitingConfirmation || app.Placer && app.Placer.Active
                            || app.UI && (app.UI.Page == "auto" || app.UI.Page == "marker" || app.UI.Page == "picker" || app.UI.Page == "continue");
                if (app.UI && app.Style && app.Style.Mode != ViewMode.Twin && !busy) app.UI.Show("continue");
                else if (app.UI) app.UI.Notify(L.Get("text.recenterHint"));
                DevLog.Write($"Recenter hint at {distance:F2} m from the tracking origin");
            }
            else if (!hintArmed && distance < RearmDistance) hintArmed = true;
        }

        async void CreateFrameAsync()
        {
            if (!anchors || !anchors.enabled || anchors.subsystem == null) return;
            creating = true;
            try
            {
                var head = app.Viewer.transform;
                var pose = new Pose(new Vector3(head.position.x, head.position.y, head.position.z), Quaternion.Euler(0, head.eulerAngles.y, 0));
                var created = await anchors.TryAddAnchorAsync(pose);
                if (!this) { if (created.status.IsSuccess() && created.value) anchors.TryRemoveAnchor(created.value); return; }
                if (created.status.IsSuccess() && created.value) { frame = created.value; dirty = true; DevLog.Write("Recenter keeper: frame anchor created"); }
                else DevLog.Write("Recenter keeper: frame anchor unavailable, status=" + created.status);
            }
            catch (System.Exception error) { DevLog.Write("Recenter keeper: " + error.Message); }
            finally { creating = false; }
        }

        void OnDestroy()
        {
            foreach (var input in inputs) if (input != null) input.trackingOriginUpdated -= OriginUpdated;
            if (frame && anchors) anchors.TryRemoveAnchor(frame);
        }

        static bool Same(Pose a, Pose b) => Vector3.Distance(a.position, b.position) < SameMetres && Quaternion.Angle(a.rotation, b.rotation) < SameDegrees;
        static Pose YawPose(Transform t) => new Pose(t.position, Quaternion.Euler(0, t.eulerAngles.y, 0));

        /// <summary>The pose of <paramref name="world"/> in the frame of <paramref name="anchor"/>.</summary>
        public static Pose Relative(Pose anchor, Pose world)
        {
            var inverse = Quaternion.Inverse(anchor.rotation);
            return new Pose(inverse * (world.position - anchor.position), inverse * world.rotation);
        }
        /// <summary>The world pose of <paramref name="local"/>, given in the frame of <paramref name="anchor"/>.</summary>
        public static Pose Compose(Pose anchor, Pose local) => new Pose(anchor.rotation * local.position + anchor.position, anchor.rotation * local.rotation);
    }
}
