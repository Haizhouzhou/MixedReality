using System;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace TwinInspector
{
    /// <summary>Opt-in, bounded metadata probes. Never opens a Camera2 camera or consumes depth for rendering.</summary>
    public sealed class DeviceProbes : MonoBehaviour
    {
        AROcclusionManager occlusion;
        bool waiting;
        float deadline;
        string statusKey;
        public string Status => string.IsNullOrEmpty(statusKey)?"":L.Get(statusKey);
        public void Initialize(Camera viewer) { occlusion=viewer.GetComponent<AROcclusionManager>(); }

        public void DepthProbe()
        {
            if(Application.isEditor){Unavailable();return;}
            if(waiting)return;
            if(!occlusion){Fail("Depth probe: AROcclusionManager missing");return;}
            try
            {
                waiting=true;deadline=Time.realtimeSinceStartup+8;statusKey="probe.waiting";
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
                DevLog.Write(text.ToString());statusKey="text.probeLogged";
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
            if(waiting&&Time.realtimeSinceStartup>=deadline)
            {
                DevLog.Write("Depth probe timed out without a frame; environmentDepthImageSupported="+occlusion?.descriptor?.environmentDepthImageSupported);
                statusKey="probe.timeout";StopDepth();
            }
        }
        void StopDepth(){waiting=false;if(occlusion){occlusion.frameReceived-=Frame;occlusion.enabled=false;}}
        void OnDisable()=>StopDepth();
        void Unavailable(){DevLog.Write("Editor: probe unavailable");statusKey="text.editorProbeUnavailable";}
        void Fail(string message){DevLog.Write(message);statusKey="probe.failed";}

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
                    DevLog.Write(text.ToString());statusKey="text.probeLogged";
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
