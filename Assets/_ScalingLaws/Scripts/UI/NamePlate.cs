using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The small name floating over somebody's head in the office.
    ///
    /// **In the scene rather than in the interface.** The office is drawn by its own camera into a
    /// render texture and shown as the background of one element, so a UI Toolkit label over it
    /// would have to be positioned by projecting a world point through that camera and then through
    /// whatever scale mode the background happens to use. Two coordinate systems, one of which is a
    /// stylesheet property somebody could change. A label parented to the person is in the same
    /// space as the person and cannot drift from them.
    ///
    /// **Minimal on purpose.** One line, one colour, no plate behind it, no outline, no icon. The
    /// room is grey boxes and low poly furniture; a label with a background and a border would be
    /// the loudest object in the frame. This is the same restraint the art direction rule already
    /// applies to everything sitting under the interface.
    ///
    /// The camera is fixed and orthographic, so there is no billboarding: the plate is turned to
    /// face it once, when it is built, and it stays right for the life of the scene.
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

        /// <summary>Character size for the mesh. Small: this is a label, not a banner.</summary>
        public const float Size = 0.085f;

        /// <summary>Resolution of the glyphs before they are scaled down. Crisp at any zoom.</summary>
        public const int FontResolution = 90;

        private static readonly Color Ink = new(0.92f, 0.95f, 1f, 0.94f);

        private Transform plate;

        /// <summary>
        /// Puts a name over somebody, or moves the one already there.
        ///
        /// Idempotent, so the shell can call it every time the office is re-dressed without
        /// stacking labels: the office is rebuilt on a move, and this project has already stacked
        /// models on top of each other once by assuming `Destroy` works outside play mode.
        /// </summary>
        public void Set(string name)
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

            var mesh = plate.GetComponent<TextMesh>();

            if (mesh != null)
            {
                mesh.text = name;
            }

            plate.localPosition = Vector3.up * (HeightOf(transform) + Clearance);
        }

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

            var mesh = host.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = FontResolution;
            mesh.characterSize = Size;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Ink;

            var renderer = host.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                // The font's own material, or the glyphs render as solid rectangles: a dynamic font
                // rasterises into an atlas that only its own material knows how to sample.
                renderer.sharedMaterial = font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            // Turned to the camera once. It is orthographic and never moves, so a billboard that
            // re-aimed every frame would be sixty matrix rebuilds a second to hold still.
            var camera = Camera.main;

            host.transform.rotation = camera != null
                ? camera.transform.rotation
                : Quaternion.Euler(30f, -45f, 0f);

            return host.transform;
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
                top = Mathf.Max(top, renderer.bounds.max.y);
            }

            return Mathf.Clamp(top - root.position.y, 0.5f, 4f);
        }
    }
}
