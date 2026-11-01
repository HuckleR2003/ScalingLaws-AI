using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What a ticked category looks like on the map: a bright column standing over every place in it,
    /// with a ring on the ground at its foot.
    ///
    /// **Because dimming alone did not work.** Ticking a category used to pull the other pins towards
    /// near-black, and from the height the city is read at that is a change of shade among a hundred
    /// other shades. The player's question is "where are they", and the answer has to be visible
    /// without hunting: something tall, in the category's own colour, that was not there a moment ago.
    ///
    /// **Built once, then switched on and off.** One column and one ring per place, parented to the
    /// pin so they travel with it, and a shared unlit material per category so eight colours cover
    /// the whole map. Unlit for the same reason the selection brackets are: a lit marker takes the
    /// colour of the time of day and goes quiet exactly where the building is hardest to see.
    /// </summary>
    public sealed class MapSiteBeacons : MonoBehaviour
    {
        /// <summary>The shortest a column is ever drawn, in metres. What a bungalow gets.</summary>
        private const float ColumnHeight = 90f;

        /// <summary>
        /// How much of the building's own height the column adds on top of it.
        ///
        /// **The column used to be a flat ninety metres whatever it stood on.** On a shop that is
        /// twice the building and impossible to miss; on a tower it starts two hundred metres up,
        /// which at the height this map is read at is out of frame. Reported on 2026-10-27 against
        /// the rival headquarters, which are the tallest things in the city, as "make the circles
        /// on the rival hq more visible, especially this one".
        /// </summary>
        private const float ColumnShareOfBuilding = 0.55f;

        /// <summary>
        /// The smallest ground ring, in metres.
        ///
        /// The ring is sized from the building's own footprint, and a slim tower has almost none,
        /// so the one marker that stays at eye level was the one that shrank on exactly the
        /// buildings whose column had gone out of frame. Both halves failed on the same place.
        /// </summary>
        private const float MinimumRingReach = 26f;

        /// <summary>How wide the column is. Thin, but never thinner than a pixel from map height.</summary>
        private const float ColumnWidth = 5f;

        /// <summary>How thick the ground ring is.</summary>
        private const float RingThickness = 2.5f;

        /// <summary>Seconds for one full breath of the pulse.</summary>
        private const float PulseSeconds = 1.6f;

        private readonly List<Beacon> beacons = new();
        private readonly Dictionary<MapCategory, Material> materials = new();

        private MapFilterState state;

        /// <summary>Handed the filter and the pins to mark, by whatever built the map's interface.</summary>
        public void Use(MapFilterState filterState, IEnumerable<MapSitePin> pins)
        {
            state = filterState;

            foreach (var pin in pins)
            {
                if (pin == null || pin.Definition == null)
                {
                    continue;
                }

                beacons.Add(Build(pin));
            }

            state.Changed += Apply;
            Apply();
        }

        private void OnDestroy()
        {
            if (state != null)
            {
                state.Changed -= Apply;
            }
        }

        private void Update()
        {
            if (state == null || !state.Filtering)
            {
                return;
            }

            // One pulse for all of them, so the marked places read as one set rather than as a
            // field of independently blinking lights.
            var breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / PulseSeconds));

            foreach (var beacon in beacons)
            {
                if (!beacon.Root.activeSelf)
                {
                    continue;
                }

                // Grows from its foot rather than from its middle: a column whose base drifts up and
                // down looks like it is hovering, which is the one thing a marker must not look like.
                //
                // Against the beacon's own height, not the constant: since the column is a share of
                // what it stands on, breathing against the floor value made a tower's marker shrink
                // to a third of itself on every pulse.
                var height = Mathf.Lerp(beacon.Height * 0.72f, beacon.Height, breath);
                var scale = beacon.Column.localScale;

                beacon.Column.localScale = new Vector3(scale.x, height, scale.z);
                beacon.Column.localPosition = new Vector3(0f, beacon.Foot + height * 0.5f, 0f);
            }
        }

        private void Apply()
        {
            foreach (var beacon in beacons)
            {
                beacon.Root.SetActive(state.Filtering && state.IsPicked(beacon.Category));
            }
        }

        private Beacon Build(MapSitePin pin)
        {
            var site = pin.Definition;
            var bounds = WorldBounds(pin.gameObject);

            // **Not parented to the pin.** Several pins stand on models the asset swap scaled (a
            // garage at 3.83, a tower at its own size), and a marker inside a scaled transform comes
            // out stretched. Nothing on this map moves at runtime, so a world position is enough.
            var root = new GameObject($"Beacon_{site.Id}");
            root.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            root.SetActive(false);

            var material = MaterialFor(pin.Category);
            var foot = bounds.size.y;

            // A share of the building, floored, so the marker keeps the same relationship to what
            // it marks whether that is a lock-up garage or a forty storey tower.
            var columnHeight = Mathf.Max(ColumnHeight, foot * ColumnShareOfBuilding);

            var column = Piece(root.transform, material, "Column");
            column.localPosition = new Vector3(0f, foot + columnHeight * 0.5f, 0f);
            column.localScale = new Vector3(ColumnWidth, columnHeight, ColumnWidth);

            // The ring is four bars rather than a torus: a square outline reads as a marker, needs
            // no mesh of its own, and cannot be mistaken for part of the building.
            var reach = Mathf.Max(MinimumRingReach,
                Mathf.Max(bounds.extents.x, bounds.extents.z) + 6f);

            foreach (var (dx, dz) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
            {
                var bar = Piece(root.transform, material, "Ring");
                bar.localPosition = new Vector3(dx * reach, 0.6f, dz * reach);
                bar.localScale = dx == 0f
                    ? new Vector3(reach * 2f + RingThickness, RingThickness, RingThickness)
                    : new Vector3(RingThickness, RingThickness, reach * 2f + RingThickness);
            }

            return new Beacon(root, column, pin.Category, foot, columnHeight);
        }

        private static Transform Piece(Transform parent, Material material, string name)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent, false);

            // Nothing should be able to click a marker and select through it.
            var collider = piece.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            piece.GetComponent<MeshRenderer>().sharedMaterial = material;
            return piece.transform;
        }

        private Material MaterialFor(MapCategory category)
        {
            if (materials.TryGetValue(category, out var existing))
            {
                return existing;
            }

            // Brighter than the pin's own colour: this is a marker to be found from across the city,
            // not a label to be matched against the legend swatch.
            var colour = Color.Lerp(MapCategoryPalette.ColourFor(category), Color.white, 0.35f);
            var material = new Material(Shader.Find("Unlit/Color")) { color = colour };

            materials[category] = material;
            return material;
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

        private sealed class Beacon
        {
            public Beacon(GameObject root, Transform column, MapCategory category, float foot,
                float height)
            {
                Root = root;
                Column = column;
                Category = category;
                Foot = foot;
                Height = height;
            }

            public GameObject Root { get; }
            public Transform Column { get; }
            public MapCategory Category { get; }

            /// <summary>How high the building is: where the column starts, above its roof.</summary>
            public float Foot { get; }

            /// <summary>How tall this one column is, which is a share of the building under it.</summary>
            public float Height { get; }
        }
    }
}
