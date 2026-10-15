using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace TwinInspector
{
    /// <summary>One velocity-adaptive filter on the derived ray, before XRI UI and physics queries.</summary>
    [DefaultExecutionOrder(-29984)]
    public sealed class TrackedRayPose : MonoBehaviour
    {
        [SerializeField] Transform source;
        [SerializeField] bool hand;
        [SerializeField, Range(.1f,.9f)] float preparationThreshold=.4f;
        [SerializeField, Range(.05f,.8f)] float rearmThreshold=.3f;
        [SerializeField, Range(.05f,.25f)] float preparationWindow=.15f;
        XRRayInteractor ray;
        readonly PinchStabilization pinch = new PinchStabilization();
        bool initialized, ready, focused=true;
        Vector3 previousPosition, filteredPosition;
        Quaternion previousRotation, filteredRotation;
        public bool Hand { get => hand; set => hand=value; }
        public Transform Source { get => source; set => source=value; }
        public float Strength { get; private set; }
        public bool Ready => ready && initialized;
        void Awake() { ray=GetComponent<XRRayInteractor>(); }
        void OnEnable() { ResetPose(); }
        void OnDisable() { ResetPose(); }
        void OnApplicationFocus(bool value) { focused=value; ResetPose(); }
        public void ResetPose() { initialized=ready=false; Strength=0; pinch.Reset(); }
        void Update()
        {
            if(!source || !source.gameObject.activeInHierarchy || !ray || !ray.enabled || !focused) { ResetPose(); return; }
            Strength=Mathf.Clamp01(ray.selectInput.ReadValue()); // The sample's value reader forwards continuous pinch strength.
            bool held=ray.selectInput.ReadIsPerformed() || ray.uiPressInput.ReadIsPerformed();
            if(!held && Strength<.3f) ready=true; // Never synthesize a press when tracking returns mid-pinch.
            var position=source.position; var rotation=source.rotation;
            float dt=Mathf.Clamp(Time.unscaledDeltaTime,.001f,.05f);
            if(!initialized)
            { filteredPosition=previousPosition=position; filteredRotation=previousRotation=rotation; initialized=true; }
            float angularSpeed=Quaternion.Angle(previousRotation,rotation)/dt;
            float speed=Vector3.Distance(previousPosition,position)/dt;
            pinch.Threshold=preparationThreshold; pinch.Rearm=Mathf.Min(rearmThreshold,preparationThreshold-.01f); pinch.Window=preparationWindow;
            float damping=pinch.Step(Strength,held,ready,Time.unscaledTime);
            float positionRate=18+Mathf.Min(speed*80,120);
            float rotationRate=(18+Mathf.Min(angularSpeed*.65f,140))*damping;
            filteredPosition=Vector3.Lerp(filteredPosition,position,1-Mathf.Exp(-positionRate*dt));
            filteredRotation=Quaternion.Slerp(filteredRotation,rotation,1-Mathf.Exp(-rotationRate*dt));
            transform.SetPositionAndRotation(filteredPosition,filteredRotation);
            previousPosition=position; previousRotation=rotation;
        }
    }
}
