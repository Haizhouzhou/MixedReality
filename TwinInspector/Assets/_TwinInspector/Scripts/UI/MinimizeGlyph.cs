using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TwinInspector
{
    /// <summary>A short bar near the bottom of the dock's minimize cell; follows its button label's ink like PinGlyph.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MinimizeGlyph : MaskableGraphic
    {
        public TMP_Text Ink;
        void LateUpdate() { if(Ink && color!=Ink.color) color=Ink.color; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=GetPixelAdjustedRect(); float s=Mathf.Min(r.width,r.height)*.5f; var c=r.center;
            int k=vh.currentVertCount;
            foreach(var v in new[]{ new Vector2(-.6f,-.45f), new Vector2(.6f,-.45f), new Vector2(.6f,-.62f), new Vector2(-.6f,-.62f) }) vh.AddVert(c+v*s,color,Vector2.zero);
            vh.AddTriangle(k,k+1,k+2); vh.AddTriangle(k,k+2,k+3);
        }
    }
}
