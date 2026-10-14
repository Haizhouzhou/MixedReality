using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;

namespace TwinInspector
{
    /// <summary>Ordered application initialization and the only service locator.</summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        [SerializeField] Settings settings;
        [SerializeField] InputActionAsset actions;
        [SerializeField] XROrigin origin;
        [SerializeField] Camera viewer;
        [SerializeField] ARSession session;
        [SerializeField] ARCameraManager cameraManager;
        [SerializeField] ARAnchorManager anchorManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARTrackedImageManager imageManager;
        [SerializeField] ARMeshManager meshManager;
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        bool starting;
        Transform poseContainer;
        public static AppBootstrap Instance { get; private set; }
        public bool Ready { get; private set; }
        public string Status { get; private set; } = "Starting TwinInspector";
        public Camera Viewer => viewer;
        public XROrigin Origin => origin;
        public DesignTokens Tokens { get; private set; }
        public TwinRoot Root { get; private set; }
        public TwinLoader Loader { get; private set; }
        public TwinStyle Style { get; private set; }
        public ManualAlignment Manual { get; private set; }
        public MarkerAlignment Markers { get; private set; }
        public AnchorPersistence Anchors { get; private set; }
        public AnnotationStore Annotations { get; private set; }
        public AnnotationPlacer Placer { get; private set; }
        public InspectorUI UI { get; private set; }
        public PerfHud Performance { get; private set; }

        void Awake()
        {
            Instance = this;
            if(!settings) settings=Resources.Load<Settings>("Settings");
            Physics.queriesHitBackfaces=true;
#if UNITY_EDITOR
            origin.transform.rotation=Quaternion.Euler(0,-90,0);
#endif
            cameraManager.enabled = false; anchorManager.enabled = false; planeManager.enabled = false; imageManager.enabled = false; meshManager.enabled = false;
            session.enabled = !Application.isEditor;
            actions.Enable();
        }
        async void Start() => await InitializeAsync();

        public async Task InitializeAsync()
        {
            if (starting || Ready) return;
            starting = true;
            try
            {
                await RequestPermissionsAsync();
                if (!Application.isEditor)
                {
                    float deadline = Time.realtimeSinceStartup + 30;
                    while (ARSession.state < ARSessionState.Ready && Time.realtimeSinceStartup < deadline)
                    { lifetime.Token.ThrowIfCancellationRequested(); await Task.Yield(); }
                    if (ARSession.state < ARSessionState.Ready) throw new Exception("XR tracking is not ready. Try again after tracking recovers.");
                    cameraManager.enabled = true; viewer.backgroundColor = Color.clear;
                    anchorManager.enabled = planeManager.enabled = imageManager.enabled = meshManager.enabled = true;
                    await Task.Yield();
                }
                Tokens = Resources.Load<DesignTokens>("DesignTokens");
                if (!Tokens) throw new Exception("Design resources are missing");
                if (!Root) { Root = new GameObject("TwinRoot").AddComponent<TwinRoot>(); Root.transform.position=new Vector3(0,0,-10); }
                Loader = gameObject.AddComponent<TwinLoader>();
                await Loader.InitializeAsync(settings,Root,viewer,Tokens.TwinMaterial);
                Anchors = gameObject.AddComponent<AnchorPersistence>(); Anchors.Initialize(Root,anchorManager,settings.Scan);
                await Anchors.RestoreAsync();
                Annotations = new AnnotationStore(settings.Scan);
                var visuals = gameObject.AddComponent<AnnotationVisuals>(); visuals.Initialize(Annotations,Root,Tokens);
                Style = gameObject.AddComponent<TwinStyle>(); Style.Initialize(Loader,Root,viewer,cameraManager,Tokens);
                Manual = gameObject.AddComponent<ManualAlignment>();
                var left = TrackingPose("XRI Left","Position","Rotation");
                var right = TrackingPose("XRI Right","Position","Rotation");
                var aim = TrackingPose("XRI Right","Aim Position","Aim Rotation");
                Manual.Initialize(Root,Anchors,planeManager,left,right,actions);
                Markers = gameObject.AddComponent<MarkerAlignment>();
                await Markers.InitializeAsync(Root,Anchors,imageManager,Loader.DataRoot,settings.Scan,lifetime.Token);
                Placer = gameObject.AddComponent<AnnotationPlacer>(); Placer.Initialize(Root,Annotations,viewer,aim,actions);
                Performance = gameObject.AddComponent<PerfHud>(); Performance.Initialize(Loader,settings);
                Ready = true; Status = Loader.Manifest.demo ? "Built-in hall · low detail" : "Region "+Loader.Manifest.region.name+" · external data";
                EnsureUI(); UI.Show("setup");
                DevLog.Write("Ready: "+Status);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Ready=false;
                foreach(var service in GetComponents<MonoBehaviour>())
                    if(service!=this && !(service is InspectorUI)) Destroy(service);
                if(Root) Destroy(Root.gameObject);
                Root=null; Loader=null; Anchors=null; Style=null; Manual=null; Markers=null; Placer=null; Performance=null;
                if(poseContainer) Destroy(poseContainer.gameObject);
                Annotations=null;
                Status = error is UnauthorizedAccessException ? "Permissions needed to continue" : "Setup needs attention";
                DevLog.Write("Startup failed: "+error);
                Tokens = Resources.Load<DesignTokens>("DesignTokens");
                if (Tokens) { EnsureUI(); UI.Show("setup"); UI.Notify(error.Message); }
            }
            finally { starting = false; }
        }

        void EnsureUI() { if (!UI) { UI = gameObject.AddComponent<InspectorUI>(); UI.Initialize(this); } }
        Transform TrackingPose(string map, string position, string rotation)
        {
            if(!poseContainer) { poseContainer=new GameObject("TwinInspector tracking poses").transform; poseContainer.SetParent(origin.CameraFloorOffsetObject.transform,false); }
            var go = new GameObject(map+" "+position+" pose"); go.transform.SetParent(poseContainer,false);
            var driver = go.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(actions.FindAction(map+"/"+position,true));
            driver.rotationInput = new InputActionProperty(actions.FindAction(map+"/"+rotation,true));
            driver.ignoreTrackingState = true;
            return go.transform;
        }

        async Task RequestPermissionsAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var needed = new List<string>();
            foreach (var permission in new[] { Permission.Camera, "android.permission.SCENE_UNDERSTANDING_FINE", "android.permission.HAND_TRACKING" })
                if (!Permission.HasUserAuthorizedPermission(permission)) needed.Add(permission);
            if (needed.Count == 0) return;
            var pending = new HashSet<string>(needed);
            var denied = new List<string>();
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += value => pending.Remove(value);
            callbacks.PermissionDenied += value => { denied.Add(value); pending.Remove(value); };
            callbacks.PermissionDeniedAndDontAskAgain += value => { denied.Add(value); pending.Remove(value); };
            Permission.RequestUserPermissions(needed.ToArray(),callbacks);
            while (pending.Count > 0) { lifetime.Token.ThrowIfCancellationRequested(); await Task.Yield(); }
            if (denied.Count > 0) throw new UnauthorizedAccessException("Enable camera, scene and hand permissions in app settings, then retry.");
#else
            await Task.CompletedTask;
#endif
        }
        void OnDestroy() { lifetime.Cancel(); lifetime.Dispose(); if (Instance==this) Instance=null; }
    }
}
