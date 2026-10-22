using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace TwinInspector
{
    /// <summary>Exports interoperable JSON and invariant-culture CSV to local app storage.</summary>
    public static class AnnotationExporter
    {
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
            var document = store.Snapshot(); document.schema = "twin-annotations/2"; document.exported_utc = DateTime.UtcNow.ToString("O");
            var name = document.scan + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string path = TwinPaths.SaveFile("exports", name);
            JsonStore.Write(path, document);
            var csv = new StringBuilder(CsvHeader + "\r\n");
            foreach (var item in document.items)
            {
                csv.Append(CsvLine(item)).Append("\r\n");
            }
            File.WriteAllText(Path.ChangeExtension(path, ".csv"), csv.ToString(), new UTF8Encoding(false));
            DevLog.Write("Annotations exported: " + path); return path;
        }
        static string Cell(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }
}
