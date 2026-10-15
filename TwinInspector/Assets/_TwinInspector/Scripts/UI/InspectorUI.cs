using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

namespace TwinInspector
{
    /// <summary>World-locked inspection pages and a four-cell palm tool strip.</summary>
    public sealed class InspectorUI : MonoBehaviour
    {
        AppBootstrap app;
        PanelBase panel, strip;
        TMP_Text liveText;
        string page = "setup";
        bool coarse, busy, subscribed;
        ToolStrip spatialStrip;
        float nextRefresh;
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
            Subscribe();
        }
        void Subscribe()
        {
            if (subscribed || !app.Ready) return;
            app.Annotations.Changed += AnnotationChanged;
            app.Placer.Placed += AnnotationPlaced;
            app.Placer.Feedback += Notify;
            app.Markers.Changed += MarkerChanged;
            subscribed = true;
        }
        void AnnotationChanged() { if (page == "list" || page == "picker") Render(); }
        void AnnotationPlaced() { app.Placer.Active = false; Show("picker"); }
        void MarkerChanged() { if (page == "marker") Render(); }
        public void Notify(string message) { if (panel) panel.Notify(message); }
        public void Show(string target)
        {
            Subscribe();
            if (!app.Ready) target = "setup";
            if (page == "marker" && target != "marker" && app.Markers) app.Markers.Cancel();
            page = target;
            if(app.Placer) app.Placer.Active=false;
            if(spatialStrip) spatialStrip.Select(target=="mode"?0:target=="align"||target=="nudge"||target=="height"||target=="marker"?1:target=="picker"?2:target=="list"?3:-1);
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
            Notify("Point at a surface and select");
        }
        async void Run(Func<Task> operation, Action after = null)
        {
            if (busy) return;
            busy = true;
            try { await operation(); if (this) after?.Invoke(); }
            catch (Exception error) { DevLog.Write(error.ToString()); if (this) Notify(error.Message); }
            finally { busy = false; }
        }
        void Render()
        {
            panel.Clear(); liveText = null;
            switch (page)
            {
                case "mode": ViewPage(); break;
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
            panel.Header("01 / TwinInspector","Inspect the building","v0.1.0");
            panel.Text(app.Status+(app.Ready ? "\n"+app.Anchors.Status : ""),24,104,432,56);
            if (!app.Ready)
            { panel.Button("Retry setup",24,184,432,() => Run(app.InitializeAsync)); return; }
            panel.Button("Manual alignment",24,168,208,() => Show("align"));
            panel.Button("Use marker",248,168,208,() => { app.Markers.Begin(); Show("marker"); });
            panel.Button("View modes",24,248,208,() => Show("mode"),true);
            panel.Button("Diagnostics",248,248,208,() => Show("diagnostics"));
        }
        void ViewPage()
        {
            panel.Header("02 / Compare","View mode",app.Style.Mode==ViewMode.Twin ? "PINCH BOTH TO ROAM" : "");
            var labels = new[] { "Reality", "Twin", "Overlay" };
            for (int i=0;i<3;i++)
            { var mode=(ViewMode)i; panel.Button(labels[i],24+i*144,104,144,() => { app.Style.SetMode(mode); Render(); },app.Style.Mode==mode); }
            panel.Text("Twin opacity",24,168,432,24);
            panel.Slider(app.Style.Blend,app.Style.SetBlend);
            panel.Button(app.Style.Technical ? "Style: technical" : "Style: colour",24,248,264,() => { app.Style.SetTechnical(!app.Style.Technical); Render(); });
            panel.Button("Done",304,248,152,() => Show("setup"),true);
        }
        void AlignPage()
        {
            panel.Header("03 / Register","Manual alignment");
            panel.Text("Pinch both hands to move the twin.\nDesktop: use Fine adjustment.",24,104,432,56);
            panel.Button("Fine adjustment",24,168,208,() => Show("nudge"));
            panel.Button("Height / floor",248,168,208,() => Show("height"));
            panel.Button("Use marker",24,248,208,() => { app.Markers.Begin(); Show("marker"); });
            panel.Button("Save alignment",248,248,208,() => Run(app.Anchors.SaveAsync,() => { Show("setup"); Notify(app.Anchors.Status); }),true);
        }
        void NudgePage()
        {
            panel.Header("03 / Register","Fine adjustment",coarse ? "5 cm / 2°" : "1 cm / 0.5°");
            float d = coarse ? .05f : .01f, yaw = coarse ? 2 : .5f;
            panel.Button("X −",24,104,144,() => app.Manual.Nudge(-d,0,0,0));
            panel.Button("Z −",168,104,144,() => app.Manual.Nudge(0,-d,0,0));
            panel.Button("Yaw −",312,104,144,() => app.Manual.Nudge(0,0,0,-yaw));
            panel.Button("X +",24,168,144,() => app.Manual.Nudge(d,0,0,0));
            panel.Button("Z +",168,168,144,() => app.Manual.Nudge(0,d,0,0));
            panel.Button("Yaw +",312,168,144,() => app.Manual.Nudge(0,0,0,yaw));
            panel.Button(coarse ? "Step: coarse" : "Step: fine",24,248,264,() => { coarse=!coarse; Render(); });
            panel.Button("Back",304,248,152,() => Show("align"));
        }
        void HeightPage()
        {
            panel.Header("03 / Register","Floor and height");
            panel.Button(app.Manual.HeightUnlocked ? "■ Height unlocked" : "□ Height locked",24,104,432,() => { app.Manual.HeightUnlocked=!app.Manual.HeightUnlocked; Render(); });
            panel.Button("Down 1 cm",24,168,208,() => { app.Manual.Nudge(0,0,-.01f,0); if(!app.Manual.HeightUnlocked) Notify("Unlock height first"); });
            panel.Button("Up 1 cm",248,168,208,() => { app.Manual.Nudge(0,0,.01f,0); if(!app.Manual.HeightUnlocked) Notify("Unlock height first"); });
            panel.Button("Snap to floor",24,248,264,() => Notify(app.Manual.SnapFloor(app.Viewer) ? "Floor aligned" : "No tracked floor available"));
            panel.Button("Back",304,248,152,() => Show("align"));
        }
        void MarkerPage()
        {
            panel.Header("03 / Register","Marker alignment");
            liveText=panel.Text(app.Markers.Status,24,104,432,56);
            panel.Text("Keep the 150 mm marker visible.\nCheck the twin before confirming.",24,168,432,56);
            panel.Button("Cancel",24,248,208,() => Show("align"));
            var confirm=panel.Button("Confirm",248,248,208,() => Run(app.Markers.ConfirmAsync,() => { Show("setup"); Notify(app.Anchors.Status); }),true);
            confirm.interactable=app.Markers.AwaitingConfirmation;
        }
        void PickerPage()
        {
            var item=app.Annotations.Selected;
            panel.Header("04 / Record",item==null ? "Choose a surface" : "Annotation "+item.index.ToString("D2"));
            for(int i=0;i<4;i++)
            { var type=(AnnotationType)i; panel.Button(type.ToString(),24+(i%2)*224,104+(i/2)*64,208,() => { app.Annotations.Retype(type); },item?.type==type.ToString().ToLowerInvariant()); }
            panel.Button("List",24,248,208,() => Show("list"));
            panel.Button("Done",248,248,208,() => { app.Placer.Active=false; Show("setup"); },true);
        }
        void ListPage()
        {
            panel.Header("04 / Record","Annotations",app.Annotations.Items.Count.ToString("D2")+" items");
            var rows=panel.Scroll(app.Annotations.Items.Count*56);
            if(app.Annotations.Items.Count==0) panel.Text("Select Annotate to mark a surface.",0,0,432,56,parent:rows);
            int n=0;
            foreach(var item in app.Annotations.Items)
            {
                var captured=item;
                float distance=Vector3.Distance(app.Viewer.transform.position,app.Root.TwinToWorld(TwinRoot.Vector(item.position_twin)));
                panel.Row($"{item.index:D2}   {item.label}     {distance:F1} m",n++*56,() => { app.Annotations.Select(captured.id); LookAt(captured); },app.Annotations.Selected==item,rows);
            }
            if(app.Annotations.Selected!=null)
            {
                int index=0; for(int i=0;i<app.Annotations.Items.Count;i++) if(app.Annotations.Items[i]==app.Annotations.Selected) index=i;
                rows.anchoredPosition=new Vector2(0,Mathf.Clamp((index-1)*56,0,Mathf.Max(0,app.Annotations.Items.Count*56-112)));
            }
            panel.Button("Retype",24,248,144,() => { if(app.Annotations.Selected!=null) Show("picker"); else Notify("Select an annotation"); });
            panel.Button("Delete",168,248,144,() => { if(app.Annotations.Selected!=null) { app.Annotations.DeleteSelected(); Notify("Annotation deleted"); } });
            panel.Button("Export",312,248,144,() => { AnnotationExporter.Export(app.Annotations); Notify("JSON and CSV saved in Exports"); },true);
        }
        void LookAt(Annotation item)
        {
            var local=app.Viewer.transform.InverseTransformPoint(app.Root.TwinToWorld(TwinRoot.Vector(item.position_twin)));
            string direction=local.z<0 ? "↓ Behind you" : Mathf.Abs(local.x)>.5f ? (local.x<0 ? "← Left" : "→ Right") : "↑ Ahead";
            Notify(direction+" · Annotation "+item.index.ToString("D2"));
        }
        void DiagnosticsPage()
        {
            panel.Header("05 / Diagnostics","Performance",Application.isEditor ? "EDITOR" : "DEVICE");
            liveText=panel.Text(app.Performance.Readout,24,104,432,120);
            panel.Button("Back",24,248,432,() => Show("setup"));
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
                if(liveText && page=="marker") liveText.text=app.Markers.Status;
                if(liveText && page=="diagnostics") liveText.text=app.Performance.Readout;
            }
        }
        void OnDestroy()
        {
            if(subscribed) { app.Annotations.Changed-=AnnotationChanged; app.Placer.Placed-=AnnotationPlaced; app.Placer.Feedback-=Notify; app.Markers.Changed-=MarkerChanged; }
            if(panel) Destroy(panel.gameObject); if(strip) Destroy(strip.gameObject);
        }
    }
}
