using System;
using System.IO;
using System.Globalization;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace TwinInspector.Tests
{
    public class SchemaV2Tests
    {
        [Test] public void LegacyUpgradesOnSaveAndRegionsRoundTrip()
        {
            string path=Path.Combine(Application.temporaryCachePath,Guid.NewGuid()+".json");
            try
            {
                File.WriteAllText(path,"{\"schema\":\"twin-annotations/1\",\"scan\":\"test\",\"next_index\":7,\"items\":[]}");
                var store=new AnnotationStore("test",path);var item=store.Add(new Vector3(13,7,2),Vector3.forward,Vector3.one,new[]{0f,0f,0f,1f});
                var r=RegionBuilder.Build(new[]{new Vector3(13,7,2),new Vector3(14,7,2),new Vector3(13,8,2)},new[]{0,1,2}).ToRegion("diff");
                r.triangles=new AnnotationRegion.TriangleReference{tile="x1_y0",indices=new[]{3,8,9}}; store.SetRegion(item.id,r);
                Assert.That(JsonConvert.DeserializeObject<AnnotationDocument>(File.ReadAllText(path)).schema,Is.EqualTo("twin-annotations/2"));
                var restored=new AnnotationStore("test",path); Assert.That(restored.Items[0].id,Is.EqualTo("a-000007"));
                CollectionAssert.AreEqual(r.hull_twin,restored.Items[0].region.hull_twin);
                File.WriteAllText(path,"{\"schema\":\"twin-annotations/3\",\"scan\":\"test\"}"); Assert.Throws<InvalidDataException>(()=>new AnnotationStore("test",path));
            }finally{File.Delete(path);File.Delete(path+".bak");}
        }
        [Test] public void CsvHasTwentyTwoColumnsForPointAndRegionAndInvariantCoordinates()
        {
            var old=CultureInfo.CurrentCulture;try {CultureInfo.CurrentCulture=new CultureInfo("de-DE");
                var a=new Annotation{id="test",type="note",position_twin=new[]{1.25f,2f,3f},normal_twin=new[]{0f,0f,1f}};
                Assert.That(AnnotationExporter.CsvLine(a).Split(',').Length,Is.EqualTo(22));
                a.region=RegionBuilder.Build(new[]{new Vector3(1,2,3),new Vector3(2,2,3),new Vector3(1,3,3)},new[]{0,1,2}).ToRegion("diff");
                var line=AnnotationExporter.CsvLine(a);Assert.That(line.Split(',').Length,Is.EqualTo(22));StringAssert.Contains("1.25",line);StringAssert.Contains("1 2 3",line);StringAssert.Contains(";",line);
            }finally{CultureInfo.CurrentCulture=old;}
        }
    }
}
