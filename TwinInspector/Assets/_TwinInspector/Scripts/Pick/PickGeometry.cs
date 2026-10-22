using UnityEngine;

namespace TwinInspector
{
    /// <summary>Pure SAM coordinate, sampling and depth-rejection rules.</summary>
    public static class PickGeometry
    {
        public const int MinimumHits=12;
        public static bool TryPixel(Vector3 viewport,out Vector2 pixel)
        {
            pixel=new Vector2(viewport.x*1024,(1-viewport.y)*1024);
            return viewport.z>0&&viewport.x>=0&&viewport.x<=1&&viewport.y>=0&&viewport.y<=1;
        }
        public static bool AcceptDepth(float distance,float prompt)=>Mathf.Abs(distance-prompt)<=Mathf.Max(.5f,prompt*.25f);
        public static bool EnoughHits(int count)=>count>=MinimumHits;
        public static bool CoherentNormal(Vector3 normal,Vector3 prompt)=>Mathf.Abs(Vector3.Dot(normal.normalized,prompt.normalized))>=.8660254f;
        public static float Bilinear(float[] mask,int width,int height,float x,float y)
        {
            x=Mathf.Clamp(x,0,width-1);y=Mathf.Clamp(y,0,height-1);int x0=(int)x,y0=(int)y,x1=Mathf.Min(x0+1,width-1),y1=Mathf.Min(y0+1,height-1);
            return Mathf.Lerp(Mathf.Lerp(mask[y0*width+x0],mask[y0*width+x1],x-x0),Mathf.Lerp(mask[y1*width+x0],mask[y1*width+x1],x-x0),y-y0);
        }
    }
}
