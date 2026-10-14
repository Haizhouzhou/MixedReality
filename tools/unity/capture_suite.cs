var app=TwinInspector.AppBootstrap.Instance;
if(app==null || !app.Ready) throw new System.Exception("Application must be ready before visual verification");
System.Func<System.Threading.Tasks.Task> run=async () =>
{
    var created=new System.Collections.Generic.List<string>();
    try
    {
        var camera=app.Viewer;
        UnityEngine.Physics.queriesHitBackfaces=true;
        var surfaces=new System.Collections.Generic.List<UnityEngine.RaycastHit>();
        for(int pitch=-45;pitch<=45;pitch+=30) for(int yaw=0;yaw<360;yaw+=15)
            if(UnityEngine.Physics.Raycast(camera.transform.position,UnityEngine.Quaternion.Euler(pitch,yaw,0)*UnityEngine.Vector3.forward,out var surface,70,1<<8)) surfaces.Add(surface);
        if(surfaces.Count<3) throw new System.Exception("Not enough loaded collision surfaces for verification");
        // Exercise the actual LOD2 colliders, recording temporary QA marks on
        // real imported surfaces. Remove only these IDs after the contact sheet.
        for(int n=0;n<3;n++)
        {
            var hit=surfaces[n*surfaces.Count/3];
            var item=app.Annotations.Add(app.Root.WorldToTwin(hit.point),app.Root.NormalToTwin(hit.normal),app.Root.WorldToTwin(camera.transform.position),app.Root.RotationToTwin(camera.transform.rotation));
            created.Add(item.id); app.Annotations.Retype((TwinInspector.AnnotationType)n);
        }
        foreach(string page in new[]{"setup","mode","align","picker","list","diagnostics"})
        {
            app.UI.Show(page); await System.Threading.Tasks.Task.Delay(400);
            var panel=app.UI.Panel;
            foreach(var t in panel.GetComponentsInChildren<UnityEngine.Transform>(true)) t.gameObject.layer=14;
            UnityEngine.Canvas.ForceUpdateCanvases();
            var go=new UnityEngine.GameObject("Documentation camera");
            var capture=go.AddComponent<UnityEngine.Camera>(); capture.orthographic=true; capture.orthographicSize=.19f; capture.clearFlags=UnityEngine.CameraClearFlags.SolidColor; capture.backgroundColor=app.Tokens.Paper2; capture.cullingMask=1<<14;
            capture.transform.SetPositionAndRotation(panel.transform.position-panel.transform.forward,panel.transform.rotation);
            var target=new UnityEngine.RenderTexture(1440,1000,24); var previous=UnityEngine.RenderTexture.active;
            capture.targetTexture=target; capture.Render(); UnityEngine.RenderTexture.active=target;
            var picture=new UnityEngine.Texture2D(1440,1000,UnityEngine.TextureFormat.RGB24,false); picture.ReadPixels(new UnityEngine.Rect(0,0,1440,1000),0,0); picture.Apply();
            System.IO.Directory.CreateDirectory("docs/images"); System.IO.File.WriteAllBytes("docs/images/"+page+".png",picture.EncodeToPNG());
            UnityEngine.RenderTexture.active=previous; capture.targetTexture=null; target.Release(); UnityEngine.Object.Destroy(picture); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(go);
        }
        var position=app.Root.transform.position; var rotation=app.Root.transform.rotation;
        app.Manual.Nudge(.01f,-.01f,0,.5f); var expected=app.Root.transform.position;
        await app.Anchors.SaveAsync(); app.Root.transform.position=position;
        await app.Anchors.RestoreAsync();
        if(UnityEngine.Vector3.Distance(expected,app.Root.transform.position)>.0001) throw new System.Exception("Alignment JSON round trip failed");
        app.Root.transform.SetPositionAndRotation(position,rotation); await app.Anchors.SaveAsync();
        System.IO.File.WriteAllText("docs/evidence/visual-capture.txt","Captured six running simulator panels.\nPASS: LOD2 mesh raycasts and temporary annotation markers.\nPASS: alignment JSON save and restore.\nQA annotations removed after capture. No physical headset claim.\n");
        if(System.IO.File.Exists("docs/evidence/visual-error.txt")) System.IO.File.Delete("docs/evidence/visual-error.txt");
    }
    catch(System.Exception error) { System.IO.File.WriteAllText("docs/evidence/visual-error.txt",error.ToString()); UnityEngine.Debug.LogException(error); }
    finally
    {
        foreach(string id in created) { app.Annotations.Select(id); app.Annotations.DeleteSelected(); }
        app.UI.Show("setup");
    }
};
_ = run();
return "Visual and persistence checks started";
