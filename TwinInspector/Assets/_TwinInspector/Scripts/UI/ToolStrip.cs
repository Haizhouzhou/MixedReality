using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TwinInspector
{
    /// <summary>A body-relative world dock: follows at the saved offset, drags by its grab bar, and when unpinned
    /// opens on a raised palm and fades once the hand is down and the viewer looks away.</summary>
    [DefaultExecutionOrder(1200)]
    public sealed class ToolStrip : MonoBehaviour
    {
        AppBootstrap app; SwissButton[] cells; SwissButton gear, pin; PanelBase strip, dockedPanel; GrabBar grab;
        /// <summary>Select() index of the Setup cell, left of the four numbered tools.</summary>
        public const int SetupIndex=4;
        const float SideWidth=64.75f, Step=139.75f, Follow=.12f;
        float bodyYaw, palmSince=-1, idleSince, fade=1;
        bool palmArmed=true, dragging, initialized;
        public Pose WorldPose { get; private set; }
        public bool PalmAnchored => false;
        public bool Shown { get; private set; } = true;
        public void Initialize(AppBootstrap application,PanelBase panel)
        {
            app=application; strip=panel; panel.StripLayout(2*SideWidth-2+4*Step+1); cells=new SwissButton[4];
            gear=SideCell(panel,"Setup",0,()=>app.UI.Show("setup")).GetComponent<SwissButton>();
            var glyph=PanelBase.Rectangle("Gear",gear.transform,(SideWidth-28)/2,18,28,28).gameObject.AddComponent<GearGlyph>();
            glyph.Ink=gear.GetComponentInChildren<TMP_Text>(); glyph.raycastTarget=false;
            string[] labels={"MODE","ALIGN","ANNOTATE","LIST"};
            System.Action[] actions={()=>app.UI.Show("mode"),()=>app.UI.Show("align"),()=>app.UI.BeginAnnotation(),()=>app.UI.Show("list")};
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
            // A wide transparent hit area keeps the short handle easy to catch with a ray or a pinch.
            float width=((RectTransform)transform).sizeDelta.x;
            var area=PanelBase.Rectangle("Grab bar",panel.Content,(width-200)/2,70,200,32); area.gameObject.AddComponent<Image>().color=Color.clear;
            var handle=PanelBase.Rectangle("Handle",area,100,16,72,4); handle.anchorMin=handle.anchorMax=new Vector2(0,1); handle.pivot=new Vector2(.5f,.5f);
            var ink=handle.gameObject.AddComponent<Image>(); ink.color=app.Tokens.Ink; ink.raycastTarget=false;
            grab=area.gameObject.AddComponent<GrabBar>(); grab.Initialize(app,this,handle,ink);
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
            if(instant) { WorldPose=Home(); transform.SetPositionAndRotation(WorldPose.position,WorldPose.rotation); PositionPanel(); }
        }
        /// <summary>Show the dock (palm, summon or pin); a hidden dock reappears directly at its place.</summary>
        public void Reveal()
        {
            if(!Shown && app) Recenter(true);
            Shown=true; idleSince=Time.unscaledTime;
        }
        public void BeginDrag() { dragging=true; Reveal(); }
        public void DragTo(Vector3 position)
        {
            var head=app.Viewer.transform.position;
            WorldPose=new Pose(position,DockPlacement.Facing(head,position));
        }
        /// <summary>The dropped pose becomes the saved offset, shared with the Menu position sliders.</summary>
        public void EndDrag()
        {
            dragging=false;
            var (distance,yaw,height)=DockPlacement.Offsets(app.Viewer.transform.position,bodyYaw,WorldPose.position);
            InteractionPreferences.DockDistance=distance; InteractionPreferences.DockYaw=yaw; InteractionPreferences.DockHeight=height; PlayerPrefs.Save();
        }
        bool PalmUp(bool left)
        {
            if(!app.Input.TryPalm(left,out var palm)) return false;
            var normal=palm.rotation*Vector3.down;
            return Vector3.Dot(normal,Vector3.up)>.45f && Vector3.Dot(normal,(app.Viewer.transform.position-palm.position).normalized)>.2f;
        }
        void LateUpdate()
        {
            if(!app || !app.Input) return;
            var camera=app.Viewer.transform;
            if(!initialized) Recenter(true);
            bool palmUp=InteractionPreferences.PalmSummon && (PalmUp(InteractionPreferences.LeftHand)||PalmUp(!InteractionPreferences.LeftHand));
            if(!palmUp) { palmSince=-1; palmArmed=true; }
            else if(palmArmed)
            {
                if(palmSince<0) palmSince=Time.unscaledTime;
                if(Time.unscaledTime-palmSince>=.18f && !app.Input.Held) { palmArmed=false; Reveal(); app.UI.Summon(); }
            }
            bodyYaw=DockPlacement.FollowBodyYaw(bodyYaw,Yaw(camera.forward));
            // Hold still under an active pointer so buttons do not slide away; otherwise ease toward the body-relative home.
            bool interacting=app.Input.OverUI || app.Input.Held;
            if(!dragging && !interacting)
            {
                var home=Home(); float t=1-Mathf.Exp(-Time.unscaledDeltaTime/Follow);
                WorldPose=new Pose(Vector3.Lerp(WorldPose.position,home.position,t),Quaternion.Slerp(WorldPose.rotation,home.rotation,t));
            }
            transform.SetPositionAndRotation(WorldPose.position,WorldPose.rotation); PositionPanel();
            UpdateVisibility(camera,palmUp,interacting);
        }
        void UpdateVisibility(Transform camera,bool palmUp,bool interacting)
        {
            bool pinned=InteractionPreferences.DockPinned;
            var center=dockedPanel && dockedPanel.gameObject.activeSelf?(WorldPose.position+dockedPanel.transform.position)/2:WorldPose.position;
            bool looking=DockPlacement.Looking(camera.position,camera.forward,center);
            // Space choice and loading always stay visible; so does a dock in use.
            bool busy=interacting||dragging||!app.Ready||(app.Placer && app.Placer.Active)||(app.Probes && app.Probes.SceneRunning)||(app.AutoAlign && app.AutoAlign.Active);
            if(pinned||palmUp||looking||busy) idleSince=Time.unscaledTime;
            if(Shown && DockPlacement.ShouldHide(pinned,palmUp,looking,busy,Time.unscaledTime-idleSince)) Shown=false;
            fade=Mathf.MoveTowards(fade,Shown?1:0,Time.unscaledDeltaTime/(Shown?.15f:DockPlacement.FadeSeconds));
            strip.Fade=fade; if(dockedPanel) dockedPanel.Fade=fade;
        }
    }
}
