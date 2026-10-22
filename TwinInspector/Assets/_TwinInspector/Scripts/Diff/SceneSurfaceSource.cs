using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace TwinInspector
{
    /// <summary>AR Foundation 6.6 mesh updates captured once in world coordinates.</summary>
    public sealed class SceneSurfaceSource : MonoBehaviour,ISceneSurfaceSource
    {
        readonly List<SurfaceChunk> chunks=new List<SurfaceChunk>();
        readonly Dictionary<string,SurfaceChunk> lookup=new Dictionary<string,SurfaceChunk>();
        readonly HashSet<string> reported=new HashSet<string>();
        ARMeshManager manager;
        public IReadOnlyList<SurfaceChunk> Chunks => chunks;
        public bool Ready => manager && manager.enabled && chunks.Count>0;
        public event Action<SurfaceChunk> ChunkChanged,ChunkRemoved;
        public void Initialize(ARMeshManager meshManager) {manager=meshManager;manager.normals=true;manager.meshInfosChanged.AddListener(OnMeshes);}
        void OnMeshes(ARMeshInfosChangedEventArgs args)
        {
            foreach(var info in args.removed) if(lookup.TryGetValue(info.id.ToString(),out var chunk)) {lookup.Remove(chunk.Id);chunks.Remove(chunk);ChunkRemoved?.Invoke(chunk);}
            foreach(var info in args.added) Capture(info);
            foreach(var info in args.updated) Capture(info);
        }
        void Capture(MeshUpdateInfo info)
        {
            string id=info.id.ToString(); if(!info.meshFilter) return;
            try {if(lookup.TryGetValue(id,out var chunk)) chunk.Capture(info.meshFilter);else {chunk=new SurfaceChunk(id,info.meshFilter);lookup.Add(id,chunk);chunks.Add(chunk);}ChunkChanged?.Invoke(chunk);}
            catch(Exception e) {if(reported.Add(id)) DevLog.Write("Scene capture unavailable: "+e.Message);}
        }
        void OnDestroy() {if(manager) manager.meshInfosChanged.RemoveListener(OnMeshes);}
    }
}
