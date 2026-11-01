using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The company marks, loaded once and shared.
    ///
    /// Same shape as <see cref="SkillIcons"/>, and the same rule: a missing file is not an error.
    /// The element is built either way and simply carries no image, so a screen never fails to
    /// render over a piece of art.
    /// </summary>
    public static class CompanyLogos
    {
        private static readonly Dictionary<string, Texture2D> Loaded = new();

        public static Texture2D Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = CompanyLogoCatalog.Default;
            }

            if (Loaded.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var texture = Resources.Load<Texture2D>(CompanyLogoCatalog.ResourceFolder + name);
            Loaded[name] = texture;
            return texture;
        }

        /// <summary>A mark at the size the caller wants. Never null, so callers do not branch.</summary>
        public static VisualElement Mark(string name, int size = 96)
        {
            var mark = new VisualElement();
            mark.AddToClassList("company-mark");
            mark.style.width = size;
            mark.style.height = size;
            mark.pickingMode = PickingMode.Ignore;

            var texture = Get(name);
            if (texture != null)
            {
                mark.style.backgroundImage = new StyleBackground(texture);
            }

            return mark;
        }
    }
}
