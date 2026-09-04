using ScalingLaws.Data;
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

        /// <summary>Width of the long form. Kept in step with `.insight--wide` by hand.</summary>
        public const float WideWidth = 400f;

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
        private static Label cardWhat;
        private static Label cardAffects;
        private static VisualElement highBand;
        private static Label highBody;
        private static VisualElement lowBand;
        private static Label lowBody;

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

            target.RegisterCallback<MouseEnterEvent>(_ =>
                Show(target, title, body, default, placement));

            target.RegisterCallback<MouseLeaveEvent>(_ => HideFor(target));
            target.RegisterCallback<DetachFromPanelEvent>(_ => HideFor(target));
        }

        /// <summary>
        /// The same card, resolved when the cursor arrives rather than when the control was built.
        ///
        /// **Copy captured at build time freezes in the language `Build` ran in.** That is now the
        /// shape of nine separate faults in this project: the tutorial line, the guide's name plate,
        /// the cold open, and the bottom bar's own labels. A card holding phrase-book keys instead
        /// of sentences cannot go stale, and it needs no retexting pass of its own.
        ///
        /// Both arguments are keys. A missing key renders as itself rather than throwing, and
        /// `LocalisationTests` can only read literals, so passing one here keeps it visible to that
        /// guard.
        /// </summary>
        public static void AttachKeyed(VisualElement target, string titleKey, string bodyKey,
            Placement placement = Placement.Above)
        {
            if (target == null || string.IsNullOrEmpty(titleKey))
            {
                return;
            }

            target.tooltip = string.Empty;

            target.RegisterCallback<MouseEnterEvent>(_ => Show(target, Loc.T(titleKey),
                string.IsNullOrEmpty(bodyKey) ? string.Empty : Loc.T(bodyKey), default, placement));

            target.RegisterCallback<MouseLeaveEvent>(_ => HideFor(target));
            target.RegisterCallback<DetachFromPanelEvent>(_ => HideFor(target));
        }

        /// <summary>
        /// The long form: what a control is, what it honestly moves, and what each end of it buys.
        ///
        /// **Two ends, because a slider has two and every explanation in this game had only one.**
        /// A sentence saying "sparsity lowers what a run costs" tells the player to push it right
        /// and nothing else, so the control is not a decision, it is a chore. Naming what the low
        /// end buys is what turns it back into one.
        ///
        /// <see cref="Affects"/> is the honest line and it is separate from <see cref="What"/> on
        /// purpose. What a technique *is* reads like a brochure; what it actually touches in this
        /// simulation is the thing a player needs and the thing a brochure never says.
        /// </summary>
        public readonly struct Reading
        {
            public Reading(string what, string affects, string high, string low)
            {
                What = what ?? string.Empty;
                Affects = affects ?? string.Empty;
                High = high ?? string.Empty;
                Low = low ?? string.Empty;
            }

            /// <summary>What the technology is, in plain words.</summary>
            public string What { get; }

            /// <summary>What it moves in the simulation. No flattery.</summary>
            public string Affects { get; }

            /// <summary>What a high setting gives, and what it costs.</summary>
            public string High { get; }

            /// <summary>What a low setting gives, and what it costs.</summary>
            public string Low { get; }

            /// <summary>
            /// Nothing to say, so the card is drawn short.
            ///
            /// **Reads the fields defensively, because a struct's constructor is not a guarantee.**
            /// The constructor above coalesces every null to an empty string and `default(Reading)`
            /// never runs it, so the four properties are null on exactly the value `AttachKeyed`
            /// passes for a short card. Every hover on a keyed tooltip threw here.
            ///
            /// The clamp-in-constructors rule this project follows protects a value somebody
            /// constructed. It says nothing about the one the language hands out for free.
            /// </summary>
            public bool IsEmpty => string.IsNullOrEmpty(What) && string.IsNullOrEmpty(Affects)
                && string.IsNullOrEmpty(High) && string.IsNullOrEmpty(Low);
        }

        /// <summary>Attaches a long-form card to any control.</summary>
        public static void Attach(VisualElement target, string title, Reading reading,
            Placement placement = Placement.Above)
        {
            if (target == null || string.IsNullOrEmpty(title))
            {
                return;
            }

            target.tooltip = string.Empty;

            target.RegisterCallback<MouseEnterEvent>(_ => Show(target, title, null, reading, placement));
            target.RegisterCallback<MouseLeaveEvent>(_ => HideFor(target));
            target.RegisterCallback<DetachFromPanelEvent>(_ => HideFor(target));
        }

        /// <summary>
        /// The little "(i)" the player reaches for, already wired to its own card.
        ///
        /// Returned rather than mounted, so the caller decides where in its row it belongs. It is a
        /// Button so it is reachable by click as well as by hover: on a laptop trackpad, resting a
        /// pointer precisely on a twenty pixel circle to read a paragraph is a worse experience than
        /// tapping it, and the click keeps the card up until the pointer leaves.
        /// </summary>
        public static Button InfoBadge(string title, Reading reading,
            Placement placement = Placement.Above)
        {
            var badge = new Button { text = "i" };
            badge.AddToClassList("infodot");

            // Clicking re-opens the same card the hover shows, so the two cannot say different
            // things. There is one Show and one payload.
            badge.clicked += () => Show(badge, title, null, reading, placement);
            Attach(badge, title, reading, placement);

            // The payload rides on the element. A badge whose card is empty renders as a heading
            // over nothing, and that is indistinguishable from a rendering fault unless something
            // can read what the badge is holding without opening it.
            badge.userData = reading;

            return badge;
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

        private static void Show(VisualElement target, string title, string body, Reading reading,
            Placement placement)
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

            // The long form widens the card. A four section reading at 268px is a column of two
            // word lines, which is harder to read than the paragraph it replaced.
            card.EnableInClassList("insight--wide", !reading.IsEmpty);

            Section(cardWhat, reading.What);
            Section(cardAffects, reading.Affects);
            Band(highBand, highBody, reading.High);
            Band(lowBand, lowBody, reading.Low);

            owner = target;
            host.Add(card);
            Place(host, target, placement, reading.IsEmpty ? Width : WideWidth);

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
        private static void Place(VisualElement host, VisualElement target, Placement placement,
            float cardWidth)
        {
            var bounds = target.worldBound;
            var width = host.resolvedStyle.width;
            var height = host.resolvedStyle.height;

            // Cleared every time, because a card that was flipped below once would keep its `top`
            // and fight the `bottom` set by the next control it opens for.
            card.style.top = StyleKeyword.Auto;

            if (placement == Placement.LeftOf)
            {
                card.style.left = StyleKeyword.Auto;
                card.style.right = width - bounds.xMin + Gap;
                card.style.bottom = height - bounds.yMax;
                return;
            }

            // Centred on the control, then held inside the window: the first and last slots in the
            // bottom bar are close enough to the edges to push a card off the screen.
            //
            // **Measured against the card's own width, not the narrow one.** The long form is 400px
            // and the clamp used the 268 constant for both, so a badge in the right-hand third
            // would have hung a third of the card off the edge of the screen.
            var left = bounds.center.x - cardWidth / 2f;
            left = Mathf.Clamp(left, 12f, Mathf.Max(12f, width - cardWidth - 12f));

            card.style.right = StyleKeyword.Auto;
            card.style.left = left;
            card.style.bottom = height - bounds.yMin + Gap;

            KeepInsideTheWindow(bounds, height);
        }

        /// <summary>
        /// Flips the card below its control when there is not room above.
        ///
        /// **A card above a control near the top of the window opens off the screen.** The
        /// horizontal clamp has been here since the bottom bar pushed one off the side; the same
        /// thing happens vertically on every panel heading in the top band, and there the card does
        /// not merely shift, it is drawn entirely outside the panel and reads as a badge that does
        /// nothing.
        ///
        /// Deferred a frame on purpose: the card has just been filled and has no measured height
        /// until it has been laid out, so deciding this immediately would decide it against zero.
        /// </summary>
        private static void KeepInsideTheWindow(Rect bounds, float height)
        {
            var placing = card;

            placing.schedule.Execute(() =>
            {
                if (placing == null || placing.parent == null || placing != card)
                {
                    return;
                }

                var box = placing.worldBound;

                if (box.height <= 0f || box.yMin >= Gap)
                {
                    return;
                }

                // Below instead, anchored by its own top edge so it grows downward away from the
                // control, which is the mirror of what the default placement does upward.
                placing.style.bottom = StyleKeyword.Auto;
                placing.style.top = Mathf.Min(bounds.yMax + Gap, Mathf.Max(Gap, height - box.height - Gap));
            }).ExecuteLater(1);
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

            cardWhat = new Label();
            cardWhat.AddToClassList("insight__what");
            card.Add(cardWhat);

            // "What it actually moves", set apart from the description above it. The two say
            // different kinds of thing and running them together buries the useful one.
            cardAffects = new Label();
            cardAffects.AddToClassList("insight__affects");
            card.Add(cardAffects);

            (highBand, highBody) = BuildBand("HIGH VALUE", "insight__band--high");
            card.Add(highBand);

            (lowBand, lowBody) = BuildBand("LOW VALUE", "insight__band--low");
            card.Add(lowBand);
        }

        private static (VisualElement Band, Label Body) BuildBand(string heading, string tone)
        {
            var band = new VisualElement();
            band.AddToClassList("insight__band");
            band.AddToClassList(tone);

            var head = new Label(heading);
            head.AddToClassList("insight__bandhead");
            band.Add(head);

            var body = new Label();
            body.AddToClassList("insight__bandbody");
            band.Add(body);

            return (band, body);
        }

        /// <summary>Fills a line, or takes it out of the layout entirely when there is nothing to say.</summary>
        private static void Section(Label label, string text)
        {
            label.text = text;
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static void Band(VisualElement band, Label body, string text)
        {
            body.text = text;
            band.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
