using GLTFast;
using GLTFast.Logging;
using GLTFast.Materials;
using GLTFast.Schema;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Uses a referenced shader for runtime imports, avoiding shader-stripping surprises.</summary>
    public sealed class TwinMaterialGenerator : IMaterialGenerator
    {
        readonly UnityEngine.Material template;
        public TwinMaterialGenerator(UnityEngine.Material material) => template=material;
        public UnityEngine.Material GetDefaultMaterial(bool pointsSupport=false) => template;
        // glTFast owns and destroys generated materials. Give it a temporary
        // instance; renderers are assigned the shared asset after instantiation.
        public UnityEngine.Material GenerateMaterial(MaterialBase source,IGltfReadable gltf,bool pointsSupport=false)
            => new UnityEngine.Material(template) { name="Twin import material" };
        public void SetLogger(ICodeLogger logger) { }
    }
}
