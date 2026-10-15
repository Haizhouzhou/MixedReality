using UnityEngine;

namespace TwinInspector
{
    /// <summary>Hysteretic, time-bounded preparation, followed by responsive manipulation.</summary>
    public sealed class PinchStabilization
    {
        public enum Phase { Normal, Preparing, Recovery }
        public Phase State { get; private set; }
        public float Threshold = .4f, Rearm = .3f, Window = .15f;
        bool armed;
        float entered;
        public void Reset() { State=Phase.Normal; armed=false; entered=0; }
        public float Step(float strength,bool confirmed,bool valid,float now)
        {
            if(!valid) { Reset(); return 1; }
            if(strength<Rearm) armed=true;
            if(State==Phase.Normal && armed && strength>=Threshold)
            { armed=false; entered=now; State=Phase.Preparing; }
            if(State==Phase.Preparing && (confirmed || strength<Rearm || now-entered>=Window))
            { State=Phase.Recovery; entered=now; }
            if(State==Phase.Recovery && now-entered>=.10f) State=Phase.Normal;
            // Confirmation immediately releases the extra damping for sliders/drags.
            return confirmed ? 1 : State==Phase.Preparing ? .16f : State==Phase.Recovery ? Mathf.Lerp(.16f,1,Mathf.Clamp01((now-entered)/.1f)) : 1;
        }
    }
}
