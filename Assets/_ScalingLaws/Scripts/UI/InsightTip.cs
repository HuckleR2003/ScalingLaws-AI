using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The card that appears when the cursor rests on a control and says what it is for.
    ///
    /// The runtime's own `tooltip` string was doing this job and doing it badly: one line of grey
    /// text in a system box, no title, no room to say what a screen is *about* rather than what the
    /// button is called. Fourteen categories along the bottom of the screen is a lot of nouns to
    /// learn from nouns alone.
    ///
    /// **It mounts on the panel root rather than beside the control.** A card drawn inside the HUD
    /// would be clipped by it, and the HUD clips its own row on purpose. Mounting at the top means
    /// one overlay, above everything, positioned from the control's own world rectangle.
    ///
    /// Only one card exists at a time. Moving the cursor along a row of slots replaces the contents
    /// rather than building fourteen cards, and a control that is removed while the cursor is on it
    /// cannot leave a card behind, because the card is not its child.
    /// </summary>
    public static class InsightTip
    {
        /// <summary>Gap between the card and the control it describes.</summary>
        public const float Gap = 8f;

        /// <summary>Width of the card. Fixed, so the placement can centre it before it is laid out.</summary>
        public const float Width = 268f;

        /// <summary>Which side of the control the card sits on.</summary>
        public enum Placement
        {
            /// <summary>Above it, centred. What the bottom bar wants.</summary>
            Above,

            /// <summary>To its left, bottoms aligned. What a control in the right margin wants.</summary>
            LeftOf
        }

        /// <summary>
        /// Where cards are mounted. Set once when a document is prepared, so nothing else has to
        /// carry a reference to the root around just in case it wants a tooltip.
        /// </summary>
        public static VisualElement Host { get; set; }

        private static VisualElement card;

        /// <summary>Which control the open card belongs to. Null when nothing is open.</summary>
        private static VisualElement owner;
        private static Label cardTitle;
        private static Label cardBody;

        /// <summary>
        /// Makes a control show a card while the cursor is on it.
        ///
        /// The title is the thing, the body is why the player would go there. Both are needed: a
        /// title alone is the label that is already printed under the icon.
        /// </summary>
        public static void Attach(VisualElement target, string title, string body,
            Placement placement = Placement.Above)
        {
            if (target == null || string.IsNullOrEmpty(title))
            {
                return;
            }

            // The runtime tooltip would otherwise open its own grey box on top of this one.
            target.tooltip = string.Empty;

            target.RegisterCallback<MouseEnterEvent>(_ => Show(target, title, body, placement));
            target.RegisterCallback<MouseLeaveEvent>(_ => HideFor(target));
            target.RegisterCallback<DetachFromPanelEvent>(_ => HideFor(target));
        }

        public static void Hide()
        {
            owner = null;
            card?.RemoveFromClassList("insight--in");
            card?.RemoveFromHierarchy();
        }

        /// <summary>
        /// Hides only a card this control actually opened.
        ///
        /// **`Hide` is static and every attached control used to call it on detach.** A day rolling
        /// over rebuilds the open page, so every element on it detaches at once, and every one of
        /// them closed whatever card was open, including a card belonging to a bottom bar slot the
        /// cursor was still resting on. The tooltip vanished once a day for no reason the player
        /// could see.
        /// </summary>
        private static void HideFor(VisualElement source)
        {
            if (owner == source)
            {
                Hide();
            }
        }

        private static void Show(VisualElement target, string title, string body, Placement placement)
        {
            var host = Host;
            if (host == null || target.panel == null)
            {
                return;
            }

            Build();

            cardTitle.text = title;
            cardBody.text = body ?? string.Empty;
            cardBody.style.display = string.IsNullOrEmpty(body) ? DisplayStyle.None : DisplayStyle.Flex;

            owner = target;
            host.Add(card);
            Place(host, target, placement);

            // Born flat and a few pixels low, released a frame later so the transition has somewhere
            // to move from. Same trick the page arrival and the intro fade use.
            card.RemoveFromClassList("insight--in");
            card.schedule.Execute(() => card.AddToClassList("insight--in")).ExecuteLater(16);
        }

        /// <summary>
        /// Positions the card from the control's world rectangle.
        ///
        /// Anchored by the edge nearest the control rather than by the opposite one, so the card
        /// grows away from what it describes and never overlaps it however many lines the body runs
        /// to. That is why <see cref="Placement.Above"/> sets `bottom` and not `top`.
        /// </summary>
        private static void Place(VisualElement host, VisualElement target, Placement placement)
        {
            var bounds = target.worldBound;
            var width = host.resolvedStyle.width;
            var height = host.resolvedStyle.height;

            if (placement == Placement.LeftOf)
            {
                card.style.left = StyleKeyword.Auto;
                card.style.right = width - bounds.xMin + Gap;
                card.style.bottom = height - bounds.yMax;
                return;
            }

            // Centred on the control, then held inside the window: the first and last slots in the
            // bottom bar are close enough to the edges to push a 268px card off the screen.
            var left = bounds.center.x - Width / 2f;
            left = Mathf.Clamp(left, 12f, Mathf.Max(12f, width - Width - 12f));

            card.style.right = StyleKeyword.Auto;
            card.style.left = left;
            card.style.bottom = height - bounds.yMin + Gap;
        }

        private static void Build()
        {
            if (card != null)
            {
                return;
            }

            card = new VisualElement();
            card.AddToClassList("insight");

            // It must never eat a click meant for what it is describing.
            card.pickingMode = PickingMode.Ignore;

            var accent = new VisualElement();
            accent.AddToClassList("insight__accent");
            HudAccent.PaintSlice(accent, 0.32f, 0.94f);
            card.Add(accent);

            cardTitle = new Label();
            cardTitle.AddToClassList("insight__title");
            card.Add(cardTitle);

            cardBody = new Label();
            cardBody.AddToClassList("insight__body");
            card.Add(cardBody);
        }
    }
}
