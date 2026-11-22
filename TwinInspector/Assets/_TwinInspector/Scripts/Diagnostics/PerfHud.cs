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
        float elapsed, lastLog, requestedRate, worstFrame;
        int frames, slowFrames;
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
            // Hitches: averages hide them, but a single long frame is what makes motion look clunky. Frames over 1.5 display
            // periods count as slow; while the app is not focused (system menu, headset off) frames do not count.
            if (Application.isFocused)
            {
                float frame = Time.unscaledDeltaTime * 1000, period = 1000 / Mathf.Max(1, RuntimeTuning.RefreshRate);
                if (frame > 1.5f * period) slowFrames++;
                worstFrame = Mathf.Max(worstFrame, frame);
            }
            if (elapsed < .5f) return;
            Fps = frames / elapsed; frames = 0; elapsed = 0;
            SubsystemManager.GetSubsystems(displays);
            CpuMs = GpuMs = -1;
            foreach (var display in displays)
            {
                if (!display.running) continue;
                if (XRStats.TryGetStat(display,"/perfmetrics_android/app/cpu_frametime",out float cpu)) CpuMs = cpu;
                if (XRStats.TryGetStat(display,"/perfmetrics_android/app/gpu_frametime",out float gpu)) GpuMs = gpu;
                // Fixed rate from Diagnostics → Performance; switching with tile loading changed the display mode every 1–2 s.
                float desired = RuntimeTuning.RefreshRate;
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
            RuntimeTuning.ApplyRendering();
            if (Time.unscaledTime-lastLog >= 10)
            {
                lastLog = Time.unscaledTime;
                DevLog.Write($"Metrics source={(Application.isEditor?"Editor":"device")} fps={Fps:F1} cpu_ms={CpuMs:F2} gpu_ms={GpuMs:F2} slow_frames={slowFrames} worst_ms={worstFrame:F0} tiles={loader.LoadedTiles} resident_tris={loader.Triangles} view={(AppBootstrap.Instance && AppBootstrap.Instance.Style ? AppBootstrap.Instance.Style.Mode + (AppBootstrap.Instance.Style.Technical ? "/Technical" : "/Colour") : "-")} {RuntimeTuning.Describe(settings)}");
                slowFrames = 0; worstFrame = 0;
            }
        }
        public string Readout => L.Format("diagnostics.metrics", Fps, CpuMs<0?L.Get("text.nA"):CpuMs.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" ms", GpuMs<0?L.Get("text.nA"):GpuMs.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" ms", loader?.LoadedTiles??0, loader?.Triangles??0);
    }
}
