using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace TwinInspector
{
    /// <summary>Chinese glyphs as runtime fallbacks of the Inter UI fonts, created from subset Noto Sans SC.</summary>
    /// <remarks>Built at runtime so no generated atlas is serialized into the Inter assets; removed again on quit/exit Play mode.</remarks>
    public static class CjkFonts
    {
        static readonly List<(TMP_FontAsset owner, TMP_FontAsset fallback)> installed = new List<(TMP_FontAsset, TMP_FontAsset)>();

        public static void Install(DesignTokens tokens)
        {
            if (installed.Count > 0) return;
            Add(tokens.Regular, "Fonts/NotoSansSC-Regular");
            Add(tokens.Medium, "Fonts/NotoSansSC-Medium");
            Add(tokens.Semibold, "Fonts/NotoSansSC-Bold");
            Application.quitting += Uninstall;
        }

        static void Add(TMP_FontAsset owner, string resource)
        {
            var font = Resources.Load<Font>(resource);
            if (!owner || !font) { DevLog.Write("CJK font unavailable: " + resource); return; }
            var fallback = TMP_FontAsset.CreateFontAsset(font, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (!fallback) { DevLog.Write("CJK font asset failed: " + resource); return; }
            fallback.name = font.name + " SDF (runtime)"; fallback.hideFlags = HideFlags.DontSave;
            owner.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            owner.fallbackFontAssetTable.Add(fallback);
            installed.Add((owner, fallback));
        }

        static void Uninstall()
        {
            Application.quitting -= Uninstall;
            foreach (var (owner, fallback) in installed)
            {
                if (owner) owner.fallbackFontAssetTable.Remove(fallback);
                if (fallback) Object.Destroy(fallback);
            }
            installed.Clear();
        }
    }
}
