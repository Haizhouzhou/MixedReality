using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.XR.Extensions;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace TwinInspector
{
    [Serializable] public sealed class MarkerCatalog
    {
        public string schema, scan;
        public List<MarkerDefinition> markers;
    }
    [Serializable] public sealed class MarkerDefinition
    {
        public string id, kind, note;
        public float size_m, yaw_deg;
        public float[] position_twin, normal_twin;
        public bool measured;
    }

    /// <summary>One-second stable marker detection with an explicit confirmation step.</summary>
    public sealed class MarkerAlignment : MonoBehaviour
    {
        TwinRoot root;
        AnchorPersistence persistence;
        ARTrackedImageManager images;
        MarkerCatalog catalog;
        TrackableId candidateId;
        float stableSince;
        Vector3 previousPosition;
        Quaternion previousRotation;
        Pose before;
        bool scanning;
        public string Status { get; private set; } = "Marker not detected";
        public bool AwaitingConfirmation { get; private set; }
        public event Action Changed;

        public async Task InitializeAsync(TwinRoot twin, AnchorPersistence anchors, ARTrackedImageManager manager, string dataRoot, string scan, CancellationToken token)
        {
            root = twin; persistence = anchors; images = manager;
            string json;
            try { json = await TwinPaths.ReadTextAsync(dataRoot, "markers.json", token); }
            catch(System.IO.IOException) { json = await TwinPaths.ReadTextAsync(Application.streamingAssetsPath+"/twin_demo", "markers.json", token); }
            catalog = JsonConvert.DeserializeObject<MarkerCatalog>(json);
            if (catalog.schema != "twin-markers/1" || catalog.scan != scan) throw new Exception("Marker catalog scan mismatch");
        }
        public void Begin() { scanning = true; AwaitingConfirmation = false; stableSince = 0; Status = "Look at a measured marker"; Changed?.Invoke(); }
        public void Cancel()
        {
            if (AwaitingConfirmation) root.transform.SetPositionAndRotation(before.position, before.rotation);
            AwaitingConfirmation = false; scanning = false; Status = "Marker alignment cancelled"; Changed?.Invoke();
        }
        public async Task ConfirmAsync()
        {
            if (!AwaitingConfirmation) return;
            AwaitingConfirmation = false; scanning = false;
            await persistence.SaveAsync(); Status = persistence.Status; Changed?.Invoke();
        }

        void Update()
        {
            if (!scanning || AwaitingConfirmation || images?.subsystem == null) return;
            bool found = false;
            foreach (var image in images.trackables)
            {
                if (image.trackingState != TrackingState.Tracking) continue;
                string id = null;
                if (image.IsMarker() && image.TryGetMarkerData(out var dictionary, out int number) && dictionary == XRMarkerDictionary.ArUco4x4_50) id = number.ToString();
                if (image.IsQrCode() && image.TryGetQrCodeData(out string value)) id = "QR:" + value;
                var definition = catalog.markers.Find(marker => marker.id == id);
                if (definition == null) continue;
                found = true;
                if (!definition.measured) { Status = "Marker pose needs measurement"; stableSince = 0; continue; }
                var pose = image.transform;
                if (candidateId != image.trackableId || Vector3.Distance(pose.position, previousPosition) > .025f || Quaternion.Angle(pose.rotation, previousRotation) > 3)
                { candidateId = image.trackableId; stableSince = Time.unscaledTime; previousPosition = pose.position; previousRotation = pose.rotation; }
                if (stableSince == 0) stableSince = Time.unscaledTime;
                if (Time.unscaledTime - stableSince >= 1) Apply(definition, pose.position, pose.up, pose.forward);
                else Status = "Hold marker in view";
                break;
            }
            if (!found) { stableSince = 0; Status = "Marker not detected"; }
        }

        void Apply(MarkerDefinition marker, Vector3 markerWorld, Vector3 normalWorld, Vector3 topWorld)
        {
            var expectedNormal = TwinRoot.SwapYZ(TwinRoot.Vector(marker.normal_twin));
            var expected = Vector3.ProjectOnPlane(expectedNormal, Vector3.up);
            var observed = Vector3.ProjectOnPlane(normalWorld, Vector3.up);
            if (expected.sqrMagnitude < .01f)
            { expected = Quaternion.Euler(0, -marker.yaw_deg, 0) * Vector3.forward; observed = Vector3.ProjectOnPlane(topWorld, Vector3.up); }
            if (observed.sqrMagnitude < .01f) return;
            before = new Pose(root.transform.position, root.transform.rotation);
            persistence.Detach();
            var rotation = Quaternion.AngleAxis(Vector3.SignedAngle(expected, observed, Vector3.up), Vector3.up);
            root.transform.SetPositionAndRotation(markerWorld - rotation * root.ToLocal(TwinRoot.Vector(marker.position_twin)), rotation);
            AwaitingConfirmation = true;
            Status = "Aligned with marker " + marker.id + " · residual n/a";
            Changed?.Invoke();
        }

        public void SimulateFirst()
        {
            if (!Application.isEditor || catalog?.markers == null || catalog.markers.Count == 0) return;
            var marker = catalog.markers[0];
            Apply(marker, root.TwinToWorld(TwinRoot.Vector(marker.position_twin)), root.NormalToWorld(TwinRoot.Vector(marker.normal_twin)), Vector3.up);
        }
    }
}
