using System;
using System.Collections.Generic;
using System.IO;

namespace TwinInspector
{
    [Serializable] public sealed class TwinManifest
    {
        public string schema;
        public string scan;
        public bool demo;
        public float[] origin_world;
        public Region region;
        public List<Tile> tiles;
        [Serializable] public sealed class Region { public string name; public float[] min, max; }
        [Serializable] public sealed class Tile
        {
            public string id;
            public float[] min, max;
            public Dictionary<string, string> files;
            public Dictionary<string, int> triangles;
            public Dictionary<string, long> bytes;
            public int Count(int lod) => triangles.TryGetValue(lod.ToString(), out var count) ? count : 0;
            public int Available(int requested)
            {
                for (int level = requested; level < 3; level++) if (files.ContainsKey(level.ToString())) return level;
                return -1;
            }
        }
        /// <summary>Reject incompatible/corrupt manifests before creating scene objects.</summary>
        public void Validate(string expectedScan)
        {
            if (schema != "twin-manifest/1" || scan != expectedScan || origin_world?.Length != 3 || tiles == null || tiles.Count == 0)
                throw new InvalidDataException("Unsupported twin manifest or scan");
            var ids = new HashSet<string>();
            foreach (var tile in tiles)
            {
                if (!ids.Add(tile.id) || tile.min?.Length != 3 || tile.max?.Length != 3 || tile.files == null || !tile.files.ContainsKey("2"))
                    throw new InvalidDataException("Invalid tile: " + tile.id);
                foreach (var item in tile.files)
                    if (!tile.triangles.ContainsKey(item.Key) || tile.triangles[item.Key] < 1 || !tile.bytes.ContainsKey(item.Key) || tile.bytes[item.Key] > 30000000)
                        throw new InvalidDataException("Invalid tile budget: " + tile.id);
                foreach (float n in tile.min) if (float.IsNaN(n) || float.IsInfinity(n)) throw new InvalidDataException("Invalid bounds");
                foreach (float n in tile.max) if (float.IsNaN(n) || float.IsInfinity(n)) throw new InvalidDataException("Invalid bounds");
            }
        }
    }
}
