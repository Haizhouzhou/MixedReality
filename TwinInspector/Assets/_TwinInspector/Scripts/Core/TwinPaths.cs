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
        /// <summary>StreamingAssets folder holding one subfolder per packaged space.</summary>
        public static string SpacesRoot => Application.streamingAssetsPath + "/twins";
        public static string PackagedRoot(TwinSpace space) => space.packaged ? SpacesRoot + "/" + space.id : null;
        static string ExternalRoot(TwinSpace space) => Path.Combine(Application.persistentDataPath, "twin", space.id);
        /// <summary>Editor only: the workstation's pipeline output, settings.EditorDataPath/id.</summary>
        static string EditorRoot(Settings settings, TwinSpace space) => Application.isEditor && !string.IsNullOrWhiteSpace(settings.EditorDataPath) ? Path.Combine(RepositoryPath(settings.EditorDataPath), space.id) : null;
        /// <summary>Relative settings paths are relative to the repository root (the folder above the Unity project).</summary>
        public static string RepositoryPath(string path) => Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", path));
        /// <summary>Packaged in the app, or transferred to this device (or the workstation in the Editor); other spaces are offered greyed out.</summary>
        public static bool Available(Settings settings, TwinSpace space) => space.packaged || File.Exists(Path.Combine(ExternalRoot(space), "manifest.json")) || ManifestScan(EditorRoot(settings, space)) == space.scan;
        public static string ModelsRoot(Settings settings) => Application.isEditor ? RepositoryPath(settings.EditorModelsPath) : Path.Combine(Application.persistentDataPath, "models");
        public static string Resolve(Settings settings, TwinSpace space)
        {
            var editor = EditorRoot(settings, space);
            if (editor != null && ManifestScan(editor) == space.scan) return editor;
            var external = ExternalRoot(space);
            if (File.Exists(Path.Combine(external, "manifest.json"))) return external;
            return PackagedRoot(space) ?? throw new FileNotFoundException("Space data is not on this device: " + space.id);
        }

        static string ManifestScan(string root)
        {
            if (root == null) return null;
            var path = Path.Combine(root, "manifest.json");
            if (!File.Exists(path)) return null;
            try { return Newtonsoft.Json.JsonConvert.DeserializeObject<TwinManifest>(File.ReadAllText(path))?.scan; }
            catch (Newtonsoft.Json.JsonException) { return null; }
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
