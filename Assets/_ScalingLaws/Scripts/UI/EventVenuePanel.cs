using System;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What the venue sells, once the player has chosen a stand size and asked.
    ///
    /// **Handed in rather than computed here, and the panel is a view.** Every figure on this
    /// screen is read: the free stand count is a function of the venue's size and who has already
    /// registered, and it belongs in `Simulation/` beside the auction that uses it. A panel that
    /// worked out its own availability would be a second answer to a question the rules also
    /// answer, which is how four copies of one heat threshold ended up disagreeing about the same
    /// cabinet in this project.
    /// </summary>
    public readonly struct EventStandInfo
    {
        public EventStandInfo(int freeStands, int totalStands, int interestedRivals)
        {
            TotalStands = Math.Max(1, totalStands);
            FreeStands = Math.Clamp(freeStands, 0, TotalStands);
            InterestedRivals = Math.Max(0, interestedRivals);
        }

        public int FreeStands { get; }
        public int TotalStands { get; }

        /// <summary>
        /// How many labs want one, derived from how their models are selling.
        ///
        /// Not a budget. `Docs/EVENTS_PLAN.md` settles why: a bidder needs a willingness, which the
        /// game can read, rather than a bank balance, which it cannot without inventing two dozen
        /// uncheckable numbers per company.
        /// </summary>
        public int InterestedRivals { get; }

        /// <summary>True when the stands are gone and the last one goes to auction.</summary>
        public bool GoesToAuction => FreeStands <= 0;
    }

    /// <summary>
    /// One event venue, opened from a news story's location tag or from the map.
    ///
    /// The layout is the author's, in his order: a panoramic banner, the name large and heavy, the
    /// organiser in cyan under it, then the crowd and the regard side by side, then the three stand
    /// sizes side by side, then one button at the foot.
    ///
    /// **The button is grey until a size is picked and then strongly orange**, and pressing it
    /// thinks for about a second before it answers. That pause is not decoration: the free stand
    /// count and the state of the field are what the EVENT HUNTER desk sells for twenty thousand a
    /// month, and revealing them the instant the window opens would make the paid desk worthless.
    /// </summary>
    public sealed class EventVenuePanel
    {
        /// <summary>How long the three dots run before the answer appears.</summary>
        public const long ThinkingMilliseconds = 1000L;

        private readonly EventVenueDefinition venue;
        private readonly CityEventDefinition happening;
        private readonly Func<StandTier, EventStandInfo> ask;

        /// <summary>What a stand costs today: list, or what the room has run it up to.</summary>
        private readonly Func<StandTier, long> priceOf;

        /// <summary>Take it. Null leaves the card a reading, which is what a render wants.</summary>
        private readonly Action<StandTier> book;

        private readonly VisualElement tileRow = new();
        private readonly VisualElement footer = new();

        private StandTier? picked;
        private bool revealed;
        private IVisualElementScheduledItem thinking;

        public EventVenuePanel(EventVenueDefinition venue, CityEventDefinition happening,
            Func<StandTier, EventStandInfo> ask, Func<StandTier, long> priceOf = null,
            Action<StandTier> book = null)
        {
            this.venue = venue;
            this.happening = happening;
            this.ask = ask;
            this.priceOf = priceOf;
            this.book = book;

            Root = new VisualElement();
            Root.AddToClassList("venue");

            Build();
        }

        public VisualElement Root { get; }

        /// <summary>
        /// Which size is selected, or null.
        ///
        /// **A seam, for the reason `ManagementScreen.ShowDesk` and `PartsShop.Order` are seams.**
        /// An EditMode element has no panel, so a click sent to a button is never dispatched and
        /// every lambda on this screen would go unmeasured. A render that cannot select a tier can
        /// only ever photograph the grey button.
        /// </summary>
        public StandTier? Picked => picked;

        public bool HasRevealed => revealed;

        /// <summary>Pick a stand size, as clicking its tile does.</summary>
        public void Pick(StandTier tier)
        {
            picked = tier;
            revealed = false;

            thinking?.Pause();
            thinking = null;

            RefreshTiles();
            RefreshFooter();
        }

        /// <summary>
        /// Ask, and answer immediately rather than after the pause.
        ///
        /// The pause is a `schedule.Execute`, which never runs without a panel, so a test or a
        /// render that went through the button would wait for ever and photograph three dots.
        /// </summary>
        public void RevealNow()
        {
            if (picked == null)
            {
                return;
            }

            thinking?.Pause();
            thinking = null;
            revealed = true;

            RefreshFooter();
        }

        private void Build()
        {
            // ---- the panorama, which does not exist yet and says so ---------------------------
            //
            // Every loader in this project falls back to a plate rather than failing, because an
            // absent file is invisible until somebody looks and `Docs/NeededGraphics.md` is
            // generated by walking Resources.Load calls against what is on disk.
            var banner = new VisualElement();
            banner.AddToClassList("venue__banner");

            var art = Resources.Load<Texture2D>("Venues/" + venue.Id.ToString().ToLowerInvariant());

            if (art != null)
            {
                banner.style.backgroundImage = new StyleBackground(art);
            }
            else
            {
                var waiting = new Label(Loc.T("venue.art_soon"));
                waiting.AddToClassList("venue__banner-waiting");
                banner.Add(waiting);
            }

            Root.Add(banner);

            // ---- name, organiser ---------------------------------------------------------------
            var name = new Label(venue.DisplayName.ToUpperInvariant());
            name.AddToClassList("venue__name");
            Root.Add(name);

            var organiser = new Label(venue.OrganiserName.ToUpperInvariant());
            organiser.AddToClassList("venue__organiser");
            Root.Add(organiser);

            var what = new Label(happening.DisplayName + ". " + venue.Blurb);
            what.AddToClassList("venue__blurb");
            Root.Add(what);

            // ---- the crowd and the regard, on one line ------------------------------------------
            var figures = new VisualElement();
            figures.AddToClassList("venue__figures");

            figures.Add(Figure(Loc.T("venue.visitors"),
                UiFormat.Count(venue.AverageVisitors), Loc.T("venue.visitors.note"), null));

            figures.Add(Figure(Loc.T("venue.prestige"),
                UiFormat.Number(venue.Prestige, 2), Loc.T("venue.prestige.note"), venue.Prestige));

            Root.Add(figures);

            // ---- the three sizes, on one line ---------------------------------------------------
            var standsHead = new Label(Loc.T("venue.stands"));
            standsHead.AddToClassList("venue__section");
            Root.Add(standsHead);

            tileRow.AddToClassList("venue__tiles");
            Root.Add(tileRow);
            RefreshTiles();

            footer.AddToClassList("venue__footer");
            Root.Add(footer);
            RefreshFooter();
        }

        private VisualElement Figure(string caption, string value, string note, double? bar)
        {
            var block = new VisualElement();
            block.AddToClassList("venue__figure");

            var head = new Label(caption);
            head.AddToClassList("venue__figure-caption");
            block.Add(head);

            var reading = new Label(value);
            reading.AddToClassList("venue__figure-value");
            block.Add(reading);

            if (bar != null)
            {
                var track = new VisualElement();
                track.AddToClassList("venue__bar");

                var fill = new VisualElement();
                fill.AddToClassList("venue__bar-fill");
                fill.style.width = Length.Percent((float)(Math.Clamp(bar.Value, 0.0, 1.0) * 100.0));
                track.Add(fill);

                block.Add(track);
            }

            var words = new Label(note);
            words.AddToClassList("venue__figure-note");
            block.Add(words);

            return block;
        }

        private void RefreshTiles()
        {
            tileRow.Clear();

            foreach (var tier in new[] { StandTier.Small, StandTier.Medium, StandTier.Large })
            {
                var captured = tier;

                var tile = new Button(() => Pick(captured));
                tile.AddToClassList("venue__tile");
                tile.EnableInClassList("venue__tile--on", picked == tier);

                var picture = new VisualElement();
                picture.AddToClassList("venue__tile-art");

                var art = Resources.Load<Texture2D>("Venues/stand_"
                    + tier.ToString().ToLowerInvariant());

                if (art != null)
                {
                    picture.style.backgroundImage = new StyleBackground(art);
                }

                tile.Add(picture);

                var roman = new Label(tier switch
                {
                    StandTier.Small => "I",
                    StandTier.Medium => "II",
                    _ => "III"
                });
                roman.AddToClassList("venue__tile-roman");
                tile.Add(roman);

                var title = new Label(EventVenueCatalog.TierName(tier));
                title.AddToClassList("venue__tile-title");
                tile.Add(title);

                var effect = new Label(Loc.T("venue.tile_effect",
                    UiFormat.Number(EventVenueCatalog.EffectMultiple(tier), 2)));
                effect.AddToClassList("venue__tile-line");
                tile.Add(effect);

                var price = new Label(UiFormat.Money(
                    EventVenueCatalog.StandCostUsd(venue.Id, tier)));
                price.AddToClassList("venue__tile-price");
                tile.Add(price);

                var note = new Label(EventVenueCatalog.TierNote(tier));
                note.AddToClassList("venue__tile-note");
                tile.Add(note);

                tileRow.Add(tile);
            }
        }

        private void RefreshFooter()
        {
            footer.Clear();

            if (revealed && picked != null)
            {
                var info = ask(picked.Value);

                var answer = new VisualElement();
                answer.AddToClassList("venue__answer");

                var free = new Label(info.GoesToAuction
                    ? Loc.T("venue.contested")
                    : Loc.T("venue.free_stands", info.FreeStands.ToString(),
                        info.TotalStands.ToString()));

                free.AddToClassList("venue__answer-head");
                free.EnableInClassList("venue__answer-head--auction", info.GoesToAuction);
                answer.Add(free);

                var rivals = new Label(Loc.T("venue.rivals_interested",
                    info.InterestedRivals.ToString()));
                rivals.AddToClassList("venue__answer-line");
                answer.Add(rivals);

                footer.Add(answer);

                // **The price is on the button, which is this project's own rule.** A card is a
                // card and the purchase is a button on it with the figure printed on the
                // button: sixty four accelerators once left an account because the whole card
                // in the room's silicon rail was clickable, including the figures somebody was
                // reading.
                if (book == null || priceOf == null)
                {
                    return;
                }

                var tier = picked.Value;
                var price = priceOf(tier);

                var take = new Button(() => book(tier))
                {
                    text = info.GoesToAuction
                        ? Loc.T("venue.bid", UiFormat.Money(price))
                        : Loc.T("venue.book", UiFormat.Money(price))
                };

                take.AddToClassList("venue__ask");
                take.AddToClassList("venue__ask--ready");
                take.AddToClassList("venue__take");
                footer.Add(take);

                return;
            }

            // **Grey and still clickable, never disabled.** A disabled element in UI Toolkit
            // dispatches no pointer event, so it could not say that a size has to be picked first,
            // and this project has shipped that silence eleven times.
            var ask_ = new Button(() =>
            {
                if (picked == null)
                {
                    AudioDirector.Deny();

                    GateNotice.Says(Loc.T("venue.getinfo"), Loc.T("venue.pickfirst"));

                    return;
                }

                AudioDirector.Play(UiSound.Confirm);
                StartThinking();
            })
            {
                text = Loc.T("venue.getinfo")
            };

            ask_.AddToClassList("venue__ask");
            ask_.EnableInClassList("venue__ask--ready", picked != null);
            ask_.EnableInClassList("button--shut", picked == null);

            footer.Add(ask_);
        }

        private void StartThinking()
        {
            footer.Clear();

            var dots = new VisualElement();
            dots.AddToClassList("venue__dots");

            // **The three start times are in the stylesheet, not here.** One `@keyframes` with
            // three delays, the same way the regulatory banner's flash is written, so the dots
            // cannot drift apart and nothing about the animation lives in two places.
            //
            // **Written out rather than built from the index**, because a class name assembled by
            // concatenation is invisible to `StylesheetTests.EveryClassTheInterfaceUsesIsStyled`,
            // which reads literals. The first version of this loop used `"venue__dot--" + index`
            // and that guard caught it, which is the same rule this project already records for
            // phrase-book keys.
            foreach (var delay in new[] { "venue__dot--1", "venue__dot--2", "venue__dot--3" })
            {
                var dot = new VisualElement();
                dot.AddToClassList("venue__dot");
                dot.AddToClassList(delay);
                dots.Add(dot);
            }

            footer.Add(dots);

            thinking = Root.schedule.Execute(RevealNow).StartingIn(ThinkingMilliseconds);
        }
    }
}
