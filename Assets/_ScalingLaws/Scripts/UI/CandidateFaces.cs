using System.Collections.Generic;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Faces for the people who apply.
    ///
    /// **Rendered once per person and kept.** A portrait is a 3D model, a camera and a render
    /// texture; doing that per row of a list would cost more than the rest of the screen put
    /// together, and doing it again every time a day rolled over would make the inbox flicker. So
    /// the studio runs once per candidate and the texture is cached against their seed.
    ///
    /// The cache is bounded. A campaign that interviews four hundred people over ten years would
    /// otherwise hold four hundred render textures for the sake of letters that closed years ago.
    /// </summary>
    public static class CandidateFaces
    {
        /// <summary>Portraits held at once. Beyond this the oldest is dropped and re-rendered if needed.</summary>
        public const int CacheSize = 24;

        /// <summary>Side of the rendered face, in pixels. Square, because every frame it sits in is.</summary>
        public const int Size = 256;

        private static readonly Dictionary<int, Texture2D> Cache = new();
        private static readonly List<int> Order = new();

        /// <summary>
        /// The candidate's face, or null when the character pack is not installed.
        ///
        /// Null is a real answer rather than a failure: the looks live in an Asset Store pack that
        /// is gitignored, so a fresh clone has none of them and every caller has to cope.
        /// </summary>
        public static Texture2D Get(Candidate candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            if (Cache.TryGetValue(candidate.PortraitSeed, out var cached) && cached != null)
            {
                return cached;
            }

            var studio = new PortraitStudio();

            if (!studio.Open())
            {
                studio.Close();
                return null;
            }

            // The seed picks the face and whether they wear glasses. Stable per person, so the
            // same candidate looks the same in the inbox on day one and day nine.
            var look = studio.LookCount > 0 ? candidate.PortraitSeed % studio.LookCount : 0;
            var glasses = studio.GlassesCount > 0
                ? (candidate.PortraitSeed / 7) % studio.GlassesCount
                : 0;

            studio.StepLook(look - studio.LookIndex);
            studio.StepGlasses(glasses - studio.GlassesIndex);
            studio.RenderNow();

            var baked = Bake(studio.Texture);
            studio.Close();

            if (baked == null)
            {
                return null;
            }

            Remember(candidate.PortraitSeed, baked);
            return baked;
        }

        /// <summary>
        /// A frame with the face in it, or with the person's initials when there is no pack.
        ///
        /// One helper rather than two, because every screen that shows a candidate wants the same
        /// fallback and a screen that forgot it would show an empty square.
        /// </summary>
        public static VisualElement Frame(Candidate candidate, int size, string accentHex)
        {
            var frame = new VisualElement();
            frame.AddToClassList("face");
            frame.style.width = size;
            frame.style.height = size;

            if (ColorUtility.TryParseHtmlString(accentHex, out var accent))
            {
                frame.style.borderTopColor = accent;
                frame.style.borderBottomColor = accent;
                frame.style.borderLeftColor = accent;
                frame.style.borderRightColor = accent;
            }

            var art = Get(candidate);

            if (art != null)
            {
                frame.style.backgroundImage = new StyleBackground(art);
                return frame;
            }

            var initials = new Label(InitialsOf(candidate?.Name));
            initials.AddToClassList("face__initials");
            frame.Add(initials);
            return frame;
        }

        private static string InitialsOf(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }

            var parts = name.Split(' ');
            return parts.Length > 1
                ? $"{parts[0][0]}{parts[^1][0]}"
                : parts[0][..1];
        }

        /// <summary>
        /// Copies a render texture into a plain one.
        ///
        /// The studio reuses its render target for whoever it renders next, so holding a reference
        /// to it would give every candidate the last candidate's face.
        /// </summary>
        private static Texture2D Bake(RenderTexture source)
        {
            if (source == null)
            {
                return null;
            }

            var wasActive = RenderTexture.active;
            RenderTexture.active = source;

            var baked = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            baked.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            baked.Apply();

            RenderTexture.active = wasActive;
            return baked;
        }

        private static void Remember(int seed, Texture2D face)
        {
            Cache[seed] = face;
            Order.Add(seed);

            while (Order.Count > CacheSize)
            {
                var oldest = Order[0];
                Order.RemoveAt(0);

                if (Cache.TryGetValue(oldest, out var stale))
                {
                    Discard(stale);
                    Cache.Remove(oldest);
                }
            }
        }

        /// <summary>
        /// Throws a texture away.
        ///
        /// **Destroy is a no-op outside play mode and logs an error**, and the soak test that
        /// builds every screen at every age of the company runs in edit mode. It caught this the
        /// first time the inbox was drawn with a face in it.
        /// </summary>
        private static void Discard(Object doomed)
        {
            if (doomed == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(doomed);
            }
            else
            {
                Object.DestroyImmediate(doomed);
            }
        }

        /// <summary>Drops everything. Called when a save is loaded, so faces do not survive a campaign.</summary>
        public static void Forget()
        {
            foreach (var face in Cache.Values)
            {
                Discard(face);
            }

            Cache.Clear();
            Order.Clear();
        }
    }
}
