using System;
using System.Collections.Generic;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Edge-connected triangle components. Welding handles glTF normal/UV splits without joining point-only contact.</summary>
    public static class DiffClusterer
    {
        public sealed class Cluster { public int[] Triangles; public RegionBuilder.Result Region; }
        public static int[] Weld(Vector3[] vertices)
        {
            var ids=new Dictionary<(int,int,int),int>();var welded=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++){var v=vertices[i];var key=((int)Math.Round(v.x*10000),(int)Math.Round(v.y*10000),(int)Math.Round(v.z*10000));if(!ids.TryGetValue(key,out int n)){n=ids.Count;ids.Add(key,n);}welded[i]=n;}
            return welded;
        }
        public static List<Cluster> Build(Vector3[] vertices,int[] triangles,bool[] unexplained,float minArea,float minExtent,int[] welded=null)
        {
            welded=welded??Weld(vertices);int count=triangles.Length/3;var parent=new int[count];var edges=new Dictionary<ulong,int>();
            int Find(int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}
            for(int i=0;i<count;i++)parent[i]=i;
            for(int i=0;i<count;i++)if(unexplained[i])for(int side=0;side<3;side++)
            {
                int a=welded[triangles[i*3+side]],b=welded[triangles[i*3+(side+1)%3]];if(a==b)continue;
                ulong key=((ulong)(uint)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
                if(edges.TryGetValue(key,out int other))parent[Find(i)]=Find(other);else edges.Add(key,i);
            }
            var groups=new Dictionary<int,List<int>>();
            for(int i=0;i<count;i++)if(unexplained[i]){int id=Find(i);if(!groups.TryGetValue(id,out var list)){list=new List<int>();groups.Add(id,list);}list.Add(i);}
            var result=new List<Cluster>();
            foreach(var list in groups.Values)
            {
                var indices=new int[list.Count*3];for(int i=0;i<list.Count;i++)Array.Copy(triangles,list[i]*3,indices,i*3,3);
                RegionBuilder.Result r;try{r=RegionBuilder.Build(vertices,indices);}catch(ArgumentException){continue;}
                if(r.Area>=minArea && Mathf.Max(r.Extent.x,Mathf.Max(r.Extent.y,r.Extent.z))>=minExtent)result.Add(new Cluster{Triangles=list.ToArray(),Region=r});
            }
            return result;
        }
    }
}
