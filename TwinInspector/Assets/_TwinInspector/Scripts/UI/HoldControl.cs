using UnityEngine;
using UnityEngine.EventSystems;
namespace TwinInspector
{
    public sealed class HoldControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Vector2 Direction;
        bool held;
        public void OnPointerDown(PointerEventData data) { held=true; AppBootstrap.Instance?.Input?.CaptureUI(data); AppBootstrap.Instance?.Roam?.Move(Direction); }
        public void OnPointerUp(PointerEventData data) => Stop();
        public void OnPointerExit(PointerEventData data) => Stop();
        void Stop() { if(held) AppBootstrap.Instance?.Roam?.Move(Vector2.zero); held=false; }
        void OnDisable() => Stop();
    }
    public sealed class SliderFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler
    {
        float initial;
        public void OnPointerDown(PointerEventData data) { initial=GetComponent<UnityEngine.UI.Slider>().value; AppBootstrap.Instance?.Input?.CaptureUI(data); }
        public void OnPointerUp(PointerEventData data) { if(!Mathf.Approximately(initial,GetComponent<UnityEngine.UI.Slider>().value)) AppBootstrap.Instance?.Feedback?.Confirm(); PlayerPrefs.Save(); }
        public void OnPointerEnter(PointerEventData data) => AppBootstrap.Instance?.Feedback?.Hover();
    }
}
