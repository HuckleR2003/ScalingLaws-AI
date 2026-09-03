using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The two sets of photographic art the interface uses: the category icons in the bottom bar,
    /// and the wide banner that sits under a page heading.
    ///
    /// Both are loaded from Resources, cached, and both fail soft. A screen must never fail to
    /// render because a picture is missing, so an absent file leaves the plate empty and the page
    /// keeps its plain heading. That rule is why the interface survived having no art at all for
    /// the first three months of this project.
    ///
    /// The files under `Resources/` are processed copies, not the originals in `Art/`. Icons keep
    /// their alpha exactly and have their colour replaced, because the originals are near black
    /// glyphs and the bar they sit on is near black. Banners are cropped to a strip and darkened
    /// with a left heavy vignette so a white heading reads over the darkest part of them.
    /// </summary>
    public static class PageArt
    {
        private static readonly Dictionary<string, Texture2D> Cache = new();

        public static Texture2D Icon(string name) => Load("Hud/" + name);

        public static Texture2D Banner(string name) => Load("Banners/" + name);

        /// <summary>
        /// The glyph on a status badge, or null while it is still a couple of letters.
        ///
        /// Null is a normal answer here. The badges shipped with initials because there was no art
        /// yet, and the loader is written so that dropping a file into `Resources/Effects/` is the
        /// entire change: no code, no catalog entry, no rebuild of the strip.
        /// </summary>
        public static Texture2D Effect(string name) => Load("Effects/" + name);

        /// <summary>The picture beside a creator stage. Null when there is not one for that stage.</summary>
        public static Texture2D Page(string name) => Load("Pages/" + name);

        private static Texture2D Load(string path)
        {
            if (Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var texture = Resources.Load<Texture2D>(path);
            Cache[path] = texture;
            return texture;
        }

        /// <summary>A hosting package cover, or null when there is no art for it yet.</summary>
        public static Texture2D Hosting(string name) =>
            string.IsNullOrEmpty(name) ? null : Load("Hosting/" + name);

        /// <summary>
        /// The strip under a page heading. Returns null when there is no art for that page, and the
        /// caller adds nothing rather than adding an empty box.
        /// </summary>
        public static VisualElement BannerFor(string name)
        {
            var texture = string.IsNullOrEmpty(name) ? null : Banner(name);
            if (texture == null)
            {
                return null;
            }

            var banner = new VisualElement();
            banner.AddToClassList("page-banner");
            banner.style.backgroundImage = new StyleBackground(texture);

            // A hairline of the accent along the bottom edge, so the banner belongs to the same
            // system as the bar at the foot of the screen rather than being a photograph dropped in.
            var edge = new VisualElement();
            edge.AddToClassList("page-banner__edge");
            HudAccent.PaintSlice(edge, 0f, 1f);
            banner.Add(edge);

            return banner;
        }
    }
}
