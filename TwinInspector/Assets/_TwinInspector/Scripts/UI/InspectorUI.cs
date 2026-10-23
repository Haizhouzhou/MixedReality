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
        bool diffRefreshPending;
        float lastUiInteraction;
        readonly GuideFlow guide=new GuideFlow();
        bool guideConsidered;
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
            app.Picker.Changed += PickChanged;
            subscribed = true;
        }
        void AnnotationChanged() { if (page == "list" || page == "picker") Render(); }
        void AnnotationPlaced() { app.Placer.Active = false; if(app.Annotations.Selected!=null)freshAnnotations.Add(app.Annotations.Selected.id); Show("picker"); }
        void DiffChanged()
        {
            string signature=app.Diff.Visible+":"+app.Diff.Selected?.id+":"+app.Diff.Candidates.Count;
            foreach(var c in app.Diff.Candidates)signature+="/"+c.id;
            if(signature==lastDiffSignature)return;lastDiffSignature=signature;
            if(page=="list"||page=="picker"||page=="display")diffRefreshPending=true;
        }
        void MarkerChanged() { if (page == "marker") Render(); }
        void PickChanged(){if(page=="picker")Render();}
        public void Notify(string message) { if (panel) panel.Notify(message); }
        public void Show(string target)
        {
            Subscribe();
            if(target=="guide"&&page!="guide")guide.Reset();
            if (!app.Ready) target = "setup";
            if (page == "marker" && target != "marker" && app.Markers) app.Markers.Cancel();
            if(page=="picker"&&target!="picker"&&app.Picker)app.Picker.Cancel();
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
            Notify(L.Get("toast.pointAtASurfaceAndSelect"));
        }
        async void Run(Func<Task> operation, Action after = null)
        {
            if (busy) return;
            busy = true;
            try { await operation(); if (this) after?.Invoke(); }
            catch (Exception error) { DevLog.Write(error.ToString()); if (this) Notify(L.Get("toast.actionFailedSeeDiagnostics")); }
            finally { busy = false; }
        }
        void Render()
        {
            diffRefreshPending=false;
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
                case "guide": GuidePage(); break;
                case "language": LanguagePage(); break;
                default: SetupPage(); break;
            }
        }
        void SetupPage()
        {
            panel.Header(L.Get("page.setup.label"),L.Get("page.setup.heading"),L.Get("meta.version"));
            panel.Text(app.DisplayStatus+(app.Ready ? "\n"+L.Get(app.Anchors.Status) : ""),24,104,432,56);
            if (!app.Ready)
            { panel.Button(L.Get("button.retrySetup"),24,184,432,() => Run(app.InitializeAsync)); return; }
            panel.Button(L.Get("button.manualAlignment"),24,168,208,() => Show("align"));
            panel.Button(L.Get("button.preferences"),248,168,208,() => Show("preferences"));
            panel.Button(L.Get("button.viewModes"),24,248,208,() => Show("mode"),true);
            panel.Button(L.Get("button.guide"),248,248,208,() => Show("guide"));
        }
        void ViewPage()
        {
            panel.Header(L.Get("page.mode.label"),L.Get("page.mode.heading"),app.Style.Mode==ViewMode.Twin?L.Get("meta.virtualInspection"):"");
            var labels=new[]{L.Get("button.reality"),L.Get("button.twin"),L.Get("button.overlay")};
            for(int i=0;i<3;i++)
            { var mode=(ViewMode)i; panel.Button(labels[i],24+i*144,104,144,()=>{ app.Style.SetMode(mode); Render(); },app.Style.Mode==mode); }
            panel.Button(app.Style.Technical?L.Get("button.technical"):L.Get("button.colour"),24,168,208,()=>{ app.Style.SetTechnical(!app.Style.Technical); Render(); },app.Style.Technical);
            var lens=panel.Button(app.Style.Lens?L.Get("button.lensOn"):L.Get("button.lensOff"),248,168,208,()=>{ app.Style.SetLens(!app.Style.Lens); Render(); },app.Style.Lens);
            lens.interactable=app.Style.Mode==ViewMode.Overlay;
            panel.Button(L.Get("button.opacityLens"),24,248,264,()=>Show("display"));
            panel.Button(app.Style.Mode==ViewMode.Twin?L.Get("button.roam"):L.Get("button.setup"),304,248,152,()=>Show(app.Style.Mode==ViewMode.Twin?"roam":"setup"));
        }
        void DisplayPage()
        {
            panel.Header(L.Get("page.mode.label"),L.Get("page.display.heading"),L.Format("meta.l0D1",app.Style.LensActive?"+":"−",app.Diff.Visible?"+":"−"));
            panel.Text(L.Get("text.twinOpacity"),24,104,432,24,19); panel.Slider(app.Style.Blend,app.Style.SetBlend,128);
            panel.Text(L.Get("text.lensRadius"),24,184,432,24,19); panel.Slider(app.Style.LensRadius,app.Style.SetLensRadius,208,.5f,5," m");
            panel.Button(L.Get("button.diffOn"),24,264,144,()=>{app.Diff.Visible=true;Render();},app.Diff.Visible);
            panel.Button(L.Get("button.diffOff"),168,264,144,()=>{app.Diff.Visible=false;Render();},!app.Diff.Visible);
            panel.Button(L.Get("button.back"),312,264,144,()=>Show("mode"));
        }
        void PreferencesPage()
        {
            panel.Header(L.Get("page.setup.label"),L.Get("button.preferences"),L.Get("meta.dominantHand"));
            panel.Button(L.Get("button.leftHand"),24,104,208,()=>{ InteractionPreferences.LeftHand=true; Render(); },InteractionPreferences.LeftHand);
            panel.Button(L.Get("button.rightHand"),248,104,208,()=>{ InteractionPreferences.LeftHand=false; Render(); },!InteractionPreferences.LeftHand);
            panel.Button(L.Get("button.diagnostics"),24,168,208,()=>Show("diagnostics"));
            panel.Button(L.Get("button.language"),248,168,208,()=>Show("language"));
            panel.Text(L.Get("text.eitherHandCanSummonAndOperate"),24,224,432,20,DesignTokens.Label);
            panel.Button(L.Get("button.feedbackSound"),24,248,208,()=>Show("sound"));
            panel.Button(L.Get("button.recenterBack"),248,248,208,()=>{ Show("setup"); Summon(); });
        }
        void SoundPage()
        {
            panel.Header(L.Get("page.setup.label"),L.Get("button.feedbackSound"),L.Get("meta.visualFeedbackAlwaysOn"));
            panel.Button(InteractionPreferences.Sound?L.Get("button.soundOn"):L.Get("button.soundMuted"),24,104,432,()=>{ InteractionPreferences.Sound=!InteractionPreferences.Sound; Render(); },InteractionPreferences.Sound);
            panel.Text(L.Get("text.volume"),24,168,432,24,19); panel.Slider(InteractionPreferences.Volume,v=>InteractionPreferences.Volume=v);
            panel.Button(L.Get("button.back"),24,248,432,()=>Show("preferences"));
        }
        void RoamPage()
        {
            if(app.Style.Mode!=ViewMode.Twin) { Show("mode"); return; }
            panel.Header(L.Get("page.mode.label"),L.Get("button.roam"),L.Get("meta.holdToMove05MS"));
            void Move(string label,float x,float y,Vector2 direction)
            { var button=panel.Button(label,x,y,144,()=>{}); button.gameObject.AddComponent<HoldControl>().Direction=direction; }
            Move(L.Get("button.left"),24,104,Vector2.left); Move(L.Get("button.forward"),168,104,Vector2.up); Move(L.Get("button.right"),312,104,Vector2.right);
            panel.Button(L.Get("button.turnMinus30"),24,168,144,()=>app.Roam.Turn(-30)); Move(L.Get("button.backwards"),168,168,Vector2.down);
            panel.Button(L.Get("button.turnPlus30"),312,168,144,()=>app.Roam.Turn(30));
            panel.Button(L.Get("button.returnToRoom"),24,248,264,()=>app.Roam.Return()); panel.Button(L.Get("button.back"),304,248,152,()=>Show("mode"));
        }
        void AlignPage()
        {
            panel.Header(L.Get("page.align.label"),L.Get("button.manualAlignment"),L.Format("text.datum0F2M",app.Style.DatumZ));
            panel.Text(L.Get("toast.pointAtTheTwinPinchAndDragWithOneHandTwoHandsAlsoTurnHeightStartsLocked"),24,104,432,56,18);
            panel.Button(L.Get("button.fineAdjustment"),24,168,208,() => Show("nudge"));
            panel.Button(L.Get("button.heightFloor"),248,168,208,() => Show("height"));
            panel.Button(L.Get("button.useMarker"),24,248,208,() => { app.Markers.Begin(); Show("marker"); });
            panel.Button(L.Get("button.saveAlignment"),248,248,208,() => Run(app.Anchors.SaveAsync,() => { Show("setup"); Notify(L.Get(app.Anchors.Status)); }),true);
        }
        void NudgePage()
        {
            panel.Header(L.Get("page.align.label"),L.Get("button.fineAdjustment"),L.Get(coarse ? "meta.step.coarse" : "meta.step.fine"));
            float d = coarse ? .05f : .01f, yaw = coarse ? 2 : .5f;
            panel.Button(L.Get("button.xMinus"),24,104,144,() => app.Manual.Nudge(-d,0,0,0));
            panel.Button(L.Get("button.zMinus"),168,104,144,() => app.Manual.Nudge(0,-d,0,0));
            panel.Button(L.Get("button.yawMinus"),312,104,144,() => app.Manual.Nudge(0,0,0,-yaw));
            panel.Button(L.Get("button.xPlus"),24,168,144,() => app.Manual.Nudge(d,0,0,0));
            panel.Button(L.Get("button.zPlus"),168,168,144,() => app.Manual.Nudge(0,d,0,0));
            panel.Button(L.Get("button.yawPlus"),312,168,144,() => app.Manual.Nudge(0,0,0,yaw));
            panel.Button(coarse ? L.Get("button.stepCoarse") : L.Get("button.stepFine"),24,248,264,() => { coarse=!coarse; Render(); });
            panel.Button(L.Get("button.back"),304,248,152,() => Show("align"));
        }
        void HeightPage()
        {
            panel.Header(L.Get("page.align.label"),L.Get("page.height.heading"));
            panel.Button(app.Manual.HeightUnlocked ? L.Get("button.unlockedHeightUnlocked") : L.Get("button.lockedHeightLocked"),24,104,432,() => { app.Manual.HeightUnlocked=!app.Manual.HeightUnlocked; Render(); });
            panel.Button(L.Get("button.down1Cm"),24,168,208,() => { app.Manual.Nudge(0,0,-.01f,0); if(!app.Manual.HeightUnlocked) Notify(L.Get("toast.unlockHeightFirst")); });
            panel.Button(L.Get("button.up1Cm"),248,168,208,() => { app.Manual.Nudge(0,0,.01f,0); if(!app.Manual.HeightUnlocked) Notify(L.Get("toast.unlockHeightFirst")); });
            panel.Button(L.Get("button.snapToFloor"),24,248,264,() => Notify(app.Manual.SnapFloor(app.Viewer) ? L.Get("text.floorAligned") : L.Get("text.noTrackedFloorAvailable")));
            panel.Button(L.Get("button.back"),304,248,152,() => Show("align"));
        }
        void MarkerPage()
        {
            panel.Header(L.Get("page.align.label"),L.Get("page.marker.heading"));
            liveText=panel.Text(app.Markers.DisplayStatus,24,104,432,56);
            panel.Text(L.Get("text.keepThe150MmMarkerVisibleCheckTheTwinBeforeConfirming"),24,168,432,56);
            panel.Button(L.Get("button.cancel"),24,248,208,() => Show("align"));
            var confirm=panel.Button(L.Get("button.confirm"),248,248,208,() => Run(app.Markers.ConfirmAsync,() => { Show("setup"); Notify(L.Get(app.Anchors.Status)); }),true);
            confirm.interactable=app.Markers.AwaitingConfirmation;
        }
        void PickerPage()
        {
            var item=app.Annotations.Selected;
            var candidate=app.Diff.Selected;
            panel.Header(L.Get("page.annotate.label"),candidate!=null?L.Get("page.annotate.candidate"):item==null ? L.Get("page.annotate.chooseSurface") : L.Format("text.annotation0D2",item.index),candidate==null?"":L.Format("text.0F1M1F1M",candidate.area_m2,Vector3.Distance(app.Viewer.transform.position,candidate.CentroidWorld(app.Root))));
            for(int i=0;i<4;i++)
            { var type=(AnnotationType)i; var button=panel.Button(AnnotationTypes.Prefix(type),24+(i%2)*224,104+(i/2)*64,208,() => { if(app.Diff.Selected!=null){app.Diff.PendingType=type;Render();}else app.Annotations.Retype(type); },candidate!=null?app.Diff.PendingType==type:item?.type==type.ToString().ToLowerInvariant());
                var text=button.GetComponentInChildren<TMP_Text>(); text.fontSize=16;text.fontSizeMin=16;text.fontSizeMax=16; text.rectTransform.anchoredPosition=new Vector2(48,-8); text.rectTransform.sizeDelta=new Vector2(150,32);
                var glyph=PanelBase.Rectangle("Type glyph",button.transform,10,10,28,28).gameObject.AddComponent<AnnotationGlyph>(); glyph.Kind=type; glyph.raycastTarget=false; }
            if(candidate!=null)
            {
                panel.Button(L.Get("button.dismiss"),24,248,208,()=>{app.Diff.Dismiss();Show("list");});
                panel.Button(L.Get("button.confirm"),248,248,208,()=>{app.Diff.Confirm();Show("list");},true);return;
            }
            bool canPick=app.Picker.Available&&item!=null&&freshAnnotations.Contains(item.id);
            if(canPick)
            {
                panel.Button(L.Get("button.list"),24,248,144,()=>Show("list"));
                var pick=panel.Button(L.Get("button.pickObject"),168,248,144,()=>app.Picker.Arm());pick.interactable=!app.Picker.Busy&&!app.Picker.Armed;
                panel.Button(L.Get("button.done"),312,248,144,()=>{app.Placer.Active=false;Show("setup");},true);
            }
            else
            {
                panel.Button(L.Get("button.list"),24,248,208,() => Show("list"));
                panel.Button(L.Get("button.done"),248,248,208,() => { app.Placer.Active=false; Show("setup"); },true);
            }
        }
        void ListPage()
        {
            int candidateCount=app.Diff.Candidates.Count;
            panel.Header(L.Get("page.list.label"),L.Get("page.list.heading"),L.Format("meta.0D2Items1D2Cand",app.Annotations.Items.Count,candidateCount));
            var rows=panel.Scroll((app.Annotations.Items.Count+candidateCount)*56);
            if(app.Annotations.Items.Count+candidateCount==0) panel.Text(L.Get("text.selectAnnotateToMarkASurface"),0,0,432,56,parent:rows);
            int n=0;
            foreach(var item in app.Annotations.Items)
            {
                var captured=item;
                float distance=Vector3.Distance(app.Viewer.transform.position,app.Root.TwinToWorld(TwinRoot.Vector(item.position_twin)));
                panel.Row(L.Format("list.annotationRow",item.index,AnnotationTypes.Prefix(item.Kind),distance,L.Get(item.Kind.ToString())),n*56,() => { app.Annotations.Select(captured.id); LookAt(captured); },app.Annotations.Selected==item,rows);
                var row=rows.GetChild(rows.childCount-1); var text=row.GetComponentInChildren<TMP_Text>(); text.fontSize=16;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=16; text.rectTransform.anchoredPosition=new Vector2(48,-7); text.rectTransform.sizeDelta=new Vector2(372,44);
                var glyph=PanelBase.Rectangle("Type glyph",row,10,14,28,28).gameObject.AddComponent<AnnotationGlyph>(); glyph.Kind=item.Kind; glyph.raycastTarget=false; n++;
            }
            foreach(var candidate in app.Diff.Candidates)
            {
                var captured=candidate;float distance=Vector3.Distance(app.Viewer.transform.position,candidate.CentroidWorld(app.Root));
                panel.Row(L.Format("text.01F1MCandidate2F1M",AnnotationTypes.Prefix(candidate.Type),distance,candidate.area_m2),n++*56,()=>{app.Diff.Select(captured);LookAt(captured.CentroidWorld(app.Root),L.Get("page.annotate.candidate"));Show("picker");},app.Diff.Selected==candidate,rows);
                var text=rows.GetChild(rows.childCount-1).GetComponentInChildren<TMP_Text>();text.fontSize=16;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=16;text.rectTransform.anchoredPosition=new Vector2(12,-7);text.rectTransform.sizeDelta=new Vector2(408,44);
            }
            if(app.Annotations.Selected!=null)
            {
                int index=0; for(int i=0;i<app.Annotations.Items.Count;i++) if(app.Annotations.Items[i]==app.Annotations.Selected) index=i;
                rows.anchoredPosition=new Vector2(0,Mathf.Clamp((index-1)*56,0,Mathf.Max(0,app.Annotations.Items.Count*56-112)));
            }
            panel.Button(L.Get("button.retype"),24,248,144,() => { if(app.Annotations.Selected!=null) Show("picker"); else Notify(L.Get("toast.selectAnAnnotation")); });
            panel.Button(L.Get("button.delete"),168,248,144,() => { if(app.Annotations.Selected!=null) { app.Annotations.DeleteSelected(); Notify(L.Get("toast.annotationDeleted")); } });
            panel.Button(L.Get("button.export"),312,248,144,() => Run(async()=>{await AnnotationExporter.ExportAsync(app.Annotations);Notify(L.Format("toast.jsonCsvAnd0D2PatchesSavedInExports",AnnotationExporter.LastPatchCount));}),true);
        }
        void LookAt(Annotation item)
            => LookAt(app.Root.TwinToWorld(TwinRoot.Vector(item.position_twin)),L.Format("text.annotation0D2",item.index));
        void LookAt(Vector3 point,string name)
        {
            var local=app.Viewer.transform.InverseTransformPoint(point);
            string direction=local.z<0 ? L.Get("text.behindYou") : Mathf.Abs(local.x)>.5f ? (local.x<0 ? L.Get("text.left") : L.Get("text.right")) : L.Get("text.ahead");
            Notify(direction+" · "+name);
        }
        void DiagnosticsPage()
        {
            panel.Header(L.Get("page.diagnostics.label"),L.Get("page.diagnostics.heading"),Application.isEditor ? L.Get("meta.editor") : L.Get("meta.device"));
            liveText=panel.Text(DiagnosticsReadout,24,104,432,120);liveText.overflowMode=TextOverflowModes.Truncate;
            panel.Button(L.Get("button.depthProbe"),24,248,144,()=>app.Probes.DepthProbe());
            panel.Button(L.Get("button.cameraProbe"),168,248,144,()=>app.Probes.CameraProbe());
            panel.Button(L.Get("button.back"),312,248,144,() => Show("setup"));
        }
        string DiagnosticsReadout=>app.Performance.Readout+(string.IsNullOrEmpty(app.Probes.Status)?"":"\n"+app.Probes.Status);
        void LanguagePage()
        {
            panel.Header(L.Get("page.setup.label"),L.Get("button.language"));
            string[] codes={"en","de","fr","it"},names={"English","Deutsch","Français","Italiano"};
            for(int i=0;i<codes.Length;i++){string code=codes[i];panel.Button(L.Get(names[i]),24+i*108,104,108,()=>L.Language=code,L.Language==code);}
            panel.Button(L.Get("button.back"),24,248,432,()=>Show("preferences"));
        }
        void GuidePage()
        {
            string prefix="guide."+(guide.Index+1)+".";
            panel.Header(L.Get("button.guide"),L.Get(prefix+"heading"),L.Format(Application.isEditor?"guide.progressEditor":"guide.progress",guide.Index+1));
            var body=panel.Text(L.Get(prefix+"body"),24,104,432,64);body.enableAutoSizing=true;body.fontSizeMin=16;body.fontSizeMax=21;
            var attempt=panel.Text(L.Format("guide.try",L.Get(prefix+"try")),24,184,432,40,16);attempt.enableAutoSizing=true;attempt.fontSizeMin=16;attempt.fontSizeMax=16;
            if(Application.isEditor)panel.Text(L.Get("guide.editorHint"),24,224,432,20,DesignTokens.Label);
            panel.Button(L.Get("button.skip"),24,248,144,()=>{guide.Skip();Show("setup");});
            var back=panel.Button(L.Get("button.back"),168,248,144,()=>{guide.Back();Render();});back.interactable=guide.Index>0;
            panel.Button(L.Get(guide.Last?"button.done":"button.next"),312,248,144,()=>{if(guide.Last){guide.Done();Show("setup");}else{guide.Next();Render();}},true);
        }
        void Update()
        {
            if (!panel) return;
            // Background candidate updates must not destroy a hovered button or a dragged ScrollRect.
            if(app.Input&&(app.Input.OverUI||app.Input.Held))lastUiInteraction=Time.unscaledTime;
            if(diffRefreshPending&&Time.unscaledTime-lastUiInteraction>.5f)
            {
                var scroll=panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                float offset=scroll?scroll.content.anchoredPosition.y:0;
                Render();
                scroll=panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                if(scroll)scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(offset,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height)));
            }
            if(app.Ready&&!guideConsidered&&page=="setup") {guideConsidered=true;if(!InteractionPreferences.GuideSeen)Show("guide");}
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
                if(liveText && page=="diagnostics") liveText.text=DiagnosticsReadout;
            }
        }
        void OnDestroy()
        {
            L.Changed -= LanguageChanged;
            if(app&&app.Diff)app.Diff.Changed-=DiffChanged;
            if(app&&app.Picker)app.Picker.Changed-=PickChanged;
            if(subscribed && app) { if(app.Annotations!=null) app.Annotations.Changed-=AnnotationChanged; if(app.Placer) { app.Placer.Placed-=AnnotationPlaced; app.Placer.Feedback-=Notify; } if(app.Markers) app.Markers.Changed-=MarkerChanged; }
            if(panel) Destroy(panel.gameObject); if(strip) Destroy(strip.gameObject);
        }
    }
}
