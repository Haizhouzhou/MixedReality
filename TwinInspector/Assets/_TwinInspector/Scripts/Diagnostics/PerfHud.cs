using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features.Android;
using XRStats = UnityEngine.XR.Provider.XRStats;

namespace TwinInspector
{
    /// <summary>Samples actual runtime statistics; unavailable device values remain n/a.</summary>
    public sealed class PerfHud : MonoBehaviour
    {
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        TwinLoader loader; Settings settings;
        float elapsed, lastLog, requestedRate;
        int frames;
        public float Fps { get; private set; }
        public float CpuMs { get; private set; } = -1;
        public float GpuMs { get; private set; } = -1;
        public float RefreshRate => requestedRate;
        public bool Visible { get; set; }
        public void Initialize(TwinLoader twin, Settings config) { loader = twin; settings = config; }
        void Update()
        {
            if (!loader) return;
            frames++; elapsed += Time.unscaledDeltaTime;
            if (elapsed < .5f) return;
            Fps = frames / elapsed; frames = 0; elapsed = 0;
            SubsystemManager.GetSubsystems(displays);
            CpuMs = GpuMs = -1;
            foreach (var display in displays)
            {
                if (!display.running) continue;
                if (XRStats.TryGetStat(display,"/perfmetrics_android/app/cpu_frametime",out float cpu)) CpuMs = cpu;
                if (XRStats.TryGetStat(display,"/perfmetrics_android/app/gpu_frametime",out float gpu)) GpuMs = gpu;
                float desired = loader.PendingLoads > 0 ? settings.LoadingRefreshRate : settings.TargetRefreshRate;
                if (!Application.isEditor && !Mathf.Approximately(desired,requestedRate))
                {
                    var status = display.TryGetSupportedDisplayRefreshRates(Allocator.Temp,out var rates);
                    if (status.IsSuccess())
                    {
                        float best = 0;
                        foreach (float rate in rates) if (Mathf.Abs(rate-desired) < Mathf.Abs(best-desired)) best=rate;
                        if (best > 0 && display.TrySetDisplayRefreshRate(best).IsSuccess()) { requestedRate=best; DevLog.Write("Display refresh requested: "+best); }
                    }
                    if (rates.IsCreated) rates.Dispose();
                }
            }
            if (Time.unscaledTime-lastLog >= 10)
            {
                lastLog = Time.unscaledTime;
                DevLog.Write($"Metrics source={(Application.isEditor?"Editor":"device")} fps={Fps:F1} cpu_ms={CpuMs:F2} gpu_ms={GpuMs:F2} tiles={loader.LoadedTiles} resident_tris={loader.Triangles}");
            }
        }
        public string Readout => L.Format("{0:F1} fps\nCPU {1}    GPU {2}\n{3} tiles    {4:N0} triangles", Fps, CpuMs<0?L.Get("n/a"):CpuMs.ToString("F1")+" ms", GpuMs<0?L.Get("n/a"):GpuMs.ToString("F1")+" ms", loader?.LoadedTiles??0, loader?.Triangles??0);
    }
}
