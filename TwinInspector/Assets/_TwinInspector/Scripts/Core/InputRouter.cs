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
        /// <summary>Near poke on panels, chosen in Preferences > Input (Pinch, Tap or Both). Off: the poke interactors and the
        /// pointing-gesture switch that turns the hand ray off are disabled. On: both work as in the original rig, and the hand ray
        /// is switched off while the hand makes a pointing (poke) shape, so a tap cannot also count as a pinch.</summary>
        public static bool PokeEnabled => InteractionPreferences.Tap;
        /// <summary>Hand rays press panels by pinching (Pinch or Both). Off (Tap only): hand rays do not press panels, but still
        /// pinch the twin; controllers are unaffected.</summary>
        public static bool PinchOnPanels => InteractionPreferences.Pinch;
        bool? pokeApplied;
        readonly LineRenderer[] handLines=new LineRenderer[2];
        readonly string[] handState={"",""};
        readonly float[] handSince={0,0};
        readonly Dictionary<XRRayInteractor,bool> pinching=new Dictionary<XRRayInteractor,bool>();
        bool pinchPressReady;
        readonly List<PanelBase> panels=new List<PanelBase>();
        /// <summary>The Galaxy XR reports a relaxed hand at 0.2 to 0.35 pinch strength and presses at 0.5 (the press point the
        /// pinch buttons were redirected to). The thresholds here and in TrackedRayPose (0.3 rest, 0.4 about to press) were made
        /// for a 0-to-1 range, so a relaxed hand often counted as "pinching": the ray was not ready, kept its owner or was damped.
        /// Rescaled so that 0.3 reads as 0 and the press point stays at 0.5.</summary>
        public static float HandPinch(float raw) => Mathf.Clamp01((raw-.3f)*2.5f);
        float Pinch(XRRayInteractor ray)
        {
            if(!ray) return 0; int index=rays.IndexOf(ray); float raw=ray.selectInput.ReadValue();
            return index>=0 && sources[index] && sources[index].Hand ? HandPinch(raw) : Mathf.Clamp01(raw);
        }
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
            for(int i=0;i<rays.Count;i++)
                // Hands press UI with the same pinch as select. The sample binds UI press to the hand-interaction "aim activate"
                // signal, which is not the pinch on every runtime.
                if(rays[i] && sources[i] && sources[i].Hand && rays[i].uiPressInput!=rays[i].selectInput) rays[i].uiPressInput=rays[i].selectInput;
            if(origin && pokeApplied!=PokeEnabled) ApplyPressMode();
            if(!pinchPressReady) pinchPressReady=PressFromPinchValue();
            if(pinchPressReady) LinkHandReaders();
            panels.Clear(); panels.AddRange(FindObjectsByType<PanelBase>(FindObjectsInactive.Exclude,FindObjectsSortMode.None));
            discoverAt=Time.unscaledTime+1;
        }
        /// <summary>On the Galaxy XR the hand's pinch strength arrives (0.6 to 0.85 when pinched) but its pinch button never fires,
        /// so select and UI press stayed off. The button bindings of the hand select and UI press actions are redirected to the
        /// strength: a button action bound to an analog control presses at the Input System's press point (0.5) and releases below it.</summary>
        static bool PressFromPinchValue()
        {
            int redirected=0;
            foreach(var action in UnityEngine.InputSystem.InputSystem.ListEnabledActions())
            {
                var map=action.actionMap?.name; if(map==null || !map.StartsWith("XRI ") || !map.EndsWith(" Interaction")) continue;
                if(action.name!="Select" && action.name!="UI Press" && action.name!="Select Value") continue;
                for(int i=0;i<action.bindings.Count;i++)
                {
                    var binding=action.bindings[i]; string path=binding.path; // the original path, also when re-run
                    if(binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(path)) continue;
                    bool hand=path.StartsWith("<HandInteraction>") || path.StartsWith("<XRHandDevice>");
                    if(!hand) continue;
                    if(path.EndsWith("/pinchTouched") || path.EndsWith("/pointerActivated"))
                    { action.ApplyBindingOverride(i,path.Substring(0,path.LastIndexOf('/'))+"/pinchValue"); redirected++; }
                    // Grasp (curled fingers, a fist) also drove select and the pinch strength (Nov 21 log: graspvalue 0.6-0.93 with
                    // pinchvalue near 0 while raising a palm). It made the idle hand take the pointer and a curled hand drag the
                    // twin. Hands select by pinching only; an empty override path disables a binding.
                    else if(path.EndsWith("/graspFirm") || path.EndsWith("/graspValue"))
                    { action.ApplyBindingOverride(i,""); redirected++; }
                }
            }
            if(redirected>0) DevLog.Write("Input: hand select and UI press follow pinch strength, grasp ignored ("+redirected+" bindings)");
            return redirected>0;
        }
        /// <summary>Switches poke (and the pointing gesture that hands the press over to it) on or off to match the preference.
        /// Called again within a second of a change in Preferences, or at once through <see cref="RefreshPressMode"/>.</summary>
        void ApplyPressMode()
        {
            bool on=PokeEnabled; pokeApplied=on;
            foreach(var poke in origin.GetComponentsInChildren<XRPokeInteractor>(true)) poke.enabled=on;
            // The sample's pointing-gesture detector switched the hand ray off whenever the hand looked like pointing, which also
            // happened on the way into a pinch and dropped it. Near/far by distance to the menu (UpdateNearFar) replaces it.
            foreach(var component in origin.GetComponentsInChildren<MonoBehaviour>(true))
                if(component && component.GetType().Name=="PokeGestureDetector") component.enabled=false;
            for(int i=0;i<rays.Count;i++) if(rays[i] && sources[i] && sources[i].Hand && !rays[i].enabled) rays[i].enabled=true;
            handNear[0]=handNear[1]=false;
            DevLog.Write("Input: "+InteractionPreferences.Press+(on?", the fingertip presses panels":"")+(PinchOnPanels?", the hand ray presses panels":""));
        }
        public void RefreshPressMode() { if(origin && pokeApplied!=PokeEnabled) ApplyPressMode(); }
        /// <summary>One log line when a hand starts or ends a pinch: what the ray saw and which inputs fired.</summary>
        void LogPinches()
        {
            foreach(var ray in rays)
            {
                if(!ray) continue; int index=rays.IndexOf(ray); var source=sources[index]; if(!source || !source.Hand) continue;
                float value=ray.isActiveAndEnabled ? ray.selectInput.ReadValue() : 0;
                pinching.TryGetValue(ray,out bool was); bool now=was ? value>.3f : value>.6f;
                if(now==was) continue; pinching[ray]=now;
                bool ui=ray.isActiveAndEnabled && ray.TryGetCurrentUIRaycastResult(out _);
                DevLog.Write($"Pinch {(IsLeft(ray)?"left":"right")} {(now?"start":"end")}: value={value:F2} select={ray.selectInput.ReadIsPerformed()} uiPress={ray.uiPressInput.ReadIsPerformed()} enabled={ray.isActiveAndEnabled} ready={source.Ready} owner={ray==owner} uiInteraction={ray.enableUIInteraction} overPanel={ui} | {PinchControls(IsLeft(ray))}");
            }
        }
        /// <summary>Every control behind the hand's Select and Select Value actions with its current value, to see which one
        /// the logged "value" comes from and at which value the press fires (Nov 21: presses only near 0.95-1.0).</summary>
        static string PinchControls(bool left)
        {
            var text=new System.Text.StringBuilder(); string map=left?"XRI Left Interaction":"XRI Right Interaction";
            foreach(var action in UnityEngine.InputSystem.InputSystem.ListEnabledActions())
            {
                if(action.actionMap?.name!=map || action.name!="Select" && action.name!="Select Value") continue;
                text.Append(action.name).Append(action.IsPressed()?"[pressed]":"").Append(": ");
                foreach(var control in action.controls)
                {
                    object value=null; try { value=control.ReadValueAsObject(); } catch(System.Exception) { }
                    text.Append(control.device.layout).Append('/').Append(control.name).Append('=')
                        .Append(value is float f ? f.ToString("F2",System.Globalization.CultureInfo.InvariantCulture) : value?.ToString() ?? "?").Append(' ');
                }
                text.Append("; ");
            }
            return text.ToString();
        }
        /// <summary>A thin line from the hand pointing at a panel, so a pinch's target is visible. A hand the app has just found
        /// shows a 25 cm stub for one second, so the viewer sees it is tracked; a stub always on looked odd. Drawn after all
        /// input work, also when no hand owns the pointer (the early returns in LateUpdate).</summary>
        const float StubLength=.25f, StubSeconds=1;
        void DrawHandLines()
        {
            for(int side=0;side<2;side++)
            {
                var ray=Desktop || !focused ? null : ActiveRay(side==0);
                LogHandState(side,ray);
                // Aiming from the plane test, not XRI's UI raycast: that one runs only for the owning ray and often reported no hit
                // while the hand pointed at the panel (Nov 21 log), which hid the line.
                Vector3 panelPoint=default; bool aiming=PinchOnPanels && PanelHit(ray,out panelPoint);
                // A hand within reach of a panel is tapping, not pointing: its aim pose then runs along the panel, not at it.
                bool tapping=handNear[side];
                if(!ray || !Available(ray) || tapping || !aiming && Time.unscaledTime-handSince[side]>StubSeconds) { if(handLines[side]) handLines[side].enabled=false; continue; }
                var line=handLines[side];
                if(!line)
                {
                    line=handLines[side]=new GameObject(side==0?"Left hand line":"Right hand line").AddComponent<LineRenderer>();
                    line.useWorldSpace=true; line.positionCount=2; line.startWidth=.002f; line.endWidth=.001f;
                    line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows=false;
                }
                // A line made before the design resources loaded (a hand found during start-up) had no material and drew in
                // Unity's magenta "missing material" colour; assign the ink whenever it is still missing.
                if(!line.sharedMaterial) { var tokens=AppBootstrap.Instance ? AppBootstrap.Instance.Tokens : null; if(tokens) line.sharedMaterial=tokens.InkMaterial; }
                var start=ray.rayOriginTransform.position;
                var end=ray==owner && OverUI && HasPointer ? PointerPosition : aiming ? panelPoint : start+ray.rayOriginTransform.forward*StubLength;
                line.enabled=true; line.SetPosition(0,start); line.SetPosition(1,end);
            }
        }
        /// <summary>One log line whenever a hand's ray appears or disappears, with the reason it is missing.</summary>
        void LogHandState(int side,XRRayInteractor ray)
        {
            string state;
            if(Desktop) state="desktop";
            else if(!focused) state="app not focused";
            else if(!ray)
            {
                // ActiveRay only returns enabled rays: tell "hand not tracked" (group off) from "ray switched off".
                var group=side==0?modality?.leftHand:modality?.rightHand;
                state=!group || !group.activeInHierarchy ? "hand not tracked (input modality off)" : "ray switched off (system gesture or palm facing you)";
            }
            else
            {
                int index=rays.IndexOf(ray); var source=index>=0?sources[index]:null;
                state=!source ? "no ray pose" : !source.Source || !source.Source.gameObject.activeInHierarchy ? "aim pose not tracked"
                    : !source.Ready ? "waiting for a relaxed hand" : "shown";
            }
            if(state==handState[side]) return;
            // The 1 s stub is for a hand that was just found, not for the ray coming back after a tap (pointing shape released).
            bool found=!handState[side].StartsWith("ray switched off") && !handState[side].StartsWith("waiting");
            handState[side]=state; DevLog.Write($"Hand ray {(side==0?"left":"right")}: {state}");
            if(state=="shown" && found) handSince[side]=Time.unscaledTime;
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
            => ChooseOwner(current,leftAvailable,rightAvailable,leftStrength,rightStrength,preferLeft,false,false);
        /// <param name="leftAims">The left hand's ray points at a menu panel (likewise <paramref name="rightAims"/>).</param>
        public static int ChooseOwner(int current,bool leftAvailable,bool rightAvailable,float leftStrength,float rightStrength,bool preferLeft,bool leftAims,bool rightAims)
        {
            bool left=leftAvailable && leftAims, right=rightAvailable && rightAims;
            // A hand pointing at a panel beats one that only pinches: after a palm summon the lowered palm hand reads 0.4-0.9
            // pinch strength without pinching and took the pointer, so the other hand's pinches on the menu did nothing (Nov 21).
            bool currentAims=current==0?left:current==1&&right, otherAims=current==0?right:current==1&&left;
            if((current==0 && leftAvailable && leftStrength>=.3f || current==1 && rightAvailable && rightStrength>=.3f) && (currentAims || !otherAims)) return current;
            if(leftAvailable && leftStrength>=.4f && (left || !right)) return 0;
            if(rightAvailable && rightStrength>=.4f && (right || !left)) return 1;
            // Nobody pinches: the hand pointing at a panel gets the pointer, so the line appears for the hand in use and not
            // only for the preferred one; when both point, the current one keeps it.
            if(left && !right) return 0;
            if(right && !left) return 1;
            if(left && right && current>=0) return current;
            return preferLeft ? (leftAvailable?0:rightAvailable?1:-1) : (rightAvailable?1:leftAvailable?0:-1);
        }
        /// <summary>Whether the ray meets one of the visible menu panels within 5 m (a plane test, before XRI's UI raycast,
        /// which only runs for the owning ray).</summary>
        bool PointsAtPanel(XRRayInteractor ray) => PanelHit(ray,out _);
        /// <summary>Per hand: the fingertip is within reach of a menu panel, so that hand taps (poke on, ray off for the menu).</summary>
        readonly bool[] handNear=new bool[2];
        const float NearEnter=.15f, NearLeave=.25f;
        /// <summary>Tap or Both: each hand taps when its fingertip is near a panel and pinches with the ray otherwise, decided by
        /// distance with hysteresis (15 cm in, 25 cm out). Only one of the two can press at a time, so they no longer compete.</summary>
        void UpdateNearFar()
        {
            if(!PokeEnabled || !modality) { handNear[0]=handNear[1]=false; return; }
            for(int side=0;side<2;side++)
            {
                var group=side==0?modality.leftHand:modality.rightHand; if(!group) continue;
                var pokes=group.GetComponentsInChildren<XRPokeInteractor>(true); if(pokes.Length==0) continue;
                var tip=pokes[0].attachTransform ? pokes[0].attachTransform.position : pokes[0].transform.position;
                float distance=DistanceToPanel(tip);
                bool near=!PinchOnPanels || (handNear[side] ? distance<NearLeave : distance<NearEnter); // Tap only: always the fingertip
                if(near!=handNear[side]) DevLog.Write($"Input: {(side==0?"left":"right")} hand {(near?"taps (near the menu)":"points (away from the menu)")}");
                handNear[side]=near;
                foreach(var poke in pokes) if(poke.enabled!=near) poke.enabled=near;
            }
        }
        /// <summary>Distance from the point to the nearest visible panel whose rectangle it lies in front of or behind; infinity if none.</summary>
        float DistanceToPanel(Vector3 point)
        {
            float best=float.PositiveInfinity;
            foreach(var panel in panels)
            {
                if(!panel || !panel.isActiveAndEnabled) continue;
                var rect=(RectTransform)panel.transform;
                if(!rect.rect.Contains((Vector2)rect.InverseTransformPoint(point))) continue;
                best=Mathf.Min(best,Mathf.Abs(new Plane(rect.forward,rect.position).GetDistanceToPoint(point)));
            }
            return best;
        }
        /// <summary>The point lies in front of or behind a visible panel, within 20 cm of its surface.</summary>
        bool NearPanel(Vector3 point)
        {
            foreach(var panel in panels)
            {
                if(!panel || !panel.isActiveAndEnabled) continue;
                var rect=(RectTransform)panel.transform; var local=rect.InverseTransformPoint(point);
                if(Mathf.Abs(new Plane(rect.forward,rect.position).GetDistanceToPoint(point))<.2f && rect.rect.Contains((Vector2)local)) return true;
            }
            return false;
        }
        readonly float[] aimSince={-1,-1};
        /// <summary>Pointing at a panel for at least 0.12 s: a hand passing through the panel (lowering a palm) only flickered
        /// "aims" and made the pointer jump between hands.</summary>
        bool SteadyAim(int side,XRRayInteractor ray)
        {
            if(!PointsAtPanel(ray)) { aimSince[side]=-1; return false; }
            if(aimSince[side]<0) aimSince[side]=Time.unscaledTime;
            return Time.unscaledTime-aimSince[side]>=.12f;
        }
        /// <summary>Plane test against the visible menu panels; <paramref name="point"/> is where the ray meets the nearest one.</summary>
        bool PanelHit(XRRayInteractor ray,out Vector3 point)
        {
            point=default; float nearest=float.MaxValue;
            if(!Available(ray)) return false;
            var aim=new Ray(ray.rayOriginTransform.position,ray.rayOriginTransform.forward);
            foreach(var panel in panels)
            {
                if(!panel || !panel.isActiveAndEnabled) continue;
                var group=panel.GetComponent<CanvasGroup>(); if(group && !group.blocksRaycasts) continue;
                var rect=(RectTransform)panel.transform;
                if(!new Plane(rect.forward,rect.position).Raycast(aim,out float distance) || distance>5) continue;
                if(distance<nearest && rect.rect.Contains((Vector2)rect.InverseTransformPoint(aim.GetPoint(distance)))) { nearest=distance; point=aim.GetPoint(distance); }
            }
            return nearest<float.MaxValue;
        }
        readonly HashSet<XRRayInteractor> linked=new HashSet<XRRayInteractor>();
        /// <summary>Nov 21 logs: the hand's Select action was pressed from 0.5 pinch strength ("Select[pressed]"), but the ray's
        /// select reader only fired near 1.0. The reader is in ObjectReference mode: it reads an object from the sample rig that
        /// decides "pressed" by itself, not the redirected actions. Each hand ray's reader is switched to read the hand's Select
        /// (press) and Select Value (strength) actions directly. The mode is set by reflection because its enum type is nested
        /// in the XRI reader classes.</summary>
        void LinkHandReaders()
        {
            for(int i=0;i<rays.Count;i++)
            {
                var ray=rays[i]; if(!ray || !sources[i] || !sources[i].Hand || linked.Contains(ray)) continue;
                string map=IsLeft(ray)?"XRI Left Interaction":"XRI Right Interaction"; InputAction select=null, value=null;
                foreach(var action in UnityEngine.InputSystem.InputSystem.ListEnabledActions())
                    if(action.actionMap?.name==map) { if(action.name=="Select") select=action; else if(action.name=="Select Value") value=action; }
                if(select==null || value==null) continue;
                linked.Add(ray);
                var reader=ray.selectInput; var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                var modeProperty=reader.GetType().GetProperty("inputSourceMode",flags);
                string mode=modeProperty?.GetValue(reader)?.ToString() ?? "?";
                var source=reader.GetType().GetField("m_ObjectReferenceObject",flags)?.GetValue(reader) as Object;
                DevLog.Write($"Input: {(IsLeft(ray)?"left":"right")} hand press reader was mode={mode} object={(source?source.GetType().Name+" on "+source.name:"-")}");
                try
                {
                    if(modeProperty!=null && mode!="InputActionReference") modeProperty.SetValue(reader,System.Enum.Parse(modeProperty.PropertyType,"InputActionReference"));
                    reader.inputActionReferencePerformed=InputActionReference.Create(select);
                    reader.inputActionReferenceValue=InputActionReference.Create(value);
                    DevLog.Write($"Input: {(IsLeft(ray)?"left":"right")} hand press reader now reads {map}/Select and Select Value (mode={modeProperty?.GetValue(reader)})");
                }
                catch(System.Exception error) { DevLog.Write("Input: could not switch the hand press reader: "+error.Message); }
            }
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
            int chosen=ChooseOwner(current,Available(left),Available(right),Pinch(left),Pinch(right),InteractionPreferences.LeftHand,SteadyAim(0,left),SteadyAim(1,right));
            var previous=owner; owner=chosen==0?left:chosen==1?right:null;
            if(owner!=previous)
                DevLog.Write($"Pointer owner: {(owner?(IsLeft(owner)?"left":"right"):"none")} (left: available={Available(left)} pinch={Pinch(left):F2} aims={PointsAtPanel(left)}; right: available={Available(right)} pinch={Pinch(right):F2} aims={PointsAtPanel(right)})");
            UpdateNearFar();
            for(int i=0;i<rays.Count;i++)
            {
                if(!rays[i]) continue; bool hand=sources[i] && sources[i].Hand;
                rays[i].enableUIInteraction=rays[i]==owner && (!hand || PinchOnPanels && !handNear[IsLeft(rays[i])?0:1]);
            }
        }
        void LateUpdate() { RouteInput(); DrawHandLines(); }
        void RouteInput()
        {
            OverUI=Pressed=Held=HasHit=HasPointer=HasBuildingHit=Valid=false; Strength=0;
            if(!viewer || !focused) return;
            if(!Desktop) LogPinches(); // before the early returns below: a pinch that reaches nothing is the case to see
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
                Held=owner.selectInput.ReadIsPerformed() || owner.uiPressInput.ReadIsPerformed(); Strength=Pinch(owner);
                Aim=new Ray(owner.rayOriginTransform.position,owner.rayOriginTransform.forward);
                HasHit=owner.TryGetCurrent3DRaycastHit(out var hit) && hit.collider && (SurfaceMask&(1<<hit.collider.gameObject.layer))!=0;
                if(HasHit) Hit=hit;
                // Compare the live straight-ray results. XRI's combined visual cache
                // can lag after a canvas is replaced; no sample line visual maintains it.
                OverUI=owner.TryGetCurrentUIRaycastResult(out var ui) && (!HasHit || ui.distance<=hit.distance+.001f);
                if(OverUI) SetPointer(ui.worldPosition,ui.worldNormal);
                // XRI's UI raycast often reported no hit while the hand pointed at the menu, and a pinch then went to the twin
                // behind it (dragging it during alignment). The plane test also counts as "over the menu", so world actions wait.
                else if(PanelHit(owner,out var panelPoint)) { OverUI=true; SetPointer(panelPoint,-owner.rayOriginTransform.forward); }
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
        void OnDisable() { ResetInput(); foreach(var line in handLines) if(line) line.enabled=false; }
        void OnDestroy() { foreach(var line in handLines) if(line) Destroy(line.gameObject); }
        void OnApplicationFocus(bool value) { focused=value; ResetInput(); }
    }
}
