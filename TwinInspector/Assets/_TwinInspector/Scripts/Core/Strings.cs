using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Resource-backed UI text; persisted data and diagnostic logs keep their English identity.</summary>
    public static class L
    {
        static Dictionary<string,string> english, current;
        static readonly Dictionary<string,string> englishKeys=new Dictionary<string,string>(StringComparer.Ordinal);
        static string language;
        static readonly HashSet<string> missing = new HashSet<string>();
        public static IReadOnlyCollection<string> MissingKeys => missing;
        public static event Action Changed;
        public static void ClearMissing()=>missing.Clear();
        public static string LanguageFor(SystemLanguage system)=>system==SystemLanguage.German?"de":system==SystemLanguage.French?"fr":system==SystemLanguage.Italian?"it":"en";
        public static string Language
        {
            get { Ensure(); return language; }
            set { language=Supported(value)?value:"en"; InteractionPreferences.Language=language; Load(); Changed?.Invoke(); }
        }
        static bool Supported(string code) => code=="en"||code=="de"||code=="fr"||code=="it";
        static void Ensure()
        {
            if(english!=null) return;
            language=InteractionPreferences.Language;
            if(!Supported(language)) language=LanguageFor(Application.systemLanguage);
            Load();
            if(Application.isEditor)foreach(var code in new[]{"en","de","fr","it"}){var table=Read(code);int count=0;foreach(var key in english.Keys)if(!table.ContainsKey(key))count++;DevLog.Write("Strings language="+code+" missing_keys="+count);}
        }
        static Dictionary<string,string> Read(string code)
        {
            var asset=Resources.Load<TextAsset>("Strings/"+code);
            return asset?JsonConvert.DeserializeObject<Dictionary<string,string>>(asset.text):new Dictionary<string,string>();
        }
        static void Load()
        {
            english=Read("en");current=language=="en"?english:Read(language);englishKeys.Clear();
            // Status and data type words retain their English identity outside the UI.
            // Resolve those exact values through the same semantic resource keys at presentation time.
            foreach(var pair in english)if(!englishKeys.ContainsKey(pair.Value))englishKeys.Add(pair.Value,pair.Key);
        }
        public static string Get(string key)
        {
            Ensure(); if(string.IsNullOrEmpty(key)) return "";
            if(!english.ContainsKey(key)&&englishKeys.TryGetValue(key,out var resolved))key=resolved;
            if(current.TryGetValue(key,out var value)) return value;
            if(missing.Add(key)) DevLog.Write("Missing UI string: "+language+":"+key);
            if(english.TryGetValue(key,out value)) return value;
            return key;
        }
        public static string Format(string key,params object[] args) => string.Format(CultureInfo.InvariantCulture,Get(key),args);
    }
}
