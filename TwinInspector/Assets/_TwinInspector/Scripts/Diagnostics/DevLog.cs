using System;
using System.IO;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Local diagnostic events; no network or telemetry.</summary>
    public static class DevLog
    {
        static string path;
        public static void Write(string message)
        {
            try
            {
                if (path == null) path = TwinPaths.SaveFile("logs", DateTime.UtcNow.ToString("yyyy-MM-dd"), ".log");
                File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
            }
            catch (IOException) { /* Logging must not prevent inspection. */ }
            Debug.Log("[TwinInspector] " + message);
        }
    }
}
