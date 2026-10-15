using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TwinInspector
{
    /// <summary>Palm anchoring with a stationary world-space comfort cone fallback.</summary>
    public sealed class ToolStrip : MonoBehaviour
    {
        AppBootstrap app;
        SwissButton[] cells;
        Vector3 fromPosition,targetPosition,anchorForward;
        Quaternion fromRotation,targetRotation;
        float movedAt;
        bool moving,wasPalm;
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
        static Vector3 Horizontal(Vector3 direction) { var v=Vector3.ProjectOnPlane(direction,Vector3.up); return v.sqrMagnitude>.001f ? v.normalized : Vector3.forward; }
        public void Recenter(bool instant=false)
        {
            if(!app) return;
            var camera=app.Viewer.transform; anchorForward=Horizontal(camera.forward);
            StartMove(camera.position+anchorForward*.55f-Vector3.up*(.55f*Mathf.Tan(22*Mathf.Deg2Rad)),Quaternion.LookRotation(anchorForward,Vector3.up));
            if(instant) { transform.SetPositionAndRotation(targetPosition,targetRotation); moving=false; }
        }
        void StartMove(Vector3 position,Quaternion rotation)
        { fromPosition=transform.position; fromRotation=transform.rotation; targetPosition=position; targetRotation=rotation; movedAt=Time.unscaledTime; moving=true; }
        void LateUpdate()
        {
            if(!app || !app.Input) return;
            var camera=app.Viewer.transform;
            PalmAnchored=false;
            if(app.Input.TryPalm(true,out var palm))
            {
                var normal=palm.rotation*Vector3.down;
                PalmAnchored=Vector3.Dot(normal,Vector3.up)>.45f && Vector3.Dot(normal,(camera.position-palm.position).normalized)>.2f;
                if(PalmAnchored)
                {
                    var position=palm.position+Vector3.up*.10f;
                    var rotation=Quaternion.LookRotation(Horizontal(position-camera.position),Vector3.up);
                    if(!wasPalm) StartMove(position,rotation);
                    else { targetPosition=position; targetRotation=rotation; }
                }
            }
            if(!PalmAnchored && (wasPalm || (!moving && (Vector3.Angle(anchorForward,Horizontal(camera.forward))>25 || Vector3.Distance(camera.position,transform.position)>1.1f)))) Recenter();
            if(moving)
            {
                float t=Mathf.Clamp01((Time.unscaledTime-movedAt)/.15f); float ease=1-Mathf.Pow(1-t,3);
                transform.SetPositionAndRotation(Vector3.Lerp(fromPosition,targetPosition,ease),Quaternion.Slerp(fromRotation,targetRotation,ease));
                moving=t<1;
            }
            else if(PalmAnchored) transform.SetPositionAndRotation(Vector3.Lerp(transform.position,targetPosition,1-Mathf.Exp(-Time.unscaledDeltaTime/.05f)),Quaternion.Slerp(transform.rotation,targetRotation,1-Mathf.Exp(-Time.unscaledDeltaTime/.05f)));
            wasPalm=PalmAnchored;
        }
    }
}
