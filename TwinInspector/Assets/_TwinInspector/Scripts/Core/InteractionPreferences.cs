using UnityEngine;

namespace TwinInspector
{
    // Small persistent preferences; annotation/alignment file schemas are unchanged.
    public static class InteractionPreferences
    {
        public static bool LeftHand { get => PlayerPrefs.GetInt("TwinInspector.leftHand",0)!=0; set { PlayerPrefs.SetInt("TwinInspector.leftHand",value?1:0); PlayerPrefs.Save(); } }
        public static bool Sound { get => PlayerPrefs.GetInt("TwinInspector.sound",1)!=0; set { PlayerPrefs.SetInt("TwinInspector.sound",value?1:0); PlayerPrefs.Save(); } }
        public static float Volume { get => PlayerPrefs.GetFloat("TwinInspector.volume",.18f); set => PlayerPrefs.SetFloat("TwinInspector.volume",Mathf.Clamp01(value)); }
    }
}
