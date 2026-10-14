if(UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Stop Play mode first");
var path=TwinInspector.TwinPaths.SaveFile("alignment","navvis_2022-02-06_12.55.11");
var state=TwinInspector.JsonStore.Read<TwinInspector.AlignmentState>(path);
if(state!=null)
{
    if(!string.IsNullOrEmpty(state.anchor_id) || TwinInspector.TwinRoot.Vector(state.position_world).sqrMagnitude>.00001f) throw new System.Exception("Refusing to remove a non-fixture alignment");
    System.IO.File.Delete(path);
    if(System.IO.File.Exists(path+".bak")) System.IO.File.Delete(path+".bak");
}
return "Only the zero-pose Editor QA alignment was removed";
