using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwinInspector
{
    /// <summary>Pinch/trigger placement using the same right-hand aim as XRI UI.</summary>
    public sealed class AnnotationPlacer : MonoBehaviour
    {
        TwinRoot root;
        AnnotationStore store;
        Camera viewer;
        Transform aim;
        InputAction press;
        XRRayInteractor[] rays;
        NearFarInteractor[] nearFar;
        public bool Active { get; set; }
        public event Action Placed;
        public event Action<string> Feedback;
        public void Initialize(TwinRoot twin, AnnotationStore annotations, Camera camera, Transform rightAim, InputActionAsset actions)
        {
            root = twin; store = annotations; viewer = camera; aim = rightAim;
            press = actions.FindAction("XRI Right Interaction/UI Press", true);
            rays = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            nearFar = FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
        void Update()
        {
            if (!Active) return;
            bool mouse = Application.isEditor && Keyboard.current != null && Keyboard.current.leftAltKey.isPressed && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (!mouse && (press == null || !press.WasPressedThisFrame())) return;
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
            foreach (var ray in rays) if (ray.isActiveAndEnabled && ray.TryGetCurrentUIRaycastResult(out _)) return;
            foreach (var ray in nearFar) if (ray.isActiveAndEnabled && ray.TryGetCurrentUIRaycastResult(out _)) return;
            Ray worldRay = mouse ? viewer.ScreenPointToRay(Mouse.current.position.ReadValue()) : new Ray(aim.position, aim.forward);
            if (!Physics.Raycast(worldRay, out var hit, 40, (1 << 8) | (1 << 9) | (1 << 10), QueryTriggerInteraction.Collide))
            { Feedback?.Invoke("Point at a twin or detected surface"); return; }
            var marker = hit.collider.GetComponentInParent<AnnotationMarker>();
            if (marker) store.Select(marker.Id);
            else
            {
                var normal=Vector3.Dot(hit.normal,worldRay.direction)>0 ? -hit.normal : hit.normal;
                store.Add(root.WorldToTwin(hit.point), root.NormalToTwin(normal), root.WorldToTwin(viewer.transform.position), root.RotationToTwin(viewer.transform.rotation));
            }
            Placed?.Invoke();
        }
    }
}
