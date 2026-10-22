using System;
using System.Collections.Generic;

namespace TwinInspector
{
    public interface ISceneSurfaceSource
    {
        IReadOnlyList<SurfaceChunk> Chunks { get; }
        event Action<SurfaceChunk> ChunkChanged;
        event Action<SurfaceChunk> ChunkRemoved;
        bool Ready { get; }
    }
}
