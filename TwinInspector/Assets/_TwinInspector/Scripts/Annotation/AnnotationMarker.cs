using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TwinInspector
{
    /// <summary>Surface disc, one-millimetre ring/leader and a numbered paper label.</summary>
    public sealed class AnnotationMarker : MonoBehaviour
    {
        [SerializeField] MeshRenderer disc;
        [SerializeField] TMP_Text label;
        [SerializeField] Image paper;
        MaterialPropertyBlock properties;
        public string Id { get; private set; }

        public void Construct(DesignTokens tokens)
        {
            if (disc) return;
            gameObject.layer = 10;
            var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plate.name = "40 mm surface disc"; plate.transform.SetParent(transform, false); plate.transform.localScale = Vector3.one * .04f;
            var oldCollider = plate.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(oldCollider); else DestroyImmediate(oldCollider);
            disc = plate.GetComponent<MeshRenderer>(); disc.sharedMaterial = tokens.AnnotationMaterial;
            var target = gameObject.AddComponent<BoxCollider>(); target.size = new Vector3(.048f, .048f, .01f); target.isTrigger = true;
            var leader = new GameObject("80 mm leader").AddComponent<LineRenderer>(); leader.transform.SetParent(transform, false);
            leader.useWorldSpace = false; leader.positionCount = 2; leader.SetPositions(new[] { new Vector3(0, .02f, 0), new Vector3(0, .1f, 0) });
            leader.startWidth = leader.endWidth = .001f; leader.sharedMaterial = tokens.InkMaterial;
            var canvasObject = new GameObject("Surface label", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false); canvasObject.transform.localPosition = new Vector3(.06f, .1f, 0); canvasObject.transform.localScale = Vector3.one * .001f;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rectangle = (RectTransform)canvasObject.transform; rectangle.sizeDelta = new Vector2(144, 32);
            paper = canvasObject.AddComponent<Image>(); paper.color = tokens.Paper; paper.raycastTarget = false;
            var text = new GameObject("Index and type", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(rectangle, false);
            var rect = (RectTransform)text.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(8, 0); rect.offsetMax = new Vector2(-8, 0);
            label = text.GetComponent<TMP_Text>(); label.font = tokens.Medium; label.fontSize = DesignTokens.Label; label.color = tokens.Ink; label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        }

        public void UpdateData(Annotation item, bool selected, TwinRoot root, DesignTokens tokens)
        {
            properties ??= new MaterialPropertyBlock();
            Construct(tokens); Id = item.id;
            transform.localPosition = root.ToLocal(TwinRoot.Vector(item.position_twin)) + TwinRoot.SwapYZ(TwinRoot.Vector(item.normal_twin)) * .003f;
            var normal = TwinRoot.SwapYZ(TwinRoot.Vector(item.normal_twin)).normalized;
            // A world-space Canvas is read from its negative-Z side.
            transform.localRotation = Quaternion.LookRotation(-normal, Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > .95f ? Vector3.forward : Vector3.up);
            properties.SetColor("_Color", tokens.TypeColor(item.Kind).linear); properties.SetFloat("_Selected", selected ? 1 : 0); disc.SetPropertyBlock(properties);
            label.text = item.index.ToString("D2") + "  " + item.type.ToUpperInvariant();
            label.color = selected ? tokens.Paper : tokens.Ink; paper.color = selected ? tokens.Ink : tokens.Paper;
        }
    }
}
