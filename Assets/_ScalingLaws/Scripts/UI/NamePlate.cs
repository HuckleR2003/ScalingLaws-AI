using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The small name floating over somebody's head in the office.
    ///
    /// **It faced the wrong camera and that is the whole reason it read side-on.** `Camera.main`
    /// returns the camera tagged `MainCamera`, and the office is drawn by a second camera parented
    /// inside the room prefab which carries no tag at all. So the plate was squarely facing a camera
    /// nobody was looking through, and from the one that renders the room it was edge on and often
    /// invisible. Reported by the author as "it is from the side and often you cannot see it", which
    /// is exactly what that is.
    ///
    /// It now faces the camera that actually renders the room, found by walking up to it rather than
    /// by a tag: the office camera is a child of the room, and a tag is a thing somebody can forget
    /// to set on the next prefab.
    ///
    /// **In the scene rather than in the interface.** The office is drawn into a render texture shown
    /// as one element's background, so a UI Toolkit label over it would have to be positioned by
    /// projecting a world point through that camera and then through whatever scale mode the
    /// background happens to use. Two coordinate systems, one of which is a stylesheet property
    /// somebody could change. A label parented to the person cannot drift from them.
    ///
    /// **Three parts, and the middle one is the point.** A name, a hairline in the colour of what
    /// they do, and that job in small type under it. The rule is what makes a room of eight people
    /// readable at a glance: colour says the shape of the team before a single word is read.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NamePlate : MonoBehaviour
    {
        /// <summary>
        /// How far above the model's own top the name floats, in metres.
        ///
        /// Measured from the renderer's bounds rather than from a fixed height, because the two
        /// character packs in this project are built at different scales: one has its head bone at
        /// 2.24m and the other at 1.53m. A constant offset put the name through one model's face
        /// and a metre above the other's.
        /// </summary>
        public const float Clearance = 0.32f;

        /// <summary>
        /// Character size for the name.
        ///
        /// **Half what it was**, at the author's request. The first pass was sized to be readable on
        /// its own and a room with eight of them was mostly text.
        /// </summary>
        public const float Size = 0.0425f;

        /// <summary>The job under the rule, smaller again.</summary>
        public const float TitleSize = 0.030f;

        /// <summary>Resolution of the glyphs before they are scaled down. Crisp at any zoom.</summary>
        public const int FontResolution = 90;

        /// <summary>
        /// How tall the colour rule is, in metres.
        ///
        /// About four pixels at the size the room is drawn, which is the band the author asked for.
        /// Thin enough to read as an underline rather than as a badge.
        /// </summary>
        public const float RuleHeight = 0.018f;

        /// <summary>How wide the rule is per character of the name. Roughly the glyph advance.</summary>
        public const float RuleWidthPerCharacter = 0.55f;

        private static readonly Color Ink = new(0.92f, 0.95f, 1f, 0.94f);
        private static readonly Color TitleInk = new(0.78f, 0.83f, 0.92f, 0.82f);

        private Transform plate;
        private TextMesh nameMesh;
        private TextMesh titleMesh;
        private Transform rule;

        /// <summary>
        /// Puts a name, a job and its colour over somebody.
        ///
        /// Idempotent, so the shell can call it every time the office is re-dressed without stacking
        /// labels: the office is rebuilt on a move, and this project has already stacked models on
        /// top of each other once by assuming `Destroy` works outside play mode.
        /// </summary>
        public void Set(string name, string title = null, Color? accent = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                Clear();
                return;
            }

            plate ??= Build();

            if (plate == null)
            {
                return;
            }

            if (nameMesh != null)
            {
                nameMesh.text = name;
            }

            if (titleMesh != null)
            {
                titleMesh.text = title ?? string.Empty;
            }

            if (rule != null)
            {
                // Sized to the name it underlines rather than to a constant, so a two word name gets
                // a rule the length of a two word name.
                rule.localScale = new Vector3(
                    Mathf.Max(0.35f, name.Length * RuleWidthPerCharacter * Size),
                    RuleHeight,
                    1f);

                var renderer = rule.GetComponent<MeshRenderer>();

                if (renderer != null)
                {
                    renderer.sharedMaterial = RulePaint(accent ?? Ink);
                }
            }

            plate.localPosition = Vector3.up * (HeightOf(transform) + Clearance);
        }

        /// <summary>What a role is called and what colour it reads as. One place, so a room agrees.</summary>
        public static string TitleFor(StaffRole role) => StaffCatalog.Get(role).DisplayName;

        /// <summary>
        /// The colour of a job.
        ///
        /// Written out per role rather than derived from the enum, for the reason every phrase key
        /// in this project is: a value built from a name is invisible to every check, and a role
        /// added tomorrow should be a compile error here rather than a silent grey.
        /// </summary>
        public static Color ColourFor(StaffRole role) => role switch
        {
            StaffRole.ResearchScientist => new Color(0.44f, 0.72f, 1.00f),
            StaffRole.InfrastructureEngineer => new Color(0.47f, 0.82f, 0.62f),
            StaffRole.DataEngineer => new Color(0.86f, 0.70f, 0.34f),
            StaffRole.SafetyEngineer => new Color(0.92f, 0.48f, 0.48f),
            StaffRole.GoToMarket => new Color(0.78f, 0.55f, 0.94f),
            _ => new Color(0.62f, 0.66f, 0.74f)
        };

        /// <summary>The founder's own colour. Deliberately none of the five: they are not staff.</summary>
        public static Color FounderColour => new(0.98f, 0.84f, 0.42f);

        public void Clear()
        {
            if (plate == null)
            {
                return;
            }

            // Destroy is a no-op outside play mode and DestroyImmediate throws inside it. Both
            // exist here because the office scene is dressed from editor tooling as well as from
            // the game.
            if (Application.isPlaying)
            {
                Destroy(plate.gameObject);
            }
            else
            {
                DestroyImmediate(plate.gameObject);
            }

            plate = null;
            nameMesh = null;
            titleMesh = null;
            rule = null;
        }

        private Transform Build()
        {
            var font = UiBootstrap.ResolveTitleFont();

            if (font == null)
            {
                // No font is a reason to draw nothing, never a reason to throw. Every loader in
                // this project degrades rather than failing, and a scene component that needs an
                // asset to not throw is a component that blocks the asset.
                return null;
            }

            var host = new GameObject("NamePlate");
            host.transform.SetParent(transform, false);

            nameMesh = Text(host.transform, font, Size, Ink, 0f);

            rule = Quad(host.transform, -Size * 1.15f);
            titleMesh = Text(host.transform, font, TitleSize, TitleInk, -Size * 2.2f);

            // **The camera that renders the room, not `Camera.main`.** See the note on the class:
            // the office camera is parented inside the room and carries no tag, so `Camera.main`
            // returned the game's camera and every plate faced a direction nobody was looking from.
            var camera = RoomCamera();

            host.transform.rotation = camera != null
                ? camera.transform.rotation
                : Quaternion.Euler(30f, -45f, 0f);

            return host.transform;
        }

        /// <summary>
        /// The camera drawing the room this person is standing in.
        ///
        /// Walks up to the room and looks inside it, because that is where the office builder puts
        /// the camera. Falls back to `Camera.main` so a person spawned into some other scene still
        /// faces something rather than facing world forward.
        /// </summary>
        private Camera RoomCamera()
        {
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                var found = parent.GetComponentInChildren<Camera>(true);

                if (found != null)
                {
                    return found;
                }
            }

            return Camera.main;
        }

        private static TextMesh Text(Transform parent, Font font, float size, Color ink, float y)
        {
            var host = new GameObject("Line");
            host.transform.SetParent(parent, false);
            host.transform.localPosition = new Vector3(0f, y, 0f);

            var mesh = host.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = FontResolution;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = ink;

            var renderer = host.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                // The font's own material, or the glyphs render as solid rectangles: a dynamic font
                // rasterises into an atlas that only its own material knows how to sample.
                renderer.sharedMaterial = font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return mesh;
        }

        private static Transform Quad(Transform parent, float y)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Rule";
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, y, 0f);

            // A collider on a name plate would eat the click meant for the person under it, which is
            // the one interaction the room has.
            var collider = quad.GetComponent<Collider>();

            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            var renderer = quad.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return quad.transform;
        }

        private static readonly System.Collections.Generic.Dictionary<Color, Material> Paints = new();

        /// <summary>
        /// One material per colour, shared.
        ///
        /// A material per plate is a draw call per plate and a leak per re-dress, and a full floor
        /// re-dresses on every office move.
        /// </summary>
        private static Material RulePaint(Color colour)
        {
            if (Paints.TryGetValue(colour, out var found) && found != null)
            {
                return found;
            }

            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            var material = new Material(shader);

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", colour);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", colour);
            }

            Paints[colour] = material;
            return material;
        }

        /// <summary>
        /// How tall this model actually is, from what it draws.
        ///
        /// The two character packs here are built at different scales, and a fixed offset put the
        /// name through one model's face and a metre over the other's.
        /// </summary>
        private static float HeightOf(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return 1.8f;
            }

            var top = float.MinValue;

            foreach (var renderer in renderers)
            {
                // The plate's own pieces are children by the time this runs again, and measuring
                // them would walk the label further up the screen on every call.
                if (renderer.transform.IsChildOf(root) && renderer.GetComponentInParent<NamePlate>() != null
                    && renderer.transform.name is "Line" or "Rule")
                {
                    continue;
                }

                top = Mathf.Max(top, renderer.bounds.max.y);
            }

            return top <= float.MinValue ? 1.8f : Mathf.Clamp(top - root.position.y, 0.5f, 4f);
        }
    }
}
