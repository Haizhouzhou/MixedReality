using TMPro;
using UnityEngine;

namespace TwinInspector
{
    /// <summary>Single source of physical dimensions, typography, colours and prefab styles.</summary>
    [CreateAssetMenu(menuName = "TwinInspector/Design Tokens")]
    public sealed class DesignTokens : ScriptableObject
    {
        [SerializeField] Color ink = Hex("111111"), paper = Hex("F2F1EC"), paper2 = Hex("E6E4DD"), muted = Hex("6F6E69"), accent = Hex("E30613");
        [SerializeField] Color missing = Hex("E30613"), added = Hex("0046E6"), changed = Hex("F2B600"), note = Hex("6F6E69"), twinTint = Hex("1F4FD8");
        [SerializeField] TMP_FontAsset regular, medium, semibold;
        [SerializeField] GameObject panelPrefab, annotationPrefab;
        [SerializeField] Material twinMaterial, annotationMaterial, inkMaterial, gridMaterial;
        [SerializeField] Material edgeMaterial;
        [SerializeField] Material diffPatchMaterial;
        public Material DiffPatchMaterial => diffPatchMaterial;
        public Material EdgeMaterial => edgeMaterial;
        public Color Ink => ink; public Color Paper => paper; public Color Paper2 => paper2; public Color Muted => muted; public Color Accent => accent; public Color TwinTint => twinTint;
        public TMP_FontAsset Regular => regular; public TMP_FontAsset Medium => medium; public TMP_FontAsset Semibold => semibold;
        public GameObject PanelPrefab => panelPrefab; public GameObject AnnotationPrefab => annotationPrefab;
        public Material TwinMaterial => twinMaterial; public Material AnnotationMaterial => annotationMaterial; public Material InkMaterial => inkMaterial; public Material GridMaterial => gridMaterial;
        public const float PanelWidth = 480, PanelHeight = 320, Margin = 24, Gutter = 16, Body = 21, Label = 12, Caption = 10, Title = 24, Display = 32, ControlHeight = 48;
        public Color TypeColor(AnnotationType type) => AnnotationTypes.Colour(type);
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
    }
}
