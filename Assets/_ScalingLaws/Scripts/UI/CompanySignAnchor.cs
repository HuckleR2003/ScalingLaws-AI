using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Where a room shows the company's name: the sign over the entrance.
    ///
    /// **Written when the room is shown, never baked.** A room is a prefab built once in the editor,
    /// and a company is named at the creator, so the prefab carries this anchor and its size and the
    /// game writes the name onto it. Asked for by the author on 2026-09-19: the entrance with the
    /// player's own company over the doors.
    ///
    /// Drawn with a <see cref="TextMesh"/> in the game's title face, the same way the name plates are,
    /// and fitted to the panel: a short name is drawn large and a long one is shrunk until it fits,
    /// rather than running off the end of the sign.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanySignAnchor : MonoBehaviour
    {
        /// <summary>How wide and tall the lettering may be, in metres.</summary>
        public float Width = 3f;

        /// <inheritdoc cref="Width"/>
        public float Height = 0.45f;

        private const string TextName = "CompanyName";

        /// <summary>Writes the name on every sign in a room. Nothing happens for a room with no sign.</summary>
        public static void ApplyAll(GameObject room, string companyName)
        {
            if (room == null)
            {
                return;
            }

            foreach (var anchor in room.GetComponentsInChildren<CompanySignAnchor>(true))
            {
                anchor.Write(companyName);
            }
        }

        /// <summary>The lettering, fitted to the sign. Upper case, as signs are.</summary>
        public void Write(string companyName)
        {
            var text = string.IsNullOrWhiteSpace(companyName) ? string.Empty : companyName.Trim().ToUpperInvariant();
            var font = UiBootstrap.ResolveTitleFont();

            if (font == null)
            {
                // No font is a reason to draw nothing, never a reason to throw.
                return;
            }

            var holder = transform.Find(TextName);

            if (holder == null)
            {
                holder = new GameObject(TextName).transform;
                holder.SetParent(transform, false);
            }

            // Not `??`: Unity's missing component is a fake null that the operator does not see.
            if (!holder.TryGetComponent<TextMesh>(out var mesh))
            {
                mesh = holder.gameObject.AddComponent<TextMesh>();
            }
            mesh.font = font;
            mesh.fontSize = NamePlate.FontResolution;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(1f, 0.97f, 0.92f);
            mesh.text = text;

            var renderer = holder.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                // The font's own material, or the glyphs draw as solid rectangles.
                renderer.sharedMaterial = font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            mesh.characterSize = FitSize(text);
        }

        /// <summary>
        /// The character size that fits the name on the sign: as tall as the sign allows, shrunk
        /// for a long name. Measured from the font's own advance widths, so it holds for any face.
        /// </summary>
        private float FitSize(string text)
        {
            // A TextMesh line is roughly fontSize / 10 units tall per unit of character size.
            var lineAtOne = NamePlate.FontResolution / 10f;
            var byHeight = Height / lineAtOne;

            if (string.IsNullOrEmpty(text))
            {
                return byHeight;
            }

            // Width at character size one, from the glyphs: advance is in font pixels at fontSize.
            var font = UiBootstrap.ResolveTitleFont();
            font.RequestCharactersInTexture(text, NamePlate.FontResolution);

            var advance = 0f;

            foreach (var character in text)
            {
                if (font.GetCharacterInfo(character, out var info, NamePlate.FontResolution))
                {
                    advance += info.advance;
                }
            }

            // TextMesh draws one font pixel as 0.1 units at character size one.
            var widthAtOne = advance * 0.1f;
            var byWidth = widthAtOne > 0.001f ? Width / widthAtOne : byHeight;

            return Mathf.Min(byHeight, byWidth);
        }
    }
}
