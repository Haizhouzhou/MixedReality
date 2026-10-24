using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TwinInspector
{
    /// <summary>A summoned world dock; a palm is a gesture, never a supporting surface.</summary>
    [DefaultExecutionOrder(1200)]
    public sealed class ToolStrip : MonoBehaviour
    {
        AppBootstrap app; SwissButton[] cells; SwissButton gear; PanelBase dockedPanel;
        /// <summary>Select() index of the Setup cell, left of the four numbered tools.</summary>
        public const int SetupIndex=4;
        const float GearWidth=64.75f, Step=139.75f;
        Vector3 fromPosition,targetPosition,anchorForward;
        Quaternion fromRotation,targetRotation;
        float movedAt,palmSince=-1;
        bool moving,palmArmed=true,explicitMove;
        public Pose WorldPose { get; private set; }
        public bool PalmAnchored => false;
        public void Initialize(AppBootstrap application,PanelBase panel)
        {
            app=application; panel.StripLayout(GearWidth-1+4*Step+1); cells=new SwissButton[4];
            var setup=panel.Button("",0,0,GearWidth,()=>app.UI.Show("setup")); setup.name="Setup"; gear=setup.GetComponent<SwissButton>();
            ((RectTransform)setup.transform).sizeDelta=new Vector2(GearWidth,64);
            ((RectTransform)setup.transform.Find("Fill")).sizeDelta=new Vector2(GearWidth-2,62);
            var glyph=PanelBase.Rectangle("Gear",setup.transform,(GearWidth-28)/2,18,28,28).gameObject.AddComponent<GearGlyph>();
            glyph.Ink=setup.GetComponentInChildren<TMP_Text>(); glyph.raycastTarget=false;
            string[] labels={"MODE","ALIGN","ANNOTATE","LIST"};
            System.Action[] actions={()=>app.UI.Show("mode"),()=>app.UI.Show("align"),()=>app.UI.BeginAnnotation(),()=>app.UI.Show("list")};
            for(int i=0;i<4;i++)
            {
                var button=panel.Button(L.Get(labels[i]),GearWidth-1+i*Step,0,140.75f,actions[i]); cells[i]=button.GetComponent<SwissButton>();
                ((RectTransform)button.transform).sizeDelta=new Vector2(140.75f,64);
                ((RectTransform)button.transform.Find("Fill")).sizeDelta=new Vector2(138.75f,62);
                var text=button.GetComponentInChildren<TMP_Text>(); text.rectTransform.anchoredPosition=new Vector2(12,-28); text.rectTransform.sizeDelta=new Vector2(116.75f,26);
                text.font=app.Tokens.Medium; text.fontSize=16; text.enableAutoSizing=true; text.fontSizeMin=12; text.fontSizeMax=16; text.textWrappingMode=TextWrappingModes.NoWrap; text.characterSpacing=6; text.alignment=TextAlignmentOptions.MidlineLeft;
                var index=panel.Text((i+1).ToString("D2"),12,8,80,18,12,false,button.transform); index.font=app.Tokens.Medium; cells[i].SetIndex(index);
            }
            Recenter(true);
        }
        public void RefreshLabels() { string[] labels={"MODE","ALIGN","ANNOTATE","LIST"}; for(int i=0;i<cells.Length;i++) cells[i].GetComponentInChildren<TMP_Text>().text=L.Get(labels[i]); }
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
        static Vector3 Horizontal(Vector3 direction) { var v=Vector3.ProjectOnPlane(direction,Vector3.up); return v.sqrMagnitude>.001f?v.normalized:Vector3.forward; }
        public void Recenter(bool instant=false)
        {
            if(!app) return;
            var camera=app.Viewer.transform; anchorForward=Horizontal(camera.forward);
            fromPosition=WorldPose.position; fromRotation=WorldPose.rotation;
            targetPosition=camera.position+anchorForward*.55f-Vector3.up*(.55f*Mathf.Tan(22*Mathf.Deg2Rad)); targetRotation=Quaternion.LookRotation(anchorForward,Vector3.up);
            movedAt=Time.unscaledTime; moving=true; explicitMove=true;
            if(instant) { WorldPose=new Pose(targetPosition,targetRotation); transform.SetPositionAndRotation(targetPosition,targetRotation); moving=false; PositionPanel(); }
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
            UpdateSpatialPose(PalmUp(InteractionPreferences.LeftHand)||PalmUp(!InteractionPreferences.LeftHand),default);
        }
        void UpdateSpatialPose(bool palmUp,Pose unused)
        {
            if(!palmUp) { palmSince=-1; palmArmed=true; }
            else if(palmArmed)
            {
                if(palmSince<0) palmSince=Time.unscaledTime;
                if(Time.unscaledTime-palmSince>=.18f && !app.Input.Held)
                { palmArmed=false; app.UI.Summon(); }
            }
            var camera=app.Viewer.transform;
            bool busy=app.Input.Busy || (app.Manual && app.Manual.Dragging) || (app.Roam && app.Roam.Moving);
            if(!busy && !moving && (Vector3.Angle(anchorForward,Horizontal(camera.forward))>25 || Vector3.Distance(camera.position,WorldPose.position)>1.1f)) { Recenter(); explicitMove=false; }
            if(moving && (!busy || explicitMove))
            {
                float t=Mathf.Clamp01((Time.unscaledTime-movedAt)/.15f); float ease=1-Mathf.Pow(1-t,3);
                WorldPose=new Pose(Vector3.Lerp(fromPosition,targetPosition,ease),Quaternion.Slerp(fromRotation,targetRotation,ease)); moving=t<1;
            }
            else if(moving && busy) { fromPosition=WorldPose.position; fromRotation=WorldPose.rotation; movedAt=Time.unscaledTime; }
            transform.SetPositionAndRotation(WorldPose.position,WorldPose.rotation); PositionPanel();
        }
    }
}
