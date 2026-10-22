using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace TwinInspector
{
    /// <summary>Ownership is chosen before XRI UI; hits are consumed after XRI's raycast.</summary>
    [DefaultExecutionOrder(-29983)]
    public sealed class InputRouter : MonoBehaviour
    {
        public const int BuildingMask=1<<8;
        public const int SurfaceMask=BuildingMask | (1<<9);
        XROrigin origin; Camera viewer; XRInputModalityManager modality;
        readonly List<XRRayInteractor> rays=new List<XRRayInteractor>();
        readonly List<TrackedRayPose> sources=new List<TrackedRayPose>();
        readonly List<XRHandSubsystem> hands=new List<XRHandSubsystem>();
        readonly List<RaycastResult> uiHits=new List<RaycastResult>();
        PointerEventData mouseEvent;
        XRRayInteractor owner;
        XRPokeInteractor uiPoke;
        float discoverAt;
        bool focused=true, uiCapture, lastDesktop;
        int consumedFrame=-1;
        public bool Desktop { get; private set; }
        public bool OverUI { get; private set; }
        public bool Pressed { get; private set; }
        public bool Held { get; private set; }
        public float Strength { get; private set; }
        public bool HasHit { get; private set; }
        public RaycastHit Hit { get; private set; }
        public bool HasBuildingHit { get; private set; }
        public RaycastHit BuildingHit { get; private set; }
        public Ray Aim { get; private set; }
        public Vector3 PointerPosition { get; private set; }
        public Vector3 PointerNormal { get; private set; }
        public bool HasPointer { get; private set; }
        public bool Valid { get; private set; }
        public bool Busy => Held || Strength>=.3f || OverUI;
        public IReadOnlyList<XRRayInteractor> Rays => rays;
        public bool ActiveLeft => owner && IsLeft(owner);
        public void Initialize(XROrigin rig,Camera camera)
        { origin=rig; viewer=camera; modality=rig.GetComponent<XRInputModalityManager>(); Discover(); UpdateDesktopMode(); }
        void Discover()
        {
            rays.Clear(); sources.Clear();
            if(origin) origin.GetComponentsInChildren(true,rays);
            foreach(var ray in rays) sources.Add(ray.GetComponent<TrackedRayPose>());
            discoverAt=Time.unscaledTime+1;
        }
        void UpdateDesktopMode()
        {
            var hmd=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            var inputHmd=UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRHMD>();
            bool headTracked=hmd.isValid && hmd.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out var tracked) && tracked;
            headTracked|=inputHmd!=null && inputHmd.enabled && inputHmd.isTracked.isPressed;
            Desktop=Application.isEditor && !headTracked;
            if(Desktop!=lastDesktop) { ResetInput(); lastDesktop=Desktop; }
        }
        public static int ChooseOwner(int current,bool leftAvailable,bool rightAvailable,float leftStrength,float rightStrength,bool preferLeft)
        {
            if(current==0 && leftAvailable && leftStrength>=.3f || current==1 && rightAvailable && rightStrength>=.3f) return current;
            if(leftAvailable && leftStrength>=.4f) return 0;
            if(rightAvailable && rightStrength>=.4f) return 1;
            return preferLeft ? (leftAvailable?0:rightAvailable?1:-1) : (rightAvailable?1:leftAvailable?0:-1);
        }
        bool IsLeft(XRRayInteractor ray) => ray && ray.handedness==UnityEngine.XR.Interaction.Toolkit.Interactors.InteractorHandedness.Left;
        bool Available(XRRayInteractor ray)
        {
            if(!ray || !ray.isActiveAndEnabled) return false;
            int index=rays.IndexOf(ray); return index>=0 && sources[index] && sources[index].Ready;
        }
        void Update()
        {
            if(!origin || !viewer) return;
            UpdateDesktopMode(); if(Time.unscaledTime>=discoverAt) Discover();
            if(Desktop || !focused) { owner=null; foreach(var ray in rays) if(ray) ray.enableUIInteraction=false; return; }
            var left=ActiveRay(true); var right=ActiveRay(false);
            int current=owner ? (IsLeft(owner)?0:1) : -1;
            int chosen=ChooseOwner(current,Available(left),Available(right),left?left.selectInput.ReadValue():0,right?right.selectInput.ReadValue():0,InteractionPreferences.LeftHand);
            owner=chosen==0?left:chosen==1?right:null;
            foreach(var ray in rays) if(ray) ray.enableUIInteraction=ray==owner;
        }
        void LateUpdate()
        {
            OverUI=Pressed=Held=HasHit=HasPointer=HasBuildingHit=Valid=false; Strength=0;
            if(!viewer || !focused) return;
            if(Desktop)
            {
                var mouse=Mouse.current;
                if(mouse==null || mouse.rightButton.isPressed) { uiCapture=false; return; }
                Valid=true; Pressed=mouse.leftButton.wasPressedThisFrame; Held=mouse.leftButton.isPressed; Strength=Held?1:0;
                if(EventSystem.current)
                {
                    mouseEvent ??= new PointerEventData(EventSystem.current);
                    mouseEvent.Reset(); mouseEvent.position=mouse.position.ReadValue(); uiHits.Clear();
                    EventSystem.current.RaycastAll(mouseEvent,uiHits);
                    OverUI=uiHits.Count>0;
                    if(OverUI) SetPointer(uiHits[0].worldPosition,uiHits[0].worldNormal);
                }
                Aim=viewer.ScreenPointToRay(mouse.position.ReadValue());
                HasHit=Physics.Raycast(Aim,out var hit,80,SurfaceMask,QueryTriggerInteraction.Ignore); if(HasHit) Hit=hit;
            }
            else
            {
                if(!Available(owner))
                {
                    if(uiPoke && uiPoke.isActiveAndEnabled && uiPoke.TryGetUIModel(out var pokeModel) && pokeModel.select)
                    { Valid=Held=OverUI=true; Strength=1; return; }
                    uiCapture=false; uiPoke=null; return;
                }
                Valid=true;
                Pressed=owner.selectInput.ReadWasPerformedThisFrame() || owner.uiPressInput.ReadWasPerformedThisFrame();
                Held=owner.selectInput.ReadIsPerformed() || owner.uiPressInput.ReadIsPerformed(); Strength=Mathf.Clamp01(owner.selectInput.ReadValue());
                Aim=new Ray(owner.rayOriginTransform.position,owner.rayOriginTransform.forward);
                HasHit=owner.TryGetCurrent3DRaycastHit(out var hit) && hit.collider && (SurfaceMask&(1<<hit.collider.gameObject.layer))!=0;
                if(HasHit) Hit=hit;
                // Compare the live straight-ray results. XRI's combined visual cache
                // can lag after a canvas is replaced; no sample line visual maintains it.
                OverUI=owner.TryGetCurrentUIRaycastResult(out var ui) && (!HasHit || ui.distance<=hit.distance+.001f);
                if(OverUI) SetPointer(ui.worldPosition,ui.worldNormal);
            }
            if(OverUI && (Held || Strength>=.3f)) uiCapture=true;
            OverUI|=uiCapture;
            if(!Held && Strength<.3f) uiCapture=false;
            HasBuildingHit=Physics.Raycast(Aim,out var building,80,BuildingMask,QueryTriggerInteraction.Ignore);
            if(HasBuildingHit) BuildingHit=building;
            if(!OverUI && HasHit) SetPointer(Hit.point,Hit.normal);
            if(consumedFrame==Time.frameCount) Pressed=false;
        }
        public bool ConsumePress() { if(!Pressed || OverUI || consumedFrame==Time.frameCount) return false; consumedFrame=Time.frameCount; Pressed=false; return true; }
        public void CaptureUI(PointerEventData data=null) { uiCapture=true; consumedFrame=Time.frameCount; uiPoke=(data as TrackedDeviceEventData)?.interactor as XRPokeInteractor; }
        void SetPointer(Vector3 position,Vector3 normal)
        { HasPointer=true; PointerPosition=position; PointerNormal=Vector3.Dot(normal,viewer.transform.position-position)<0?-normal:normal; }
        XRRayInteractor ActiveRay(bool left)
        {
            if(!modality) return null;
            var hand=left?modality.leftHand:modality.rightHand; var controller=left?modality.leftController:modality.rightController;
            foreach(var ray in rays) if(ray && ray.isActiveAndEnabled && ((hand && ray.transform.IsChildOf(hand.transform)) || (controller && ray.transform.IsChildOf(controller.transform)))) return ray;
            return null;
        }
        public bool TrySelectPose(bool left,out Vector3 position,bool handsOnly=false)
        {
            position=default; if(Desktop || !focused) return false;
            var ray=ActiveRay(left); if(!Available(ray) || !ray.selectInput.ReadIsPerformed()) return false;
            var hand=left?modality.leftHand:modality.rightHand;
            if(hand && ray.transform.IsChildOf(hand.transform))
            { if(TryPalm(left,out var palm)) { position=palm.position; return true; } }
            if(handsOnly) return false;
            int index=rays.IndexOf(ray); position=sources[index].Source.position; return true;
        }
        public bool TrySingleSelect(out Vector3 position) => TrySelectPose(ActiveLeft,out position);
        public bool TryTwoHandSelect(out Vector3 left,out Vector3 right,bool handsOnly=false)
        { bool l=TrySelectPose(true,out left,handsOnly); bool r=TrySelectPose(false,out right,handsOnly); return l&&r; }
        public bool TryPalm(bool left,out Pose pose)
        {
            pose=default; if(Desktop || !origin || !focused) return false;
            var ray=ActiveRay(left); if(!ray || !ray.gameObject.activeInHierarchy || !ray.enabled) return false; // Includes system gesture cancellation.
            SubsystemManager.GetSubsystems(hands);
            foreach(var system in hands)
            {
                var hand=left?system.leftHand:system.rightHand;
                if(!system.running || !hand.isTracked || !hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var local)) continue;
                var space=origin.CameraFloorOffsetObject.transform;
                pose=new Pose(space.TransformPoint(local.position),space.rotation*local.rotation); return true;
            }
            return false;
        }
        void ResetInput()
        { owner=null; uiPoke=null; uiCapture=false; Valid=Pressed=Held=HasHit=HasPointer=HasBuildingHit=false; Strength=0; foreach(var source in sources) if(source) source.ResetPose(); }
        void OnDisable() => ResetInput();
        void OnApplicationFocus(bool value) { focused=value; ResetInput(); }
    }
}
