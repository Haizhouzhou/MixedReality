using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TwinInspector
{
    /// <summary>The immutable surface anchor and its camera-facing presentation have independent transforms.</summary>
    public sealed class AnnotationMarker : MonoBehaviour
    {
        [SerializeField] MeshRenderer disc;
        [SerializeField] TMP_Text label;
        [SerializeField] Image paper;
        [SerializeField] Transform presentation;
        [SerializeField] LineRenderer leader;
        MaterialPropertyBlock properties;
        AnnotationType kind;
        DesignTokens tokens;
        bool selected,hovered;
        float displayDiameter=.04f;
        public string Id { get; private set; }
        public Vector3 AnchorPosition => transform.position;
        public float SelectionDiameter { get; private set; }
        public static float BaseGlyphDiameter(AnnotationType type) => type==AnnotationType.New?.0348f:type==AnnotationType.Changed?.0336f:type==AnnotationType.Note?.0379f:.038f;
        public void Construct(DesignTokens design)
        {
            tokens=design;
            if(presentation) return;
            // Rebuilding the prefab is idempotent; old disc/box targets are removed.
            for(int i=transform.childCount-1;i>=0;i--) Remove(transform.GetChild(i).gameObject);
            foreach(var collider in GetComponents<Collider>()) Remove(collider);
            gameObject.layer=10;
            presentation=new GameObject("Billboard presentation").transform; presentation.SetParent(transform,false);
            var plate=GameObject.CreatePrimitive(PrimitiveType.Quad); plate.name="40 mm glyph geometry"; plate.transform.SetParent(presentation,false); plate.transform.localScale=Vector3.one*.04f;
            Remove(plate.GetComponent<Collider>()); disc=plate.GetComponent<MeshRenderer>(); disc.sharedMaterial=tokens.AnnotationMaterial;
            var attachment=GameObject.CreatePrimitive(PrimitiveType.Quad); attachment.name="Physical attachment"; attachment.transform.SetParent(transform,false); attachment.transform.localScale=Vector3.one*.008f; Remove(attachment.GetComponent<Collider>()); attachment.GetComponent<MeshRenderer>().sharedMaterial=tokens.InkMaterial;
            leader=new GameObject("Surface leader").AddComponent<LineRenderer>(); leader.transform.SetParent(transform,false); leader.useWorldSpace=true; leader.positionCount=2; leader.startWidth=leader.endWidth=.001f; leader.sharedMaterial=tokens.InkMaterial;
            var canvasObject=new GameObject("Index and type",typeof(RectTransform),typeof(Canvas)); canvasObject.transform.SetParent(presentation,false); canvasObject.transform.localScale=Vector3.one*.001f;
            canvasObject.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace; canvasObject.GetComponent<Canvas>().sortingOrder=12;
            var rectangle=(RectTransform)canvasObject.transform; rectangle.sizeDelta=new Vector2(248,34);
            paper=canvasObject.AddComponent<Image>(); paper.color=tokens.Paper; paper.raycastTarget=false;
            var text=new GameObject("Annotation caption",typeof(RectTransform),typeof(TextMeshProUGUI)); text.transform.SetParent(rectangle,false);
            var rect=(RectTransform)text.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=new Vector2(8,0); rect.offsetMax=new Vector2(-8,0);
            label=text.GetComponent<TMP_Text>(); label.font=tokens.Medium; label.fontSize=18; label.color=tokens.Ink; label.alignment=TextAlignmentOptions.MidlineLeft; label.raycastTarget=false;
            label.textWrappingMode=TextWrappingModes.NoWrap;
        }
        static void Remove(Object value) { if(Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        public void UpdateData(Annotation item,bool isSelected,TwinRoot root,DesignTokens design)
        {
            Construct(design); properties ??= new MaterialPropertyBlock(); Id=item.id; kind=item.Kind; selected=isSelected;
            var normal=TwinRoot.SwapYZ(TwinRoot.Vector(item.normal_twin)).normalized;
            transform.localPosition=root.ToLocal(TwinRoot.Vector(item.position_twin))+normal*.003f;
            transform.localRotation=Quaternion.LookRotation(-normal,Mathf.Abs(Vector3.Dot(normal,Vector3.up))>.95f?Vector3.forward:Vector3.up);
            properties.SetColor("_Color",AnnotationTypes.Colour(kind).linear); properties.SetFloat("_Kind",(int)kind); disc.SetPropertyBlock(properties);
            label.text=item.index.ToString("D2")+"  "+AnnotationTypes.Prefix(kind); Paint();
        }
        public void Hover(bool value) { if(hovered==value) return; hovered=value; Paint(); }
        void Paint()
        {
            if(!label) return;
            label.color=selected?tokens.Paper:tokens.Ink; paper.color=selected?tokens.Ink:hovered?tokens.Paper2:tokens.Paper;
            paper.GetComponent<Canvas>().sortingOrder=selected?14:hovered?13:12;
        }
        public void Present(Camera viewer)
        {
            float distance=Vector3.Distance(viewer.transform.position,AnchorPosition);
            float target=AngularPresentation.Diameter(distance,1.5f,.04f);
            // Fast attack maintains the minimum angle; gentle release avoids size pumping.
            displayDiameter=target>=displayDiameter?target:Mathf.Lerp(displayDiameter,target,1-Mathf.Exp(-Time.unscaledDeltaTime*12));
            float scale=displayDiameter/BaseGlyphDiameter(kind);
            presentation.SetPositionAndRotation(AnchorPosition-transform.forward*.012f,Quaternion.LookRotation((AnchorPosition-viewer.transform.position).normalized,viewer.transform.up));
            disc.transform.localScale=Vector3.one*(.04f*scale);
            SelectionDiameter=Mathf.Max(displayDiameter,AngularPresentation.Diameter(distance,2,.055f));
            float textScale=AngularPresentation.Diameter(distance,.55f,.013f)/.013f;
            paper.transform.localScale=Vector3.one*(.001f*textScale);
            paper.transform.localPosition=new Vector3(0,displayDiameter*.72f+.017f*textScale,0);
            leader.SetPosition(0,AnchorPosition); leader.SetPosition(1,presentation.position+presentation.up*displayDiameter*.72f);
            leader.startWidth=leader.endWidth=AngularPresentation.Diameter(distance,.012f,.001f);
        }
    }
}
