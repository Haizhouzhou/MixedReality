using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace TwinInspector.Editor
{
    /// <summary>Keep sample tracking and pinch readers; replace sample presentation and far interaction.</summary>
    public static class ModalityRigBuilder
    {
        public static void Apply(GameObject rig)
        {
            if(PrefabUtility.IsPartOfPrefabInstance(rig)) PrefabUtility.UnpackPrefabInstance(rig,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var modality=rig.GetComponent<XRInputModalityManager>();
            if(!modality || !modality.leftHand || !modality.rightHand || !modality.leftController || !modality.rightController) throw new InvalidOperationException("Four modality groups are required");
            Configure(modality.leftHand,true); Configure(modality.rightHand,true);
            Configure(modality.leftController,false); Configure(modality.rightController,false);
            foreach(var component in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string type=component.GetType().Name;
                if(type.EndsWith("Provider") || type=="ControllerInputActionManager" || type=="CurveVisualController" || type=="XRInteractorLineVisual" || type=="XRGazeInteractor" || type=="ClimbTeleportInteractor") component.enabled=false;
            }
            foreach(var line in rig.GetComponentsInChildren<LineRenderer>(true)) { line.enabled=false; line.forceRenderingOff=true; }
            EditorUtility.SetDirty(rig);
        }
        static void Configure(GameObject group,bool hand)
        {
            var near=group.GetComponentInChildren<NearFarInteractor>(true);
            if(!near) throw new InvalidOperationException("Expected sample pinch/select readers on "+group.name);
            var existing=group.transform.Find("Inspection ray");
            var ray=existing ? existing.GetComponent<XRRayInteractor>() : new GameObject("Inspection ray").AddComponent<XRRayInteractor>();
            ray.transform.SetParent(group.transform,false);
            if(hand)
            {
                var pose=group.transform.Find("Aim Pose");
                if(!pose) throw new InvalidOperationException("Hand aim pose missing");
                var follow=ray.GetComponent<TrackedRayPose>() ?? ray.gameObject.AddComponent<TrackedRayPose>(); follow.Source=pose;
            }
            var filtered=ray.GetComponent<TrackedRayPose>() ?? ray.gameObject.AddComponent<TrackedRayPose>();
            if(!hand) filtered.Source=group.transform;
            filtered.Hand=hand;
            // The sample stabilizes its separate pinch-grab pose. Never stack that
            // filter on our raw aim chain; only TrackedRayPose filters this ray.
            foreach(var stabilizer in ray.GetComponents<XRTransformStabilizer>()) stabilizer.enabled=false;
            ray.rayOriginTransform=ray.transform;
            ray.handedness=near.handedness;
            ray.selectInput=hand ? near.selectInput : near.uiPressInput;
            ray.uiPressInput=near.uiPressInput;
            ray.enableUIInteraction=true; ray.raycastMask=InputRouter.SurfaceMask | (1<<5); ray.maxRaycastDistance=80;
            ray.hitDetectionType=XRRayInteractor.HitDetectionType.Raycast;
            ray.lineType=XRRayInteractor.LineType.StraightLine;
            near.enabled=false;
            foreach(var old in group.GetComponentsInChildren<XRRayInteractor>(true)) if(old!=ray) old.enabled=false;
            // Persist group membership so near poke gets first refusal and cannot double-click with the ray.
            var interaction=group.GetComponent<XRInteractionGroup>();
            if(interaction)
            {
                var fields=new SerializedObject(interaction); var members=fields.FindProperty("m_StartingGroupMembers");
                fields.FindProperty("m_StartingInteractionOverridesMap").ClearArray();
                members.ClearArray();
                foreach(var poke in group.GetComponentsInChildren<XRPokeInteractor>(true)) { members.InsertArrayElementAtIndex(members.arraySize); members.GetArrayElementAtIndex(members.arraySize-1).objectReferenceValue=poke; }
                members.InsertArrayElementAtIndex(members.arraySize); members.GetArrayElementAtIndex(members.arraySize-1).objectReferenceValue=ray;
                fields.ApplyModifiedPropertiesWithoutUndo();
            }
            if(!hand)
            {
                foreach(var component in group.GetComponentsInChildren<MonoBehaviour>(true))
                    if(component.GetType().Namespace?.Contains("AffordanceSystem")==true) component.enabled=false;
                foreach(var renderer in group.GetComponentsInChildren<Renderer>(true)) { renderer.enabled=false; renderer.forceRenderingOff=true; }
                foreach(var component in group.GetComponentsInChildren<MonoBehaviour>(true))
                    if(component.GetType().Name=="ControllerAnimator") component.enabled=false;
                // Strip controller mesh assets completely; no later affordance can reveal them.
                foreach(var renderer in group.GetComponentsInChildren<MeshRenderer>(true)) UnityEngine.Object.DestroyImmediate(renderer);
                foreach(var renderer in group.GetComponentsInChildren<SkinnedMeshRenderer>(true)) UnityEngine.Object.DestroyImmediate(renderer);
            }
            else
            {
                foreach(var t in group.GetComponentsInChildren<Transform>(true)) if(t.name=="Pinch Visual") t.gameObject.SetActive(false);
                foreach(var component in group.GetComponents<MonoBehaviour>())
                {
                    string type=component.GetType().Name;
                    if(type!="PokeGestureDetector" && type!="MetaSystemGestureDetector") continue;
                    var fields=new SerializedObject(component);
                    foreach(string eventName in type=="PokeGestureDetector" ? new[]{"m_PokeGestureStarted","m_PokeGestureEnded"} : new[]{"m_SystemGestureStarted","m_SystemGestureEnded"})
                    {
                        var calls=fields.FindProperty(eventName+".m_PersistentCalls.m_Calls");
                        for(int i=0;i<calls.arraySize;i++)
                        {
                            var call=calls.GetArrayElementAtIndex(i); var target=call.FindPropertyRelative("m_Target");
                            if(type=="PokeGestureDetector")
                            {
                                target.objectReferenceValue=ray;
                                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue=typeof(XRRayInteractor).AssemblyQualifiedName;
                                call.FindPropertyRelative("m_MethodName").stringValue="set_enabled";
                            }
                            else if(target.objectReferenceValue==near.gameObject) target.objectReferenceValue=ray.gameObject;
                        }
                    }
                    fields.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }
    }
}
