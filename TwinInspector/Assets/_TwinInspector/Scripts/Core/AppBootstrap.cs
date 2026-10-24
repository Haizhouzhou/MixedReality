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
        TaskCompletionSource<TwinSpace> spaceChoice;
        public static AppBootstrap Instance { get; private set; }
        public bool Ready { get; private set; }
        public bool Starting => starting;
        public string Status { get; private set; } = "Starting TwinInspector";
        public string DisplayStatus=>Ready&&Loader?(Loader.Manifest.demo?L.Format("status.builtIn",Space.name):L.Format("status.region",Loader.Manifest.region.name))
            :starting&&Space!=null?L.Format("status.loadingSpace",Space.name):L.Get(Status);
        public SpaceCatalog Spaces { get; private set; }
        /// <summary>The space chosen at start-up; null until chosen and again after a failed start.</summary>
        public TwinSpace Space { get; private set; }
        public bool ChoosingSpace => spaceChoice != null;
        public Camera Viewer => viewer;
        public Settings Config => settings;
        public ARMeshManager MeshManager => meshManager;
        public ISceneSurfaceSource SceneSource { get; set; }
        public DiffEngine Diff { get; private set; }
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
        public DeviceProbes Probes { get; private set; }
        public ObjectPicker Picker { get; private set; }
        public InputRouter Input { get; private set; }
        public HandLocomotion Roam { get; private set; }
        public MicroFeedback Feedback { get; private set; }

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
            Input = gameObject.AddComponent<InputRouter>(); Input.Initialize(origin,viewer);
            if(Application.isEditor) gameObject.AddComponent<EditorDesktopDriver>().Initialize(this);
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
                CjkFonts.Install(Tokens);
                Spaces ??= await SpaceCatalog.LoadAsync(lifetime.Token);
                if (Space == null)
                {
                    spaceChoice = new TaskCompletionSource<TwinSpace>();
                    using (lifetime.Token.Register(() => spaceChoice?.TrySetCanceled()))
                    { EnsureUI(); UI.Show("space"); Space = await spaceChoice.Task; }
                    spaceChoice = null;
                }
                if (UI) UI.Show("setup"); // loading status, without Retry while starting
                DevLog.Write("Space: "+Space.id+" ("+Space.scan+")");
                if (!Root) { Root = new GameObject("TwinRoot").AddComponent<TwinRoot>(); Root.transform.position=new Vector3(0,0,-10); }
                Loader = gameObject.AddComponent<TwinLoader>();
                await Loader.InitializeAsync(settings,Space,Root,viewer,Tokens.TwinMaterial);
                Anchors = gameObject.AddComponent<AnchorPersistence>(); Anchors.Initialize(Root,anchorManager,Space.Storage);
                await Anchors.RestoreAsync();
                Annotations = new AnnotationStore(Space.Storage);
                var visuals = gameObject.AddComponent<AnnotationVisuals>(); visuals.Initialize(Annotations,Root,Tokens);
                Style = gameObject.AddComponent<TwinStyle>(); Style.Initialize(Loader,Root,viewer,cameraManager,Tokens);
                Manual = gameObject.AddComponent<ManualAlignment>();
                Manual.Initialize(Root,Anchors,planeManager,Input);
                Markers = gameObject.AddComponent<MarkerAlignment>();
                await Markers.InitializeAsync(Root,Anchors,imageManager,Loader.DataRoot,TwinPaths.PackagedRoot(Space),Space.scan,lifetime.Token);
                Placer = gameObject.AddComponent<AnnotationPlacer>(); Placer.Initialize(Root,Annotations,viewer,Input);
                gameObject.AddComponent<SwissReticle>().Initialize(this);
                Roam=gameObject.AddComponent<HandLocomotion>(); Roam.Initialize(this);
                Feedback=gameObject.AddComponent<MicroFeedback>(); Feedback.Initialize();
                Performance = gameObject.AddComponent<PerfHud>(); Performance.Initialize(Loader,settings);
                Probes = gameObject.AddComponent<DeviceProbes>(); Probes.Initialize(viewer);
                Picker = gameObject.AddComponent<ObjectPicker>(); Picker.Initialize(this);
                if(!Application.isEditor) {var source=gameObject.AddComponent<SceneSurfaceSource>();source.Initialize(meshManager);SceneSource=source;}
                Diff=gameObject.AddComponent<DiffEngine>();Diff.Initialize(this);
                gameObject.AddComponent<CandidateVisuals>().Initialize(this);
                Ready = true; Status = Space.name+(Loader.Manifest.demo ? " · built-in data" : " · Region "+Loader.Manifest.region.name+" · external data");
                EnsureUI(); UI.Show("setup");
                DevLog.Write("Ready: "+Status);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Teardown();
                // Retry offers the space choice again, in case the chosen space caused the failure.
                Space=null; spaceChoice=null;
                Status = error is UnauthorizedAccessException ? "Permissions needed to continue" : "Setup needs attention";
                DevLog.Write("Startup failed: "+error);
                Tokens = Resources.Load<DesignTokens>("DesignTokens");
                if (Tokens) { EnsureUI(); UI.Show("setup"); UI.Notify(L.Get(Status)); }
            }
            finally { starting = false; }
        }

        /// <summary>Unload the current space and load another; annotations and saved alignment stay with their own scan.</summary>
        public async Task SwitchSpaceAsync(string id)
        {
            var target = Spaces?.Find(id);
            if (!Ready || starting || target == null || target == Space || !TwinPaths.Available(settings, target)) return;
            DevLog.Write("Switching space: "+Space.id+" -> "+target.id);
            Style.SetMode(ViewMode.Overlay); // leaves Twin roaming and restores the physical frame first
            Teardown();
            Space = target; Status = "Starting TwinInspector";
            UI.Show("setup");
            await Task.Yield(); // destroyed services must be gone before their replacements are added
            await InitializeAsync();
        }

        /// <summary>Destroy every per-space service, keeping the UI, input and Editor driver.</summary>
        void Teardown()
        {
            Ready=false;
            if(UI) UI.ReleaseServices();
            foreach(var service in GetComponents<MonoBehaviour>())
                if(service!=this && !(service is InspectorUI) && !(service is InputRouter) && !(service is EditorDesktopDriver)) Destroy(service);
            if(SceneSource is MonoBehaviour source && source && source.gameObject!=gameObject) Destroy(source.gameObject);
            if(Root) Destroy(Root.gameObject);
            // Explicit nulls: callers use ?. which does not see destroyed Unity objects as null.
            Root=null; Loader=null; Anchors=null; Style=null; Manual=null; Markers=null; Placer=null; Performance=null;
            Probes=null; Picker=null; Roam=null; Feedback=null; Diff=null; SceneSource=null; Annotations=null;
        }

        /// <summary>Answer the start-up question; null or an unknown id opens the configured default space.</summary>
        public void ChooseSpace(string id)
        {
            if (spaceChoice == null || Spaces == null) return;
            var space = Spaces.Find(id);
            bool usable(TwinSpace s) => TwinPaths.Available(settings, s);
            spaceChoice.TrySetResult(space != null && usable(space) ? space : Spaces.Default(settings.DefaultSpace, usable));
        }

        void EnsureUI() { if (!UI) { UI = gameObject.AddComponent<InspectorUI>(); UI.Initialize(this); } }
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
            // PermissionDenied covers denial including the permanent-denial path.
            // The deprecated DontAskAgain callback is unreliable on current Android.
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
