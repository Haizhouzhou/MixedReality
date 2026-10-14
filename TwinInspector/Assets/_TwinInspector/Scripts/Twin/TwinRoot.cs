using UnityEngine;

namespace TwinInspector
{
    /// <summary>Only alignment transform. Persisted coordinates stay Z-up in metres.</summary>
    public sealed class TwinRoot : MonoBehaviour
    {
        Vector3 origin;
        public Vector3 Origin => origin;
        public void Initialize(float[] originWorld) => origin = Vector(originWorld);
        public static Vector3 Vector(float[] p) => new Vector3(p[0], p[1], p[2]);
        public static float[] Array(Vector3 p) => new[] { p.x, p.y, p.z };
        public static Vector3 SwapYZ(Vector3 p) => new Vector3(p.x, p.z, p.y);
        public Vector3 ToLocal(Vector3 twin) => SwapYZ(twin - origin);
        public Vector3 TwinToWorld(Vector3 twin) => transform.TransformPoint(ToLocal(twin));
        public Vector3 WorldToTwin(Vector3 world) => SwapYZ(transform.InverseTransformPoint(world)) + origin;
        public Vector3 NormalToWorld(Vector3 twin) => transform.TransformDirection(SwapYZ(twin)).normalized;
        public Vector3 NormalToTwin(Vector3 world) => SwapYZ(transform.InverseTransformDirection(world)).normalized;
        public float[] RotationToTwin(Quaternion world)
        {
            var local = Quaternion.Inverse(transform.rotation) * world;
            return new[] { -local.x, -local.z, -local.y, local.w };
        }
        public Bounds LocalBounds(TwinManifest.Tile tile)
        {
            var lo = ToLocal(Vector(tile.min)); var hi = ToLocal(Vector(tile.max));
            return new Bounds((lo + hi) * .5f, hi - lo);
        }
    }
}
