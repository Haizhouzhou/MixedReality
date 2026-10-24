using System;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace TwinInspector
{
    /// <summary>Reusable 480×320 mm paper panel and grid-aligned controls.</summary>
    public sealed class PanelBase : MonoBehaviour
    {
        DesignTokens tokens;
        RectTransform content;
        TMP_Text title, section, meta, toast;
        CanvasGroup group;
        float toastUntil, shownAt;
        Vector3 settled;
        public DesignTokens Tokens => tokens;
        /// <summary>Dock visibility (1 shown, 0 hidden); below one half the panel stops taking input.</summary>
        public float Fade { get; set; } = 1;
        public RectTransform Content => content;
        public void Initialize(DesignTokens design, Camera viewer)
        {
            tokens = design;
            gameObject.layer=5;
            var rect = GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(480, 320);
            var canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = viewer; canvas.sortingOrder = 20;
            if(!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();
            transform.localScale = Vector3.one * .001f;
            GetComponent<Image>().color = tokens.Paper;
            group = GetComponent<CanvasGroup>();
            section = Text("SECTION", 24,24,260,20,DesignTokens.Label, true, rect); section.characterSpacing = 6;
            title = Text("Title",24,48,432,40,DesignTokens.Title,true,rect);
            meta = Text("",300,24,156,20,DesignTokens.Caption,false,rect); meta.alignment = TextAlignmentOptions.TopRight;
            content = Rectangle("Content",rect,0,0,480,320);
            foreach(var edge in new[]{new Vector4(0,0,480,1),new Vector4(0,319,480,1),new Vector4(0,0,1,320),new Vector4(479,0,1,320),new Vector4(8,8,8,1),new Vector4(8,8,1,8),new Vector4(464,311,8,1),new Vector4(471,304,1,8)})
            { var rule=Rectangle("Registration rule",rect,edge.x,edge.y,edge.z,edge.w).gameObject.AddComponent<Image>(); rule.color=tokens.Ink; rule.raycastTarget=false; }
            // Put transient feedback above the panel so it cannot cover the docked tool strip.
            var footer = Rectangle("Toast",rect,0,-56,480,48); var surface = footer.gameObject.AddComponent<Image>(); surface.color = tokens.Paper; surface.raycastTarget = false;
            toast = Text("",24,8,432,32,DesignTokens.Body,false,footer); toast.transform.parent.gameObject.SetActive(false);
            toast.enableAutoSizing=true;toast.fontSizeMin=16;toast.fontSizeMax=DesignTokens.Body;toast.textWrappingMode=TextWrappingModes.NoWrap;
        }
        public void Header(string label, string heading, string status = "") { section.text = label.ToUpperInvariant(); title.text = heading; meta.text = status; }
        public void Clear()
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        public void Summon(Camera viewer)
        {
            var forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            settled = viewer.transform.position + forward - Vector3.up * .10f;
            transform.SetPositionAndRotation(settled, Quaternion.LookRotation(forward, Vector3.up));
            shownAt = Time.unscaledTime;
        }
        public void StripLayout(float width)
        { ((RectTransform)transform).sizeDelta = new Vector2(width,64); content.sizeDelta = new Vector2(width,64); section.gameObject.SetActive(false); title.gameObject.SetActive(false); meta.gameObject.SetActive(false); foreach(Transform child in transform) if(child.name=="Registration rule") child.gameObject.SetActive(false); }
        public void Notify(string message) { toast.text = message; toastUntil = Time.unscaledTime + 2.5f; toast.transform.parent.gameObject.SetActive(true); }
        void Update()
        {
            if (group) { group.alpha = Mathf.Min(Mathf.Clamp01((Time.unscaledTime - shownAt) / .15f), Fade); group.blocksRaycasts = group.interactable = Fade > .5f; }
            if (toast && Time.unscaledTime > toastUntil) toast.transform.parent.gameObject.SetActive(false);
        }
        public static RectTransform Rectangle(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.gameObject.layer=5; rect.SetParent(parent,false);
            rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1); rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,height); return rect;
        }
        public TMP_Text Text(string value, float x, float y, float width, float height, float size = DesignTokens.Body, bool emphasis = false, Transform parent = null)
        {
            var rect = Rectangle(value, parent ? parent : content, x,y,width,height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = emphasis ? tokens.Semibold : tokens.Regular;
            text.fontSize = size; text.color = tokens.Ink; text.text = value; text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.TopLeft; text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.fontFeatures.Add((OTL_FeatureTag)0x746E756D); // OpenType tnum
            return text;
        }
        public Button Button(string label, float x, float y, float width, Action action, bool primary = false, Transform parent = null)
        {
            var rect = Rectangle(label, parent ? parent : content, x,y,width,48);
            rect.gameObject.AddComponent<Image>().color = tokens.Ink;
            var inset = Rectangle("Fill",rect,1,1,width-2,46); var fill = inset.gameObject.AddComponent<Image>();
            var text = Text(label,12,8,width-24,32,DesignTokens.Body,false,rect); text.alignment = TextAlignmentOptions.MidlineLeft; text.enableAutoSizing=true; text.fontSizeMin=16; text.fontSizeMax=DesignTokens.Body; text.textWrappingMode=TextWrappingModes.NoWrap;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = fill; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { button.GetComponent<SwissButton>().Accepted(); action(); AppBootstrap.Instance?.Feedback?.Confirm(); });
            rect.gameObject.AddComponent<SwissButton>().Configure(fill,text,tokens,primary); return button;
        }
        public void Slider(float value, Action<float> changed, float y=192, float min=0, float max=1, string suffix=" %", float x=24, float width=352, string format="F1")
        {
            var root = Rectangle("Blend slider",content,x,y,width,40);
            root.gameObject.AddComponent<Image>().color=Color.clear;
            var line = Rectangle("One mm rule",root,0,19.5f,width,1); line.gameObject.AddComponent<Image>().color = tokens.Ink;
            var handleArea = Rectangle("Handle area",root,6,14,width-12,12);
            var handle = Rectangle("Square handle",handleArea,0,0,12,0); handle.pivot = new Vector2(.5f,.5f); handle.anchorMin = new Vector2(0,0); handle.anchorMax=new Vector2(0,1); handle.gameObject.AddComponent<Image>().color = tokens.Ink;
            var slider = root.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>(); slider.transition = Selectable.Transition.None; slider.value = value;
            var readout = Text(suffix==" %"?Mathf.RoundToInt(value*100)+suffix:value.ToString(format,System.Globalization.CultureInfo.InvariantCulture)+suffix,x+width+8,y+4,456-(x+width+8),32,DesignTokens.Body); readout.alignment = TextAlignmentOptions.TopRight;
            slider.onValueChanged.AddListener(v => { readout.text = suffix==" %"?Mathf.RoundToInt(v*100)+suffix:v.ToString(format,System.Globalization.CultureInfo.InvariantCulture)+suffix; changed(v); });
            root.gameObject.AddComponent<SliderFeedback>();
        }
        public RectTransform Scroll(float contentHeight)
        {
            var viewport = Rectangle("List viewport",content,24,104,432,112); viewport.gameObject.AddComponent<RectMask2D>();
            var contentRect = Rectangle("Rows",viewport,0,0,432,Mathf.Max(112,contentHeight));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = contentRect; scroll.horizontal = false; scroll.vertical = true; scroll.inertia = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
            viewport.gameObject.AddComponent<Image>().color = tokens.Paper; return contentRect;
        }
        public void Row(string label,float y,Action action,bool selected,Transform parent)
        {
            var rect=Rectangle(label,parent,0,y,432,56); var fill=rect.gameObject.AddComponent<Image>();
            var text=Text(label,12,12,408,32,DesignTokens.Body,false,rect);
            text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=DesignTokens.Body;text.textWrappingMode=TextWrappingModes.NoWrap;
            var rule=Rectangle("Row rule",rect,0,55,432,1); rule.gameObject.AddComponent<Image>().color=tokens.Ink;
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=fill; button.transition=Selectable.Transition.None; button.onClick.AddListener(()=> { action(); AppBootstrap.Instance?.Feedback?.Confirm(); });
            rect.gameObject.AddComponent<SwissButton>().Configure(fill,text,tokens,selected);
        }
    }
}
