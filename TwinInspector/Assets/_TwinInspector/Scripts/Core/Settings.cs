using UnityEngine;

namespace TwinInspector
{
    /// <summary>Runtime streaming and display budgets, editable as one asset.</summary>
    [CreateAssetMenu(menuName = "TwinInspector/Settings")]
    public sealed class Settings : ScriptableObject
    {
        // Editor only: folder holding one pipeline output per space id (relative paths start at the repository root).
        // A space whose manifest there matches its scan loads in full detail; empty uses packaged/device data only.
        [SerializeField] string editorDataPath = "work/twin";
        // Space opened when the start-up choice is skipped.
        [SerializeField] string defaultSpace = "HG_MainHall";
        [SerializeField] float[] lodDistances = { 12, 30, 80 };
        [SerializeField] int maxTriangles = 1200000;
        [SerializeField] int loadConcurrency = 2;
        [SerializeField] float targetRefreshRate = 90;
        [SerializeField] float loadingRefreshRate = 72;
        [SerializeField] bool diffEnabled = true;
        [SerializeField] float diffDistanceMax = .35f, diffMinAreaM2 = .15f, diffMinExtentM = .25f;
        // diffFloorZ is relative to the manifest datum_z of the loaded space.
        [SerializeField] float diffObservedRadiusM = 1, diffChangedPairDistanceM = 1, diffFloorZ;
        [SerializeField] int diffPersistUpdates = 3, diffRaycastsPerFrame = 2048, diffOverlapsPerFrame = 2048;
        [SerializeField] bool diffIgnoreFloorCeiling = true;
        [SerializeField] float diffMainThreadBudgetMs = 2;
        // Experimental until cold-start dispatch satisfies the frame budget on the target device.
        [SerializeField] bool objectPickEnabled = false;
        [SerializeField] float objectPickFramesBudgetMs = 4;
        [SerializeField] string editorModelsPath = "tools/sam/models";
        public bool DiffEnabled => diffEnabled;
        public float DiffDistanceMax => Mathf.Max(.001f, diffDistanceMax);
        public float DiffMinAreaM2 => diffMinAreaM2;
        public float DiffMinExtentM => diffMinExtentM;
        public float DiffObservedRadiusM => diffObservedRadiusM;
        public float DiffChangedPairDistanceM => diffChangedPairDistanceM;
        public float DiffFloorZ => diffFloorZ;
        public int DiffPersistUpdates => Mathf.Max(1, diffPersistUpdates);
        public int DiffRaycastsPerFrame => Mathf.Clamp(diffRaycastsPerFrame, 1, 2048);
        public int DiffOverlapsPerFrame => Mathf.Clamp(diffOverlapsPerFrame, 1, 2048);
        public bool DiffIgnoreFloorCeiling => diffIgnoreFloorCeiling;
        public float DiffMainThreadBudgetMs => diffMainThreadBudgetMs;
        public bool ObjectPickEnabled => objectPickEnabled;
        public float ObjectPickFramesBudgetMs => objectPickFramesBudgetMs;
        public string EditorModelsPath => editorModelsPath;
        public string EditorDataPath => editorDataPath;
        public string DefaultSpace => defaultSpace;
        public float[] LodDistances => lodDistances;
        public int MaxTriangles => maxTriangles;
        public int LoadConcurrency => loadConcurrency;
        public float TargetRefreshRate => targetRefreshRate;
        public float LoadingRefreshRate => loadingRefreshRate;
    }
}
