using System;
using System.IO;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>"Continue here": restarts the app where the user stands, so the system re-centres its stationary
    /// boundary (Galaxy XR centres it where the app starts), and carries the twin's placement across the restart.
    /// Persistent anchors cannot be relied on (the headset's persistence service may time out), and a saved world pose
    /// is meaningless in the next session. The new session's floor origin starts at the head, so the twin is stored
    /// relative to the head's floor position and heading, and its height relative to the floor.</summary>
    public static class ContinueHere
    {
        /// <summary>A hand-off older than this is ignored, so a later ordinary start opens normally.</summary>
        const double MaxAgeSeconds = 180;

        [Serializable] public sealed class Handoff
        {
            public string schema = "twin-handoff/1";
            public string space, created_utc, mode;
            public float[] position_head, rotation_head_xyzw;
            public float height_above_floor;
        }

        static string FilePath => Path.Combine(Application.persistentDataPath, "handoff.json");

        public static void SaveAndRestart(AppBootstrap app)
        {
            if (!app || !app.Ready || !app.Root) return;
            var root = app.Root.transform;
            var head = HeadFloorPose(app);
            var relative = RecenterKeeper.Relative(head, new Pose(root.position, root.rotation));
            float floor = Floor(app);
            var handoff = new Handoff
            {
                space = app.Space.id, created_utc = DateTime.UtcNow.ToString("O"), mode = app.Style ? app.Style.Mode.ToString() : "",
                position_head = TwinRoot.Array(relative.position),
                rotation_head_xyzw = new[] { relative.rotation.x, relative.rotation.y, relative.rotation.z, relative.rotation.w },
                height_above_floor = root.position.y - floor
            };
            JsonStore.Write(FilePath, handoff);
            DevLog.Write($"Continue here: saved space={handoff.space} relative={relative.position:F2} yaw={relative.rotation.eulerAngles.y:F1}° height={handoff.height_above_floor:F2}; restarting");
            Restart();
        }

        /// <summary>The pending hand-off, removed once read; null when there is none or it is stale.</summary>
        public static Handoff TakePending()
        {
            var handoff = JsonStore.Read<Handoff>(FilePath);
            foreach (var suffix in new[] { "", ".bak", ".pending" })
                try { if (File.Exists(FilePath + suffix)) File.Delete(FilePath + suffix); } catch (IOException) { }
            if (handoff == null || handoff.schema != "twin-handoff/1" || handoff.position_head?.Length != 3 || handoff.rotation_head_xyzw?.Length != 4) return null;
            if (!DateTime.TryParse(handoff.created_utc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var created) || (DateTime.UtcNow - created).TotalSeconds > MaxAgeSeconds)
            { DevLog.Write("Continue here: stale hand-off ignored"); return null; }
            return handoff;
        }

        public static void Apply(AppBootstrap app, Handoff handoff)
        {
            var q = handoff.rotation_head_xyzw;
            var relative = new Pose(TwinRoot.Vector(handoff.position_head), new Quaternion(q[0], q[1], q[2], q[3]));
            var target = RecenterKeeper.Compose(HeadFloorPose(app), relative);
            target.position.y = Floor(app) + handoff.height_above_floor;
            app.Anchors.Detach();
            app.Root.transform.SetPositionAndRotation(target.position, Quaternion.Euler(0, target.rotation.eulerAngles.y, 0));
            if (Enum.TryParse(handoff.mode, out ViewMode mode) && app.Style && mode != ViewMode.Twin) app.Style.SetMode(mode);
            DevLog.Write($"Continue here: restored twin at {target.position:F2} yaw={target.rotation.eulerAngles.y:F1}° head={app.Viewer.transform.position:F2}");
        }

        /// <summary>The head projected to the floor, with heading only.</summary>
        static Pose HeadFloorPose(AppBootstrap app)
        {
            var head = app.Viewer.transform;
            return new Pose(new Vector3(head.position.x, 0, head.position.z), Quaternion.Euler(0, head.eulerAngles.y, 0));
        }

        static float Floor(AppBootstrap app)
        {
            float floor = TrackingSpace.FloorHeight(app.Origin);
            return float.IsNaN(floor) ? 0 : floor;
        }

        /// <summary>Starts a fresh task of this app and ends the current process; Android creates a new process for it.</summary>
        static void Restart()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var packages = activity.Call<AndroidJavaObject>("getPackageManager");
                using var intent = packages.Call<AndroidJavaObject>("getLaunchIntentForPackage", Application.identifier);
                const int NewTask = 0x10000000, ClearTask = 0x00008000;
                intent.Call<AndroidJavaObject>("addFlags", NewTask | ClearTask).Dispose();
                activity.Call("startActivity", intent);
                using var process = new AndroidJavaClass("android.os.Process");
                process.CallStatic("killProcess", process.CallStatic<int>("myPid"));
            }
            catch (Exception error) { DevLog.Write("Continue here: restart failed: " + error.Message); }
#else
            DevLog.Write("Continue here: restart is only available on the headset");
#endif
        }
    }
}
