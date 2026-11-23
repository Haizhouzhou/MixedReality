using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TwinInspector
{
    /// <summary>A body-relative world dock: follows at the saved offset, and when unpinned
    /// opens on a raised palm and fades once the hand is down and the viewer looks away.</summary>
    [DefaultExecutionOrder(1200)]
    public sealed class ToolStrip : MonoBehaviour
    {
        AppBootstrap app; SwissButton[] cells; SwissButton gear, pin, minimize; PanelBase strip, dockedPanel;
        /// <summary>Select() index of the Setup cell, left of the four numbered tools.</summary>
        public const int SetupIndex=4;
        const float SideWidth=64.75f, Step=139.75f;
        float bodyYaw, palmSince=-1, idleSince, fade=1;
        bool palmArmed=true, initialized, gliding, headKnown, palmBlockedLogged;
        Vector3 glideVelocity, lastHead;
        float headSpeed, stillSince;
        public Pose WorldPose { get; private set; }
        public bool PalmAnchored => false;
        public bool Shown { get; private set; } = true;
        public void Initialize(AppBootstrap application,PanelBase panel)
        {
            app=application; strip=panel; panel.StripLayout(3*SideWidth-3+4*Step+1); cells=new SwissButton[4];
            gear=SideCell(panel,"Setup",0,()=>app.UI.Show("setup")).GetComponent<SwissButton>();
            var glyph=PanelBase.Rectangle("Gear",gear.transform,(SideWidth-28)/2,18,28,28).gameObject.AddComponent<GearGlyph>();
            glyph.Ink=gear.GetComponentInChildren<TMP_Text>(); glyph.raycastTarget=false;
            string[] labels={"MODE","ALIGN","ANNOTATE","LIST"};
            System.Action[] actions={()=>app.UI.Show("mode"),()=>app.UI.Show("align"),()=>app.UI.Show("annotate"),()=>app.UI.Show("list")};
            for(int i=0;i<4;i++)
            {
                var button=panel.Button(L.Get(labels[i]),SideWidth-1+i*Step,0,140.75f,actions[i]); cells[i]=button.GetComponent<SwissButton>();
                ((RectTransform)button.transform).sizeDelta=new Vector2(140.75f,64);
                ((RectTransform)button.transform.Find("Fill")).sizeDelta=new Vector2(138.75f,62);
                var text=button.GetComponentInChildren<TMP_Text>(); text.rectTransform.anchoredPosition=new Vector2(12,-28); text.rectTransform.sizeDelta=new Vector2(116.75f,26);
                text.font=app.Tokens.Medium; text.fontSize=16; text.enableAutoSizing=true; text.fontSizeMin=12; text.fontSizeMax=16; text.textWrappingMode=TextWrappingModes.NoWrap; text.characterSpacing=6; text.alignment=TextAlignmentOptions.MidlineLeft;
                var index=panel.Text((i+1).ToString("D2"),12,8,80,18,12,false,button.transform); index.font=app.Tokens.Medium; cells[i].SetIndex(index);
            }
            var pinButton=SideCell(panel,"Pin",SideWidth-2+4*Step,()=>{ InteractionPreferences.DockPinnedSetting=!InteractionPreferences.DockPinnedSetting; RefreshPin(); });
            pin=pinButton.GetComponent<SwissButton>();
            var pinGlyph=PanelBase.Rectangle("Pin glyph",pin.transform,(SideWidth-28)/2,18,28,28).gameObject.AddComponent<PinGlyph>();
            pinGlyph.Ink=pin.GetComponentInChildren<TMP_Text>(); pinGlyph.raycastTarget=false;
            // Minimize hides the whole dock at once, pinned or not; raising a palm brings it back (as for an auto-hidden dock).
            var minimizeButton=SideCell(panel,"Minimize",2*SideWidth-3+4*Step,Minimize);
            minimize=minimizeButton.GetComponent<SwissButton>();
            var minimizeGlyph=PanelBase.Rectangle("Minimize glyph",minimize.transform,(SideWidth-28)/2,18,28,28).gameObject.AddComponent<MinimizeGlyph>();
            minimizeGlyph.Ink=minimize.GetComponentInChildren<TMP_Text>(); minimizeGlyph.raycastTarget=false;
            RefreshPin(); Recenter(true);
        }
        Button SideCell(PanelBase panel,string name,float x,System.Action action)
        {
            var button=panel.Button("",x,0,SideWidth,action); button.name=name;
            ((RectTransform)button.transform).sizeDelta=new Vector2(SideWidth,64);
            ((RectTransform)button.transform.Find("Fill")).sizeDelta=new Vector2(SideWidth-2,62);
            return button;
        }
        public void RefreshLabels() { string[] labels={"MODE","ALIGN","ANNOTATE","LIST"}; for(int i=0;i<cells.Length;i++) cells[i].GetComponentInChildren<TMP_Text>().text=L.Get(labels[i]); }
        /// <summary>Pin shows the effective state; it is unavailable while the palm gesture is off, because then nothing could reopen the dock.</summary>
        public void RefreshPin()
        {
            if(!pin) return;
            pin.SetSelected(InteractionPreferences.DockPinned); pin.GetComponent<Button>().interactable=InteractionPreferences.PalmSummon;
            // Without the palm gesture nothing could bring a minimized dock back.
            if(minimize) minimize.GetComponent<Button>().interactable=InteractionPreferences.PalmSummon;
            if(InteractionPreferences.DockPinned) Reveal();
        }
        public void Select(int index) { if(cells!=null) for(int i=0;i<cells.Length;i++) cells[i].SetSelected(i==index); if(gear) gear.SetSelected(index==SetupIndex); }
        public void Dock(PanelBase panel) { dockedPanel=panel; PositionPanel(); }
        void PositionPanel()
        {
            if(!dockedPanel) return;
            var stripRect=(RectTransform)transform; var panelRect=(RectTransform)dockedPanel.transform;
            var offset=new Vector3(stripRect.rect.center.x*transform.lossyScale.x-panelRect.rect.center.x*panelRect.lossyScale.x,
                stripRect.rect.yMax*transform.lossyScale.y+.024f-panelRect.rect.yMin*panelRect.lossyScale.y,0);
            panelRect.SetPositionAndRotation(WorldPose.position+WorldPose.rotation*offset,WorldPose.rotation);
        }
        static float Yaw(Vector3 direction) { var v=Vector3.ProjectOnPlane(direction,Vector3.up); return v.sqrMagnitude>.001f?Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg:0; }
        Pose Home()
        {
            var head=app.Viewer.transform.position;
            var position=DockPlacement.Home(head,bodyYaw,InteractionPreferences.DockDistance,InteractionPreferences.DockYaw,InteractionPreferences.DockHeight);
            return new Pose(position,DockPlacement.Facing(head,position));
        }
        /// <summary>Places the dock at its saved offset from where the viewer now faces.</summary>
        public void Recenter(bool instant=false)
        {
            if(!app) return;
            bodyYaw=Yaw(app.Viewer.transform.forward); initialized=true;
            if(instant) { WorldPose=Home(); gliding=false; glideVelocity=Vector3.zero; transform.SetPositionAndRotation(WorldPose.position,WorldPose.rotation); PositionPanel(); }
        }
        /// <summary>Hide the dock now, even when pinned, until a raised palm (or Summon) brings it back.</summary>
        public void Minimize()
        {
            if(!InteractionPreferences.PalmSummon) return;
            Shown=false; palmArmed=false; // the hand that pressed may already be palm-up; wait until it is lowered once
            DevLog.Write("Menu minimized; raise a palm to bring it back");
        }
        /// <summary>Show the dock (palm, summon or pin); a hidden dock reappears directly at its place.</summary>
        public void Reveal()
        {
            if(!Shown && app) Recenter(true);
            Shown=true; idleSince=Time.unscaledTime;
        }
        bool PalmUp(bool left)
        {
            if(!app.Input.TryPalm(left,out var palm)) return false;
            var normal=palm.rotation*Vector3.down; float toViewer=Vector3.Dot(normal,(app.Viewer.transform.position-palm.position).normalized);
            // Palm up and somewhat towards the viewer, or held open towards the face: both are what people do to call a menu.
            return Vector3.Dot(normal,Vector3.up)>.45f && toViewer>.2f || toViewer>.7f;
        }
        void LateUpdate()
        {
            if(!app || !app.Input) return;
            var camera=app.Viewer.transform;
            if(!initialized) Recenter(true);
            bool palmLeft=InteractionPreferences.PalmSummon && PalmUp(true), palmRight=InteractionPreferences.PalmSummon && PalmUp(false);
            bool palmUp=palmLeft||palmRight;
            if(!palmUp) { palmSince=-1; palmArmed=true; palmBlockedLogged=false; }
            else if(palmArmed)
            {
                if(palmSince<0) palmSince=Time.unscaledTime;
                // A hold by the other hand (a drag in progress) waits. The palm hand itself often reads 0.4-0.9 pinch strength
                // while turned up, so its own "hold" must not block it: that is why raising a palm sometimes did nothing (Nov 21).
                bool otherHolding=app.Input.Held && (palmLeft&&!palmRight ? !app.Input.ActiveLeft : palmRight&&!palmLeft ? app.Input.ActiveLeft : false);
                if(Time.unscaledTime-palmSince>=.18f)
                {
                    if(!otherHolding) { palmArmed=false; Reveal(); app.UI.Summon(); DevLog.Write("Menu summoned by palm ("+(palmLeft?"left":"right")+")"); }
                    else if(!palmBlockedLogged) { palmBlockedLogged=true; DevLog.Write("Palm up ignored: the other hand is holding"); }
                }
            }
            bodyYaw=DockPlacement.FollowBodyYaw(bodyYaw,Yaw(camera.forward));
            // Horizontal head speed, lightly smoothed so single tracking jumps do not count as walking.
            {
                float step=Mathf.Max(Time.unscaledDeltaTime,1e-4f); var head=camera.position;
                float speed=headKnown?new Vector2(head.x-lastHead.x,head.z-lastHead.z).magnitude/step:0;
                headSpeed=Mathf.Lerp(headSpeed,speed,1-Mathf.Exp(-step/.15f)); lastHead=head; headKnown=true;
                if(headSpeed>=DockPlacement.WalkSpeed) stillSince=Time.unscaledTime;
            }
            // Hold still under an active pointer so buttons do not slide away. Otherwise follow lazily: a dock that moved with every
            // head bob and small step was always in motion, which at frame rates below 72 Hz showed as judder.
            bool interacting=app.Input.OverUI || app.Input.Held;
            if(!interacting)
            {
                var home=Home(); float dt=Time.unscaledDeltaTime;
                gliding=DockPlacement.ShouldGlide(gliding,Vector3.Distance(WorldPose.position,home.position),Quaternion.Angle(WorldPose.rotation,home.rotation));
                if(gliding && !DockPlacement.MayMove(headSpeed,Time.unscaledTime-stillSince)) glideVelocity=Vector3.zero; // wait in the room while walking
                else if(gliding && Vector3.Distance(WorldPose.position,home.position)>DockPlacement.JumpDistance) { WorldPose=home; gliding=false; glideVelocity=Vector3.zero; }
                else if(gliding)
                {
                    // SmoothDamp eases in and out; the rotation settles at the same pace.
                    var position=Vector3.SmoothDamp(WorldPose.position,home.position,ref glideVelocity,DockPlacement.GlideSeconds,Mathf.Infinity,dt);
                    WorldPose=new Pose(position,Quaternion.Slerp(WorldPose.rotation,home.rotation,1-Mathf.Exp(-dt/(DockPlacement.GlideSeconds*.5f))));
                }
                else glideVelocity=Vector3.zero;
            }
            else { gliding=false; glideVelocity=Vector3.zero; }
            transform.SetPositionAndRotation(WorldPose.position,WorldPose.rotation); PositionPanel();
            UpdateVisibility(camera,palmUp,interacting);
        }
        void UpdateVisibility(Transform camera,bool palmUp,bool interacting)
        {
            bool pinned=InteractionPreferences.DockPinned;
            var center=dockedPanel && dockedPanel.gameObject.activeSelf?(WorldPose.position+dockedPanel.transform.position)/2:WorldPose.position;
            bool looking=DockPlacement.Looking(camera.position,camera.forward,center);
            // Space choice and loading always stay visible; so does a dock in use.
            bool busy=interacting||!app.Ready||(app.Placer && app.Placer.Active)||(app.Probes && app.Probes.SceneRunning)||(app.AutoAlign && app.AutoAlign.Active);
            if(pinned||palmUp||looking||busy) idleSince=Time.unscaledTime;
            if(Shown && DockPlacement.ShouldHide(pinned,palmUp,looking,busy,Time.unscaledTime-idleSince)) Shown=false;
            fade=Mathf.MoveTowards(fade,Shown?1:0,Time.unscaledDeltaTime/(Shown?.15f:DockPlacement.FadeSeconds));
            strip.Fade=fade; if(dockedPanel) dockedPanel.Fade=fade;
        }
    }
}
