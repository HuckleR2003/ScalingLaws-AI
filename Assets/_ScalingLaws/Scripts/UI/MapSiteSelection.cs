using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Clicking a place on the map: picks it, outlines it in yellow, and opens its card.
    ///
    /// **Corner brackets rather than a wireframe box.** Twelve full edges around a building reads as
    /// a crate it has been packed into; short bars at the corners read as something selected, and
    /// they stay legible when the building behind them is a dark tower or a pale shed. Twenty-four
    /// bars, three per corner, built once and moved — a selection that allocated geometry per click
    /// would litter the scene with every building the player ever looked at.
    ///
    /// **Unlit yellow on purpose.** A lit material takes the colour of whatever time of day the
    /// scene is in, and a highlight that dims in shadow is a highlight that fails exactly where the
    /// building is hardest to see.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class MapSiteSelection : MonoBehaviour
    {
        /// <summary>How long each corner bar runs, as a share of the building's smallest side.</summary>
        private const float BracketShare = 0.22f;

        /// <summary>Thickness of a bar, in metres. Thick enough to read from the map's own height.</summary>
        private const float BracketThickness = 1.6f;

        /// <summary>Clear of the building's own surface, so the bracket never fights with its walls.</summary>
        private const float Clearance = 0.6f;

        private static readonly Color Highlight = new(1f, 0.83f, 0.12f);

        private Camera cam;
        private Transform brackets;
        private Transform[] bars;
        private MapSiteCard card;
        private MapSitePin selected;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        /// <summary>Handed the card to drive, by whatever built the map's interface.</summary>
        public void Use(MapSiteCard siteCard)
        {
            card = siteCard;
            card.Hide();
        }

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            // A click that started on the interface belongs to the interface. Without this, closing
            // the card with its own button immediately reselects whatever is behind it.
            if (card is { PointerIsOver: true })
            {
                return;
            }

            var ray = cam.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out var hit, 10000f))
            {
                Clear();
                return;
            }

            var pin = hit.collider.GetComponentInParent<MapSitePin>();

            if (pin == null || pin.Definition == null)
            {
                Clear();
                return;
            }

            Select(pin);
        }

        private void Select(MapSitePin pin)
        {
            selected = pin;
            card?.Show(pin.Definition);
            Outline(WorldBounds(pin.gameObject));
        }

        /// <summary>Drops the selection from outside — what the card's own close button calls.</summary>
        public void Deselect() => Clear();

        private void Clear()
        {
            selected = null;
            card?.Hide();

            if (brackets != null)
            {
                brackets.gameObject.SetActive(false);
            }
        }

        /// <summary>Moves the twenty-four bars onto the corners of a box, building them on first use.</summary>
        private void Outline(Bounds bounds)
        {
            if (bars == null)
            {
                Build();
            }

            brackets.gameObject.SetActive(true);

            var size = bounds.size + Vector3.one * (Clearance * 2f);
            var run = Mathf.Max(1.5f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * BracketShare);

            var index = 0;

            foreach (var sx in new[] { -1f, 1f })
            {
                foreach (var sy in new[] { -1f, 1f })
                {
                    foreach (var sz in new[] { -1f, 1f })
                    {
                        var corner = bounds.center + new Vector3(
                            sx * size.x * 0.5f, sy * size.y * 0.5f, sz * size.z * 0.5f);

                        // Three bars per corner, each running back along one axis towards the middle.
                        Place(bars[index++], corner,
                            new Vector3(run, BracketThickness, BracketThickness),
                            new Vector3(-sx * run * 0.5f, 0f, 0f));

                        Place(bars[index++], corner,
                            new Vector3(BracketThickness, run, BracketThickness),
                            new Vector3(0f, -sy * run * 0.5f, 0f));

                        Place(bars[index++], corner,
                            new Vector3(BracketThickness, BracketThickness, run),
                            new Vector3(0f, 0f, -sz * run * 0.5f));
                    }
                }
            }
        }

        private static void Place(Transform bar, Vector3 corner, Vector3 size, Vector3 offset)
        {
            bar.position = corner + offset;
            bar.localScale = size;
        }

        private void Build()
        {
            brackets = new GameObject("SiteHighlight").transform;

            var material = new Material(Shader.Find("Unlit/Color")) { color = Highlight };
            bars = new Transform[24];

            for (var index = 0; index < bars.Length; index++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"Bracket{index:00}";
                bar.transform.SetParent(brackets, false);

                // Nothing should be able to click the highlight itself and reselect through it.
                var collider = bar.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                bar.GetComponent<MeshRenderer>().sharedMaterial = material;
                bars[index] = bar.transform;
            }
        }

        private static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<MeshRenderer>();

            if (renderers.Length == 0)
            {
                return new Bounds(go.transform.position, Vector3.one * 4f);
            }

            var bounds = renderers[0].bounds;

            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }
    }
}
