using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace TwinInspector
{
    /// <summary>Short handle under the dock: faint at rest, full when a ray or hand comes near, drags the whole dock.</summary>
    public sealed class GrabBar : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        const float NearHand=.15f;
        AppBootstrap app; ToolStrip strip; RectTransform pill; Image ink;
        bool hovered, dragging; float grabDistance, emphasis; Vector3 grabOffset;
        public bool Dragging => dragging;
        public void Initialize(AppBootstrap application,ToolStrip owner,RectTransform handle,Image surface)
        { app=application; strip=owner; pill=handle; ink=surface; }
        public void OnPointerEnter(PointerEventData data) => hovered=true;
        public void OnPointerExit(PointerEventData data) => hovered=false;
        public void OnPointerDown(PointerEventData data) => app.Input?.CaptureUI(data);
        public void OnBeginDrag(PointerEventData data)
        {
            var aim=app.Input.Aim; var dock=strip.transform.position;
            grabDistance=Vector3.Dot(transform.position-aim.origin,aim.direction);
            grabOffset=dock-(aim.origin+aim.direction*grabDistance);
            dragging=true; strip.BeginDrag();
        }
        public void OnDrag(PointerEventData data) { if(dragging) { var aim=app.Input.Aim; strip.DragTo(aim.origin+aim.direction*grabDistance+grabOffset); } }
        public void OnEndDrag(PointerEventData data) { if(!dragging) return; dragging=false; strip.EndDrag(); }
        void OnDisable() { if(dragging) { dragging=false; strip.EndDrag(); } hovered=false; }
        void Update()
        {
            bool near=hovered||dragging;
            if(!near && app && app.Input)
                foreach(bool left in new[]{true,false})
                    if(app.Input.TryPalm(left,out var palm) && Vector3.Distance(palm.position,transform.position)<NearHand) near=true;
            emphasis=Mathf.MoveTowards(emphasis,near?1:0,Time.unscaledDeltaTime/.15f);
            pill.sizeDelta=new Vector2(Mathf.Lerp(72,160,emphasis),Mathf.Lerp(4,10,emphasis));
            var c=ink.color; c.a=Mathf.Lerp(.35f,1,emphasis); ink.color=c;
        }
    }
}
