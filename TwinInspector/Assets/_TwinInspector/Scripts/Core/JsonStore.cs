using System;
using System.IO;
using Newtonsoft.Json;

namespace TwinInspector
{
    /// <summary>Atomic, readable JSON with a recoverable previous version.</summary>
    public static class JsonStore
    {
        public static readonly JsonSerializerSettings Format = new JsonSerializerSettings
        { Formatting = Formatting.Indented, TypeNameHandling = TypeNameHandling.None };

        public static T Read<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try { return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), Format); }
            catch (Exception error) when (error is IOException || error is JsonException)
            {
                DevLog.Write("JSON recovery: " + Path.GetFileName(path) + " / " + error.Message);
                return File.Exists(path + ".bak") ? JsonConvert.DeserializeObject<T>(File.ReadAllText(path + ".bak"), Format) : null;
            }
        }

        public static void Write<T>(string path, T data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".pending";
            File.WriteAllText(temporary, JsonConvert.SerializeObject(data, Format));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
