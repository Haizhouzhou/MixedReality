using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

namespace TwinInspector
{
    /// <summary>Numbered inspection pages sharing one summoned world dock with the four-cell tool strip.</summary>
    public sealed class InspectorUI : MonoBehaviour
    {
        AppBootstrap app;
        PanelBase panel, strip;
        TMP_Text liveText;
        string page = "setup";
        bool coarse, busy, subscribed;
        ToolStrip spatialStrip;
        float nextRefresh;
        readonly HashSet<string> freshAnnotations=new HashSet<string>();
        string lastDiffSignature;
        public string Page => page;
        public PanelBase Panel => panel;

        public void Initialize(AppBootstrap application)
        {
            app = application;
            panel = Instantiate(app.Tokens.PanelPrefab).GetComponent<PanelBase>();
            panel.name = "Inspection panel"; panel.Initialize(app.Tokens,app.Viewer);
            strip = Instantiate(app.Tokens.PanelPrefab).GetComponent<PanelBase>();
            strip.name = "Spatial tool strip"; strip.Initialize(app.Tokens,app.Viewer);
            spatialStrip=strip.gameObject.AddComponent<ToolStrip>(); spatialStrip.Initialize(app,strip);
            spatialStrip.Dock(panel);
            Subscribe(); L.Changed += LanguageChanged;
        }
        void LanguageChanged() { spatialStrip.RefreshLabels(); Render(); }
        void Subscribe()
        {
            if (subscribed || !app.Ready) return;
            app.Annotations.Changed += AnnotationChanged;
            app.Placer.Placed += AnnotationPlaced;
            app.Placer.Feedback += Notify;
            app.Markers.Changed += MarkerChanged;
            app.Diff.Changed += DiffChanged;
            subscribed = true;
        }
        void AnnotationChanged() { if (page == "list" || page == "picker") Render(); }
        void AnnotationPlaced() { app.Placer.Active = false; if(app.Annotations.Selected!=null)freshAnnotations.Add(app.Annotations.Selected.id); Show("picker"); }
        void DiffChanged()
        {
            string signature=app.Diff.Visible+":"+app.Diff.Selected?.id+":"+app.Diff.Candidates.Count;
            foreach(var c in app.Diff.Candidates)signature+="/"+c.id;
            if(signature==lastDiffSignature)return;lastDiffSignature=signature;
            if(page=="list"||page=="picker"||page=="display")Render();
        }
        void MarkerChanged() { if (page == "marker") Render(); }
        public void Notify(string message) { if (panel) panel.Notify(message); }
        public void Show(string target)
        {
            Subscribe();
            if (!app.Ready) target = "setup";
            if (page == "marker" && target != "marker" && app.Markers) app.Markers.Cancel();
            page = target;
            if(app.Roam) app.Roam.Move(Vector2.zero);
            if(app.Ready && (target=="align" || target=="nudge" || target=="height" || target=="marker") && app.Style.Mode!=ViewMode.Overlay) app.Style.SetMode(ViewMode.Overlay);
            if(app.Placer) app.Placer.Active=false;
            if(spatialStrip) spatialStrip.Select((target=="mode"||target=="display"||target=="roam")?0:target=="align"||target=="nudge"||target=="height"||target=="marker"?1:target=="picker"?2:target=="list"?3:-1);
            if (app.Manual) app.Manual.SetActive(target == "align" || target == "nudge" || target == "height");
            if (app.Performance) app.Performance.Visible = target == "diagnostics";
            if(spatialStrip && spatialStrip.PalmAnchored) spatialStrip.Recenter();
            panel.gameObject.SetActive(true); Render();
        }
        public void Summon() { if(!app.Placer || !app.Placer.Active) panel.gameObject.SetActive(true); if(spatialStrip) spatialStrip.Recenter(); }
        public void BeginAnnotation()
        {
            if (!app.Ready) return;
            app.Manual.SetActive(false); app.Placer.Active = true; if(spatialStrip) spatialStrip.Select(2);
            panel.gameObject.SetActive(false);
            Notify(L.Get("Point at a surface and select"));
        }
        async void Run(Func<Task> operation, Action after = null)
        {
            if (busy) return;
            busy = true;
            try { await operation(); if (this) after?.Invoke(); }
            catch (Exception error) { DevLog.Write(error.ToString()); if (this) Notify(L.Get("Action failed. See diagnostics.")); }
            finally { busy = false; }
        }
        void Render()
        {
            panel.Clear(); liveText = null;
            switch (page)
            {
                case "mode": ViewPage(); break;
                case "display": DisplayPage(); break;
                case "preferences": PreferencesPage(); break;
                case "sound": SoundPage(); break;
                case "roam": RoamPage(); break;
                case "align": AlignPage(); break;
                case "nudge": NudgePage(); break;
                case "height": HeightPage(); break;
                case "marker": MarkerPage(); break;
                case "list": ListPage(); break;
                case "picker": PickerPage(); break;
                case "diagnostics": DiagnosticsPage(); break;
                default: SetupPage(); break;
            }
        }
        void SetupPage()
        {
            panel.Header(L.Get("00 / Setup"),L.Get("Inspect the building"),"v1.3");
            panel.Text(L.Get(app.Status)+(app.Ready ? "\n"+L.Get(app.Anchors.Status) : ""),24,104,432,56);
            if (!app.Ready)
            { panel.Button(L.Get("Retry setup"),24,184,432,() => Run(app.InitializeAsync)); return; }
            panel.Button(L.Get("Manual alignment"),24,168,208,() => Show("align"));
            panel.Button(L.Get("Preferences"),248,168,208,() => Show("preferences"));
            panel.Button(L.Get("View modes"),24,248,208,() => Show("mode"),true);
            panel.Button(L.Get("Guide"),248,248,208,() => Show("guide"));
        }
        void ViewPage()
        {
            panel.Header(L.Get("01 / Mode"),L.Get("Compare"),app.Style.Mode==ViewMode.Twin?L.Get("VIRTUAL INSPECTION"):"");
            var labels=new[]{L.Get("Reality"),L.Get("Twin"),L.Get("Overlay")};
            for(int i=0;i<3;i++)
            { var mode=(ViewMode)i; panel.Button(labels[i],24+i*144,104,144,()=>{ app.Style.SetMode(mode); Render(); },app.Style.Mode==mode); }
            panel.Button(app.Style.Technical?L.Get("Technical"):L.Get("Colour"),24,168,208,()=>{ app.Style.SetTechnical(!app.Style.Technical); Render(); },app.Style.Technical);
            var lens=panel.Button(app.Style.Lens?L.Get("Lens: on"):L.Get("Lens: off"),248,168,208,()=>{ app.Style.SetLens(!app.Style.Lens); Render(); },app.Style.Lens);
            lens.interactable=app.Style.Mode==ViewMode.Overlay;
            panel.Button(L.Get("Opacity / Lens"),24,248,264,()=>Show("display"));
            panel.Button(app.Style.Mode==ViewMode.Twin?L.Get("Roam"):L.Get("Setup"),304,248,152,()=>Show(app.Style.Mode==ViewMode.Twin?"roam":"setup"));
        }
        void DisplayPage()
        {
            panel.Header(L.Get("01 / Mode"),L.Get("Opacity and Lens"),L.Format("L{0} · D{1}",app.Style.LensActive?"+":"−",app.Diff.Visible?"+":"−"));
            panel.Text(L.Get("Twin opacity"),24,104,432,24); panel.Slider(app.Style.Blend,app.Style.SetBlend,128);
            panel.Text(L.Get("Lens radius"),24,184,432,24); panel.Slider(app.Style.LensRadius,app.Style.SetLensRadius,208,.5f,5," m");
            panel.Button(L.Get("Diff on"),24,264,144,()=>app.Diff.Visible=true,app.Diff.Visible);
            panel.Button(L.Get("Diff off"),168,264,144,()=>app.Diff.Visible=false,!app.Diff.Visible);
            panel.Button(L.Get("Back"),312,264,144,()=>Show("mode"));
        }
        void PreferencesPage()
        {
            panel.Header(L.Get("00 / Setup"),L.Get("Preferences"),L.Get("DOMINANT HAND"));
            panel.Button(L.Get("Left hand"),24,104,208,()=>{ InteractionPreferences.LeftHand=true; Render(); },InteractionPreferences.LeftHand);
            panel.Button(L.Get("Right hand"),248,104,208,()=>{ InteractionPreferences.LeftHand=false; Render(); },!InteractionPreferences.LeftHand);
            panel.Button(L.Get("Diagnostics"),24,168,208,()=>Show("diagnostics"));
            panel.Button(L.Get("Language"),248,168,208,()=>Show("language"));
            panel.Text(L.Get("EITHER HAND CAN SUMMON AND OPERATE"),24,224,432,20,DesignTokens.Label);
            panel.Button(L.Get("Feedback sound"),24,248,208,()=>Show("sound"));
            panel.Button(L.Get("Recenter / back"),248,248,208,()=>{ Show("setup"); Summon(); });
        }
        void SoundPage()
        {
            panel.Header(L.Get("00 / Setup"),L.Get("Feedback sound"),L.Get("VISUAL FEEDBACK ALWAYS ON"));
            panel.Button(InteractionPreferences.Sound?L.Get("Sound: on"):L.Get("Sound: muted"),24,104,432,()=>{ InteractionPreferences.Sound=!InteractionPreferences.Sound; Render(); },InteractionPreferences.Sound);
            panel.Text(L.Get("Volume"),24,168,432,24); panel.Slider(InteractionPreferences.Volume,v=>InteractionPreferences.Volume=v);
            panel.Button(L.Get("Back"),24,248,432,()=>Show("preferences"));
        }
        void RoamPage()
        {
            if(app.Style.Mode!=ViewMode.Twin) { Show("mode"); return; }
            panel.Header(L.Get("01 / Mode"),L.Get("Roam"),L.Get("HOLD TO MOVE · 0.5 m/s"));
            void Move(string label,float x,float y,Vector2 direction)
            { var button=panel.Button(label,x,y,144,()=>{}); button.gameObject.AddComponent<HoldControl>().Direction=direction; }
            Move(L.Get("Left"),24,104,Vector2.left); Move(L.Get("Forward"),168,104,Vector2.up); Move(L.Get("Right"),312,104,Vector2.right);
            panel.Button(L.Get("Turn −30°"),24,168,144,()=>app.Roam.Turn(-30)); Move(L.Get("Backwards"),168,168,Vector2.down);
            panel.Button(L.Get("Turn +30°"),312,168,144,()=>app.Roam.Turn(30));
            panel.Button(L.Get("Return to room"),24,248,264,()=>app.Roam.Return()); panel.Button(L.Get("Back"),304,248,152,()=>Show("mode"));
        }
        void AlignPage()
        {
            panel.Header(L.Get("02 / Align"),L.Get("Manual alignment"),L.Format("Datum {0:F2} m",app.Style.DatumZ));
            panel.Text(L.Get("Point at the twin; pinch and drag with one hand.\nTwo hands also turn. Height starts locked."),24,104,432,56,18);
            panel.Button(L.Get("Fine adjustment"),24,168,208,() => Show("nudge"));
            panel.Button(L.Get("Height / floor"),248,168,208,() => Show("height"));
            panel.Button(L.Get("Use marker"),24,248,208,() => { app.Markers.Begin(); Show("marker"); });
            panel.Button(L.Get("Save alignment"),248,248,208,() => Run(app.Anchors.SaveAsync,() => { Show("setup"); Notify(L.Get(app.Anchors.Status)); }),true);
        }
        void NudgePage()
        {
            panel.Header(L.Get("02 / Align"),L.Get("Fine adjustment"),coarse ? "5 cm / 2°" : "1 cm / 0.5°");
            float d = coarse ? .05f : .01f, yaw = coarse ? 2 : .5f;
            panel.Button(L.Get("X −"),24,104,144,() => app.Manual.Nudge(-d,0,0,0));
            panel.Button(L.Get("Z −"),168,104,144,() => app.Manual.Nudge(0,-d,0,0));
            panel.Button(L.Get("Yaw −"),312,104,144,() => app.Manual.Nudge(0,0,0,-yaw));
            panel.Button(L.Get("X +"),24,168,144,() => app.Manual.Nudge(d,0,0,0));
            panel.Button(L.Get("Z +"),168,168,144,() => app.Manual.Nudge(0,d,0,0));
            panel.Button(L.Get("Yaw +"),312,168,144,() => app.Manual.Nudge(0,0,0,yaw));
            panel.Button(coarse ? L.Get("Step: coarse") : L.Get("Step: fine"),24,248,264,() => { coarse=!coarse; Render(); });
            panel.Button(L.Get("Back"),304,248,152,() => Show("align"));
        }
        void HeightPage()
        {
            panel.Header(L.Get("02 / Align"),L.Get("Floor and height"));
            panel.Button(app.Manual.HeightUnlocked ? L.Get("■ Height unlocked") : L.Get("□ Height locked"),24,104,432,() => { app.Manual.HeightUnlocked=!app.Manual.HeightUnlocked; Render(); });
            panel.Button(L.Get("Down 1 cm"),24,168,208,() => { app.Manual.Nudge(0,0,-.01f,0); if(!app.Manual.HeightUnlocked) Notify(L.Get("Unlock height first")); });
            panel.Button(L.Get("Up 1 cm"),248,168,208,() => { app.Manual.Nudge(0,0,.01f,0); if(!app.Manual.HeightUnlocked) Notify(L.Get("Unlock height first")); });
            panel.Button(L.Get("Snap to floor"),24,248,264,() => Notify(app.Manual.SnapFloor(app.Viewer) ? L.Get("Floor aligned") : L.Get("No tracked floor available")));
            panel.Button(L.Get("Back"),304,248,152,() => Show("align"));
        }
        void MarkerPage()
        {
            panel.Header(L.Get("02 / Align"),L.Get("Marker alignment"));
            liveText=panel.Text(app.Markers.DisplayStatus,24,104,432,56);
            panel.Text(L.Get("Keep the 150 mm marker visible.\nCheck the twin before confirming."),24,168,432,56);
            panel.Button(L.Get("Cancel"),24,248,208,() => Show("align"));
            var confirm=panel.Button(L.Get("Confirm"),248,248,208,() => Run(app.Markers.ConfirmAsync,() => { Show("setup"); Notify(L.Get(app.Anchors.Status)); }),true);
            confirm.interactable=app.Markers.AwaitingConfirmation;
        }
        void PickerPage()
        {
            var item=app.Annotations.Selected;
            var candidate=app.Diff.Selected;
            panel.Header(L.Get("03 / Annotate"),candidate!=null?L.Get("Candidate"):item==null ? L.Get("Choose a surface") : L.Format("Annotation {0:D2}",item.index),candidate==null?"":L.Format("{0:F1} m² · {1:F1} m",candidate.area_m2,Vector3.Distance(app.Viewer.transform.position,candidate.CentroidWorld(app.Root))));
            for(int i=0;i<4;i++)
            { var type=(AnnotationType)i; var button=panel.Button(AnnotationTypes.Prefix(type),24+(i%2)*224,104+(i/2)*64,208,() => { if(app.Diff.Selected!=null){app.Diff.PendingType=type;Render();}else app.Annotations.Retype(type); },candidate!=null?app.Diff.PendingType==type:item?.type==type.ToString().ToLowerInvariant());
                var text=button.GetComponentInChildren<TMP_Text>(); text.fontSize=16; text.rectTransform.anchoredPosition=new Vector2(48,-8); text.rectTransform.sizeDelta=new Vector2(150,32);
                var glyph=PanelBase.Rectangle("Type glyph",button.transform,10,10,28,28).gameObject.AddComponent<AnnotationGlyph>(); glyph.Kind=type; glyph.raycastTarget=false; }
            if(candidate!=null)
            {
                panel.Button(L.Get("Dismiss"),24,248,208,()=>{app.Diff.Dismiss();Show("list");});
                panel.Button(L.Get("Confirm"),248,248,208,()=>{app.Diff.Confirm();Show("list");},true);return;
            }
            panel.Button(L.Get("List"),24,248,208,() => Show("list"));
            panel.Button(L.Get("Done"),248,248,208,() => { app.Placer.Active=false; Show("setup"); },true);
        }
        void ListPage()
        {
            int candidateCount=app.Diff.Candidates.Count;
            panel.Header(L.Get("04 / List"),L.Get("Annotations"),L.Format("{0:D2} ITEMS · {1:D2} CAND",app.Annotations.Items.Count,candidateCount));
            var rows=panel.Scroll((app.Annotations.Items.Count+candidateCount)*56);
            if(app.Annotations.Items.Count+candidateCount==0) panel.Text(L.Get("Select Annotate to mark a surface."),0,0,432,56,parent:rows);
            int n=0;
            foreach(var item in app.Annotations.Items)
            {
                var captured=item;
                float distance=Vector3.Distance(app.Viewer.transform.position,app.Root.TwinToWorld(TwinRoot.Vector(item.position_twin)));
                panel.Row($"{item.index:D2}  {AnnotationTypes.Prefix(item.Kind)}   {distance:F1} m\n{L.Get(item.label)}",n*56,() => { app.Annotations.Select(captured.id); LookAt(captured); },app.Annotations.Selected==item,rows);
                var row=rows.GetChild(rows.childCount-1); var text=row.GetComponentInChildren<TMP_Text>(); text.fontSize=15; text.rectTransform.anchoredPosition=new Vector2(48,-7); text.rectTransform.sizeDelta=new Vector2(372,44);
                var glyph=PanelBase.Rectangle("Type glyph",row,10,14,28,28).gameObject.AddComponent<AnnotationGlyph>(); glyph.Kind=item.Kind; glyph.raycastTarget=false; n++;
            }
            foreach(var candidate in app.Diff.Candidates)
            {
                var captured=candidate;float distance=Vector3.Distance(app.Viewer.transform.position,candidate.CentroidWorld(app.Root));
                panel.Row(L.Format("?? {0}   {1:F1} m\ncandidate · {2:F1} m²",AnnotationTypes.Prefix(candidate.Type),distance,candidate.area_m2),n++*56,()=>{app.Diff.Select(captured);LookAt(captured.CentroidWorld(app.Root),L.Get("Candidate"));Show("picker");},app.Diff.Selected==candidate,rows);
                var text=rows.GetChild(rows.childCount-1).GetComponentInChildren<TMP_Text>();text.fontSize=15;text.rectTransform.anchoredPosition=new Vector2(12,-7);text.rectTransform.sizeDelta=new Vector2(408,44);
            }
            if(app.Annotations.Selected!=null)
            {
                int index=0; for(int i=0;i<app.Annotations.Items.Count;i++) if(app.Annotations.Items[i]==app.Annotations.Selected) index=i;
                rows.anchoredPosition=new Vector2(0,Mathf.Clamp((index-1)*56,0,Mathf.Max(0,app.Annotations.Items.Count*56-112)));
            }
            panel.Button(L.Get("Retype"),24,248,144,() => { if(app.Annotations.Selected!=null) Show("picker"); else Notify(L.Get("Select an annotation")); });
            panel.Button(L.Get("Delete"),168,248,144,() => { if(app.Annotations.Selected!=null) { app.Annotations.DeleteSelected(); Notify(L.Get("Annotation deleted")); } });
            panel.Button(L.Get("Export"),312,248,144,() => { AnnotationExporter.Export(app.Annotations); Notify(L.Get("JSON and CSV saved in Exports")); },true);
        }
        void LookAt(Annotation item)
            => LookAt(app.Root.TwinToWorld(TwinRoot.Vector(item.position_twin)),L.Format("Annotation {0:D2}",item.index));
        void LookAt(Vector3 point,string name)
        {
            var local=app.Viewer.transform.InverseTransformPoint(point);
            string direction=local.z<0 ? L.Get("↓ Behind you") : Mathf.Abs(local.x)>.5f ? (local.x<0 ? L.Get("← Left") : L.Get("→ Right")) : L.Get("↑ Ahead");
            Notify(direction+" · "+name);
        }
        void DiagnosticsPage()
        {
            panel.Header(L.Get("05 / Diagnostics"),L.Get("Performance"),Application.isEditor ? L.Get("EDITOR") : L.Get("DEVICE"));
            liveText=panel.Text(app.Performance.Readout,24,104,432,120);
            panel.Button(L.Get("Back"),24,248,432,() => Show("setup"));
        }
        void Update()
        {
            if (!panel) return;
            if(Keyboard.current?.f1Key.wasPressedThisFrame==true) Summon();
            if(app.Ready && Application.isEditor)
            {
                if(Keyboard.current?.f2Key.wasPressedThisFrame==true) { app.Markers.SimulateFirst(); Show("marker"); }
                if(Keyboard.current?.f3Key.wasPressedThisFrame==true) BeginAnnotation();
                if(Keyboard.current?.f4Key.wasPressedThisFrame==true) { app.Style.SetMode((ViewMode)(((int)app.Style.Mode+1)%3)); if(page=="mode") Render(); }
            }
            if(Time.unscaledTime>=nextRefresh)
            {
                nextRefresh=Time.unscaledTime+.25f;
                if(liveText && page=="marker") liveText.text=app.Markers.DisplayStatus;
                if(liveText && page=="diagnostics") liveText.text=app.Performance.Readout;
            }
        }
        void OnDestroy()
        {
            L.Changed -= LanguageChanged;
            if(app&&app.Diff)app.Diff.Changed-=DiffChanged;
            if(subscribed && app) { if(app.Annotations!=null) app.Annotations.Changed-=AnnotationChanged; if(app.Placer) { app.Placer.Placed-=AnnotationPlaced; app.Placer.Feedback-=Notify; } if(app.Markers) app.Markers.Changed-=MarkerChanged; }
            if(panel) Destroy(panel.gameObject); if(strip) Destroy(strip.gameObject);
        }
    }
}
