using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The seven skill icons, loaded once and shared.
    ///
    /// They appear in the creator, and they will appear again in the panel that opens when a person
    /// is clicked in the office, so the lookup lives here rather than in whichever screen needed it
    /// first. A missing file is not an error: the element is built either way and simply has no
    /// image, which keeps a screen from failing to render over a piece of art.
    /// </summary>
    public static class SkillIcons
    {
        private const string ResourceFolder = "Skills/";

        private static readonly Dictionary<PlayerSkill, string> FileNames = new()
        {
            { PlayerSkill.Development, "development" },
            { PlayerSkill.Management, "management" },
            { PlayerSkill.Teamwork, "teamwork" },
            { PlayerSkill.Concept, "concept" },
            { PlayerSkill.Software, "software" },
            { PlayerSkill.DataEngineering, "data_engineering" },
            { PlayerSkill.Safety, "safety" }
        };

        private static readonly Dictionary<PlayerSkill, Texture2D> Loaded = new();

        public static Texture2D Get(PlayerSkill skill)
        {
            if (Loaded.TryGetValue(skill, out var cached))
            {
                return cached;
            }

            var texture = FileNames.TryGetValue(skill, out var file)
                ? Resources.Load<Texture2D>(ResourceFolder + file)
                : null;

            Loaded[skill] = texture;
            return texture;
        }

        /// <summary>An icon sized for a row or a badge. Never null, so callers do not branch.</summary>
        public static VisualElement Badge(PlayerSkill skill, int size = 34)
        {
            var element = new VisualElement();
            element.AddToClassList("skill-icon");
            element.style.width = size;
            element.style.height = size;

            var texture = Get(skill);
            if (texture != null)
            {
                element.style.backgroundImage = new StyleBackground(texture);
            }
            else
            {
                element.AddToClassList("skill-icon--missing");
            }

            return element;
        }
    }
}
