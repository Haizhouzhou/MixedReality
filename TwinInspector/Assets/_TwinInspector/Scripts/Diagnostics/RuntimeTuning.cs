using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TwinInspector
{
    /// <summary>Performance switches for headset measurements, changed at runtime from Diagnostics → Performance and
    /// kept across restarts (PlayerPrefs), so one build can compare every combination. Index 0 is always the
    /// shipped behaviour except for the refresh rate, which is fixed instead of switching with tile loading.</summary>
    public static class RuntimeTuning
    {
        /// <summary>Resident triangle budget; 0 means the Settings asset value (1.2 M). The default is 800 k: HG Region A
        /// at 1.15-1.2 M ran at 29-41 FPS with 45 ms GPU frames, HG E7 at 865 k at 62-72 FPS and CAB at 589 k at 70-72 FPS
        /// (Galaxy XR, Nov 21).</summary>
        public static readonly int[] Budgets = { 800000, 0, 500000 };
        /// <summary>Factor on the Settings LOD distances (LOD0, LOD1, culling).</summary>
        public static readonly float[] DistanceScales = { 1f, .6f, .35f };
        public static readonly int[] MsaaSamples = { 4, 2, 1 };
        public static readonly float[] RefreshRates = { 72, 90 };
        public const string Budget = "budget", Distance = "distance", Msaa = "msaa", Refresh = "refresh";
        public static event Action Changed;

        public static int Index(string key, int count) => Mathf.Clamp(PlayerPrefs.GetInt("tuning." + key, 0), 0, count - 1);
        public static void Cycle(string key, int count)
        {
            PlayerPrefs.SetInt("tuning." + key, (Index(key, count) + 1) % count); PlayerPrefs.Save();
            DevLog.Write("Tuning changed: " + Describe(null));
            Changed?.Invoke();
        }
        public static void Reset()
        {
            foreach (var key in new[] { Budget, Distance, Msaa, Refresh }) PlayerPrefs.DeleteKey("tuning." + key);
            PlayerPrefs.Save(); DevLog.Write("Tuning reset: " + Describe(null)); Changed?.Invoke();
        }

        public static int MaxTriangles(Settings settings) { int value = Budgets[Index(Budget, Budgets.Length)]; return value > 0 || settings == null ? value : settings.MaxTriangles; }
        public static float DistanceScale => DistanceScales[Index(Distance, DistanceScales.Length)];
        public static float LodDistance(Settings settings, int level) => settings.LodDistances[level] * DistanceScale;
        public static int Samples => MsaaSamples[Index(Msaa, MsaaSamples.Length)];
        public static float RefreshRate => RefreshRates[Index(Refresh, RefreshRates.Length)];

        /// <summary>Applies the anti-aliasing choice to the active URP asset (a no-op when it already matches).</summary>
        public static void ApplyRendering()
        {
            if (Application.isEditor) return; // changing the asset in Play mode would also change it on disk
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline && pipeline.msaaSampleCount != Samples)
                pipeline.msaaSampleCount = Samples;
        }

        public static string Describe(Settings settings)
            => $"budget={(MaxTriangles(settings) > 0 ? (MaxTriangles(settings) / 1000) + "k" : "asset")} distance={DistanceScale * 100:F0}% msaa={Samples}x refresh={RefreshRate:F0}";
    }
}
