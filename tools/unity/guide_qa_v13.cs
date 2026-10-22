var app=TwinInspector.AppBootstrap.Instance;if(!app||!app.Ready)return "WAIT app";
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var log=new System.Text.StringBuilder();bool seen=TwinInspector.InteractionPreferences.GuideSeen;string language=TwinInspector.L.Language;
 System.Action<bool,string> check=(ok,text)=>{if(!ok)throw new System.Exception(text);log.AppendLine("PASS: "+text);};
 System.Action<string> press=key=>{var label=TwinInspector.L.Get(key);var button=System.Linq.Enumerable.First(app.UI.Panel.GetComponentsInChildren<UnityEngine.UI.Button>(),b=>b.name==label);button.onClick.Invoke();};
 System.Action<string> capture=name=>{var camera=app.Viewer;var old=camera.targetTexture;var active=UnityEngine.RenderTexture.active;var target=new UnityEngine.RenderTexture(1600,1000,24);var image=new UnityEngine.Texture2D(1600,1000,UnityEngine.TextureFormat.RGB24,false);try{camera.targetTexture=target;camera.Render();UnityEngine.RenderTexture.active=target;image.ReadPixels(new UnityEngine.Rect(0,0,1600,1000),0,0);image.Apply();System.IO.File.WriteAllBytes("docs/screenshots/v13-guide-"+name+".png",image.EncodeToPNG());}finally{camera.targetTexture=old;UnityEngine.RenderTexture.active=active;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);}};
 try
 {
  foreach(var code in new[]{"en","de"})
  {
   TwinInspector.L.Language=code;TwinInspector.InteractionPreferences.GuideSeen=false;typeof(TwinInspector.InspectorUI).GetField("guideConsidered",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(app.UI,false);
   app.UI.Show("setup");app.UI.Summon();await System.Threading.Tasks.Task.Delay(500);check(app.UI.Page=="guide","first-launch preference opens guide: "+code);capture(code);
   for(int i=0;i<5;i++){check(!TwinInspector.InteractionPreferences.GuideSeen,"Next does not complete onboarding");press("button.next");await System.Threading.Tasks.Task.Delay(80);}
   check(System.Linq.Enumerable.Any(app.UI.Panel.GetComponentsInChildren<UnityEngine.UI.Button>(),b=>b.name==TwinInspector.L.Get("button.done")),"sixth card shows Done");press("button.done");check(TwinInspector.InteractionPreferences.GuideSeen&&app.UI.Page=="setup","Done persists completion");
   press("button.guide");check(app.UI.Page=="guide","Setup reopens guide");press("button.skip");check(app.UI.Page=="setup","Skip closes guide");
  }
  System.IO.File.WriteAllText("docs/evidence/guide-v13.txt",log.ToString());
 }
 catch(System.Exception error){System.IO.File.WriteAllText("docs/evidence/guide-v13.txt",log+"FAIL: "+error);UnityEngine.Debug.LogException(error);}
 finally{TwinInspector.InteractionPreferences.GuideSeen=seen;TwinInspector.L.Language=language;app.UI.Show("setup");}
};_ = run();return "Guide QA started";
