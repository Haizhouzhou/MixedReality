using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TwinInspector
{
    /// <summary>Resolves external data, an Editor override, or packaged Android assets.</summary>
    public static class TwinPaths
    {
        public static string Resolve(Settings settings)
        {
#if UNITY_EDITOR
            if (File.Exists(Path.Combine(settings.EditorDataPath, "manifest.json"))) return settings.EditorDataPath;
#endif
            var external = Path.Combine(Application.persistentDataPath, "twin", settings.DataFolder);
            return File.Exists(Path.Combine(external, "manifest.json")) ? external : Application.streamingAssetsPath + "/twin_demo";
        }

        public static string Url(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative.Contains("..") || relative.Contains(":") || relative.StartsWith("/") || relative.StartsWith("\\"))
                throw new InvalidDataException("Unsafe relative data path");
            var combined = root.TrimEnd('/', '\\') + "/" + relative.Replace('\\', '/');
            return combined.Contains("://") ? combined : new Uri(Path.GetFullPath(combined)).AbsoluteUri;
        }

        public static async Task<string> ReadTextAsync(string root, string relative, CancellationToken token)
        {
            using var request = UnityWebRequest.Get(Url(root, relative));
            request.timeout = 30;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (token.IsCancellationRequested) { request.Abort(); token.ThrowIfCancellationRequested(); }
                await Task.Yield();
            }
            if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error + ": " + relative);
            return request.downloadHandler.text;
        }

        public static string SaveFile(string category, string scan, string extension = ".json")
        {
            var directory = Path.Combine(Application.persistentDataPath, category);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, scan + extension);
        }
    }
}
