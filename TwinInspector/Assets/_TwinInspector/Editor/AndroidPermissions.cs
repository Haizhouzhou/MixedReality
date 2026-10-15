using System.IO;
using System;
using System.Collections.Generic;
using System.Xml;
using UnityEditor.Android;

namespace TwinInspector.Editor
{
    /// <summary>Adds runtime-requested permissions without replacing Unity's GameActivity manifest.</summary>
    public sealed class AndroidPermissions : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string file=Path.Combine(path,"src/main/AndroidManifest.xml");
            var document=new XmlDocument(); document.Load(file);
            const string ns="http://schemas.android.com/apk/res/android";
            foreach(var permission in new[]{"android.permission.CAMERA","android.permission.SCENE_UNDERSTANDING_FINE","android.permission.HAND_TRACKING"})
            {
                bool exists=false;
                foreach(XmlElement node in document.GetElementsByTagName("uses-permission")) if(node.GetAttribute("name",ns)==permission) exists=true;
                if(exists) continue;
                var element=document.CreateElement("uses-permission"); element.SetAttribute("name",ns,permission); document.DocumentElement.AppendChild(element);
            }
            document.Save(file);
            NormalizeXrManifest(Path.Combine(path,"xrmanifest.androidlib","AndroidManifest.xml"));
        }

        /// <summary>Merges declarations emitted by both Unity Android XR and Google Extensions.</summary>
        public static void NormalizeXrManifest(string file)
        {
            if(!File.Exists(file)) return;
            const string ns="http://schemas.android.com/apk/res/android";
            var document=new XmlDocument(); document.Load(file);
            var seen=new Dictionary<string,XmlElement>();
            var nodes=document.SelectNodes("/manifest/uses-feature | /manifest/uses-permission | /manifest/application/uses-native-library");
            foreach(XmlElement node in nodes)
            {
                string key=node.ParentNode.Name+"/"+node.Name+"/"+node.GetAttribute("name",ns);
                if(!seen.TryGetValue(key,out var first)) { seen.Add(key,node); continue; }
                foreach(XmlAttribute attribute in node.Attributes)
                {
                    if(attribute.NamespaceURI!=ns || attribute.LocalName=="name") continue;
                    string name=attribute.LocalName, value=attribute.Value;
                    if(name=="version")
                    {
                        string old=first.GetAttribute(name,ns);
                        int Version(string text) => string.IsNullOrEmpty(text) ? 0 : text.StartsWith("0x",StringComparison.OrdinalIgnoreCase) ? Convert.ToInt32(text.Substring(2),16) : int.Parse(text,System.Globalization.CultureInfo.InvariantCulture);
                        first.SetAttribute(name,ns,Math.Max(Version(old),Version(value)).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else if(name=="required")
                        first.SetAttribute(name,ns,first.GetAttribute(name,ns)!="false" || value!="false" ? "true" : "false");
                    else if(first.HasAttribute(name,ns) && first.GetAttribute(name,ns)!=value)
                        throw new InvalidDataException("Conflicting duplicate XR manifest attribute: "+key+"/"+name);
                    else first.SetAttribute(name,ns,value);
                }
                node.ParentNode.RemoveChild(node);
            }
            document.Save(file);
        }
    }
}
