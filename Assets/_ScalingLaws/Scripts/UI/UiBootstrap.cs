using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The three things every screen needs before it draws anything, in one place.
    ///
    /// This exists because of two real failures. The stylesheet never set a font, so everything
    /// rendered in whatever the runtime theme fell back to, which reads like a text editor. And a
    /// screen that throws while building renders nothing at all, so the player gets a coloured void
    /// with no way to tell whether the game is broken or still loading.
    /// </summary>
    public static class UiBootstrap
    {
        /// <summary>Base size for body text. The stylesheet scales headings from this.</summary>
        public const int BaseFontSize = 15;

        private static Font cachedFont;
        private static bool fontLookupDone;

        /// <summary>
        /// A font that definitely exists. UI Toolkit will happily resolve to nothing and render
        /// invisible text, so nothing is left to the theme.
        /// </summary>
        public static Font ResolveFont()
        {
            if (fontLookupDone)
            {
                return cachedFont;
            }

            fontLookupDone = true;

            try
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (Exception)
            {
                cachedFont = null;
            }

            if (cachedFont == null)
            {
                // Older editors shipped the same font under a different name.
                try
                {
                    cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch (Exception)
                {
                    cachedFont = null;
                }
            }

            return cachedFont;
        }

        /// <summary>Applies the stylesheet, the font and the base size to a document root.</summary>
        public static void Prepare(VisualElement root, StyleSheet theme)
        {
            if (root == null)
            {
                return;
            }

            if (theme != null && !root.styleSheets.Contains(theme))
            {
                root.styleSheets.Add(theme);
            }

            var font = ResolveFont();
            if (font != null)
            {
                root.style.unityFont = new StyleFont(font);
                root.style.unityFontDefinition = new StyleFontDefinition(font);
            }

            root.style.fontSize = BaseFontSize;
            root.style.color = new Color(0.96f, 0.98f, 1f, 1f);
        }

        /// <summary>
        /// Replaces whatever is on screen with the exception, readably. Far better than a blank
        /// panel: the player can say what broke and the log line is already on their screen.
        /// </summary>
        public static void ShowFailure(VisualElement root, string where, Exception exception)
        {
            Debug.LogError($"[Scaling Laws] {where} failed: {exception}");

            if (root == null)
            {
                return;
            }

            root.Clear();
            root.style.backgroundColor = new Color(0.10f, 0.03f, 0.05f, 1f);
            root.style.paddingLeft = 32;
            root.style.paddingTop = 32;

            var font = ResolveFont();
            if (font != null)
            {
                root.style.unityFont = new StyleFont(font);
            }

            var heading = new Label($"{where} could not be built");
            heading.style.fontSize = 24;
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.color = Color.white;
            heading.style.marginBottom = 16;
            root.Add(heading);

            var detail = new Label(exception?.ToString() ?? "No detail available.");
            detail.style.whiteSpace = WhiteSpace.Normal;
            detail.style.fontSize = 13;
            detail.style.color = new Color(1f, 0.75f, 0.75f, 1f);
            root.Add(detail);
        }
    }
}
