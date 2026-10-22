using UnityEngine;
using UnityEngine.XR.ARFoundation;
namespace TwinInspector
{
    public enum ViewMode { Passthrough, Twin, Overlay }
    [DefaultExecutionOrder(-100)]
    public sealed class TwinStyle : MonoBehaviour
    {
        TwinLoader loader; TwinRoot root; Camera viewer; ARCameraManager passthrough; DesignTokens tokens; GameObject grid;
        Mesh gridMesh; Vector3 lensCenter; float lastHit=-10,visibility;
        bool hasLensCenter;
        public ViewMode Mode { get; private set; }=ViewMode.Overlay;
        public float Blend { get; private set; }=.45f;
        public bool Technical { get; private set; }=true;
        public bool Lens { get; private set; }
        public float LensRadius { get; private set; }=1.5f;
        public float DatumZ { get; private set; }
        public Vector3 LensCenter => lensCenter;
        public float LensVisibility => visibility;
        public bool LensActive => Lens && Mode==ViewMode.Overlay;
        public void Initialize(TwinLoader twin,TwinRoot twinRoot,Camera camera,ARCameraManager cameraManager,DesignTokens design)
        {
            loader=twin; root=twinRoot; viewer=camera; passthrough=cameraManager; tokens=design;
            grid=new GameObject("Metre floor grid",typeof(MeshFilter),typeof(MeshRenderer)); grid.transform.SetParent(root.transform,false);
            var lo=root.ToLocal(TwinRoot.Vector(loader.Manifest.region.min)); var hi=root.ToLocal(TwinRoot.Vector(loader.Manifest.region.max));
            float y=DatumZ-root.Origin.z+.006f;
            gridMesh=new Mesh { name="Twin datum extent" }; gridMesh.vertices=new[]{new Vector3(lo.x,y,lo.z),new Vector3(hi.x,y,lo.z),new Vector3(hi.x,y,hi.z),new Vector3(lo.x,y,hi.z)};
            gridMesh.triangles=new[]{0,2,1,0,3,2}; gridMesh.RecalculateBounds(); grid.GetComponent<MeshFilter>().sharedMesh=gridMesh; grid.GetComponent<MeshRenderer>().sharedMaterial=tokens.GridMaterial;
            Apply();
        }
        public void SetMode(ViewMode mode)
        {
            var app=AppBootstrap.Instance;
            if(Mode==ViewMode.Twin && mode!=ViewMode.Twin) app?.Roam?.LeaveTwin();
            Mode=mode; if(mode==ViewMode.Twin) app?.Roam?.EnterTwin(); Apply();
            app?.Anchors?.SuspendForRoaming(mode==ViewMode.Twin);
        }
        public void SetBlend(float value) { Blend=Mathf.Clamp01(value); Apply(); }
        public void SetTechnical(bool value) { Technical=value; Apply(); }
        public void SetLens(bool value) { Lens=value; lastHit=-10; hasLensCenter=false; visibility=0; Apply(); }
        public void SetLensRadius(float radius) => LensRadius=Mathf.Clamp(radius,.5f,5);
        public bool PlacementVisible(Vector3 point)
        { return Mode!=ViewMode.Passthrough && (Mode==ViewMode.Twin || Blend>.02f) && (!LensActive || hasLensCenter && visibility>.5f && Vector3.Distance(point,lensCenter)<LensRadius*.95f); }
        void Apply()
        {
            if(!loader) return;
            loader.SetVisible(Mode!=ViewMode.Passthrough); loader.SetLens(LensActive); loader.SetTechnical(Technical);
            if(passthrough) passthrough.enabled=!Application.isEditor && Mode!=ViewMode.Twin;
            viewer.clearFlags=CameraClearFlags.SolidColor;
            viewer.backgroundColor=Mode==ViewMode.Twin?tokens.Paper2:Application.isEditor?new Color(.35f,.35f,.34f,1):Color.clear;
            Shader.SetGlobalFloat("_TwinAlpha",Mode==ViewMode.Twin?1:Blend); Shader.SetGlobalFloat("_TwinTechnical",Technical?1:0);
            Shader.SetGlobalFloat("_TwinLensEnabled",LensActive?1:0);
        }
        void LateUpdate()
        {
            if(!root) return;
            Shader.SetGlobalMatrix("_WorldToTwin",root.WorldToTwinMatrix); Shader.SetGlobalFloat("_TwinDatum",DatumZ);
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            Shader.SetGlobalFloat("_TwinOutlineScale",viewer.stereoEnabled && pipeline?pipeline.renderScale:1);
            var app=AppBootstrap.Instance;
            grid.SetActive(Mode==ViewMode.Twin || Mode==ViewMode.Overlay && app && app.Manual && app.Manual.Active);
            if(LensActive && app && app.Input)
            {
                var input=app.Input;
                if(input.Valid && input.OverUI && hasLensCenter) lastHit=Time.unscaledTime;
                else if(input.Valid && input.HasBuildingHit && !input.OverUI)
                { lensCenter=input.BuildingHit.point; lastHit=Time.unscaledTime; hasLensCenter=true; }
                float elapsed=Time.unscaledTime-lastHit;
                float target=hasLensCenter?1-Mathf.Clamp01((elapsed-.15f)/.15f):0;
                visibility=!input.Valid?Mathf.MoveTowards(visibility,0,Time.unscaledDeltaTime/.15f):target;
            }
            else visibility=0;
            Shader.SetGlobalVector("_TwinLens",new Vector4(lensCenter.x,lensCenter.y,lensCenter.z,LensRadius)); Shader.SetGlobalFloat("_TwinLensVisibility",visibility);
        }
        void OnDestroy() { if(gridMesh) Destroy(gridMesh); if(grid) Destroy(grid); Shader.SetGlobalFloat("_TwinLensEnabled",0); }
    }
}
