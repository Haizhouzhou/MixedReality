using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TwinInspector
{
    /// <summary>Instant focus inversion; no sprites, gradients or animated decoration.</summary>
    public sealed class SwissButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        Image fill; TMP_Text label; DesignTokens tokens; bool selected;
        public void Configure(Image surface, TMP_Text text, DesignTokens design, bool active)
        { fill = surface; label = text; tokens = design; SetSelected(active); }
        public void SetSelected(bool active) { selected = active; Paint(active); }
        void Paint(bool inverse) { fill.color = inverse ? tokens.Ink : tokens.Paper; label.color = inverse ? tokens.Paper : tokens.Ink; }
        public void OnPointerEnter(PointerEventData data) => Paint(true);
        public void OnPointerExit(PointerEventData data) => Paint(selected);
        public void OnSelect(BaseEventData data) => Paint(true);
        public void OnDeselect(BaseEventData data) => Paint(selected);
    }
}
