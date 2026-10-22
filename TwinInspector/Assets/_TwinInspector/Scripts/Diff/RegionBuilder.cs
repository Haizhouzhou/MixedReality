using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Frame-independent PCA projection and convex hull, safe on a worker thread.</summary>
    public static class RegionBuilder
    {
        public sealed class Result
        {
            public Vector3 Centroid, Normal, Extent;
            public Vector3[] Hull;
            public float Area;
            public AnnotationRegion ToRegion(string source) => new AnnotationRegion {
                source = source, centroid_twin = TwinRoot.Array(Centroid), normal_twin = TwinRoot.Array(Normal),
                extent_m = TwinRoot.Array(Extent), area_m2 = Area, hull_twin = Array.ConvertAll(Hull, TwinRoot.Array) };
        }
        public static Result Build(Vector3[] points, int[] triangles)
        {
            if (points == null || points.Length < 3 || triangles.Length < 3) throw new ArgumentException("A region needs a triangle");
            var unique = new HashSet<Vector3>();
            Vector3 center = Vector3.zero, oriented = Vector3.zero; float area = 0;
            for (int i=0; i<triangles.Length; i+=3)
            {
                var a=points[triangles[i]]; var b=points[triangles[i+1]]; var c=points[triangles[i+2]];
                var cross=Vector3.Cross(b-a,c-a); float weight=cross.magnitude*.5f;
                center+=(a+b+c)*(weight/3); area+=weight; oriented+=cross;
                unique.Add(a); unique.Add(b); unique.Add(c);
            }
            if (area < 1e-10f) throw new ArgumentException("Degenerate region");
            center/=area;
            var covariance=new double[3,3];
            foreach(var p in unique) { var d=p-center; for(int j=0;j<3;j++) for(int k=0;k<3;k++) covariance[j,k]+=(double)d[j]*d[k]; }
            var eigen=new double[,] {{1,0,0},{0,1,0},{0,0,1}};
            // Jacobi rotations diagonalise the symmetric covariance; smallest eigenvector is the plane normal.
            for(int iteration=0;iteration<32;iteration++)
            {
                int p=0,q=1;
                for(int j=0;j<3;j++) for(int k=j+1;k<3;k++) if(Math.Abs(covariance[j,k])>Math.Abs(covariance[p,q])) {p=j;q=k;}
                if(Math.Abs(covariance[p,q])<1e-12) break;
                double angle=.5*Math.Atan2(2*covariance[p,q],covariance[q,q]-covariance[p,p]);
                double s=Math.Sin(angle),c=Math.Cos(angle);
                for(int j=0;j<3;j++) {double a=covariance[j,p],b=covariance[j,q]; covariance[j,p]=c*a-s*b; covariance[j,q]=s*a+c*b;}
                for(int j=0;j<3;j++) {double a=covariance[p,j],b=covariance[q,j]; covariance[p,j]=c*a-s*b; covariance[q,j]=s*a+c*b;}
                for(int j=0;j<3;j++) {double a=eigen[j,p],b=eigen[j,q]; eigen[j,p]=c*a-s*b; eigen[j,q]=s*a+c*b;}
            }
            int smallest=0,largest=0;
            for(int i=1;i<3;i++) {if(covariance[i,i]<covariance[smallest,smallest]) smallest=i; if(covariance[i,i]>covariance[largest,largest]) largest=i;}
            var n=new Vector3((float)eigen[0,smallest],(float)eigen[1,smallest],(float)eigen[2,smallest]).normalized;
            if(Vector3.Dot(n,oriented)<0) n=-n;
            var u=new Vector3((float)eigen[0,largest],(float)eigen[1,largest],(float)eigen[2,largest]).normalized;
            if(largest==smallest) u=Vector3.Cross(n,Math.Abs(n.x)<.9?Vector3.right:Vector3.up).normalized;
            var v=Vector3.Cross(n,u).normalized;
            var projected=new List<Vector2>(unique.Count);
            var min=new Vector3(float.MaxValue,float.MaxValue,float.MaxValue); var max=-min;
            foreach(var p in unique) {var d=p-center; var xyz=new Vector3(Vector3.Dot(d,u),Vector3.Dot(d,v),Vector3.Dot(d,n)); min=Vector3.Min(min,xyz);max=Vector3.Max(max,xyz);projected.Add(new Vector2(xyz.x,xyz.y));}
            projected.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));
            var hull=new List<Vector2>();
            foreach(var p in projected) {while(hull.Count>=2 && Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=1e-8f) hull.RemoveAt(hull.Count-1);hull.Add(p);}
            int lower=hull.Count;
            for(int i=projected.Count-2;i>=0;i--) {var p=projected[i];while(hull.Count>lower && Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=1e-8f) hull.RemoveAt(hull.Count-1);hull.Add(p);}
            if(hull.Count>1) hull.RemoveAt(hull.Count-1);
            while(hull.Count>64) {int remove=0;float best=float.MaxValue;for(int i=0;i<hull.Count;i++){float a=Math.Abs(Cross(hull[(i+hull.Count-1)%hull.Count],hull[i],hull[(i+1)%hull.Count]));if(a<best){best=a;remove=i;}}hull.RemoveAt(remove);}
            return new Result { Centroid=center,Normal=n,Extent=max-min,Area=area,Hull=hull.ConvertAll(p=>center+u*p.x+v*p.y).ToArray() };
        }
        static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
    }
}
