using UnityEngine;
namespace TwinInspector
{
    /// <summary>Persisted enum/string identity is shared by world glyphs and both UI representations.</summary>
    public static class AnnotationTypes
    {
        public static string Prefix(AnnotationType type) => type==AnnotationType.Missing?"[−] MISSING":type==AnnotationType.New?"[+] NEW":type==AnnotationType.Changed?"[Δ] CHANGED":"[•] NOTE";
        public static Color Colour(AnnotationType type)
        {
            string hex=type==AnnotationType.Missing?"#E30613":type==AnnotationType.New?"#0046E6":type==AnnotationType.Changed?"#F2B600":"#6F6E69";
            ColorUtility.TryParseHtmlString(hex,out var colour); return colour;
        }
    }
}
