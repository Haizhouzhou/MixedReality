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
