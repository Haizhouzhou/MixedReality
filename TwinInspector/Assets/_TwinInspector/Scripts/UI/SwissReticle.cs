using UnityEngine;

namespace TwinInspector
{
    /// <summary>Eight millimetre ink ring with a one millimetre paper outline; no beam.</summary>
    public sealed class SwissReticle : MonoBehaviour
    {
        AppBootstrap app; GameObject visual; Mesh mesh; Material ink,paper;
        public void Initialize(AppBootstrap application)
        {
            app=application;
            ink=new Material(app.Tokens.InkMaterial); paper=new Material(app.Tokens.InkMaterial);
            paper.SetColor("_BaseColor",app.Tokens.Paper);
            visual=new GameObject("Swiss surface reticle",typeof(MeshFilter),typeof(MeshRenderer));
            mesh=new Mesh { name="8 mm reticle / 1 mm paper outline" };
            const int n=64; var vertices=new Vector3[n*4];
            float[] radius={.002f,.003f,.004f,.005f};
            for(int r=0;r<4;r++) for(int i=0;i<n;i++) { float a=i*Mathf.PI*2/n; vertices[r*n+i]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius[r]; }
            mesh.vertices=vertices; mesh.subMeshCount=2;
            mesh.SetTriangles(Ring(n,1),0);
            var borders=new int[n*12]; Ring(n,0).CopyTo(borders,0); Ring(n,2).CopyTo(borders,n*6); mesh.SetTriangles(borders,1);
            mesh.RecalculateBounds(); visual.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=visual.GetComponent<MeshRenderer>(); renderer.sharedMaterials=new[]{ink,paper}; renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            visual.SetActive(false);
        }
        static int[] Ring(int n,int r)
        {
            var triangles=new int[n*6];
            // Like a world-space Canvas, the visible face is local -Z.
            for(int i=0;i<n;i++) { int j=(i+1)%n,a=r*n+i,b=r*n+j,c=(r+1)*n+i,d=(r+1)*n+j; int k=i*6; triangles[k]=a;triangles[k+1]=b;triangles[k+2]=c;triangles[k+3]=b;triangles[k+4]=d;triangles[k+5]=c; }
            return triangles;
        }
        void LateUpdate()
        {
            if(!app || !app.Input || !visual) return;
            bool show=app.Input.HasPointer && (!app.Input.Desktop || app.Placer.Active);
            visual.SetActive(show); if(!show) return;
            var normal=app.Input.PointerNormal;
            visual.transform.SetPositionAndRotation(app.Input.PointerPosition+normal*.002f,Quaternion.LookRotation(-normal));
            ink.SetColor("_BaseColor",app.Input.Held || app.Placer.Active ? app.Tokens.Accent : app.Tokens.Ink);
        }
        void OnDestroy() { if(visual) Destroy(visual); if(mesh) Destroy(mesh); if(ink) Destroy(ink); if(paper) Destroy(paper); }
    }
}
