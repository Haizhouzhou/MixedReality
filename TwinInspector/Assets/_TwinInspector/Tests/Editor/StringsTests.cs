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
            try{CultureInfo.CurrentCulture=new CultureInfo("de-DE");L.Language="en";Assert.That(L.Format("text.datum0F2M",1.25),Is.EqualTo("Datum 1.25 m"));}
            finally{CultureInfo.CurrentCulture=old;L.Language=language;L.ClearMissing();}
        }
        [Test] public void LanguagesShareKeysAndFitTheirSlots()
        {
            var english=JsonConvert.DeserializeObject<Dictionary<string,string>>(Resources.Load<TextAsset>("Strings/en").text);
            var slots=JsonConvert.DeserializeObject<Dictionary<string,int>>(Resources.Load<TextAsset>("Strings/slots").text);
            CollectionAssert.AreEquivalent(new[]{"en","zh","de","fr","it"},L.Codes);
            foreach(var code in L.Codes)
            {
                var table=JsonConvert.DeserializeObject<Dictionary<string,string>>(Resources.Load<TextAsset>("Strings/"+code).text);CollectionAssert.AreEquivalent(english.Keys,table.Keys,code);
                foreach(var slot in slots){string text=System.Text.RegularExpressions.Regex.Replace(table[slot.Key],@"\{\d+(?::[^}]+)?\}","00");Assert.That(text.Length,Is.LessThanOrEqualTo(slot.Value),code+":"+slot.Key+" = "+text);}
            }
        }
        [Test] public void FirstRunIsEnglishAndChoiceIsRemembered()
        {
            string saved=InteractionPreferences.Language,previous=L.Language;
            var field=typeof(L).GetField("english",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            try
            {
                InteractionPreferences.Language="";field.SetValue(null,null);
                Assert.That(L.Language,Is.EqualTo("en"));
                L.Language="zh";Assert.That(InteractionPreferences.Language,Is.EqualTo("zh"));Assert.That(L.Get("button.back"),Is.EqualTo("返回"));
                L.Language="xx";Assert.That(L.Language,Is.EqualTo("en"));
            }
            finally{L.Language=previous;InteractionPreferences.Language=saved;L.ClearMissing();}
        }
        [Test] public void FallbackRecordsMissingKey()
        {
            string previous=L.Language;
            try
            {
                L.Language="fr";L.ClearMissing();var field=typeof(L).GetField("current",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var current=(Dictionary<string,string>)field.GetValue(null);current.Remove("button.back");
                Assert.That(L.Get("button.back"),Is.EqualTo("Back"));CollectionAssert.Contains(L.MissingKeys,"button.back");
            }
            finally{L.Language=previous;L.ClearMissing();}
        }
    }
}
