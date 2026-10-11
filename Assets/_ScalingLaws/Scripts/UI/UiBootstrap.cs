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

        /// <summary>
        /// How far the panel is allowed to shrink before the type stops being readable.
        ///
        /// **A font size in the stylesheet is not a font size on the screen.** The panel is
        /// `ScaleWithScreenSize` against 1920x1080 with the match at a half, so everything is
        /// multiplied by the square root of the width ratio times the square root of the height
        /// ratio. The 11px floor `StylesheetTests` guards is therefore 11px at the reference and
        /// nowhere else: measured, it draws at 9.2px on a 1600x900 window and **7.8px on a
        /// 1366x768 one**, which is the commonest laptop panel still shipping.
        ///
        /// 340 rules sit on exactly 11px and 703 of 1088 are under 13, so that is most of the
        /// interface rather than a corner of it. This project already learned the same arithmetic
        /// once on the research board, where a 0.85 zoom took an 11px title to 9.4 and no
        /// stylesheet test could see it.
        ///
        /// Below this the panel stops shrinking and the layout is given the room it would have
        /// shrunk into instead, which it can take: the bottom bar is sized for 1920 and already
        /// copes with about 1820 on a 16:10 window. 0.85 puts the smallest type at 9.4px.
        /// </summary>
        public const float ScaleFloor = 0.85f;

        /// <summary>
        /// The narrowest canvas the interface is built to stand up in.
        ///
        /// The bottom bar is the binding piece: about 445px of controls plus fifteen slots at
        /// their 62px minimum. Below this the floor gives way and the type shrinks, because a
        /// page that does not fit is worse than a page that is small.
        /// </summary>
        public const float MinimumCanvasWidth = 1600f;

        /// <summary>
        /// Keeps the panel from shrinking the type past <see cref="ScaleFloor"/>.
        ///
        /// Unity works the scale out from the window and the reference resolution; `scale` is a
        /// multiplier on top of it, so the correction is the floor over whatever Unity arrived at.
        /// At or above the reference it is exactly one and nothing here changes anything.
        ///
        /// **Re-applied on geometry rather than polled.** The root's geometry changes when the
        /// window does, which is the only moment this answer can change, and the guard against
        /// setting the same value twice is what stops the resize feeding itself: a new scale moves
        /// the geometry, which raises the event again.
        /// </summary>
        public static void HoldTheTypeFloor(UIDocument document)
        {
            if (document == null || document.rootVisualElement == null)
            {
                return;
            }

            Apply(document);
            document.rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => Apply(document));
        }

        /// <summary>The correction for one window, or one when the window is big enough.</summary>
        public static float CorrectionFor(Vector2 window, Vector2 reference)
        {
            if (window.x <= 0f || window.y <= 0f || reference.x <= 0f || reference.y <= 0f)
            {
                return 1f;
            }

            // Unity's own arithmetic for ScaleWithScreenSize with the match at a half, which is
            // what the panel settings asset carries. Written out rather than guessed: the whole
            // point of this is that the number matches what the screen actually does.
            var natural = Mathf.Sqrt(window.x / reference.x) * Mathf.Sqrt(window.y / reference.y);

            if (natural >= ScaleFloor || natural <= 0f)
            {
                return 1f;
            }

            // **The floor yields to the layout, because a canvas can be too narrow to hold it.**
            //
            // Holding the type at 0.85 means handing the layout `window / 0.85` of virtual room,
            // and on a small enough window that is less than the interface needs: the bottom bar
            // alone wants about 1,400 for its controls and fifteen slots at their minimum. Pushed
            // past that the pages stop fitting, which is worse than small type and is not a
            // trade anybody would choose.
            //
            // Found by the suite rather than by thinking. In batchmode the screen is a few
            // hundred pixels, so the first version of this computed a correction of 2.2, handed
            // the layout a 565px canvas, and three PlayMode fixtures reported cards opening off
            // the bottom of the window. The same arithmetic would have met a player who dragged
            // the window small.
            var floored = ScaleFloor / natural;
            var widest = window.x / (natural * MinimumCanvasWidth);

            return Mathf.Clamp(Mathf.Min(floored, widest), 1f, floored);
        }

        private static void Apply(UIDocument document)
        {
            var settings = document == null ? null : document.panelSettings;
            if (settings == null || settings.scaleMode != PanelScaleMode.ScaleWithScreenSize)
            {
                return;
            }

            // **The window is the thing the panel is drawing into, which is not always the
            // screen.** A panel with a target texture renders at the texture's size while
            // `Screen` still reports the application window, and every proof render in this
            // project goes to a texture. Read from the screen alone, the first version of this
            // computed a correction of 2.208 against a 1920 frame and handed the layout an 869
            // pixel canvas, which would have quietly mangled every frame the project renders to
            // look at itself.
            var window = settings.targetTexture != null
                ? new Vector2(settings.targetTexture.width, settings.targetTexture.height)
                : new Vector2(Screen.width, Screen.height);
            var reference = new Vector2(
                settings.referenceResolution.x, settings.referenceResolution.y);

            var wanted = CorrectionFor(window, reference);

            if (!Mathf.Approximately(settings.scale, wanted))
            {
                settings.scale = wanted;
            }
        }

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
            Toast.Host = root;
            GateNotice.Host = root;
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
