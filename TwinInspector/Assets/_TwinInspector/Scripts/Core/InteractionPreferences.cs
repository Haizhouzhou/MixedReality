using UnityEngine;

namespace TwinInspector
{
    // Small persistent preferences; annotation/alignment file schemas are unchanged.
    public static class InteractionPreferences
    {
        public static string Language { get => PlayerPrefs.GetString("TwinInspector.language", ""); set { PlayerPrefs.SetString("TwinInspector.language",value); PlayerPrefs.Save(); } }
        public static bool GuideSeen { get => PlayerPrefs.GetInt("TwinInspector.guideSeen",0)!=0; set { PlayerPrefs.SetInt("TwinInspector.guideSeen",value?1:0); PlayerPrefs.Save(); } }
        public static bool LeftHand { get => PlayerPrefs.GetInt("TwinInspector.leftHand",0)!=0; set { PlayerPrefs.SetInt("TwinInspector.leftHand",value?1:0); PlayerPrefs.Save(); } }
        public static bool Sound { get => PlayerPrefs.GetInt("TwinInspector.sound",1)!=0; set { PlayerPrefs.SetInt("TwinInspector.sound",value?1:0); PlayerPrefs.Save(); } }
        public static float Volume { get => PlayerPrefs.GetFloat("TwinInspector.volume",.18f); set => PlayerPrefs.SetFloat("TwinInspector.volume",Mathf.Clamp01(value)); }
        // Dock placement relative to the body direction; saved on leaving the Menu position page or after a drag.
        public const float DefaultDockDistance=.6f, DefaultDockYaw=30, DefaultDockHeight=-.2f;
        public static float DockDistance { get => PlayerPrefs.GetFloat("TwinInspector.dockDistance",DefaultDockDistance); set => PlayerPrefs.SetFloat("TwinInspector.dockDistance",Mathf.Clamp(value,.4f,.9f)); }
        public static float DockYaw { get => PlayerPrefs.GetFloat("TwinInspector.dockYaw",DefaultDockYaw); set => PlayerPrefs.SetFloat("TwinInspector.dockYaw",Mathf.Clamp(value,-45,45)); }
        public static float DockHeight { get => PlayerPrefs.GetFloat("TwinInspector.dockHeight",DefaultDockHeight); set => PlayerPrefs.SetFloat("TwinInspector.dockHeight",Mathf.Clamp(value,-.3f,.2f)); }
        public static void ResetDock() { DockDistance=DefaultDockDistance; DockYaw=DefaultDockYaw; DockHeight=DefaultDockHeight; PlayerPrefs.Save(); }
        public static bool PalmSummon { get => PlayerPrefs.GetInt("TwinInspector.palmSummon",1)!=0; set { PlayerPrefs.SetInt("TwinInspector.palmSummon",value?1:0); PlayerPrefs.Save(); } }
        public static bool DockPinnedSetting { get => PlayerPrefs.GetInt("TwinInspector.dockPinned",1)!=0; set { PlayerPrefs.SetInt("TwinInspector.dockPinned",value?1:0); PlayerPrefs.Save(); } }
        /// <summary>Without the palm gesture a hidden dock could not be reopened, so it stays pinned.</summary>
        public static bool DockPinned => DockPinnedSetting || !PalmSummon;
    }
}
