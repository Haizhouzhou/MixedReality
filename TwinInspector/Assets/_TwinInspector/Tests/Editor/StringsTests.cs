using System.Globalization;
using Newtonsoft.Json;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TwinInspector.Tests
{
    public class StringsTests
    {
        [Test] public void EnglishTableAndInvariantFormatting()
        {
            var table=JsonConvert.DeserializeObject<Dictionary<string,string>>(Resources.Load<TextAsset>("Strings/en").text);
            Assert.That(table.Count,Is.GreaterThan(100));
            var old=CultureInfo.CurrentCulture;var language=L.Language;
            try{CultureInfo.CurrentCulture=new CultureInfo("de-DE");L.Language="en";Assert.That(L.Format("Datum {0:F2} m",1.25),Is.EqualTo("Datum 1.25 m"));}
            finally{CultureInfo.CurrentCulture=old;L.Language=language;}
        }
    }
}
