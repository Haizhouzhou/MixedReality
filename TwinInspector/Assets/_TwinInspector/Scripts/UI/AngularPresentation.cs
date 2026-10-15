using UnityEngine;

namespace TwinInspector
{
    public static class AngularPresentation
    {
        public static float Diameter(float distance,float angleDegrees,float minimum=0)
            => Mathf.Max(minimum,2*Mathf.Max(0,distance)*Mathf.Tan(.5f*angleDegrees*Mathf.Deg2Rad));
    }
}
