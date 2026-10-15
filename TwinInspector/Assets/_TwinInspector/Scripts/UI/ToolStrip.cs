using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TwinInspector
{
    /// <summary>Palm anchoring with a stationary world-space comfort cone fallback.</summary>
    [DefaultExecutionOrder(1200)]
    public sealed class ToolStrip : MonoBehaviour
    {
        AppBootstrap app;
        SwissButton[] cells;
        Vector3 fromPosition,targetPosition,anchorForward;
        Quaternion fromRotation,targetRotation;
        PanelBase dockedPanel;
        Pose stripFrom;
        float movedAt;
        float stripMovedAt;
        bool moving,wasPalm,stripMoving;
        public Pose WorldPose { get; private set; }
        public bool PalmAnchored { get; private set; }
        public void Initialize(AppBootstrap application,PanelBase panel)
        {
            app=application; panel.StripLayout();
            cells=new SwissButton[4];
            string[] labels={"MODE","ALIGN","ANNOTATE","LIST"};
            System.Action[] actions={()=>app.UI.Show("mode"),()=>app.UI.Show("align"),()=>app.UI.BeginAnnotation(),()=>app.UI.Show("list")};
            for(int i=0;i<4;i++)
            {
                var button=panel.Button(labels[i],i*139.75f,0,140.75f,actions[i]); cells[i]=button.GetComponent<SwissButton>();
                ((RectTransform)button.transform).sizeDelta=new Vector2(140.75f,64);
                ((RectTransform)button.transform.Find("Fill")).sizeDelta=new Vector2(138.75f,62);
                var text=button.GetComponentInChildren<TMP_Text>(); text.rectTransform.sizeDelta=new Vector2(116.75f,48); text.font=app.Tokens.Medium; text.fontSize=16; text.characterSpacing=6; text.alignment=TextAlignmentOptions.Center;
            }
            var rect=(RectTransform)transform;
            foreach(var edge in new[]{new Vector4(0,0,560,1),new Vector4(0,63,560,1),new Vector4(0,0,1,64),new Vector4(559,0,1,64)})
            {
                var border=PanelBase.Rectangle("1 mm ink border",rect,edge.x,edge.y,edge.z,edge.w).gameObject.AddComponent<Image>(); border.color=app.Tokens.Ink; border.raycastTarget=false;
            }
            Recenter(true);
        }
        public void Select(int index) { if(cells!=null) for(int i=0;i<cells.Length;i++) cells[i].SetSelected(i==index); }
        public void Dock(PanelBase panel) { dockedPanel=panel; PositionPanel(); }
        void PositionPanel()
        {
            if(!dockedPanel) return;
            var stripRect=(RectTransform)transform;
            var panelRect=(RectTransform)dockedPanel.transform;
            // Align the panel's bottom edge above the strip's top edge, at the same depth.
            var offset=new Vector3(stripRect.rect.center.x*transform.lossyScale.x-panelRect.rect.center.x*panelRect.lossyScale.x,
                stripRect.rect.yMax*transform.lossyScale.y+.024f-panelRect.rect.yMin*panelRect.lossyScale.y,0);
            panelRect.SetPositionAndRotation(WorldPose.position+WorldPose.rotation*offset,WorldPose.rotation);
        }
        static Vector3 Horizontal(Vector3 direction) { var v=Vector3.ProjectOnPlane(direction,Vector3.up); return v.sqrMagnitude>.001f ? v.normalized : Vector3.forward; }
        public void Recenter(bool instant=false)
        {
            if(!app) return;
            var camera=app.Viewer.transform; anchorForward=Horizontal(camera.forward);
            StartMove(camera.position+anchorForward*.55f-Vector3.up*(.55f*Mathf.Tan(22*Mathf.Deg2Rad)),Quaternion.LookRotation(anchorForward,Vector3.up));
            if(instant) { WorldPose=new Pose(targetPosition,targetRotation); if(!PalmAnchored) transform.SetPositionAndRotation(targetPosition,targetRotation); moving=false; PositionPanel(); }
        }
        void StartMove(Vector3 position,Quaternion rotation)
        { fromPosition=WorldPose.position; fromRotation=WorldPose.rotation; targetPosition=position; targetRotation=rotation; movedAt=Time.unscaledTime; moving=true; }
        void LateUpdate()
        {
            if(!app || !app.Input) return;
            bool palmUp=false;
            if(app.Input.TryPalm(true,out var palm))
            {
                var normal=palm.rotation*Vector3.down;
                palmUp=Vector3.Dot(normal,Vector3.up)>.45f && Vector3.Dot(normal,(app.Viewer.transform.position-palm.position).normalized)>.2f;
            }
            UpdateSpatialPose(palmUp,palm);
        }
        void UpdateSpatialPose(bool palmUp,Pose palm)
        {
            var camera=app.Viewer.transform;
            PalmAnchored=palmUp;
            if(!PalmAnchored && (wasPalm || (!moving && (Vector3.Angle(anchorForward,Horizontal(camera.forward))>25 || Vector3.Distance(camera.position,WorldPose.position)>1.1f)))) Recenter();
            if(moving)
            {
                float t=Mathf.Clamp01((Time.unscaledTime-movedAt)/.15f); float ease=1-Mathf.Pow(1-t,3);
                WorldPose=new Pose(Vector3.Lerp(fromPosition,targetPosition,ease),Quaternion.Slerp(fromRotation,targetRotation,ease));
                moving=t<1;
            }
            var stripTarget=WorldPose;
            if(PalmAnchored)
            {
                var position=palm.position+Vector3.up*.10f;
                stripTarget=new Pose(position,Quaternion.LookRotation(Horizontal(position-camera.position),Vector3.up));
            }
            if(PalmAnchored!=wasPalm)
            {
                stripFrom=new Pose(transform.position,transform.rotation); stripMovedAt=Time.unscaledTime; stripMoving=true;
            }
            if(stripMoving)
            {
                float t=Mathf.Clamp01((Time.unscaledTime-stripMovedAt)/.15f); float ease=1-Mathf.Pow(1-t,3);
                transform.SetPositionAndRotation(Vector3.Lerp(stripFrom.position,stripTarget.position,ease),Quaternion.Slerp(stripFrom.rotation,stripTarget.rotation,ease));
                stripMoving=t<1;
            }
            else if(PalmAnchored) transform.SetPositionAndRotation(Vector3.Lerp(transform.position,stripTarget.position,1-Mathf.Exp(-Time.unscaledDeltaTime/.05f)),Quaternion.Slerp(transform.rotation,stripTarget.rotation,1-Mathf.Exp(-Time.unscaledDeltaTime/.05f)));
            else transform.SetPositionAndRotation(WorldPose.position,WorldPose.rotation);
            // The full panel stays at the shared world anchor while only the compact strip follows a palm.
            PositionPanel();
            wasPalm=PalmAnchored;
        }
    }
}
