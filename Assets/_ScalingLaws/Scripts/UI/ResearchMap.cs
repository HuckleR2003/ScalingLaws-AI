using System;
using UnityEngine;
using ScalingLaws.Data;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A pannable, zoomable window onto one era of the tree.
    ///
    /// **One map per era rather than one map for everything.** Thirty-eight nodes over four eras on
    /// a single canvas is a picture the player has to navigate before they can read it, and three
    /// of those eras are years away from mattering. An era is the unit the player actually thinks
    /// in — "what can I do now", then "what does this unlock" — so an era is the unit that gets its
    /// own board. The eras stack down the page as they always did; what is new is that each one is
    /// a surface you can lean into.
    ///
    /// It opens showing everything, which is the rule that matters: a map that starts zoomed in is
    /// a map that hides the thing the player came to see. Zoom and pan are for leaning closer, not
    /// for finding your way back to the default.
    ///
    /// Implemented with `scale` and `translate` on a content layer rather than by moving elements.
    /// The nodes inside keep their own layout, their own tooltips and their own buttons, so this
    /// wraps the existing tree instead of replacing it.
    /// </summary>
    public sealed class ResearchMap : VisualElement
    {
        /// <summary>Closest the player can lean in. Past this the cards are larger than the frame.</summary>
        public const float MaximumZoom = 2.4f;

        /// <summary>And furthest out. Past this the labels stop being legible anyway.</summary>
        public const float MinimumZoom = 0.55f;

        /// <summary>How much one wheel notch moves the zoom. Multiplicative, so it feels even.</summary>
        public const float ZoomStep = 0.12f;

        /// <summary>Pixels of drag before a press counts as a pan rather than a click.</summary>
        public const float DragThreshold = 4f;

        private readonly VisualElement content;
        private readonly VisualElement bar;
        private readonly Label reading;

        private Vector2 pan;
        private float zoom = 1f;

        private bool dragging;
        private Vector2 grabbedAt;
        private Vector2 panWhenGrabbed;

        public ResearchMap()
        {
            AddToClassList("rmap");

            // The viewport clips, so panned content disappears at the frame edge rather than
            // drawing over the era below it.
            style.overflow = Overflow.Hidden;

            content = new VisualElement();
            content.AddToClassList("rmap__content");

            // The content is transformed about its top left, so the arithmetic below only has to
            // deal with one origin. Centre-origin scaling moves the content sideways as it grows,
            // which makes zooming at the cursor much harder to get right.
            content.style.transformOrigin = new TransformOrigin(0f, 0f);
            Add(content);

            bar = new VisualElement();
            bar.AddToClassList("rmap__bar");

            reading = new Label("100%");
            reading.AddToClassList("rmap__reading");
            bar.Add(reading);

            var out_ = new Button(() => ZoomBy(-2)) { text = "-" };
            out_.AddToClassList("rmap__step");
            bar.Add(out_);

            var in_ = new Button(() => ZoomBy(2)) { text = "+" };
            in_.AddToClassList("rmap__step");
            bar.Add(in_);

            var fit = new Button(Reset) { text = Loc.T("common.fit") };
            fit.AddToClassList("rmap__fit");
            bar.Add(fit);

            Add(bar);

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        /// <summary>Where the tree goes. Callers add nodes here, not to the map itself.</summary>
        public VisualElement Surface => content;

        /// <summary>
        /// The zoom reading and its three buttons, so a caller with a better place for them can
        /// take them.
        ///
        /// They live in the map's own top right corner by default, which is correct for a map used
        /// on its own and was wrong on the research page: the track is centred in the frame and its
        /// last node ended up underneath them, clickable in about half its area. Adding this
        /// element to another parent moves it, and `rmap__bar--inline` puts it back in flow.
        /// </summary>
        public VisualElement Controls => bar;

        public float Zoom => zoom;

        /// <summary>Back to everything visible, which is where it started.</summary>
        public void Reset()
        {
            zoom = 1f;
            pan = Vector2.zero;
            Apply();
        }

        private void OnWheel(WheelEvent wheel)
        {
            // Zoom about the cursor, so the node under the pointer stays under the pointer. Zooming
            // about the corner means leaning in on something on the right pushes it off the screen,
            // and the player spends the next second panning it back.
            var local = new Vector2(wheel.localMousePosition.x, wheel.localMousePosition.y);
            var before = (local - pan) / zoom;

            var next = Mathf.Clamp(zoom * (1f - Mathf.Sign(wheel.delta.y) * ZoomStep),
                MinimumZoom, MaximumZoom);

            if (Mathf.Approximately(next, zoom))
            {
                return;
            }

            zoom = next;
            pan = local - before * zoom;

            Apply();
            wheel.StopPropagation();
        }

        private void ZoomBy(int notches)
        {
            var centre = new Vector2(resolvedStyle.width / 2f, resolvedStyle.height / 2f);
            var before = (centre - pan) / zoom;

            zoom = Mathf.Clamp(zoom * (1f + notches * ZoomStep), MinimumZoom, MaximumZoom);
            pan = centre - before * zoom;

            Apply();
        }

        private void OnPointerDown(PointerDownEvent press)
        {
            // Left button only, and the press is not captured yet: a press that turns out to be a
            // click has to reach the node underneath. Capture happens once the pointer has actually
            // moved, in OnPointerMove.
            if (press.button != 0)
            {
                return;
            }

            dragging = true;
            grabbedAt = press.position;
            panWhenGrabbed = pan;
        }

        private void OnPointerMove(PointerMoveEvent move)
        {
            if (!dragging)
            {
                return;
            }

            var travelled = (Vector2)move.position - grabbedAt;

            if (travelled.magnitude < DragThreshold && !this.HasPointerCapture(move.pointerId))
            {
                return;
            }

            if (!this.HasPointerCapture(move.pointerId))
            {
                // Past the threshold this is a pan, so the map takes the pointer and the button
                // under it never sees the release.
                this.CapturePointer(move.pointerId);
                AddToClassList("rmap--dragging");
            }

            pan = panWhenGrabbed + travelled;
            Apply();
            move.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent release)
        {
            dragging = false;

            if (this.HasPointerCapture(release.pointerId))
            {
                this.ReleasePointer(release.pointerId);
                RemoveFromClassList("rmap--dragging");
                release.StopPropagation();
            }
        }

        private void Apply()
        {
            content.style.scale = new Scale(new Vector2(zoom, zoom));
            content.style.translate = new Translate(pan.x, pan.y);
            reading.text = $"{Mathf.RoundToInt(zoom * 100f)}%";
        }
    }
}
