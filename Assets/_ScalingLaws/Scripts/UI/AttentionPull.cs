using ScalingLaws.Persistence;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Takes something the tour is pointing at, puts it in the middle of the screen, and lets it
    /// travel back to where it lives.
    ///
    /// **It exists because a ring on a corner is not a signpost.** The support walkthrough opens by
    /// saying "the official page is here" over the banner in the top right, and a tester reported
    /// that it was very hard to see that anything was lit at all. `ScrollIntoView` already answers
    /// the other half of that problem, which is a target below the fold; this is the one where the
    /// target is on screen the whole time and the eye is in the wrong corner.
    ///
    /// **The movement is the message, so it runs backwards.** The element appears in the middle and
    /// walks home, rather than sliding out and back: the eye follows a moving thing, and what it
    /// has to learn is where the thing ends up, not where it started.
    /// </summary>
    public static class AttentionPull
    {
        /// <summary>The author asked for a second and a half and it is the right length.</summary>
        public const float Seconds = 1.5f;

        /// <summary>
        /// How far from the middle a thing has to sit before it is worth pulling at all.
        ///
        /// Anything whose centre is inside the middle half of the window is already where the eye
        /// lands, and dragging it out and back would be decoration. A corner is what this is for.
        /// </summary>
        public const float MiddleHalf = 0.25f;

        /// <summary>
        /// True when <paramref name="box"/> sits far enough out to be worth pointing at, measured
        /// against the window <paramref name="panel"/>. Pure, so a test can hold the rule without a
        /// panel or a clock.
        /// </summary>
        public static bool IsOffToOneSide(Rect box, Rect panel)
        {
            if (panel.width <= 0f || panel.height <= 0f)
            {
                return false;
            }

            var acrossFromMiddle = Mathf.Abs(box.center.x - panel.center.x) / panel.width;
            var downFromMiddle = Mathf.Abs(box.center.y - panel.center.y) / panel.height;

            return acrossFromMiddle > MiddleHalf || downFromMiddle > MiddleHalf;
        }

        /// <summary>
        /// Plays it, if there is anything to play. Silent and harmless when the element has no
        /// panel yet, has not been laid out, sits in the middle already, or when the player has
        /// asked for less movement.
        /// </summary>
        public static void Play(VisualElement target)
        {
            // **Reduce motion is accessibility here, not performance.** A player who has turned it
            // on still gets the ring and the sentence; they do not get a banner flying across the
            // window. Same reading the tokenizer field is held to.
            if (target?.panel == null || GameSettings.ReduceMotion)
            {
                return;
            }

            var panel = target.panel.visualTree.layout;
            var box = target.worldBound;

            if (float.IsNaN(box.width) || box.width <= 0f || box.height <= 0f
                || !IsOffToOneSide(box, panel))
            {
                return;
            }

            var across = panel.center.x - box.center.x;
            var down = panel.center.y - box.center.y;

            // Frame one: no transition, so this is a jump rather than a slide out. Putting it in
            // the middle instantly is what makes the walk home read as one movement.
            target.style.transitionDuration = new List<TimeValue> { new(0f, TimeUnit.Second) };
            target.style.translate = new Translate(across, down);

            // Frame two: turn the transition on and clear the offset, and it travels. It has to be
            // a later frame: setting both in one pass gives the layout nothing to animate from.
            target.schedule.Execute(() =>
            {
                target.style.transitionProperty =
                    new List<StylePropertyName> { new("translate") };
                target.style.transitionDuration =
                    new List<TimeValue> { new(Seconds, TimeUnit.Second) };
                target.style.translate = new Translate(0f, 0f);
            }).ExecuteLater(32);

            // And take the inline styles off afterwards. Leaving a transition on an element that
            // the day rollover rebuilds anyway is how a corner starts sliding for no reason later.
            target.schedule.Execute(() =>
            {
                target.style.translate = StyleKeyword.Null;
                target.style.transitionDuration = StyleKeyword.Null;
                target.style.transitionProperty = StyleKeyword.Null;
            }).ExecuteLater((long)(Seconds * 1000f) + 160);
        }
    }
}
