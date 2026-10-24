using System;
using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace TwinInspector.Editor
{
    /// <summary>Reproducibly authors assets and Inspector through Unity's supported Editor APIs.</summary>
    public static class ProjectBuilder
    {
        const string Root="Assets/_TwinInspector";
        const string Samples="Assets/Samples/XR Interaction Toolkit/3.6.1";
        [MenuItem("TwinInspector/Rebuild application assets and Inspector")]
        public static void Setup()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before authoring Main");
            foreach(var folder in new[]{"Resources","Prefabs","Materials","Scenes","Fonts"}) Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh();
            PlayerSettings.companyName="MR Twin Team"; PlayerSettings.productName="TwinInspector"; PlayerSettings.bundleVersion="1.5.0";
            PlayerSettings.Android.bundleVersionCode=6;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            string[] layerNames = { "Building", "SceneMesh", "Annotation", "TwinVisual", "DiffPatch" };
            for (int i=0;i<layerNames.Length;i++) layers.GetArrayElementAtIndex(8+i).stringValue=layerNames[i];
            tags.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.mrtwin.inspector");
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.colorSpace=ColorSpace.Linear;
            foreach(var group in new[]{BuildTargetGroup.Standalone,BuildTargetGroup.Android})
            {
                var xr=UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if(!xr) continue;
                foreach(var feature in xr.GetFeatures()) if(feature.GetType().Name=="XRStreamingFeature")
                { var fields=new SerializedObject(feature); fields.FindProperty("_autoStartOnPlay").boolValue=false; fields.FindProperty("_autoStopOnExit").boolValue=false; fields.ApplyModifiedPropertiesWithoutUndo(); }
            }
            ConfigureRendering();
            var tokens=Asset<DesignTokens>(Root+"/Resources/DesignTokens.asset");
            var regular=FontAsset("Inter-Regular"); var medium=FontAsset("Inter-Medium"); var semibold=FontAsset("Inter-SemiBold");
            Set(tokens,"regular",regular); Set(tokens,"medium",medium); Set(tokens,"semibold",semibold);
            Set(tokens,"twinMaterial",Material("Twin","TwinInspector/Twin"));
            Set(tokens,"annotationMaterial",Material("Annotation","TwinInspector/Annotation"));
            Set(tokens,"gridMaterial",Material("FloorGrid","TwinInspector/FloorGrid"));
            Set(tokens,"edgeMaterial",Material("ArchitecturalEdges","TwinInspector/ArchitecturalEdges"));
            Set(tokens,"diffPatchMaterial",Material("DiffPatch","TwinInspector/DiffPatch"));
            var ink=Material("Ink","Universal Render Pipeline/Unlit"); ink.SetColor("_BaseColor",tokens.Ink); Set(tokens,"inkMaterial",ink);
            var paper=new GameObject("PanelBase",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(Image),typeof(TrackedDeviceGraphicRaycaster),typeof(PanelBase));
            paper.layer=5;
            paper.GetComponent<RectTransform>().sizeDelta=new Vector2(480,320);
            paper.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            Set(tokens,"panelPrefab",PrefabUtility.SaveAsPrefabAsset(paper,Root+"/Prefabs/PanelBase.prefab")); UnityEngine.Object.DestroyImmediate(paper);
            var marker=new GameObject("AnnotationMarker"); marker.AddComponent<AnnotationMarker>().Construct(tokens);
            Set(tokens,"annotationPrefab",PrefabUtility.SaveAsPrefabAsset(marker,Root+"/Prefabs/AnnotationMarker.prefab")); UnityEngine.Object.DestroyImmediate(marker);
            EditorUtility.SetDirty(tokens);
            var settings=Asset<Settings>(Root+"/Resources/Settings.asset");
            var library=Asset<XRReferenceImageLibrary>(Root+"/Resources/MarkerLibrary.asset");
            if(library.count==0)
            {
                foreach(var name in new[]{"ArUco4x4_50-0","QrCode"})
                { library.Add(); int index=library.count-1; library.SetName(index,name); library.SetSpecifySize(index,true); library.SetSize(index,new Vector2(.15f,.15f)); }
                EditorUtility.SetDirty(library);
            }
            var sceneMesh=new GameObject("Detected surface",typeof(MeshFilter),typeof(MeshCollider)); sceneMesh.layer=9;
            var meshPrefab=PrefabUtility.SaveAsPrefabAsset(sceneMesh,Root+"/Prefabs/SceneMesh.prefab"); UnityEngine.Object.DestroyImmediate(sceneMesh);
            AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            // NewScene can unload assets that were referenced only by local variables.
            settings=AssetDatabase.LoadAssetAtPath<Settings>(Root+"/Resources/Settings.asset");
            library=AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(Root+"/Resources/MarkerLibrary.asset");
            meshPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/SceneMesh.prefab");
            tokens=AssetDatabase.LoadAssetAtPath<DesignTokens>(Root+"/Resources/DesignTokens.asset");
            var rigPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Samples+"/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab");
            if(!rigPrefab) throw new FileNotFoundException("Import the XRI Hands Interaction Demo first");
            var rig=(GameObject)PrefabUtility.InstantiatePrefab(rigPrefab); rig.name="XR Origin";
            ModalityRigBuilder.Apply(rig);
            var origin=rig.GetComponent<XROrigin>(); var camera=origin.Camera;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=tokens.Paper2; camera.nearClipPlane=.05f; camera.farClipPlane=100;
            camera.allowHDR=false; camera.allowMSAA=true;
            var cameraData=camera.GetUniversalAdditionalCameraData(); cameraData.renderPostProcessing=false; cameraData.renderShadows=false;
            // Spatial alignment must never be changed by game-style locomotion providers.
            foreach(var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
            { string type=behaviour.GetType().Name; if(type.Contains("MoveProvider") || type.Contains("TurnProvider") || type.Contains("TeleportationProvider") || type.Contains("GrabMoveProvider")) behaviour.enabled=false; }
            var cameraManager=camera.GetComponent<ARCameraManager>() ?? camera.gameObject.AddComponent<ARCameraManager>(); cameraManager.enabled=false;
            var occlusion=camera.GetComponent<AROcclusionManager>() ?? camera.gameObject.AddComponent<AROcclusionManager>(); occlusion.enabled=false;
            var anchor=rig.AddComponent<ARAnchorManager>(); anchor.enabled=false;
            var planes=rig.AddComponent<ARPlaneManager>(); planes.requestedDetectionMode=PlaneDetectionMode.Horizontal; planes.enabled=false;
            var images=rig.AddComponent<ARTrackedImageManager>(); images.referenceLibrary=library; images.requestedMaxNumberOfMovingImages=2; images.enabled=false;
            var meshObject=new GameObject("Scene meshing"); meshObject.transform.SetParent(origin.CameraFloorOffsetObject.transform,false);
            var meshes=meshObject.AddComponent<ARMeshManager>(); meshes.meshPrefab=meshPrefab.GetComponent<MeshFilter>(); meshes.density=.25f; meshes.enabled=false;
            var session=new GameObject("AR Session").AddComponent<ARSession>(); session.enabled=false;
            var actions=AssetDatabase.LoadAssetAtPath<InputActionAsset>(Samples+"/Starter Assets/XRI Default Input Actions.inputactions");
            var app=new GameObject("TwinInspector").AddComponent<AppBootstrap>();
            Set(app,"settings",settings); Set(app,"actions",actions); Set(app,"origin",origin); Set(app,"viewer",camera); Set(app,"session",session);
            Set(app,"cameraManager",cameraManager); Set(app,"anchorManager",anchor); Set(app,"planeManager",planes); Set(app,"imageManager",images); Set(app,"meshManager",meshes);
            var simulator=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Samples+"/XR Device Simulator/XR Device Simulator.prefab"));
            simulator.tag="EditorOnly";
            simulator.SetActive(false);
            foreach(var behaviour in simulator.GetComponents<MonoBehaviour>()) if(behaviour.GetType().Name=="XRDeviceSimulator")
            { var fields=new SerializedObject(behaviour); fields.FindProperty("m_DeviceSimulatorUI").objectReferenceValue=null; fields.ApplyModifiedPropertiesWithoutUndo(); }
            foreach(var canvas in simulator.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(XRUIInputModule));
            events.GetComponent<XRUIInputModule>().enableMouseInput=true;
            RenderSettings.skybox=null; RenderSettings.fog=false;
            EditorSceneManager.SaveScene(scene,Root+"/Scenes/Inspector.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/Inspector.unity",true)};
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Directory.CreateDirectory("docs/evidence"); File.WriteAllText("docs/evidence/setup-v13.txt",DateTime.UtcNow.ToString("O")+"\nMain, DesignTokens, Settings, fonts, materials, prefabs and Android XR rendering authored successfully.\n");
            Debug.Log("TwinInspector project setup complete");
        }
        static void ConfigureRendering()
        {
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root+"/Resources/TwinRenderer.asset");
            if(!renderer)
            { AssetDatabase.CopyAsset("Assets/Settings/Mobile_Renderer.asset",Root+"/Resources/TwinRenderer.asset"); renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root+"/Resources/TwinRenderer.asset"); }
            renderer.renderingMode=RenderingMode.ForwardPlus;
            foreach(var feature in renderer.rendererFeatures) feature.SetActive(false);
            EditorUtility.SetDirty(renderer);
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Resources/TwinPipeline.asset");
            if(!pipeline) { pipeline=UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline,Root+"/Resources/TwinPipeline.asset"); }
            pipeline.supportsHDR=false; pipeline.msaaSampleCount=4; pipeline.renderScale=1;
            pipeline.supportsCameraDepthTexture=false; pipeline.supportsCameraOpaqueTexture=false;
            var pipelineFields=new SerializedObject(pipeline);
            pipelineFields.FindProperty("m_MainLightShadowsSupported").boolValue=false;
            pipelineFields.FindProperty("m_AdditionalLightShadowsSupported").boolValue=false;
            pipelineFields.ApplyModifiedPropertiesWithoutUndo(); pipeline.shadowDistance=0;
            pipeline.gpuResidentDrawerMode=GPUResidentDrawerMode.InstancedDrawing;
            pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras=true;
            GraphicsSettings.defaultRenderPipeline=pipeline;
            int previous=QualitySettings.GetQualityLevel();
            for(int i=0;i<QualitySettings.names.Length;i++) { QualitySettings.SetQualityLevel(i,false); QualitySettings.renderPipeline=pipeline; }
            QualitySettings.SetQualityLevel(previous,false); QualitySettings.vSyncCount=0;
            var graph=GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
            if(graph!=null && graph.enableRenderCompatibilityMode) throw new Exception("Render Graph must be enabled");
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            graphics.FindProperty("m_BrgStripping").intValue=(int)BatchRendererGroupStrippingMode.KeepAll;
            graphics.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }
        static string UiCharacters()
        {
            var chars=new System.Collections.Generic.HashSet<char>(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~°±−×·←→↑↓□■Δ•²…");
            foreach(var path in Directory.GetFiles(Root+"/Resources/Strings","*.json"))foreach(char c in File.ReadAllText(path))if(!char.IsControl(c))chars.Add(c);
            var ordered=new char[chars.Count];chars.CopyTo(ordered);Array.Sort(ordered);return new string(ordered);
        }
        static TMP_FontAsset FontAsset(string name)
        {
            string path=Root+"/Fonts/"+name+" SDF.asset";
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if(font) { font.TryAddCharacters(UiCharacters(),true); TabularDigits(font); return font; }
            var source=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/"+name+".otf");
            if(!source) throw new FileNotFoundException("Font missing: "+name);
            font=TMP_FontAsset.CreateFontAsset(source,64,8,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true); font.name=name+" SDF";
            AssetDatabase.CreateAsset(font,path);
            font.TryAddCharacters(UiCharacters(),true);
            foreach(var atlas in font.atlasTextures) { atlas.name=name+" Atlas"; AssetDatabase.AddObjectToAsset(atlas,font); }
            font.material.name=name+" Material"; AssetDatabase.AddObjectToAsset(font.material,font);
            TabularDigits(font); EditorUtility.SetDirty(font); return font;
        }
        static void TabularDigits(TMP_FontAsset font)
        {
            // Retain the bundled symbols and adjusted digit advances in players.
            // Clearing dynamic data during build would silently discard these metrics.
            var serialized=new SerializedObject(font);
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue=false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // TMP exposes only a subset of OpenType tags. Give Inter's digit
            // glyphs equal advances explicitly, preserving centred outlines.
            float advance=0;
            for(uint c='0';c<='9';c++) if(font.characterLookupTable.TryGetValue(c,out var ch)) advance=Mathf.Max(advance,ch.glyph.metrics.horizontalAdvance);
            for(uint c='0';c<='9';c++) if(font.characterLookupTable.TryGetValue(c,out var ch))
            { var m=ch.glyph.metrics; ch.glyph.metrics=new UnityEngine.TextCore.GlyphMetrics(m.width,m.height,m.horizontalBearingX+(advance-m.horizontalAdvance)*.5f,m.horizontalBearingY,advance); }
            EditorUtility.SetDirty(font);
        }
        static Material Material(string name,string shader)
        {
            string path=Root+"/Materials/"+name+".mat"; var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) { var found=Shader.Find(shader); if(!found) throw new Exception("Shader missing: "+shader); material=new Material(found){name=name,enableInstancing=true}; AssetDatabase.CreateAsset(material,path); }
            return material;
        }
        static T Asset<T>(string path) where T:ScriptableObject
        { var value=AssetDatabase.LoadAssetAtPath<T>(path); if(!value) { value=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value,path); } return value; }
        static void Set(UnityEngine.Object target,string name,UnityEngine.Object value)
        { if(!value) throw new Exception("Required reference missing: "+name); var serialized=new SerializedObject(target); var property=serialized.FindProperty(name); if(property==null) throw new Exception("Field missing: "+name); property.objectReferenceValue=value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
