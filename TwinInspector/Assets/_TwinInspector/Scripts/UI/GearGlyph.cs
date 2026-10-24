using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TwinInspector
{
    /// <summary>Eight-tooth gear for the Setup cell; follows its button label's ink so selection inverts it.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GearGlyph : MaskableGraphic
    {
        public TMP_Text Ink;
        const int Teeth=8;
        void LateUpdate() { if(Ink && color!=Ink.color) color=Ink.color; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=GetPixelAdjustedRect(); Vector2 center=r.center;
            float outer=Mathf.Min(r.width,r.height)*.5f, root=outer*.76f, hole=outer*.34f, period=360f/Teeth;
            // Star-shaped outline: tapered tooth tops on the outer radius, short arcs on the root radius.
            var outline=new System.Collections.Generic.List<(float angle,float radius)>();
            for(int k=0;k<Teeth;k++)
            {
                float c=k*period;
                outline.Add((c-period*.24f,root)); outline.Add((c-period*.15f,outer)); outline.Add((c+period*.15f,outer)); outline.Add((c+period*.24f,root));
                for(int s=1;s<4;s++) outline.Add((c+period*(.24f+.52f*s/4),root));
            }
            for(int i=0;i<outline.Count;i++)
            {
                var a=outline[i]; var b=outline[(i+1)%outline.Count]; int k=vh.currentVertCount;
                vh.AddVert(center+Polar(a.angle,hole),color,Vector2.zero); vh.AddVert(center+Polar(a.angle,a.radius),color,Vector2.zero);
                vh.AddVert(center+Polar(b.angle,b.radius),color,Vector2.zero); vh.AddVert(center+Polar(b.angle,hole),color,Vector2.zero);
                vh.AddTriangle(k,k+1,k+2); vh.AddTriangle(k,k+2,k+3);
            }
            static Vector2 Polar(float degrees,float radius)=>new Vector2(Mathf.Cos(degrees*Mathf.Deg2Rad),Mathf.Sin(degrees*Mathf.Deg2Rad))*radius;
        }
    }
}
