using System;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Immutable geometry snapshot for a version of a session-space scene mesh.</summary>
    public sealed class SurfaceChunk
    {
        public string Id { get; }
        public int Version { get; private set; }
        public MeshFilter Filter { get; private set; }
        public MeshCollider Collider { get; private set; }
        public Vector3[] Vertices { get; private set; }
        public Vector3[] Normals { get; private set; }
        public int[] Triangles { get; private set; }
        public Matrix4x4 LocalToWorld { get; private set; }
        public Bounds Bounds { get; private set; }
        public SurfaceChunk(string id,MeshFilter filter) { Id=id; Capture(filter); }
        public void Capture(MeshFilter filter)
        {
            var mesh=filter.sharedMesh;
            if(!mesh || !mesh.isReadable) throw new InvalidOperationException("Scene mesh must remain CPU-readable: "+Id);
            Filter=filter; filter.gameObject.layer=9;
            Collider=filter.GetComponent<MeshCollider>();if(!Collider)Collider=filter.gameObject.AddComponent<MeshCollider>();Collider.sharedMesh=mesh;
            LocalToWorld=filter.transform.localToWorldMatrix;
            var normalMatrix=LocalToWorld.inverse.transpose;
            Vertices=mesh.vertices; Normals=mesh.normals;Triangles=mesh.triangles;
            if(Normals.Length!=Vertices.Length) {mesh.RecalculateNormals();Normals=mesh.normals;}
            var bounds=new Bounds(LocalToWorld.MultiplyPoint3x4(mesh.bounds.center),Vector3.zero);
            for(int i=0;i<Vertices.Length;i++) { Vertices[i]=LocalToWorld.MultiplyPoint3x4(Vertices[i]); Normals[i]=normalMatrix.MultiplyVector(Normals[i]).normalized;bounds.Encapsulate(Vertices[i]); }
            Bounds=bounds; Version++;
        }
    }
}
