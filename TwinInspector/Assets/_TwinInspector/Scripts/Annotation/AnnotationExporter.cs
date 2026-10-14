using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace TwinInspector
{
    /// <summary>Exports interoperable JSON and invariant-culture CSV to local app storage.</summary>
    public static class AnnotationExporter
    {
        public static string Export(AnnotationStore store)
        {
            var document = store.Snapshot(); document.exported_utc = DateTime.UtcNow.ToString("O");
            var name = document.scan + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string path = TwinPaths.SaveFile("exports", name);
            JsonStore.Write(path, document);
            var csv = new StringBuilder("id,index,type,x_twin,y_twin,z_twin,nx,ny,nz,created_utc,label,comment\r\n");
            foreach (var item in document.items)
            {
                csv.Append(Cell(item.id)).Append(',').Append(item.index).Append(',').Append(Cell(item.type));
                foreach (float value in item.position_twin) csv.Append(',').Append(value.ToString("R", CultureInfo.InvariantCulture));
                foreach (float value in item.normal_twin) csv.Append(',').Append(value.ToString("R", CultureInfo.InvariantCulture));
                csv.Append(',').Append(Cell(item.created_utc)).Append(',').Append(Cell(item.label)).Append(',').Append(Cell(item.comment)).Append("\r\n");
            }
            File.WriteAllText(Path.ChangeExtension(path, ".csv"), csv.ToString(), new UTF8Encoding(false));
            DevLog.Write("Annotations exported: " + path); return path;
        }
        static string Cell(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    }
}
