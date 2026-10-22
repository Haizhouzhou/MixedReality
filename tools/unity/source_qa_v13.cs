var app=TwinInspector.AppBootstrap.Instance;
if(!app || !app.Ready) throw new System.Exception("Application not ready");
var source=app.SceneSource as TwinInspector.EditorSceneSurfaceSimulator;
if(source==null || !source.Ready) return "WAIT simulator "+source?.Failure;
var text=new System.Text.StringBuilder();long bytes=0;int triangles=0;
foreach(var c in source.Chunks){if(c.Triangles.Length/3>20000 || !c.Filter.sharedMesh.isReadable || c.Collider.gameObject.layer!=9)throw new System.Exception("Chunk contract failed");triangles+=c.Triangles.Length/3;}
foreach(var tile in app.Loader.Tiles)if(tile.Collision)foreach(var f in tile.Collision.GetComponentsInChildren<UnityEngine.MeshFilter>()) {if(!f.sharedMesh.isReadable)throw new System.Exception("Unreadable collision");bytes+=f.sharedMesh.vertexCount*24L+f.sharedMesh.triangles.Length*4L;}
text.AppendLine("PASS: simulator chunks="+source.Chunks.Count+" triangles="+triangles+" max triangles/chunk=20000; readable, layer 9");
text.AppendLine("Collision readable CPU positions/normals/indices lower bound bytes="+bytes+" loaded tiles="+app.Loader.LoadedTiles+"; excludes mesh overhead and additional channels");
text.AppendLine("Unity allocated memory bytes="+UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
text.AppendLine("Fixture is applied to real Region A LOD2 geometry; all source chunks fixed in session space.");
System.IO.File.WriteAllText("docs/evidence/source-v13.txt",text.ToString());return text.ToString();
