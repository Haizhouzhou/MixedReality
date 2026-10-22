using UnityEngine;

namespace TwinInspector
{
    /// <summary>Runtime streaming and display budgets, editable as one asset.</summary>
    [CreateAssetMenu(menuName = "TwinInspector/Settings")]
    public sealed class Settings : ScriptableObject
    {
        [SerializeField] string scan = "navvis_2022-02-06_12.55.11";
        [SerializeField] string dataFolder = "HGE_2022-02-06";
        [SerializeField] string editorDataPath = "E:/MixedReality/work/twin/HGE_2022-02-06/region_a";
        [SerializeField] float[] lodDistances = { 12, 30, 80 };
        [SerializeField] int maxTriangles = 1200000;
        [SerializeField] int loadConcurrency = 2;
        [SerializeField] float targetRefreshRate = 90;
        [SerializeField] float loadingRefreshRate = 72;
        [SerializeField] bool diffEnabled = true;
        [SerializeField] float diffDistanceMax = .35f, diffMinAreaM2 = .15f, diffMinExtentM = .25f;
        [SerializeField] float diffObservedRadiusM = 1, diffChangedPairDistanceM = 1, diffFloorZ;
        [SerializeField] int diffPersistUpdates = 3, diffRaycastsPerFrame = 2048, diffOverlapsPerFrame = 2048;
        [SerializeField] bool diffIgnoreFloorCeiling = true;
        [SerializeField] float diffMainThreadBudgetMs = 2;
        [SerializeField] bool objectPickEnabled = true;
        [SerializeField] float objectPickFramesBudgetMs = 4;
        [SerializeField] string editorModelsPath = "E:/MixedReality/tools/sam/models";
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
        public string Scan => scan;
        public string DataFolder => dataFolder;
        public string EditorDataPath => editorDataPath;
        public float[] LodDistances => lodDistances;
        public int MaxTriangles => maxTriangles;
        public int LoadConcurrency => loadConcurrency;
        public float TargetRefreshRate => targetRefreshRate;
        public float LoadingRefreshRate => loadingRefreshRate;
    }
}
