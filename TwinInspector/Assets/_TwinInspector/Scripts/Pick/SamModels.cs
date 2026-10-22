using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace TwinInspector
{
    /// <summary>External model contract, including hashes and the single source of preprocessing constants.</summary>
    [Serializable,Preserve] public sealed class SamModels
    {
        public int input_size=1024;
        public float[] mean, std;
        public string encoder,decoder,encoder_sha256,decoder_sha256,revision;
        public bool low_res_only;
        public static async Task<SamModels> VerifyAsync(string directory)
        {
            return await Task.Run(()=>
            {
                var data=JsonStore.Read<SamModels>(Path.Combine(directory,"models.json"));
                if(data==null||data.input_size!=1024||data.mean?.Length!=3||data.std?.Length!=3)throw new InvalidDataException("Invalid SAM model manifest");
                for(int i=0;i<3;i++)if(data.std[i]<=0||float.IsNaN(data.mean[i])||float.IsNaN(data.std[i]))throw new InvalidDataException("Invalid normalization");
                Verify(directory,data.encoder,data.encoder_sha256);Verify(directory,data.decoder,data.decoder_sha256);return data;
            });
        }
        static void Verify(string directory,string file,string hash)
        {
            if(string.IsNullOrEmpty(file)||Path.GetFileName(file)!=file||!file.EndsWith(".sentis",StringComparison.Ordinal))throw new InvalidDataException("Unsafe model path");
            if(!string.Equals(Hash(Path.Combine(directory,file)),hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SAM model hash mismatch: "+file);
        }
        public static string Hash(string file)
        {
            using(var stream=File.OpenRead(file))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
    }
}
