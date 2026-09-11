using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The bar down the right-hand edge of a page, drawn over the page rather than beside it.
    ///
    /// **Every screen in this game scrolls and none of them showed a bar for it.** The scrollers were
    /// built with `ScrollerVisibility.Hidden` on purpose: the runtime theme's scrollbar is a wide
    /// grey slab that takes its width out of the content, so turning it on would re-flow every page
    /// in the game the moment a page got long enough to need one, and pages get longer and shorter
    /// as a campaign runs. A player therefore had no way to tell that a section continued below the
    /// fold, which is the reported fault: sections look cut off rather than scrollable.
    ///
    /// So this one is **absolutely positioned inside the scroller's own box**, outside its content
    /// container, which is what makes it cost zero width. Nothing moves when it appears and nothing
    /// moves when it goes away, and it goes away whenever the page fits.
    /// </summary>
    public sealed class PageScrollbar : VisualElement
    {
        /// <summary>Shortest the thumb is allowed to get, so a very long page still has a grip.</summary>
        public const float MinimumThumb = 34f;

        /// <summary>
        /// How far a click on the empty part of the track moves, as a share of the window.
        ///
        /// Under one on purpose: a screenful that overlaps the last one by a sliver keeps the line
        /// the reader stopped on, and jumping exactly one window loses it every time.
        /// </summary>
        public const float TrackJump = 0.85f;

        private readonly ScrollView view;
        private readonly VisualElement thumb = new();

        private bool dragging;
        private float grabbedAt;
        private float offsetWhenGrabbed;

        public PageScrollbar(ScrollView view)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));

            AddToClassList("pscroll");
            pickingMode = PickingMode.Position;

            thumb.AddToClassList("pscroll__thumb");
            Add(thumb);

            RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            RegisterCallback<PointerDownEvent>(OnTrackDown);

            thumb.RegisterCallback<PointerDownEvent>(OnThumbDown);
            thumb.RegisterCallback<PointerMoveEvent>(OnThumbMove);
            thumb.RegisterCallback<PointerUpEvent>(OnThumbUp);

            // Two sources, because they answer different halves of the same question: the offset
            // moving is the player scrolling, and the content resizing is the page changing under
            // them. Either one changes where the thumb belongs.
            view.verticalScroller.valueChanged += _ => Refresh();
            view.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            view.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
        }

        /// <summary>
        /// Where the thumb goes, as pure arithmetic.
        ///
        /// Separated from the element because this is the half that can be wrong in a way nobody
        /// sees: a thumb that is subtly the wrong length still looks like a scrollbar. Height is the
        /// share of the content that is on screen, position is the share of the scrollable range
        /// that has been travelled, and the travel is measured against the **track minus the thumb**
        /// rather than the whole track, or the thumb runs off the bottom by its own length.
        /// </summary>
        public static (float Height, float Top) ThumbGeometry(float viewport, float content,
            float offset, float track, float minimumThumb = MinimumThumb)
        {
            if (viewport <= 0f || content <= 0f || track <= 0f || content <= viewport)
            {
                return (0f, 0f);
            }

            var height = Mathf.Clamp(track * (viewport / content), Mathf.Min(minimumThumb, track),
                track);

            var scrollable = Mathf.Max(0f, content - viewport);
            var travelled = scrollable <= 0f ? 0f : Mathf.Clamp01(offset / scrollable);

            return (height, travelled * (track - height));
        }

        /// <summary>Redraws the thumb, and hides the whole bar when the page fits the window.</summary>
        public void Refresh()
        {
            var viewport = view.layout.height;
            var content = view.contentContainer.layout.height;
            var track = layout.height;

            if (float.IsNaN(viewport) || float.IsNaN(content) || float.IsNaN(track))
            {
                return;
            }

            var (height, top) = ThumbGeometry(viewport, content, view.scrollOffset.y, track);

            // A bar over a page that fits is furniture, and worse, it is furniture that says the
            // page continues when it does not.
            var needed = height > 0f;

            style.display = needed ? DisplayStyle.Flex : DisplayStyle.None;

            if (!needed)
            {
                return;
            }

            thumb.style.height = height;
            thumb.style.top = top;
        }

        private float Scrollable() =>
            Mathf.Max(0f, view.contentContainer.layout.height - view.layout.height);

        private void OnThumbDown(PointerDownEvent down)
        {
            dragging = true;
            grabbedAt = down.position.y;
            offsetWhenGrabbed = view.scrollOffset.y;

            thumb.CapturePointer(down.pointerId);
            down.StopPropagation();
        }

        private void OnThumbMove(PointerMoveEvent move)
        {
            if (!dragging)
            {
                return;
            }

            var track = layout.height;
            var (height, _) = ThumbGeometry(view.layout.height, view.contentContainer.layout.height,
                view.scrollOffset.y, track);

            var room = track - height;
            if (room <= 0f)
            {
                return;
            }

            // A pixel of thumb is worth more than a pixel of page, by exactly the ratio the thumb
            // was shortened by. Moving the page one-for-one with the pointer is the thing that makes
            // a hand-built scrollbar feel wrong.
            var moved = (move.position.y - grabbedAt) / room * Scrollable();

            view.scrollOffset = new Vector2(view.scrollOffset.x, offsetWhenGrabbed + moved);
        }

        private void OnThumbUp(PointerUpEvent up)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            thumb.ReleasePointer(up.pointerId);
            up.StopPropagation();
        }

        /// <summary>A click on the empty track is a screenful in that direction, not a jump to it.</summary>
        private void OnTrackDown(PointerDownEvent down)
        {
            if (dragging)
            {
                return;
            }

            var step = view.layout.height * TrackJump;
            var above = down.localPosition.y < thumb.layout.y;

            view.scrollOffset = new Vector2(view.scrollOffset.x,
                Mathf.Clamp(view.scrollOffset.y + (above ? -step : step), 0f, Scrollable()));

            down.StopPropagation();
        }
    }
}
