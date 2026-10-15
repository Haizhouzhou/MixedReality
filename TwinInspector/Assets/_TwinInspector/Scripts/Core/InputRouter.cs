using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwinInspector
{
    /// <summary>One input boundary for desktop, hand pinch and optional controller trigger.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class InputRouter : MonoBehaviour
    {
        public const int SurfaceMask = (1 << 8) | (1 << 9) | (1 << 10);
        XROrigin origin;
        Camera viewer;
        XRInputModalityManager modality;
        readonly List<XRRayInteractor> rays = new List<XRRayInteractor>();
        readonly List<XRHandSubsystem> hands = new List<XRHandSubsystem>();
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        float discoverAt;
        public bool Desktop { get; private set; }
        public bool OverUI { get; private set; }
        public bool Pressed { get; private set; }
        public bool Held { get; private set; }
        public bool HasHit { get; private set; }
        public RaycastHit Hit { get; private set; }
        public Vector3 PointerPosition { get; private set; }
        public Vector3 PointerNormal { get; private set; }
        public bool HasPointer { get; private set; }
        public IReadOnlyList<XRRayInteractor> Rays => rays;
        public void Initialize(XROrigin rig, Camera camera)
        {
            origin = rig; viewer = camera; modality = rig.GetComponent<XRInputModalityManager>();
            Discover(); UpdateDesktopMode();
        }
        void Discover()
        {
            rays.Clear();
            if(origin) origin.GetComponentsInChildren(true, rays);
            discoverAt = Time.unscaledTime + 1;
        }
        void UpdateDesktopMode()
        {
            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            Desktop = Application.isEditor && !(hmd.isValid && hmd.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracked) && tracked);
        }
        void Update()
        {
            if(!origin || !viewer) return;
            UpdateDesktopMode();
            if(Time.unscaledTime >= discoverAt) Discover();
            OverUI = Pressed = Held = HasHit = HasPointer = false;
            if(Desktop)
            {
                var mouse = Mouse.current;
                if(mouse == null || mouse.rightButton.isPressed) return;
                Pressed = mouse.leftButton.wasPressedThisFrame; Held = mouse.leftButton.isPressed;
                if(EventSystem.current)
                {
                    uiHits.Clear();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = mouse.position.ReadValue() }, uiHits);
                    OverUI = uiHits.Count > 0 || EventSystem.current.IsPointerOverGameObject();
                }
                var ray = viewer.ScreenPointToRay(mouse.position.ReadValue());
                RaycastHit hit = default;
                HasHit = !OverUI && Physics.Raycast(ray, out hit, 80, SurfaceMask, QueryTriggerInteraction.Collide);
                if(HasHit) { Hit = hit; SetPointer(hit.point, hit.normal); }
                return;
            }
            // Prefer the interactor that actually confirmed, then a valid UI/surface ray.
            XRRayInteractor candidate = null;
            foreach(var ray in rays)
            {
                if(!ray || !ray.isActiveAndEnabled) continue;
                bool press = ray.selectInput.ReadWasPerformedThisFrame() || ray.uiPressInput.ReadWasPerformedThisFrame();
                if(press) { candidate = ray; break; }
                if(candidate == null && (ray.TryGetCurrentUIRaycastResult(out _) || ray.TryGetCurrent3DRaycastHit(out _))) candidate = ray;
            }
            if(!candidate) return;
            Pressed = candidate.selectInput.ReadWasPerformedThisFrame() || candidate.uiPressInput.ReadWasPerformedThisFrame();
            Held = candidate.selectInput.ReadIsPerformed() || candidate.uiPressInput.ReadIsPerformed();
            candidate.TryGetCurrentRaycast(out _,out _,out _,out _,out bool uiClosest);
            OverUI = candidate.TryGetCurrentUIRaycastResult(out var ui) && uiClosest;
            if(OverUI) { SetPointer(ui.worldPosition, ui.worldNormal); return; }
            HasHit = candidate.TryGetCurrent3DRaycastHit(out var surface) && surface.collider && (SurfaceMask & (1 << surface.collider.gameObject.layer)) != 0;
            if(HasHit) { Hit = surface; SetPointer(surface.point, surface.normal); }
        }
        void SetPointer(Vector3 position, Vector3 normal)
        {
            HasPointer = true; PointerPosition = position;
            PointerNormal = Vector3.Dot(normal, viewer.transform.position-position) < 0 ? -normal : normal;
        }
        XRRayInteractor ActiveRay(bool left)
        {
            if(!modality) return null;
            var hand = left ? modality.leftHand : modality.rightHand;
            var controller = left ? modality.leftController : modality.rightController;
            foreach(var ray in rays)
                if(ray && ray.isActiveAndEnabled && ((hand && ray.transform.IsChildOf(hand.transform)) || (controller && ray.transform.IsChildOf(controller.transform)))) return ray;
            return null;
        }
        public bool TryTwoHandSelect(out Vector3 left, out Vector3 right, bool handsOnly = false)
        {
            left = right = default;
            if(Desktop) return false;
            var l = ActiveRay(true); var r = ActiveRay(false);
            if(!l || !r || !l.selectInput.ReadIsPerformed() || !r.selectInput.ReadIsPerformed()) return false;
            bool bothHands=modality && modality.leftHand && modality.rightHand && l.transform.IsChildOf(modality.leftHand.transform) && r.transform.IsChildOf(modality.rightHand.transform);
            if(bothHands && TryPalm(true, out var lp) && TryPalm(false, out var rp)) { left=lp.position; right=rp.position; return true; }
            if(handsOnly) return false;
            left = l.transform.position; right = r.transform.position; return true;
        }
        public bool TryPalm(bool left, out Pose pose)
        {
            pose = default;
            if(Desktop || !origin) return false;
            SubsystemManager.GetSubsystems(hands);
            foreach(var system in hands)
            {
                var hand = left ? system.leftHand : system.rightHand;
                if(!system.running || !hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var local)) continue;
                var space = origin.CameraFloorOffsetObject.transform;
                pose = new Pose(space.TransformPoint(local.position), space.rotation * local.rotation); return true;
            }
            return false;
        }
    }
}
