using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TwinInspector
{
    /// <summary>One non-stereo, colour-only twin render. Retains its pose through mask back-projection.</summary>
    public sealed class TwinCapture : IDisposable
    {
        public Camera Camera {get;private set;}
        RenderTexture target;
        public Vector2 Prompt {get;private set;}
        public TwinCapture(Camera viewer,Vector3 hit)
        {
            Camera=new GameObject("SAM twin capture").AddComponent<Camera>();Camera.enabled=false;
            // URP SingleCameraRequest uses its standalone (non-XR-pass) path. The built-in
            // stereoTargetEye setter is unsupported under SRP and emits a warning in Unity 6.
            Camera.transform.SetPositionAndRotation(viewer.transform.position,Quaternion.LookRotation(hit-viewer.transform.position,Vector3.up));
            Camera.fieldOfView=viewer.fieldOfView;Camera.aspect=1;Camera.nearClipPlane=.05f;Camera.farClipPlane=80;Camera.cullingMask=1<<11;
            Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=new Color(.949f,.945f,.925f);Camera.allowHDR=false;Camera.allowMSAA=false;
            if(!PickGeometry.TryPixel(Camera.WorldToViewportPoint(hit),out var prompt)){Dispose();throw new InvalidOperationException("Prompt outside capture");}Prompt=prompt;
            target=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();
        }
        public async Task<Color32[]> ReadAsync(CancellationToken token)
        {
            float alpha=Shader.GetGlobalFloat("_TwinAlpha"),technical=Shader.GetGlobalFloat("_TwinTechnical"),lens=Shader.GetGlobalFloat("_TwinLensEnabled");
            try
            {
                Shader.SetGlobalFloat("_TwinAlpha",1);Shader.SetGlobalFloat("_TwinTechnical",0);Shader.SetGlobalFloat("_TwinLensEnabled",0);
                RenderPipeline.SubmitRenderRequest(Camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            }
            finally{Shader.SetGlobalFloat("_TwinAlpha",alpha);Shader.SetGlobalFloat("_TwinTechnical",technical);Shader.SetGlobalFloat("_TwinLensEnabled",lens);}
            var request=AsyncGPUReadback.Request(target,0,TextureFormat.RGBA32);
            // The texture must remain alive until the asynchronous copy finishes, including cancellation.
            while(!request.done)await Awaitable.NextFrameAsync();
            token.ThrowIfCancellationRequested();if(request.hasError)throw new InvalidOperationException("SAM capture readback failed");
            return request.GetData<Color32>().ToArray();
        }
        public void Dispose(){if(target){target.Release();UnityEngine.Object.Destroy(target);target=null;}if(Camera){UnityEngine.Object.Destroy(Camera.gameObject);Camera=null;}}
    }
}
