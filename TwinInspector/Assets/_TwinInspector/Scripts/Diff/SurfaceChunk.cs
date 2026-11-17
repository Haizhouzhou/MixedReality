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
        public Geometry Snapshot { get; private set; }
        public Vector3[] Vertices => Snapshot?.Vertices;
        public Vector3[] Normals => Snapshot?.Normals;
        public int[] Triangles => Snapshot?.Triangles;
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
            // Built in locals and published only when complete: readers on worker threads must never see local-space
            // vertices or a vertex array that does not belong to the triangle array.
            var vertices=mesh.vertices; var normals=mesh.normals; var triangles=mesh.triangles;
            if(normals.Length!=vertices.Length) {mesh.RecalculateNormals();normals=mesh.normals;}
            var bounds=new Bounds(LocalToWorld.MultiplyPoint3x4(mesh.bounds.center),Vector3.zero);
            for(int i=0;i<vertices.Length;i++) { vertices[i]=LocalToWorld.MultiplyPoint3x4(vertices[i]); normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;bounds.Encapsulate(vertices[i]); }
            Snapshot=new Geometry(vertices,normals,triangles);
            Bounds=bounds; Version++;
        }
        /// <summary>One consistent version of the arrays; take this once on the main thread before handing geometry to a worker.</summary>
        public sealed class Geometry
        {
            public readonly Vector3[] Vertices, Normals; public readonly int[] Triangles;
            public Geometry(Vector3[] vertices,Vector3[] normals,int[] triangles) { Vertices=vertices; Normals=normals; Triangles=triangles; }
        }
    }
}
