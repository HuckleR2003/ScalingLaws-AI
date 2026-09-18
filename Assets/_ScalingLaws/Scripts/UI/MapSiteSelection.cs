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
        private const float BracketShare = 0.3f;

        /// <summary>
        /// Thickness of a bar, in metres.
        ///
        /// Raised from 1.6 m after the author reported that a selected building was hard to tell from
        /// its neighbours: the map is read from three hundred metres up, where 1.6 m is a hairline.
        /// </summary>
        private const float BracketThickness = 2.8f;

        /// <summary>Clear of the building's own surface, so the bracket never fights with its walls.</summary>
        private const float Clearance = 0.6f;

        private static readonly Color Highlight = new(1f, 0.83f, 0.12f);

        private Camera cam;
        private Transform brackets;
        private Transform[] bars;
        private MapSiteCard card;
        private MapSitePin selected;
        private Vector3 anchor;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            // The card stands beside the building rather than in a corner, so it has to be moved
            // whenever the map moves under it — which, with a camera the player pans and zooms, is
            // most frames. Late, so it reads the camera after this frame's panning.
            if (selected == null || card == null || cam == null)
            {
                return;
            }

            var screen = cam.WorldToScreenPoint(anchor);

            if (screen.z <= 0f)
            {
                return;
            }

            var room = card.parent?.contentRect ?? Rect.zero;

            if (room.width <= 0f || room.height <= 0f || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            // Panel coordinates by hand rather than through `RuntimePanelUtils`: this panel fills the
            // screen, so the conversion is two ratios, and doing it here means the card cannot end up
            // depending on which scale mode the panel settings are left in.
            card.PlaceNear(new Vector2(
                screen.x / Screen.width * room.width,
                (1f - screen.y / Screen.height) * room.height));
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
                // Water and some pools carry no collider, so the ray can miss everything. The point
                // on the ground is still known from the terrain's own height, and the plaza pool is
                // exactly where a player clicks to pick the plaza.
                var fallback = GroundUnder(ray);
                var near = fallback.HasValue ? NearestPin(fallback.Value) : null;

                if (near == null)
                {
                    Clear();
                    return;
                }

                Pick(near);
                return;
            }

            var pin = hit.collider.GetComponentInParent<MapSitePin>();

            // **A click that lands on ground near a place picks the place.** The pin is a post two
            // metres wide, and the places the author could not click (Terrace Park, Valley Plaza,
            // the wind farm) are pools, lawns and turbines that belong to the landscape rather than
            // to the pin, so the ray hit the terrain every time. The site's own radius, or a
            // minimum a player can hit from map height, decides what counts as near.
            if (pin == null || pin.Definition == null)
            {
                pin = NearestPin(new Vector2(hit.point.x, hit.point.z));
            }

            if (pin == null || pin.Definition == null)
            {
                Clear();
                return;
            }

            Pick(pin);
        }

        /// <summary>
        /// The smallest reach a place has for a click, in metres. A site drawn with no radius still
        /// covers a plaza's worth of ground, which is what a player aims at from three hundred
        /// metres up.
        /// </summary>
        public const float MinimumReach = 45f;

        private MapSitePin[] pins;

        /// <summary>The place whose reach this ground point is inside, nearest first. Null for none.</summary>
        private MapSitePin NearestPin(Vector2 ground)
        {
            pins ??= FindObjectsByType<MapSitePin>(FindObjectsSortMode.None);

            MapSitePin best = null;
            var bestDistance = float.MaxValue;

            foreach (var candidate in pins)
            {
                var site = candidate == null ? null : candidate.Definition;

                if (site == null || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var distance = Vector2.Distance(ground, new Vector2(site.Position.X, site.Position.Z));

                if (distance <= ReachOf(site.Radius) && distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// Where a ray meets the ground when it hit no collider: a horizontal plane at the terrain's
        /// height, refined twice because that height depends on where the ray lands.
        /// </summary>
        private static Vector2? GroundUnder(Ray ray)
        {
            if (ray.direction.y >= -0.001f)
            {
                return null;
            }

            var height = 0f;
            var point = Vector3.zero;

            for (var pass = 0; pass < 3; pass++)
            {
                var distance = (height - ray.origin.y) / ray.direction.y;
                point = ray.origin + ray.direction * distance;
                height = Data.CityLayout.GroundHeightAt(new Data.MapPoint(point.x, point.z));
            }

            return new Vector2(point.x, point.z);
        }

        /// <summary>How far from its centre a click still picks a place. Tested without a scene.</summary>
        public static float ReachOf(float siteRadius) => Mathf.Max(MinimumReach, siteRadius);

        /// <summary>
        /// Picks a place from outside, exactly as clicking it does. What the legend's SHOW button
        /// calls after flying the camera there, so the two ways of arriving at a building leave the
        /// screen in the same state rather than one of them opening a card and the other not.
        /// </summary>
        public void Select(MapSitePin pin)
        {
            if (pin == null || pin.Definition == null)
            {
                return;
            }

            Pick(pin);
        }

        private void Pick(MapSitePin pin)
        {
            selected = pin;

            var bounds = WorldBounds(pin.gameObject);

            // The card is hung off the building's shoulder rather than its middle, so it does not sit
            // on top of the thing it describes.
            anchor = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

            card?.Show(pin.Definition);
            Outline(bounds);
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
