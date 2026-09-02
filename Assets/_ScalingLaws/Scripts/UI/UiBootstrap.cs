using System;
using ScalingLaws.Data;
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

        private static Font cachedTitleFont;
        private static bool titleLookupDone;

        /// <summary>
        /// The face section headings and page titles are set in.
        ///
        /// **Montserrat SemiBold, bundled rather than asked for by name.** Everything in this game
        /// was rendering in the editor's fallback Arial, which is legible and says nothing, and a
        /// tycoon interface is mostly headings. Loading a system font by name would look right on
        /// the machine it was written on and fall back to Arial on every other one, silently, which
        /// is the failure this project keeps finding in other forms.
        ///
        /// It is under the SIL Open Font License, so it may travel in a public repository as long
        /// as the licence travels with it. `Resources/Fonts/OFL.txt` is that copy and must not be
        /// deleted.
        ///
        /// Falls back to the body font rather than to nothing: a missing heading face should make
        /// the game look plainer, never make the headings vanish.
        /// </summary>
        public static Font ResolveTitleFont()
        {
            if (titleLookupDone)
            {
                return cachedTitleFont;
            }

            titleLookupDone = true;
            cachedTitleFont = Resources.Load<Font>("Fonts/Montserrat-SemiBold") ?? ResolveFont();

            return cachedTitleFont;
        }

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

        /// <summary>
        /// Where the stylesheet lives for a runtime load. It is duplicated into Resources on
        /// purpose: relying on a serialized field meant one missed editor step rendered the whole
        /// game as an unstyled column of full-width bars, and nothing in the build failed.
        /// </summary>
        public const string StyleSheetResourcePath = "ScalingLaws";

        /// <summary>
        /// The stylesheet, from the serialized field if it was wired and from Resources if it was
        /// not. The UI must never silently render unstyled.
        /// </summary>
        public static StyleSheet ResolveTheme(StyleSheet assigned)
        {
            if (assigned != null)
            {
                return assigned;
            }

            var loaded = Resources.Load<StyleSheet>(StyleSheetResourcePath);
            if (loaded == null)
            {
                Debug.LogError(
                    $"[Scaling Laws] No stylesheet. Expected one at Resources/{StyleSheetResourcePath}.uss. "
                    + "Every screen will render as an unstyled column.");
            }

            return loaded;
        }

        /// <summary>Applies the stylesheet, the font and the base size to a document root.</summary>
        public static void Prepare(VisualElement root, StyleSheet theme)
        {
            if (root == null)
            {
                return;
            }

            var sheet = ResolveTheme(theme);
            if (sheet != null && !root.styleSheets.Contains(sheet))
            {
                root.styleSheets.Add(sheet);
            }

            var font = ResolveFont();
            if (font != null)
            {
                root.style.unityFont = new StyleFont(font);
                root.style.unityFontDefinition = new StyleFontDefinition(font);
            }

            root.style.fontSize = BaseFontSize;
            root.style.color = new Color(0.96f, 0.98f, 1f, 1f);

            // Insight cards mount here rather than beside the control they describe, so they are
            // above everything and cannot be clipped by a panel that clips its own contents. Set
            // once, here, because this is the one function every document already calls.
            InsightTip.Host = root;
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

            var heading = new Label(Loc.T("boot.could_not_build", where));
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
