using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TwinInspector
{
    public sealed class CandidateVisuals : MonoBehaviour
    {
        public sealed class Visual {public Candidate Candidate;public AnnotationMarker Marker;public RegionPatch Patch;}
        readonly Dictionary<string,Visual> visuals=new Dictionary<string,Visual>();
        AppBootstrap app;
        public IEnumerable<Visual> Items=>visuals.Values;
        public int PatchTriangles {get;private set;}
        public void Initialize(AppBootstrap application){app=application;app.Diff.Changed+=Refresh;L.Changed+=Refresh;Refresh();}
        void Refresh()
        {
            if(!app||!app.Diff)return;
            var live=new HashSet<string>(app.Diff.Candidates.Select(c=>c.id));
            foreach(var id in visuals.Keys.Where(id=>!live.Contains(id)).ToArray()){Destroy(visuals[id].Marker.gameObject);Destroy(visuals[id].Patch.gameObject);visuals.Remove(id);}
            PatchTriangles=0;
            foreach(var c in app.Diff.Candidates)
            {
                if(!visuals.TryGetValue(c.id,out var item))
                {
                    var parent=c.space==CandidateSpace.TwinLocal?app.Root.transform:app.SceneSource.Chunks.FirstOrDefault(s=>s.Id==c.chunk_or_tile)?.Filter.transform;
                    if(!parent)continue;
                    var marker=Instantiate(app.Tokens.AnnotationPrefab,parent).GetComponent<AnnotationMarker>();
                    var patch=new GameObject("Candidate patch "+c.id).AddComponent<RegionPatch>();patch.transform.SetParent(parent,false);
                    item=new Visual{Marker=marker,Patch=patch};visuals.Add(c.id,item);
                }
                if((item.Candidate==null||item.Candidate.chunk_or_tile_version!=c.chunk_or_tile_version||item.Candidate.triangles.Length!=c.triangles.Length||Vector3.Distance(TwinRoot.Vector(item.Candidate.centroid),TwinRoot.Vector(c.centroid))>.0001f) && app.Diff.Geometry(c,out var vertices,out var triangles))
                {
                    var matrix=item.Patch.transform.worldToLocalMatrix*(c.space==CandidateSpace.TwinLocal?app.Root.WorldToTwinMatrix.inverse:Matrix4x4.identity);
                    item.Patch.Build(vertices,triangles,c.triangles,Array.ConvertAll(c.hull,TwinRoot.Vector),matrix,app.Tokens.DiffPatchMaterial,c.Type,.35f,Mathf.Max(0,50000-PatchTriangles));
                }
                item.Candidate=c;item.Marker.UpdateCandidate(c,app.Diff.Selected==c,app.Root,app.Tokens);PatchTriangles+=item.Patch.TriangleCount;
            }
        }
        void LateUpdate()
        {
            if(!app||!app.Ready)return;
            foreach(var item in visuals.Values)
            {
                bool show=app.Diff.Visible&&!app.Diff.Paused&&app.Style.PlacementVisible(item.Candidate.CentroidWorld(app.Root));
                item.Marker.gameObject.SetActive(show);item.Patch.gameObject.SetActive(show);if(show)item.Marker.Present(app.Viewer);
            }
        }
        void OnDestroy(){if(app&&app.Diff)app.Diff.Changed-=Refresh;L.Changed-=Refresh;foreach(var v in visuals.Values){if(v.Marker)Destroy(v.Marker.gameObject);if(v.Patch)Destroy(v.Patch.gameObject);}}
    }
}
