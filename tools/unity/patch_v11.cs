if(UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Stop Play before scene patch");
var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_TwinInspector/Scenes/Inspector.unity");
var rig=UnityEngine.Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
TwinInspector.Editor.ModalityRigBuilder.Apply(rig.gameObject);
foreach(var behaviour in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None))
 if(behaviour.GetType().Name=="XRDeviceSimulator") behaviour.gameObject.SetActive(false);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEditor.AssetDatabase.SaveAssets();
var modality=rig.GetComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager>();
int rays=0,pokes=0,models=0;
foreach(var ray in rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(true)) if(ray.enabled) rays++;
foreach(var poke in rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRPokeInteractor>(true)) if(poke.enabled) pokes++;
models=modality.leftController.GetComponentsInChildren<UnityEngine.MeshRenderer>(true).Length+modality.rightController.GetComponentsInChildren<UnityEngine.MeshRenderer>(true).Length;
return $"v1.1 scene saved: {rays} inspection rays, {pokes} poke interactors, {models} controller mesh renderers; simulator disabled";
