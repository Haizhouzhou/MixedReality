using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Lazily owns two external-model workers. Encoder scheduling yields at the configured frame budget.</summary>
    public sealed class SamRunner : IDisposable
    {
        Worker encoder,decoder;readonly SamModels models;readonly string directory;
        public double EncoderMs {get;private set;}
        public double MaxScheduleMs {get;private set;}
        public double DecoderScheduleMs {get;private set;}
        public SamRunner(SamModels modelManifest,string modelDirectory){models=modelManifest;directory=modelDirectory;}
        public async Task<float[]> RunAsync(Color32[] pixels,Vector2 prompt,float budget,CancellationToken token)
        {
            if(encoder==null)
            {
                var backend=SystemInfo.supportsComputeShaders?BackendType.GPUCompute:BackendType.CPU;
                encoder=new Worker(ModelLoader.Load(Path.Combine(directory,models.encoder)),backend);
                decoder=new Worker(ModelLoader.Load(Path.Combine(directory,models.decoder)),backend);
                DevLog.Write("SAM backend="+backend);
            }
            int plane=1024*1024;var data=new float[plane*3];var frame=Stopwatch.StartNew();
            for(int y=0;y<1024;y++)
            {
                for(int x=0;x<1024;x++)
                {
                    // GPU texture rows are bottom-up; SAM uses top-left pixel coordinates.
                    var color=pixels[(1023-y)*1024+x];int n=y*1024+x;
                    data[n]=(color.r-models.mean[0])/models.std[0];data[plane+n]=(color.g-models.mean[1])/models.std[1];data[2*plane+n]=(color.b-models.mean[2])/models.std[2];
                }
                if(frame.Elapsed.TotalMilliseconds>=budget){await Awaitable.NextFrameAsync(token);frame.Restart();}
            }
            using(var input=new Tensor<float>(new TensorShape(1,3,1024,1024),data))
            {
                var total=Stopwatch.StartNew();var schedule=encoder.ScheduleIterable(input);frame.Restart();MaxScheduleMs=0;
                try
                {
                    while(schedule.MoveNext())
                    {
                        token.ThrowIfCancellationRequested();
                        if(frame.Elapsed.TotalMilliseconds>=budget){MaxScheduleMs=Math.Max(MaxScheduleMs,frame.Elapsed.TotalMilliseconds);await Awaitable.NextFrameAsync(token);frame.Restart();}
                    }
                    MaxScheduleMs=Math.Max(MaxScheduleMs,frame.Elapsed.TotalMilliseconds);
                }
                finally{(schedule as IDisposable)?.Dispose();}
                var embedding=encoder.PeekOutput() as Tensor<float>;
                embedding.ReadbackRequest();
                while(!embedding.IsReadbackRequestDone())await Awaitable.NextFrameAsync(token);
                // Keep encoder output on the GPU; decoder consumes that same tensor.
                using(var coords=new Tensor<float>(new TensorShape(1,2,2),new[]{prompt.x,prompt.y,0f,0f}))
                using(var labels=new Tensor<float>(new TensorShape(1,2),new[]{1f,-1f}))
                using(var mask=new Tensor<float>(new TensorShape(1,1,256,256)))
                using(var hasMask=new Tensor<float>(new TensorShape(1),new[]{0f}))
                using(var original=new Tensor<float>(new TensorShape(2),new[]{1024f,1024f}))
                {
                    EncoderMs=total.Elapsed.TotalMilliseconds;token.ThrowIfCancellationRequested();
                    decoder.SetInput("image_embeddings",embedding);decoder.SetInput("point_coords",coords);decoder.SetInput("point_labels",labels);decoder.SetInput("mask_input",mask);decoder.SetInput("has_mask_input",hasMask);
                    if(!models.low_res_only)decoder.SetInput("orig_im_size",original);
                    frame.Restart();decoder.Schedule();DecoderScheduleMs=frame.Elapsed.TotalMilliseconds;
                    var output=decoder.PeekOutput("low_res_masks") as Tensor<float>;
                    using(var cpu=await output.ReadbackAndCloneAsync())
                    {token.ThrowIfCancellationRequested();return cpu.DownloadToArray();}
                }
            }
        }
        public void Dispose(){encoder?.Dispose();decoder?.Dispose();encoder=null;decoder=null;}
    }
}
