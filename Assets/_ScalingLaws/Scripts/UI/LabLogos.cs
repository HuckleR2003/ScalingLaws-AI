using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The rival labs' marks, loaded once and shared.
    ///
    /// Same contract as <see cref="ResearchIcons"/> and <see cref="SkillIcons"/>: a lab with no file
    /// draws its initial in a coloured disc instead of a hole, so the field is legible before every
    /// mark exists and stays legible if one is ever renamed.
    ///
    /// **The filenames are parodies and the display names are not**, which is a mismatch worth
    /// knowing about rather than papering over. `CompetitorCatalog.NameOf` returns the real company
    /// names while the art was drawn as OpenSI, Astral, DeepThink and so on. This map is the only
    /// place the two meet, so renaming the labs later is a change to one dictionary.
    /// </summary>
    public static class LabLogos
    {
        private const string ResourceFolder = "Labs/";

        private static readonly Dictionary<CompetitorId, string> FileNames = new()
        {
            { CompetitorId.OpenAi, "lab_opensi" },
            { CompetitorId.Anthropic, "lab_antropic" },
            { CompetitorId.GoogleDeepMind, "lab_deepthink" },
            { CompetitorId.MetaAi, "lab_infinity" },
            { CompetitorId.MistralAi, "lab_astral" },
            { CompetitorId.DeepSeek, "lab_deepsearch" },
            { CompetitorId.XAi, "lab_xai" },
            { CompetitorId.AlibabaQwen, "lab_swen" },
            { CompetitorId.Groq, "lab_groq" },

            // Named before the art exists. Badge() draws initials when the file is missing, which
            // is a real mark rather than a hole, so the four new labs read correctly today.
            { CompetitorId.StabilityAi, "lab_stability" },
            { CompetitorId.InflectionAi, "lab_inflection" },
            { CompetitorId.AlephAlpha, "lab_alephalpha" },
            { CompetitorId.Cohere, "lab_cohere" }
        };

        private static readonly Dictionary<CompetitorId, Texture2D> Loaded = new();

        /// <summary>The mark for a lab, or null when there is no file for it.</summary>
        public static Texture2D Get(CompetitorId lab)
        {
            if (Loaded.TryGetValue(lab, out var cached))
            {
                return cached;
            }

            Texture2D texture = null;
            if (FileNames.TryGetValue(lab, out var file))
            {
                texture = Resources.Load<Texture2D>(ResourceFolder + file);
            }

            Loaded[lab] = texture;
            return texture;
        }

        /// <summary>
        /// A badge for one lab: the mark if it exists, otherwise its initial on a coloured disc.
        ///
        /// The fallback is deliberate rather than a placeholder to be removed. The player is one of
        /// the owners in every list this appears in and the player has no mark at all, so something
        /// has to stand in, and an initial reads better than a blank square.
        /// </summary>
        public static VisualElement Badge(CompetitorId lab, string displayName, bool isPlayer = false)
        {
            var badge = new VisualElement();
            badge.AddToClassList("lab-badge");
            badge.EnableInClassList("lab-badge--mine", isPlayer);

            var texture = isPlayer ? null : Get(lab);
            if (texture != null)
            {
                badge.style.backgroundImage = new StyleBackground(texture);
                badge.AddToClassList("lab-badge--art");
                return badge;
            }

            var initial = new Label(string.IsNullOrWhiteSpace(displayName)
                ? "?"
                : displayName.Substring(0, 1).ToUpperInvariant());

            initial.AddToClassList("lab-badge__initial");
            badge.Add(initial);
            return badge;
        }
    }
}
