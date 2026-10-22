using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace TwinInspector
{
    /// <summary>Editor-only free flight; an actively tracked HMD takes control automatically.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class EditorDesktopDriver : MonoBehaviour
    {
        AppBootstrap app;
        TrackedPoseDriver poseDriver;
        bool wasDesktop, flying;
        float yaw, pitch;
        float previousCameraOffset;
#if UNITY_EDITOR
        bool simulatorStarted;
#endif
        public float MoveSpeed { get; private set; } = 2;
        public void Initialize(AppBootstrap application) { app=application; poseDriver=app.Viewer.GetComponent<TrackedPoseDriver>(); }
        void Update()
        {
            if(!app || !app.Input) return;
#if UNITY_EDITOR
            if(app.Ready && !simulatorStarted) {simulatorStarted=true;var simulator=new GameObject("Editor scene surface simulator").AddComponent<EditorSceneSurfaceSimulator>();app.SceneSource=simulator;_ = simulator.InitializeAsync(app);}
#endif
            bool desktop=app.Input.Desktop;
            if(poseDriver) poseDriver.enabled=!desktop;
            if(!desktop) { if(wasDesktop) { ReleaseCursor(); app.Origin.CameraYOffset=previousCameraOffset; } wasDesktop=false; return; }
            var camera=app.Viewer.transform;
            if(!wasDesktop)
            {
                previousCameraOffset=app.Origin.CameraYOffset; app.Origin.CameraYOffset=0;
                camera.localPosition=new Vector3(0,1.6f,0); camera.localRotation=Quaternion.identity;
                yaw=camera.eulerAngles.y; pitch=0; wasDesktop=true;
            }
            var mouse=Mouse.current; var keys=Keyboard.current;
            // XROrigin may apply its device-height offset after our first frame.
            camera.localPosition=new Vector3(0,1.6f-app.Origin.CameraFloorOffsetObject.transform.localPosition.y,0);
            if(mouse==null || keys==null) return;
            flying=mouse.rightButton.isPressed && (Application.isFocused || Application.isBatchMode);
            Cursor.lockState=flying ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible=!flying;
            if(flying)
            {
                var delta=mouse.delta.ReadValue(); yaw+=delta.x*.12f; pitch=Mathf.Clamp(pitch-delta.y*.12f,-85,85);
                camera.rotation=Quaternion.Euler(pitch,yaw,0);
                MoveSpeed=Mathf.Clamp(MoveSpeed*Mathf.Pow(1.2f,mouse.scroll.ReadValue().y/120f),.1f,20);
            }
            var forward=Vector3.ProjectOnPlane(camera.forward,Vector3.up).normalized;
            var right=Vector3.Cross(Vector3.up,forward);
            float x=(keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0), z=(keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0);
            float y=(keys.eKey.isPressed||keys.spaceKey.isPressed?1:0)-(keys.qKey.isPressed||keys.leftShiftKey.isPressed?1:0);
            if(Application.isFocused || Application.isBatchMode) app.Origin.transform.position+=Vector3.ClampMagnitude(right*x+forward*z+Vector3.up*y,1)*MoveSpeed*(keys.altKey.isPressed?3:1)*Time.unscaledDeltaTime;
            if(!app.Ready || !app.UI) return;
            if(keys.digit1Key.wasPressedThisFrame) app.UI.Show("mode");
            if(keys.digit2Key.wasPressedThisFrame) app.UI.Show("align");
            if(keys.digit3Key.wasPressedThisFrame) app.UI.BeginAnnotation();
            if(keys.digit4Key.wasPressedThisFrame) app.UI.Show("list");
            if(keys.mKey.wasPressedThisFrame) { app.Markers.SimulateFirst(); app.UI.Show("marker"); }
            if(keys.tabKey.wasPressedThisFrame) app.UI.Summon();
        }
        void OnGUI()
        {
            if(!app || !app.Input || !app.Input.Desktop || !app.Performance || !app.Performance.Visible) return;
            var style=new GUIStyle(GUI.skin.label) { fontSize=12, normal={textColor=new Color(.067f,.067f,.067f)} };
            GUI.color=new Color(.949f,.945f,.925f); GUI.DrawTexture(new Rect(12,Screen.height-60,650,48),Texture2D.whiteTexture); GUI.color=Color.white;
            GUI.Label(new Rect(20,Screen.height-56,630,44),L.Format("desktop.hint",MoveSpeed),style);
        }
        void ReleaseCursor() { flying=false; Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
        void OnDisable() { if(Application.isEditor) ReleaseCursor(); }
    }
}
