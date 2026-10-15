using UnityEngine;
using UnityEngine.UI;
namespace TwinInspector
{
    /// <summary>Actual polygon identity in UGUI; captions are separate, noninteractive graphics.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AnnotationGlyph : MaskableGraphic
    {
        public AnnotationType Kind;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=GetPixelAdjustedRect(); Vector2 center=r.center; float radius=Mathf.Min(r.width,r.height)*.48f;
            int count=Kind==AnnotationType.Missing?48:Kind==AnnotationType.Changed?3:4;
            float start=Kind==AnnotationType.New?45:90;
            float scale=Kind==AnnotationType.New?1.12f:1;
            Polygon(radius*scale,Color.black,0);
            Polygon((radius-2)*scale,AnnotationTypes.Colour(Kind),0);
            var ink=new Color(.067f,.067f,.067f,1);
            if(Kind==AnnotationType.Missing || Kind==AnnotationType.New) { Bar(new Vector2(-radius*.45f,0),new Vector2(radius*.45f,0),3); if(Kind==AnnotationType.New) Bar(new Vector2(0,-radius*.45f),new Vector2(0,radius*.45f),3); }
            else if(Kind==AnnotationType.Changed) { var a=new Vector2(0,radius*.35f); var b=new Vector2(-radius*.33f,-radius*.30f); var c=new Vector2(radius*.33f,-radius*.30f); Bar(a,b,2); Bar(b,c,2); Bar(c,a,2); }
            else { int oldCount=count; count=24; Polygon(radius*.15f,ink,0); count=oldCount; }
            void Polygon(float size,Color tint,float unused)
            {
                int first=vh.currentVertCount; vh.AddVert(center,tint,Vector2.zero);
                for(int i=0;i<count;i++) { float a=(start+360f*i/count)*Mathf.Deg2Rad; vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*size,tint,Vector2.zero); }
                for(int i=0;i<count;i++) vh.AddTriangle(first,first+1+i,first+1+(i+1)%count);
            }
            void Bar(Vector2 a,Vector2 b,float width)
            {
                Quad(a,b,width+2,new Color(.95f,.945f,.925f)); Quad(a,b,width,ink);
            }
            void Quad(Vector2 a,Vector2 b,float width,Color tint)
            {
                var d=(b-a).normalized; var n=new Vector2(-d.y,d.x)*width*.5f; int k=vh.currentVertCount;
                vh.AddVert(center+a-n,tint,Vector2.zero); vh.AddVert(center+a+n,tint,Vector2.zero); vh.AddVert(center+b+n,tint,Vector2.zero); vh.AddVert(center+b-n,tint,Vector2.zero); vh.AddTriangle(k,k+1,k+2); vh.AddTriangle(k,k+2,k+3);
            }
        }
    }
}
