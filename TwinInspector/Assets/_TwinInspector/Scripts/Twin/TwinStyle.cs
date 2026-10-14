using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace TwinInspector
{
    public enum ViewMode { Passthrough, Twin, Overlay }
    /// <summary>Immediate view switching with global twin style/blend properties.</summary>
    public sealed class TwinStyle : MonoBehaviour
    {
        TwinLoader loader; Camera viewer; ARCameraManager passthrough; DesignTokens tokens; GameObject grid;
        public ViewMode Mode { get; private set; } = ViewMode.Overlay;
        public float Blend { get; private set; } = .45f;
        public bool Technical { get; private set; } = true;
        public void Initialize(TwinLoader twin, TwinRoot root, Camera camera, ARCameraManager cameraManager, DesignTokens design)
        {
            loader = twin; viewer = camera; passthrough = cameraManager; tokens = design;
            grid = new GameObject("Metre floor grid", typeof(MeshFilter), typeof(MeshRenderer)); grid.transform.SetParent(root.transform, false);
            var lo = root.ToLocal(TwinRoot.Vector(loader.Manifest.region.min)); var hi = root.ToLocal(TwinRoot.Vector(loader.Manifest.region.max));
            float y = -root.Origin.z + .006f;
            var mesh = new Mesh { name = "Twin floor extent" };
            mesh.vertices = new[] { new Vector3(lo.x,y,lo.z), new Vector3(hi.x,y,lo.z), new Vector3(hi.x,y,hi.z), new Vector3(lo.x,y,hi.z) };
            mesh.triangles = new[] { 0,2,1,0,3,2 }; mesh.RecalculateBounds();
            grid.GetComponent<MeshFilter>().sharedMesh = mesh; grid.GetComponent<MeshRenderer>().sharedMaterial = tokens.GridMaterial;
            Apply();
        }
        public void SetMode(ViewMode mode) { Mode = mode; Apply(); }
        public void SetBlend(float value) { Blend = Mathf.Clamp01(value); Apply(); }
        public void SetTechnical(bool value) { Technical = value; Apply(); }
        void Apply()
        {
            if (!loader) return;
            loader.SetVisible(Mode != ViewMode.Passthrough);
            if (passthrough) passthrough.enabled = !Application.isEditor && Mode != ViewMode.Twin;
            viewer.clearFlags = CameraClearFlags.SolidColor;
            viewer.backgroundColor = Mode == ViewMode.Twin ? tokens.Paper2 : Application.isEditor ? new Color(.35f,.35f,.34f,1) : Color.clear;
            Shader.SetGlobalFloat("_TwinAlpha", Mode == ViewMode.Twin ? 1 : Blend);
            Shader.SetGlobalFloat("_TwinTechnical", Technical ? 1 : 0);
            Shader.SetGlobalColor("_TwinTint", tokens.TwinTint.linear);
            grid.SetActive(Mode == ViewMode.Twin);
        }
        void OnDestroy() { if (grid) Destroy(grid.GetComponent<MeshFilter>().sharedMesh); }
    }
}
