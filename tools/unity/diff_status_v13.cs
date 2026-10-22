var a=TwinInspector.AppBootstrap.Instance;if(!a||!a.Ready)return "WAIT app";
var simulator=a.SceneSource as TwinInspector.EditorSceneSurfaceSimulator;
var d=a.Diff;var text=new System.Text.StringBuilder();text.AppendLine("source="+simulator?.Ready+" chunks="+simulator?.Chunks.Count+" error="+simulator?.Failure+" settled="+d?.Settled+" main_ms="+d?.AverageMainMs);
if(d!=null)foreach(var c in d.Candidates)text.AppendLine(c.id+" "+c.kind+" "+c.CentroidTwin(a.Root)+" area="+c.area_m2+" source="+c.chunk_or_tile+" paired="+c.paired_new);
System.IO.File.WriteAllText("docs/evidence/diff-progress-v13.txt",text.ToString());return text.ToString();
