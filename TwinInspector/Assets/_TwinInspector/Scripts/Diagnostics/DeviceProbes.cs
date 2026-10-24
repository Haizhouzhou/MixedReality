using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace TwinInspector
{
    /// <summary>Opt-in, bounded metadata probes. Never opens a Camera2 camera or consumes depth for rendering.</summary>
    public sealed class DeviceProbes : MonoBehaviour
    {
        public const float SceneSeconds=30;
        AROcclusionManager occlusion;
        ARMeshManager meshes;
        ARPlaneManager planes;
        Transform head;
        bool waiting;
        float deadline, sceneUntil, walked;
        int added, updated, removed;
        Vector3 lastHead;
        string statusKey, statusText;
        public string Status => statusText ?? (string.IsNullOrEmpty(statusKey)?"":L.Get(statusKey));
        public bool SceneRunning => sceneUntil>0;
        public void Initialize(Camera viewer, ARMeshManager meshManager, ARPlaneManager planeManager)
        { occlusion=viewer.GetComponent<AROcclusionManager>(); meshes=meshManager; planes=planeManager; head=viewer.transform; }

        /// <summary>Thirty seconds of scene meshing and plane evidence for automatic alignment: is a mesh delivered at all, and how much?</summary>
        public void SceneProbe()
        {
            if(Application.isEditor){Unavailable();return;}
            if(sceneUntil>0)return;
            statusKey=null; added=updated=removed=0; walked=0; lastHead=head.position; sceneUntil=Time.realtimeSinceStartup+SceneSeconds;
            var text=new StringBuilder("Scene probe started ("+SceneSeconds+" s)\n");
            text.AppendLine("ARSession.state="+ARSession.state);
#if UNITY_ANDROID && !UNITY_EDITOR
            text.AppendLine("SCENE_UNDERSTANDING_FINE granted="+Permission.HasUserAuthorizedPermission("android.permission.SCENE_UNDERSTANDING_FINE"));
#endif
            var descriptors=new List<XRMeshSubsystemDescriptor>(); SubsystemManager.GetSubsystemDescriptors(descriptors);
            var loaded=XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRMeshSubsystem>();
            text.AppendLine("mesh descriptors=["+string.Join(",",descriptors.ConvertAll(d=>d.id))+"] loader mesh subsystem="+(loaded!=null)+" running="+loaded?.running);
            // Retry here too, so the probe shows whether meshing can start now.
            if(meshes&&!meshes.enabled){meshes.enabled=true;text.AppendLine("meshManager re-enabled: enabled="+meshes.enabled);}
            var meshSubsystem=meshes?meshes.subsystem:null;
            text.AppendLine(FormattableString.Invariant($"meshManager present={(bool)meshes} enabled={meshes&&meshes.enabled} subsystem={meshSubsystem!=null} running={meshSubsystem?.running} descriptor={meshSubsystem?.subsystemDescriptor?.id} density={(meshes?meshes.density:0)} meshes={(meshes?meshes.meshes.Count:0)}"));
            var planeSubsystem=planes?planes.subsystem:null;
            text.AppendLine(FormattableString.Invariant($"planeManager present={(bool)planes} enabled={planes&&planes.enabled} subsystem={planeSubsystem!=null} running={planeSubsystem?.running} detectionMode={(planes?planes.requestedDetectionMode.ToString():"-")} planes={(planes?planes.trackables.count:0)}"));
            text.AppendLine("environmentDepthImageSupported="+occlusion?.descriptor?.environmentDepthImageSupported);
            DevLog.Write(text.ToString());
            if(meshes) meshes.meshInfosChanged.AddListener(Count);
            AppBootstrap.Instance?.UI?.Notify(L.Get("toast.sceneProbeStarted")); AppBootstrap.Instance?.Feedback?.Confirm();
        }
        void Count(ARMeshInfosChangedEventArgs args){added+=args.added.Count;updated+=args.updated.Count;removed+=args.removed.Count;}
        void FinishScene()
        {
            if(meshes) meshes.meshInfosChanged.RemoveListener(Count);
            int chunks=0; long vertices=0, triangles=0; float area=0, floorArea=0, wallArea=0;
            var bounds=new Bounds(head.position,Vector3.zero);
            if(meshes)
                foreach(var filter in meshes.meshes)
                {
                    var mesh=filter?filter.sharedMesh:null; if(!mesh)continue;
                    chunks++; var v=mesh.vertices; var t=mesh.triangles; var world=filter.transform.localToWorldMatrix;
                    vertices+=v.Length; triangles+=t.Length/3;
                    for(int i=0;i<v.Length;i++){v[i]=world.MultiplyPoint3x4(v[i]);bounds.Encapsulate(v[i]);}
                    for(int i=0;i+2<t.Length;i+=3)
                    {
                        var cross=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]); float a=cross.magnitude*.5f; if(a<=0)continue;
                        float up=Mathf.Abs(cross.y)/(2*a); area+=a; if(up>.9f)floorArea+=a; else if(up<.3f)wallArea+=a;
                    }
                }
            int horizontal=0, vertical=0; float horizontalArea=0, verticalArea=0;
            if(planes) foreach(var plane in planes.trackables)
            {
                float a=plane.size.x*plane.size.y;
                if(plane.alignment==PlaneAlignment.Vertical){vertical++;verticalArea+=a;} else if(plane.alignment==PlaneAlignment.HorizontalUp||plane.alignment==PlaneAlignment.HorizontalDown){horizontal++;horizontalArea+=a;}
            }
            DevLog.Write(FormattableString.Invariant($"Scene probe result: chunks={chunks} events added={added} updated={updated} removed={removed} vertices={vertices} triangles={triangles} area_m2={area:F1} floorlike_m2={floorArea:F1} walllike_m2={wallArea:F1} bounds_m={bounds.size.x:F1}x{bounds.size.y:F1}x{bounds.size.z:F1} planes horizontal={horizontal} ({horizontalArea:F1} m2) vertical={vertical} ({verticalArea:F1} m2) walked_m={walked:F1}"));
            sceneUntil=0; statusText=L.Format("probe.sceneDone",chunks,area);
            AppBootstrap.Instance?.UI?.Notify(statusText); AppBootstrap.Instance?.Feedback?.Confirm();
        }

        public void DepthProbe()
        {
            if(Application.isEditor){Unavailable();return;}
            if(waiting)return;
            if(!occlusion){Fail("Depth probe: AROcclusionManager missing");return;}
            try
            {
                waiting=true;deadline=Time.realtimeSinceStartup+8;statusText=null;statusKey="probe.waiting";
                occlusion.frameReceived+=Frame;
                occlusion.requestedEnvironmentDepthMode=EnvironmentDepthMode.Fastest;
                occlusion.enabled=true;
                DevLog.Write("Depth probe started (8 s timeout)");
            }
            catch(Exception error){Fail("Depth probe: "+error);StopDepth();}
        }

        void Frame(AROcclusionFrameEventArgs frame)
        {
            if(!waiting)return;
            try
            {
                var text=new StringBuilder("Depth probe frame\n");
                text.AppendLine("environmentDepthImageSupported="+occlusion.descriptor?.environmentDepthImageSupported);
                foreach(var external in frame.externalTextures)
                {
                    var texture=external.texture;
                    text.Append("propertyId=").Append(external.propertyId).Append(" name=").Append(PropertyName(external.propertyId));
                    if(texture)text.Append(" dimension=").Append(texture.dimension).Append(" width=").Append(texture.width).Append(" height=").Append(texture.height)
                        .Append(" arrayDepth=").Append(texture is Texture2DArray array?array.depth:0).Append(" format=").Append(texture.graphicsFormat);
                    else text.Append(" texture=null");
                    text.AppendLine();
                }
                bool fovsOk=frame.TryGetFovs(out var fovs);text.AppendLine("TryGetFovs="+fovsOk);
                if(fovsOk)foreach(var fov in fovs)text.AppendLine(FormattableString.Invariant($"fov left={fov.angleLeft} right={fov.angleRight} up={fov.angleUp} down={fov.angleDown}"));
                bool posesOk=frame.TryGetPoses(out var poses);text.AppendLine("TryGetPoses="+posesOk);
                if(posesOk)foreach(var pose in poses)text.AppendLine("pose="+pose);
                bool planesOk=frame.TryGetNearFarPlanes(out var planes);text.AppendLine("TryGetNearFarPlanes="+planesOk);
                if(planesOk)text.AppendLine(FormattableString.Invariant($"near={planes.nearZ} far={planes.farZ}"));
                bool cpu=occlusion.TryAcquireEnvironmentDepthCpuImage(out var image);text.AppendLine("TryAcquireEnvironmentDepthCpuImage="+cpu);
                if(cpu){try{text.AppendLine("CPU dimensions="+image.dimensions+" format="+image.format);}finally{image.Dispose();}}
                DevLog.Write(text.ToString());statusText=null;statusKey="text.probeLogged";
            }
            catch(Exception error){Fail("Depth probe: "+error);}
            finally{StopDepth();}
        }
        static string PropertyName(int id)
        {
            // ARShaderOcclusion + AndroidOpenXROcclusionSubsystem property names from the installed packages.
            foreach(var name in new[]{"_EnvironmentDepthTexture","_EnvironmentConfidenceTexture","_EnvironmentDepthTexturePreprocessed","_EnvironmentDepthProjectionMatrices","_NdcLinearConversionParameters","_IsOcclusionOn","_HumanDepth","_HumanStencil"})
                if(Shader.PropertyToID(name)==id)return name;
            return "unknown";
        }
        void Update()
        {
            if(sceneUntil>0)
            {
                walked+=Vector3.Distance(Vector3.ProjectOnPlane(head.position,Vector3.up),Vector3.ProjectOnPlane(lastHead,Vector3.up)); lastHead=head.position;
                float left=sceneUntil-Time.realtimeSinceStartup;
                if(left<=0) FinishScene();
                else statusText=L.Format("probe.sceneRunning",Mathf.CeilToInt(left),meshes?meshes.meshes.Count:0);
            }
            if(waiting&&Time.realtimeSinceStartup>=deadline)
            {
                DevLog.Write("Depth probe timed out without a frame; environmentDepthImageSupported="+occlusion?.descriptor?.environmentDepthImageSupported);
                statusKey="probe.timeout";StopDepth();
            }
        }
        void StopDepth(){waiting=false;if(occlusion){occlusion.frameReceived-=Frame;occlusion.enabled=false;}}
        void OnDisable(){StopDepth();if(sceneUntil>0&&meshes)meshes.meshInfosChanged.RemoveListener(Count);sceneUntil=0;}
        void Unavailable(){DevLog.Write("Editor: probe unavailable");statusText=null;statusKey="text.editorProbeUnavailable";}
        void Fail(string message){DevLog.Write(message);statusText=null;statusKey="probe.failed";}

        public void CameraProbe()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var manager=activity.Call<AndroidJavaObject>("getSystemService","camera"))
                using(var type=new AndroidJavaClass("android.hardware.camera2.CameraCharacteristics"))
                {
                    var ids=manager.Call<string[]>("getCameraIdList");var text=new StringBuilder("Camera2 metadata probe cameras="+ids.Length+"\n");
                    foreach(var id in ids)
                    using(var properties=manager.Call<AndroidJavaObject>("getCameraCharacteristics",id))
                    {
                        text.AppendLine("camera="+id);
                        foreach(var keyName in new[]{"LENS_FACING","SENSOR_INFO_PIXEL_ARRAY_SIZE","SENSOR_INFO_ACTIVE_ARRAY_SIZE","LENS_INTRINSIC_CALIBRATION","LENS_POSE_TRANSLATION","LENS_POSE_ROTATION","LENS_POSE_REFERENCE","REQUEST_AVAILABLE_CAPABILITIES"})
                        using(var key=type.GetStatic<AndroidJavaObject>(keyName))
                        using(var value=properties.Call<AndroidJavaObject>("get",key))
                        {
                            string display="unavailable";
                            if(value!=null)
                            {
                                if(keyName=="REQUEST_AVAILABLE_CAPABILITIES")display=string.Join(",",AndroidJNI.FromIntArray(value.GetRawObject()));
                                else if(keyName=="LENS_INTRINSIC_CALIBRATION"||keyName=="LENS_POSE_TRANSLATION"||keyName=="LENS_POSE_ROTATION")display="available";
                                else display=value.Call<string>("toString");
                            }
                            text.AppendLine(keyName+"="+display);
                        }
                    }
                    DevLog.Write(text.ToString());statusText=null;statusKey="text.probeLogged";
                }
            }
            catch(AndroidJavaException error){Fail("Camera2 probe denied/unavailable (including SecurityException): "+error.Message);}
            catch(Exception error){Fail("Camera2 probe: "+error);}
#else
            Unavailable();
#endif
        }
    }
}
