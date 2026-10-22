using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Exports interoperable JSON and invariant-culture CSV to local app storage.</summary>
    public static class AnnotationExporter
    {
        public static int LastPatchCount {get;private set;}
        sealed class SceneSnapshot { public Vector3[] Vertices,Normals; public int[] Triangles; }
        sealed class ExportRequest {public AnnotationDocument Document;public string Path;public Matrix4x4 WorldToTwin;public SceneSnapshot[] Chunks;}
        public const string CsvHeader = "id,index,type,x_twin,y_twin,z_twin,nx,ny,nz,created_utc,label,comment,region_kind,region_source,area_m2,centroid_x,centroid_y,centroid_z,extent_x,extent_y,extent_z,hull_points";
        public static string CsvLine(Annotation item)
        {
            var cells = new List<string> { item.id, item.index.ToString(CultureInfo.InvariantCulture), item.type };
            cells.AddRange(item.position_twin.Select(F)); cells.AddRange(item.normal_twin.Select(F));
            cells.AddRange(new[] { item.created_utc, item.label, item.comment });
            var r = item.region;
            if (r == null) cells.AddRange(new string[10]);
            else
            {
                cells.AddRange(new[] { r.kind, r.source, F(r.area_m2) });
                cells.AddRange(r.centroid_twin.Select(F)); cells.AddRange(r.extent_m.Select(F));
                cells.Add(string.Join(";", r.hull_twin.Select(p => string.Join(" ",p.Select(F)))));
            }
            return string.Join(",", cells.Select(Cell));
        }
        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        public static string Export(AnnotationStore store)
        {
            var request=Prepare(store);var result=Write(request);Report(request);return result;
        }
        public static async Task<string> ExportAsync(AnnotationStore store)
        {
            var request=Prepare(store);var result=await Task.Run(()=>Write(request));Report(request);return result;
        }
        static ExportRequest Prepare(AnnotationStore store)
        {
            var document = store.Snapshot(); document.schema = "twin-annotations/2"; document.exported_utc = DateTime.UtcNow.ToString("O");
            var name = document.scan + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string path = TwinPaths.SaveFile("exports", name);
            var app=AppBootstrap.Instance;
            return new ExportRequest{Document=document,Path=path,WorldToTwin=app&&app.Root?app.Root.WorldToTwinMatrix:Matrix4x4.identity,Chunks=app?.SceneSource?.Chunks.Select(c=>new SceneSnapshot{Vertices=c.Vertices,Normals=c.Normals,Triangles=c.Triangles}).ToArray()??Array.Empty<SceneSnapshot>()};
        }
        static string Write(ExportRequest request)
        {
            var document=request.Document;var path=request.Path;LastPatchCount=0;
            JsonStore.Write(path, document);
            var csv = new StringBuilder(CsvHeader + "\r\n");
            foreach (var item in document.items)
            {
                csv.Append(CsvLine(item)).Append("\r\n");
            }
            File.WriteAllText(Path.ChangeExtension(path, ".csv"), csv.ToString(), new UTF8Encoding(false));
            if(request.Chunks.Length>0)
            {
                string folder=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+"-patches");
                foreach(var item in document.items)
                {
                    if(item.Kind!=AnnotationType.New && item.Kind!=AnnotationType.Changed)continue;
                    var center=TwinRoot.Vector(item.region?.centroid_twin??item.position_twin);var vertices=new List<Vector3>();var triangles=new List<int>();
                    foreach(var chunk in request.Chunks)
                    {
                        var map=new Dictionary<int,int>();
                        for(int i=0;i<chunk.Triangles.Length&&triangles.Count<600000;i+=3)
                        {
                            int a=chunk.Triangles[i],b=chunk.Triangles[i+1],c=chunk.Triangles[i+2];
                            var centroid=request.WorldToTwin.MultiplyPoint3x4((chunk.Vertices[a]+chunk.Vertices[b]+chunk.Vertices[c])/3);
                            if((centroid-center).sqrMagnitude>2.25f)continue;
                            int Index(int source){if(map.TryGetValue(source,out int existing))return existing;int index=vertices.Count;vertices.Add(request.WorldToTwin.MultiplyPoint3x4(chunk.Vertices[source]));map.Add(source,index);return index;}
                            int ia=Index(a),ib=Index(b),ic=Index(c);
                            var desired=request.WorldToTwin.MultiplyVector(chunk.Normals[a]+chunk.Normals[b]+chunk.Normals[c]);
                            if(Vector3.Dot(Vector3.Cross(vertices[ib]-vertices[ia],vertices[ic]-vertices[ia]),desired)<0){int swap=ib;ib=ic;ic=swap;}
                            triangles.Add(ia);triangles.Add(ib);triangles.Add(ic);
                        }
                        if(triangles.Count>=600000)break;
                    }
                    if(triangles.Count==0)continue;
                    Directory.CreateDirectory(folder);
                    // IDs are produced by AnnotationStore; reject imported path separators defensively.
                    if(item.id.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidDataException("Unsafe annotation id");
                    using(var writer=new StreamWriter(Path.Combine(folder,item.id+".obj"),false,new UTF8Encoding(false)))ObjWriter.Write(writer,vertices.ToArray(),triangles.ToArray(),document.scan,item.id,document.exported_utc);
                    LastPatchCount++;
                }
            }
            return path;
        }
        static void Report(ExportRequest request){DevLog.Write("Annotations exported: "+request.Path+" patches="+LastPatchCount);if(request.Chunks.Length==0)DevLog.Write("Refinement patches skipped: no scene mesh available");}
        static string Cell(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }
}
