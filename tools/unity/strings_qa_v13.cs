var app=TwinInspector.AppBootstrap.Instance;
if(!app||!app.Ready||!app.Diff.Settled)return "WAIT engine";
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder();var previous=TwinInspector.L.Language;var candidate=System.Linq.Enumerable.FirstOrDefault(app.Diff.Candidates);int missingGlyphs=0,totalMissing=0;TwinInspector.Annotation point=null;
 UnityEngine.Application.LogCallback logger=(message,stack,type)=>{if(message.Contains("Unicode value")&&message.Contains("not found"))missingGlyphs++;};UnityEngine.Application.logMessageReceived+=logger;
 var fields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
 var guide=(TwinInspector.GuideFlow)typeof(TwinInspector.InspectorUI).GetField("guide",fields).GetValue(app.UI);
 var render=typeof(TwinInspector.InspectorUI).GetMethod("Render",fields);
 var strip=UnityEngine.Object.FindFirstObjectByType<TwinInspector.ToolStrip>();
 System.Action<string> capture=name=>
 {
  var camera=app.Viewer;var old=camera.targetTexture;var active=UnityEngine.RenderTexture.active;var target=new UnityEngine.RenderTexture(1600,1000,24);var image=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false);
  try{camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;image.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0);image.Apply();System.IO.File.WriteAllBytes("docs/screenshots/v13-"+name+".png",image.EncodeToPNG());}
  finally{camera.targetTexture=old;UnityEngine.RenderTexture.active=active;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);}
 };
 try
 {
  // Freeze detection only for typography inspection; the existing real candidates remain the display fixture.
  while(app.Diff.HasPendingWork)await System.Threading.Tasks.Task.Yield();
  app.Diff.enabled=false;app.UI.Notify("");app.UI.Show("setup");app.UI.Summon();await System.Threading.Tasks.Task.Delay(3000);
  point=app.Annotations.Add(new UnityEngine.Vector3(20,10,1),UnityEngine.Vector3.up,new UnityEngine.Vector3(20,10,1.6f),new float[]{0,0,0,1});
  ((System.Collections.Generic.HashSet<string>)typeof(TwinInspector.InspectorUI).GetField("freshAnnotations",fields).GetValue(app.UI)).Add(point.id);
  foreach(var code in new[]{"en","de","fr","it"})
  {
   TwinInspector.L.Language=code;TwinInspector.L.ClearMissing();int pages=0;int overflow=0;
   foreach(var page in new[]{"setup","mode","display","preferences","sound","language","list","picker","picker-point","diagnostics","roam","align","nudge","height","marker","guide"})
   {
    if(page=="roam")app.Style.SetMode(TwinInspector.ViewMode.Twin);else if(app.Style.Mode==TwinInspector.ViewMode.Twin)app.Style.SetMode(TwinInspector.ViewMode.Overlay);
    if(page=="picker"&&candidate!=null)app.Diff.Select(candidate);
    if(page=="picker-point")app.Annotations.Select(point.id);
    app.UI.Show(page=="picker-point"?"picker":page);
    if(page=="diagnostics")app.Probes.CameraProbe();
    int count=page=="guide"?6:1;
    for(int card=0;card<count;card++)
    {
     if(page=="guide"){guide.Go(card);render.Invoke(app.UI,null);}
     await System.Threading.Tasks.Task.Delay(120);UnityEngine.Canvas.ForceUpdateCanvases();
     foreach(var text in System.Linq.Enumerable.Concat(app.UI.Panel.GetComponentsInChildren<TMPro.TMP_Text>(),strip.GetComponentsInChildren<TMPro.TMP_Text>()))
     {
      text.ForceMeshUpdate(false,true);int allowed=text.rectTransform.rect.height>=100?5:text.rectTransform.rect.height>=40&&text.fontSize<=21?2:1;
      if(text.isTextOverflowing||text.textInfo.lineCount>allowed){overflow++;log.AppendLine("OVERFLOW "+code+" "+page+" card="+card+" text="+text.text+" size="+text.fontSize+" rect="+text.rectTransform.rect+" lines="+text.textInfo.lineCount+" preferred="+text.preferredWidth+","+text.preferredHeight);}
     }
     pages++;
     if(page=="mode"&&code!="en")capture(code+"-mode");if(page=="list"&&code=="de")capture("de-list");
    }
   }
   log.AppendLine("Language="+code+" pages="+pages+" overflow="+overflow+" missing_keys="+TwinInspector.L.MissingKeys.Count);
   totalMissing+=TwinInspector.L.MissingKeys.Count;
  }
  log.AppendLine("Missing glyph warnings="+missingGlyphs);System.IO.File.WriteAllText("docs/evidence/strings-v13.txt",log.ToString());
  if(log.ToString().Contains("OVERFLOW")||missingGlyphs>0||totalMissing>0)throw new System.Exception("String layout check failed; inspect strings-v13.txt");
 }
 catch(System.Exception error){log.AppendLine("FAIL: "+error);System.IO.File.WriteAllText("docs/evidence/strings-v13.txt",log.ToString());UnityEngine.Debug.LogException(error);}
 finally{if(point!=null){app.Annotations.Select(point.id);app.Annotations.DeleteSelected();}UnityEngine.Application.logMessageReceived-=logger;TwinInspector.L.Language=previous;app.UI.Show("setup");app.Diff.enabled=true;}
};_ = run();return "Four-language layout QA started";
