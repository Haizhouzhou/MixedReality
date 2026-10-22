var app=TwinInspector.AppBootstrap.Instance;
if(!app||!app.Ready||!app.Diff.Settled) return "WAIT engine";
var source=(TwinInspector.EditorSceneSurfaceSimulator)app.SceneSource;
var fixture=source.Fixture;
var missing=System.Array.Find(fixture.edits,e=>e.id=="e1");var added=System.Array.Find(fixture.edits,e=>e.id=="e2");var moved=System.Array.Find(fixture.edits,e=>e.id=="e3");
var candidates=app.Diff.Candidates;
System.Func<TwinInspector.Candidate,TwinInspector.CandidateKind,float[],bool> near=(c,k,p)=>c.kind==k&&UnityEngine.Vector3.Distance(c.CentroidTwin(app.Root),TwinInspector.TwinRoot.Vector(p))<=.5f;
int m=System.Linq.Enumerable.Count(candidates,c=>near(c,TwinInspector.CandidateKind.Missing,missing.center_twin));
int n=System.Linq.Enumerable.Count(candidates,c=>near(c,TwinInspector.CandidateKind.New,added.center_twin));
var changed=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(candidates,c=>near(c,TwinInspector.CandidateKind.Changed,moved.from_twin)));
if(m<1||n<1||changed.Length!=1||candidates.Count-m-n-changed.Length>2)throw new System.Exception("Fixture counts failed M="+m+" N="+n+" C="+changed.Length+" all="+candidates.Count);
var c=changed[0];var pair=System.Linq.Enumerable.First(app.Diff.Members,x=>x.id==c.paired_new);var old=System.Linq.Enumerable.First(app.Diff.Members,x=>x.id==c.paired_missing);
if(!pair.hiddenByPair||!old.hiddenByPair||UnityEngine.Vector3.Distance(pair.CentroidTwin(app.Root),TwinInspector.TwinRoot.Vector(moved.to_twin))>.5f)throw new System.Exception("Changed pairing failed");
System.Func<System.Threading.Tasks.Task> run=async()=>
{
 var text=new System.Text.StringBuilder();int maxRays=0,maxOverlaps=0;
 for(int i=0;i<180;i++){await System.Threading.Tasks.Task.Yield();maxRays=System.Math.Max(maxRays,app.Diff.LastRays);maxOverlaps=System.Math.Max(maxOverlaps,app.Diff.LastOverlaps);}
 if(maxRays>2048||maxOverlaps>2048||app.Diff.AverageMainMs>2)throw new System.Exception("Diff budget exceeded");
 text.AppendLine("PASS: real Region A fixture, missing="+m+" new="+n+" changed="+changed.Length+" other="+(candidates.Count-m-n-changed.Length));
 text.AppendLine("PASS: Changed pairs are one-to-one, both members hidden, paired New within 0.5m of planted target.");
 text.AppendLine("Measured Editor main_ms="+app.Diff.AverageMainMs.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" max rays/frame="+maxRays+" max overlaps/frame="+maxOverlaps);
 foreach(var item in candidates)text.AppendLine(item.kind+" "+item.CentroidTwin(app.Root)+" area="+item.area_m2);
 text.AppendLine("Device geometry, latency and tracking remain unverified. Candidates are geometric hypotheses, not verified changes.");
 System.IO.File.WriteAllText("docs/evidence/diff-engine-v13.txt",text.ToString());
};_ = run();return "Diff engine assertions passed; recording 180 frames";
