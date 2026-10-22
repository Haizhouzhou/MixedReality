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
        static string language;
        static readonly HashSet<string> missing = new HashSet<string>();
        public static IReadOnlyCollection<string> MissingKeys => missing;
        public static event Action Changed;
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
            if(!Supported(language)) language=Application.systemLanguage==SystemLanguage.German?"de":Application.systemLanguage==SystemLanguage.French?"fr":Application.systemLanguage==SystemLanguage.Italian?"it":"en";
            Load();
        }
        static Dictionary<string,string> Read(string code)
        {
            var asset=Resources.Load<TextAsset>("Strings/"+code);
            return asset?JsonConvert.DeserializeObject<Dictionary<string,string>>(asset.text):new Dictionary<string,string>();
        }
        static void Load() { english=Read("en"); current=language=="en"?english:Read(language); }
        public static string Get(string key)
        {
            Ensure(); if(string.IsNullOrEmpty(key)) return "";
            if(current.TryGetValue(key,out var value)) return value;
            if(english.TryGetValue(key,out value)) return value;
            if(missing.Add(key)) DevLog.Write("Missing UI string: "+key);
            return key;
        }
        public static string Format(string key,params object[] args) => string.Format(CultureInfo.InvariantCulture,Get(key),args);
    }
}
