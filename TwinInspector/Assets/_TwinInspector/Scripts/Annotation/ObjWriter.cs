using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Writes already transformed Z-up vertices without reflecting or scaling them.</summary>
    public static class ObjWriter
    {
        public static void Write(TextWriter writer, Vector3[] vertices, int[] triangles, string scan, string id, string utc)
        {
            if (triangles.Length % 3 != 0) throw new ArgumentException("Incomplete triangle");
            foreach (int index in triangles) if (index < 0 || index >= vertices.Length) throw new ArgumentOutOfRangeException(nameof(triangles));
            writer.WriteLine("# TwinInspector refinement patch; twin frame, metres, right-handed Z-up");
            writer.WriteLine("# scan=" + Clean(scan) + " annotation=" + Clean(id) + " utc=" + Clean(utc));
            foreach (var v in vertices) writer.WriteLine("v " + F(v.x) + " " + F(v.y) + " " + F(v.z));
            for (int i = 0; i < triangles.Length; i += 3) writer.WriteLine($"f {triangles[i]+1} {triangles[i+1]+1} {triangles[i+2]+1}");
        }
        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        static string Clean(string v) => (v ?? "").Replace('\r',' ').Replace('\n',' ');
    }
}
