using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.InferenceEngine;

namespace TwinInspector.Editor
{
    /// <summary>Serializes imported ONNX assets to external, integrity-checked models; never bundles their weights.</summary>
    public static class SamModelSerializer
    {
        [MenuItem("TwinInspector/Serialize SAM models")]
        public static void Serialize()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            string output=Path.Combine(root,"tools/sam/models");Directory.CreateDirectory(output);
            var manifest=JsonUtility.FromJson<SamModels>(File.ReadAllText(Path.Combine(root,"work/sam/onnx-manifest.json")));
            foreach(var name in new[]{"mobile_sam_encoder","mobile_sam_decoder"})
            {
                var asset=AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/_TwinInspector/ModelsImport/"+name+".onnx");
                if(!asset)throw new InvalidOperationException("Missing imported model: "+name);
                string path=Path.Combine(output,name+".sentis");ModelWriter.Save(path,ModelLoader.Load(asset));
                if(new FileInfo(path).Length>=50000000)throw new InvalidDataException("Model exceeds 50 MB: "+path);
                var check=ModelLoader.Load(path);if(check.layers.Count==0)throw new InvalidDataException("Empty model: "+name);
            }
            manifest.encoder="mobile_sam_encoder.sentis";manifest.decoder="mobile_sam_decoder.sentis";
            manifest.encoder_sha256=SamModels.Hash(Path.Combine(output,manifest.encoder));manifest.decoder_sha256=SamModels.Hash(Path.Combine(output,manifest.decoder));
            manifest.low_res_only=File.Exists(Path.Combine(root,"work/sam/low-res-only.flag"));
            JsonStore.Write(Path.Combine(output,"models.json"),manifest);
            File.WriteAllText("docs/evidence/sam-serialize-v13.txt",JsonUtility.ToJson(manifest,true)+"\nBoth serialized models loaded successfully.\n");
        }
    }
}
