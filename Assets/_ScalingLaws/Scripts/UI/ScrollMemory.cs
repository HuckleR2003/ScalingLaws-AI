using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Where a rebuilt list was scrolled to, so the rebuild does not throw the reader back to the top.
    ///
    /// **Most screens here rebuild themselves on a click**, and the ones with a list in them build a
    /// new `ScrollView` each time. A new scroller starts at the top, so choosing a company in the
    /// founder creator, answering a letter or opening a rival sent the page back to the beginning
    /// every time. The page scroller in `GameShell` is kept rather than rebuilt for exactly this
    /// reason; the lists inside screens could not be kept the same way, because they are built by
    /// methods that return a fresh element.
    ///
    /// **Restored on the first layout, never a frame later.** Setting an offset against content that
    /// has not been laid out does nothing, and restoring it on the next frame draws one frame at the
    /// top first, which is the flicker the page scroller had until it stopped being rebuilt.
    /// `GeometryChangedEvent` arrives after layout and before the frame is drawn.
    /// </summary>
    public static class ScrollMemory
    {
        private static readonly Dictionary<string, Vector2> Offsets = new();

        /// <summary>
        /// Puts a freshly built scroller back where the last one of the same name was, and records
        /// where this one goes from now on.
        /// </summary>
        public static ScrollView Keep(ScrollView scroll, string key)
        {
            if (scroll == null || string.IsNullOrEmpty(key))
            {
                return scroll;
            }

            if (Offsets.TryGetValue(key, out var saved) && saved.sqrMagnitude > 0f)
            {
                EventCallback<GeometryChangedEvent> restore = null;

                restore = _ =>
                {
                    scroll.contentContainer.UnregisterCallback(restore);
                    scroll.scrollOffset = saved;
                };

                scroll.contentContainer.RegisterCallback(restore);
            }

            // Recorded only while this scroller is on a panel. A scroller being taken down clamps
            // to zero as its content goes, and that must not overwrite the position its
            // replacement is about to restore.
            scroll.verticalScroller.valueChanged += _ =>
            {
                if (scroll.panel != null)
                {
                    Offsets[key] = scroll.scrollOffset;
                }
            };

            return scroll;
        }

        /// <summary>Forgets a position, for a list that should open at the top next time.</summary>
        public static void Forget(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                Offsets.Remove(key);
            }
        }

        /// <summary>What is remembered for a key. For the tests.</summary>
        public static Vector2 Remembered(string key) =>
            key != null && Offsets.TryGetValue(key, out var saved) ? saved : Vector2.zero;
    }
}
