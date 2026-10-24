using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TwinInspector
{
    /// <summary>Tilted push-pin for the dock's keep-visible cell; follows its button label's ink like GearGlyph.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PinGlyph : MaskableGraphic
    {
        public TMP_Text Ink;
        void LateUpdate() { if(Ink && color!=Ink.color) color=Ink.color; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=GetPixelAdjustedRect(); Vector2 center=r.center; float s=Mathf.Min(r.width,r.height)*.5f;
            var tilt=Quaternion.Euler(0,0,-35);
            Vector2 P(float x,float y)=>center+(Vector2)(tilt*new Vector3(x*s,y*s,0));
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d){int k=vh.currentVertCount;foreach(var v in new[]{a,b,c,d})vh.AddVert(v,color,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);}
            // Head disc, tapered body, flat collar and needle, drawn upright then tilted.
            int first=vh.currentVertCount; vh.AddVert(P(0,.62f),color,Vector2.zero);
            for(int i=0;i<=24;i++){float a=i*Mathf.PI*2/24;vh.AddVert(P(Mathf.Cos(a)*.3f,.62f+Mathf.Sin(a)*.3f),color,Vector2.zero);if(i>0)vh.AddTriangle(first,first+i,first+i+1);}
            Quad(P(-.2f,.42f),P(.2f,.42f),P(.13f,.02f),P(-.13f,.02f));
            Quad(P(-.38f,.02f),P(.38f,.02f),P(.38f,-.1f),P(-.38f,-.1f));
            Quad(P(-.04f,-.1f),P(.04f,-.1f),P(.015f,-.95f),P(-.015f,-.95f));
        }
    }
}
