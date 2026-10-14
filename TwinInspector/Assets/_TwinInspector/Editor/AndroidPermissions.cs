using System.IO;
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
        }
    }
}
