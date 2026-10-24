using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TwinInspector
{
    /// <summary>One inspectable building space. Its id names every copy of its data: StreamingAssets/twins/id (when packaged),
    /// files/twin/id on the headset, work/twin/id on the workstation, and its annotation/alignment/dismissal files.</summary>
    [Serializable] public sealed class TwinSpace
    {
        public string id, name, building, scan;
        /// <summary>A LOD2 copy ships in the APK; other spaces need their data transferred to the headset.</summary>
        public bool packaged;
        /// <summary>File key for annotations, alignment and dismissals; spaces never share it, even when cut from one scan.</summary>
        public string Storage => id;
    }

    /// <summary>twins/spaces.json, the list offered before the twin loads.</summary>
    [Serializable] public sealed class SpaceCatalog
    {
        public string schema;
        public List<TwinSpace> spaces;

        public static async Task<SpaceCatalog> LoadAsync(CancellationToken token)
        {
            var catalog = JsonConvert.DeserializeObject<SpaceCatalog>(await TwinPaths.ReadTextAsync(TwinPaths.SpacesRoot, "spaces.json", token));
            catalog.Validate();
            return catalog;
        }

        public void Validate()
        {
            if (schema != "twin-spaces/1" || spaces == null || spaces.Count == 0) throw new InvalidDataException("Unsupported space catalog");
            var ids = new HashSet<string>();
            foreach (var space in spaces)
                if (space == null || !SafeFolder(space.id) || !ids.Add(space.id) || string.IsNullOrWhiteSpace(space.name) || string.IsNullOrWhiteSpace(space.scan))
                    throw new InvalidDataException("Invalid space: " + space?.id);
        }

        /// <summary>The configured default when usable, else the first usable space, else the first listed.</summary>
        public TwinSpace Default(string id, Func<TwinSpace, bool> usable = null)
        {
            var preferred = Find(id);
            if (usable == null) return preferred ?? spaces[0];
            return preferred != null && usable(preferred) ? preferred : spaces.Find(space => usable(space)) ?? spaces[0];
        }
        public TwinSpace Find(string id) => spaces.Find(space => space.id == id);

        static bool SafeFolder(string folder) => !string.IsNullOrWhiteSpace(folder) && folder.IndexOfAny(new[] { '/', '\\', ':' }) < 0 && folder != "." && folder != "..";
    }
}
