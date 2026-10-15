using UnityEngine;

namespace TwinInspector
{
    /// <summary>Two cached, quiet non-spatial transients. Accepted actions own confirmation.</summary>
    public sealed class MicroFeedback : MonoBehaviour
    {
        AudioSource source; AudioClip hover,confirm;
        float nextHover; int confirmationFrame=-1;
        bool onSurface;
        public float ConfirmedAt { get; private set; } = -10;
        public bool SurfaceConfirmation { get; private set; }
        public Vector3 SurfacePoint { get; private set; }
        public Vector3 SurfaceNormal { get; private set; }
        public void Initialize()
        {
            source=gameObject.AddComponent<AudioSource>(); source.playOnAwake=false; source.spatialBlend=0;
            int rate=AudioSettings.outputSampleRate; if(rate<8000) rate=48000;
            hover=Make("Hover 1200 Hz",rate,false); confirm=Make("Confirm 800 + 1600 Hz",rate,true);
        }
        static AudioClip Make(string name,int rate,bool confirmation)
        {
            float duration=confirmation?.012f:.006f; int count=Mathf.CeilToInt(rate*duration);
            var samples=new float[count];
            for(int i=1;i<count-1;i++)
            {
                float t=(float)i/rate, x=(float)i/(count-1);
                float envelope=Mathf.Sin(Mathf.PI*x); envelope*=envelope;
                float tone=confirmation ? .6f*Mathf.Sin(2*Mathf.PI*800*t)+.4f*Mathf.Sin(2*Mathf.PI*1600*t) : Mathf.Sin(2*Mathf.PI*1200*t);
                samples[i]=tone*envelope*.25f;
            }
            var clip=AudioClip.Create(name,count,1,rate,false); clip.SetData(samples,0); return clip;
        }
        public void Hover()
        { if(Time.unscaledTime<nextHover) return; nextHover=Time.unscaledTime+.09f; Play(hover); }
        public void Confirm(bool surface=false)
        {
            if(confirmationFrame==Time.frameCount) return;
            confirmationFrame=Time.frameCount; ConfirmedAt=Time.unscaledTime; SurfaceConfirmation=surface;
            if(surface && AppBootstrap.Instance) { SurfacePoint=AppBootstrap.Instance.Input.PointerPosition; SurfaceNormal=AppBootstrap.Instance.Input.PointerNormal; }
            Play(confirm);
        }
        void Play(AudioClip clip) { if(source && InteractionPreferences.Sound) source.PlayOneShot(clip,InteractionPreferences.Volume); }
        void LateUpdate()
        {
            var app=AppBootstrap.Instance;
            bool valid=app && app.Ready && app.Placer.Active && app.Input.HasBuildingHit && !app.Input.OverUI && app.Style.PlacementVisible(app.Input.BuildingHit.point);
            if(valid && !onSurface) Hover(); onSurface=valid;
        }
        void OnDestroy() { if(hover) Destroy(hover); if(confirm) Destroy(confirm); if(source) Destroy(source); }
    }
}
