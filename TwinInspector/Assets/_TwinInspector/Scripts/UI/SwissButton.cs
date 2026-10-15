using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace TwinInspector
{
    /// <summary>Focus, press, persistent choice and unavailable controls remain distinct.</summary>
    public sealed class SwissButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        Image fill,bar; TMP_Text label,index; DesignTokens tokens; Selectable control;
        bool selected,hovered,pressed,lastInteractable=true;
        float confirmedUntil;
        public void Accepted() { confirmedUntil=Time.unscaledTime+.12f; Paint(); }
        public void Configure(Image surface,TMP_Text text,DesignTokens design,bool active)
        {
            fill=surface; label=text; tokens=design; control=GetComponent<Selectable>();
            var rect=PanelBase.Rectangle("3 mm selection rule",transform,1,45,((RectTransform)transform).sizeDelta.x-2,3);
            rect.anchorMin=new Vector2(0,0); rect.anchorMax=new Vector2(1,0); rect.pivot=new Vector2(.5f,0); rect.anchoredPosition=new Vector2(0,1); rect.sizeDelta=new Vector2(-2,3);
            bar=rect.gameObject.AddComponent<Image>(); bar.color=tokens.Accent; bar.raycastTarget=false; SetSelected(active);
        }
        public void SetIndex(TMP_Text text) { index=text; Paint(); }
        public void SetSelected(bool value) { selected=value; Paint(); }
        void Paint()
        {
            if(!fill) return;
            bool enabled=!control || control.IsInteractable(); lastInteractable=enabled;
            bool inverse=enabled&&(selected||pressed||Time.unscaledTime<confirmedUntil);
            fill.color=!enabled?tokens.Paper2:inverse?tokens.Ink:hovered?tokens.Paper2:tokens.Paper;
            label.color=!enabled?tokens.Muted:inverse?tokens.Paper:tokens.Ink;
            label.fontStyle=enabled?FontStyles.Normal:FontStyles.Strikethrough;
            if(index) index.color=label.color;
            if(bar) bar.enabled=enabled&&(selected||Time.unscaledTime<confirmedUntil);
        }
        void Update() { if(control && control.IsInteractable()!=lastInteractable) Paint(); if(confirmedUntil>0 && Time.unscaledTime>=confirmedUntil) { confirmedUntil=0; Paint(); } }
        public void OnPointerEnter(PointerEventData data) { hovered=true; Paint(); if(lastInteractable) AppBootstrap.Instance?.Feedback?.Hover(); }
        public void OnPointerExit(PointerEventData data) { hovered=false; Paint(); }
        public void OnSelect(BaseEventData data) { hovered=true; Paint(); }
        public void OnDeselect(BaseEventData data) { hovered=false; Paint(); }
        public void OnPointerDown(PointerEventData data) { if(!lastInteractable) return; pressed=true; AppBootstrap.Instance?.Input?.CaptureUI(data); Paint(); }
        public void OnPointerUp(PointerEventData data) { pressed=false; Paint(); }
        void OnDisable() { pressed=hovered=false; }
    }
}
