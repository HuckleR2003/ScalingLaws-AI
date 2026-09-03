using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Where the company is, and where it could be instead.
    ///
    /// **This is the first piece of the second map.** An office used to be a desk count with a rent
    /// on it, chosen from a card in a grid alongside the hiring cards. Each tier is turning into
    /// somewhere the company physically is, so the chooser is a row of places with photographs of
    /// them, and a tier with no place built yet is not offered rather than being offered as a grey
    /// square. `OfficeCatalog.Places` is the line between the two.
    ///
    /// The layout follows the author's mock: a wide row per place, the name in a bar across the top
    /// with a diagonal cut at its end, rent and desks underneath it, and the photograph filling the
    /// right third.
    /// </summary>
    public sealed class OfficeChooser
    {
        private readonly Func<CompanyState> state;
        private readonly Func<OfficeTier, bool, string> tryMove;
        private readonly Func<OfficeTier, bool, string> tryBuy;
        private readonly Action closed;

        // Nullable rather than a None member, because OfficeTier values live in saves
        // and Garage is legitimately zero.
        //
        // **One card rather than two armed buttons.** Both actions used to arm on the first click
        // and fire on the second, which tells the player to press again without telling them what
        // they are about to spend. The card names the rent, the fit-out, the desks and the price
        // to own it, which is the comparison this screen exists to put side by side.
        private OfficeTier? deal;
        private string problem = string.Empty;

        public OfficeChooser(Func<CompanyState> state, Func<OfficeTier, bool, string> tryMove,
            Action closed, Func<OfficeTier, bool, string> tryBuy = null)
        {
            this.state = state;
            this.tryMove = tryMove;
            this.tryBuy = tryBuy ?? ((_, _) => Loc.T("offices.buy_unwired"));
            this.closed = closed;

            Root = new VisualElement();
            Root.AddToClassList("offices");
        }

        public VisualElement Root { get; }

        /// <summary>
        /// Whether a move arrives with the place already furnished.
        ///
        /// **On by default**, because the pack is cheaper than the same pieces bought one at a time
        /// and an empty floor is the unusual choice, not the normal one. Public so a test can drive
        /// the move without a panel: an EditMode test never dispatches a click, so a toggle that
        /// could only be read off the control would make both paths through here untestable.
        /// </summary>
        public bool Furnished { get; set; } = true;

        /// <summary>Whether this place has floor to put anything on.</summary>
        private static bool CanBeFurnished(OfficeTier tier) =>
            RoomCatalog.For(tier).AllowsFurniture;

        /// <summary>What furnishing adds to a move into this place, which is nothing when it cannot.</summary>
        private long FurnishingOn(OfficeTier tier) =>
            Furnished && CanBeFurnished(tier) ? OfficeCatalog.FurnishedPackUsd : 0L;

        public void Refresh()
        {
            Root.Clear();
            var company = state();

            var head = new VisualElement();
            head.AddToClassList("offices__head");

            var left = new VisualElement();

            var kicker = new Label(Loc.T("offices.premises"));
            kicker.AddToClassList("offices__kicker");
            left.Add(kicker);

            var title = new Label(Loc.T("offices.where_company"));
            title.AddToClassList("offices__title");
            left.Add(title);

            var strap = new Label(Loc.T("offices.strap",
                company.Staff.Headcount, company.Staff.OfficeDefinition.Desks));

            strap.AddToClassList("offices__strap");
            left.Add(strap);
            head.Add(left);

            left.Add(BuildFurnishedToggle());

            var close = new Button(closed) { text = Loc.T("common.close") };
            close.AddToClassList("chip");
            head.Add(close);

            Root.Add(head);

            // One hairline with a slice of the interface accent sitting on it, rather than a two
            // pixel coral rule the whole width of the page. The old header shouted at the row of
            // cream title bars underneath it and the screen had nowhere quiet to look.
            var rule = new VisualElement();
            rule.AddToClassList("offices__rule");

            var slice = new VisualElement();
            slice.AddToClassList("offices__rule-accent");
            HudAccent.PaintSlice(slice, 0.30f, 0.62f);
            rule.Add(slice);

            Root.Add(rule);

            if (problem.Length > 0)
            {
                var trouble = new Label(problem);
                trouble.AddToClassList("offices__problem");
                Root.Add(trouble);
            }

            foreach (var place in OfficeCatalog.Places())
            {
                Root.Add(BuildRow(place, company));
            }

            // Where the ladder goes next, drawn rather than described. The sentence that used to
            // sit here said more places were being built and named none of them, which is a
            // promise the player cannot plan against. It was also the last untranslated literal
            // on this page.
            foreach (var soon in OfficeCatalog.ComingSoon)
            {
                Root.Add(BuildAnnouncedRow(soon));
            }

            // Last, so it paints over the list. An absolutely positioned element in UI Toolkit
            // still paints in document order, which this project learned the hard way when the
            // server room's corner banner came out underneath the floor.
            if (deal.HasValue && OfficeCatalog.TryGet(deal.Value, out var open))
            {
                Root.Add(BuildDeal(open, company));
            }
        }

        /// <summary>
        /// What signing for a place actually costs, before signing for it.
        ///
        /// **Four numbers, because four numbers decide it**, and until now the screen showed two of
        /// them on two buttons that each had to be pressed twice. The rent runs for the rest of the
        /// campaign. The fit-out is never refunded. The desks are the only thing in the game that
        /// caps hiring. The purchase price is about ten years of the rent, which is the comparison
        /// the whole screen exists to put side by side.
        ///
        /// Both ways out are on the card and neither is the default. A dialog with one obvious
        /// button is a confirmation; this is a decision.
        /// </summary>
        private VisualElement BuildDeal(OfficeDefinition place, CompanyState company)
        {
            var here = company.Staff.Office == place.Tier;
            var fitOut = here ? 0L : place.FitOutCostUsd + FurnishingOn(place.Tier);
            var owed = place.PurchasePriceUsd + fitOut;

            var scrim = new VisualElement();
            scrim.AddToClassList("deal-scrim");

            // Clicking away closes it. A modal with only a small X is a modal people feel trapped
            // in, and there is nothing here that a stray click can spend.
            scrim.RegisterCallback<ClickEvent>(_ => Open(null));

            var card = new VisualElement();
            card.AddToClassList("deal");

            // The card swallows its own clicks, or pressing RENT would also hit the scrim behind it
            // and close the card before the button ran.
            card.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            var kicker = new Label(Loc.T("offices.deal_kicker"));
            kicker.AddToClassList("deal__kicker");
            card.Add(kicker);

            var name = new Label(place.DisplayName.ToUpperInvariant());
            name.AddToClassList("deal__name");
            card.Add(name);

            card.Add(DealRow(Loc.T("offices.deal_rent"),
                Loc.T("offices.deal_a_month", UiFormat.Money(place.MonthlyRentUsd))));

            card.Add(DealRow(Loc.T("offices.deal_fitout"),
                here ? Loc.T("offices.deal_none") : UiFormat.Money(fitOut)));

            card.Add(DealRow(Loc.T("offices.deal_desks"), place.Desks.ToString()));

            card.Add(DealRow(Loc.T("offices.deal_price"),
                place.CanBeBought ? UiFormat.Money(place.PurchasePriceUsd)
                    : Loc.T("offices.deal_not_for_sale")));

            var note = new Label(Loc.T("offices.deal_note"));
            note.AddToClassList("deal__note");
            card.Add(note);

            var buttons = new VisualElement();
            buttons.AddToClassList("deal__buttons");

            if (!here)
            {
                var rentable = company.CashUsd >= place.RequiredCashUsd
                    && company.CashUsd >= fitOut;

                var rent = new Button(() => Move(place.Tier))
                {
                    text = Loc.T("offices.deal_rent_it", UiFormat.Money(fitOut))
                };

                rent.AddToClassList("deal__button");
                rent.SetEnabled(rentable);
                buttons.Add(rent);
            }

            if (place.CanBeBought && !company.Staff.Owns(place.Tier))
            {
                var buy = new Button(() => Buy(place.Tier))
                {
                    text = Loc.T("offices.deal_buy_it", UiFormat.Money(owed))
                };

                buy.AddToClassList("deal__button");
                buy.AddToClassList("deal__button--buy");
                buy.SetEnabled(company.CashUsd >= owed);
                buttons.Add(buy);
            }

            var cancel = new Button(() => Open(null)) { text = Loc.T("common.not_now") };
            cancel.AddToClassList("deal__button");
            cancel.AddToClassList("deal__button--quiet");
            buttons.Add(cancel);

            card.Add(buttons);
            scrim.Add(card);
            return scrim;
        }

        private static VisualElement DealRow(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("deal__row");

            var caption = new Label(label);
            caption.AddToClassList("deal__label");
            row.Add(caption);

            var amount = new Label(value);
            amount.AddToClassList("deal__value");
            row.Add(amount);

            return row;
        }

        /// <summary>
        /// A place that is announced and not built.
        ///
        /// Deliberately flat: no price, no button, no hover. Everything that would make it look
        /// clickable is left off, because a row that reads as an option and refuses every click
        /// reads as a bug rather than as a plan.
        /// </summary>
        private static VisualElement BuildAnnouncedRow(OfficeCatalog.AnnouncedOffice soon)
        {
            var row = new VisualElement();
            row.AddToClassList("soonrow");

            var head = new VisualElement();
            head.AddToClassList("soonrow__head");

            var kicker = new Label(Loc.T("office.soon"));
            kicker.AddToClassList("soonrow__kicker");
            head.Add(kicker);

            var name = new Label(soon.DisplayName);
            name.AddToClassList("soonrow__name");
            head.Add(name);

            var desks = new Label(Loc.Counted(soon.Desks, "noun.desk"));
            desks.AddToClassList("soonrow__desks");
            head.Add(desks);

            row.Add(head);

            var note = new Label(soon.Note);
            note.AddToClassList("soonrow__note");
            row.Add(note);

            return row;
        }

        /// <summary>
        /// The one option that rides with every move on this page.
        ///
        /// It sits in the header rather than on each row because it is a preference about how you
        /// move, not a property of any one building, and eleven copies of the same tick is a page
        /// that looks like it is asking eleven questions.
        /// </summary>
        private VisualElement BuildFurnishedToggle()
        {
            var block = new VisualElement();
            block.AddToClassList("offices__furnish");

            block.Add(UiParts.Tick(Loc.T("offices.furnished"), Furnished, picked =>
            {
                Furnished = picked;
                Refresh();
            }));

            var saving = OfficeCatalog.FurnishedPackListUsd - OfficeCatalog.FurnishedPackUsd;

            var note = new Label(Loc.T("offices.furnish_note",
                UiFormat.Money(OfficeCatalog.FurnishedPackUsd),
                UiFormat.Money((long)OfficeCatalog.FurnishedPackListUsd),
                UiFormat.Money((long)saving)));

            note.AddToClassList("offices__furnishnote");
            block.Add(note);

            return block;
        }

        private VisualElement BuildRow(OfficeDefinition place, CompanyState company)
        {
            var here = company.Staff.Office == place.Tier;
            var moveBill = place.FitOutCostUsd + FurnishingOn(place.Tier);
            var affordable = company.CashUsd >= moveBill
                             && company.CashUsd >= place.RequiredCashUsd;

            var openYet = company.Date.IsOnOrAfter(place.EarliestDate);

            var row = new VisualElement();
            row.AddToClassList("office-row");
            row.EnableInClassList("office-row--here", here);

            // Where you are is marked by a lit edge rather than by a coral outline round the whole
            // card. One row in three with a full border on it made the page read as a warning.
            var edge = new VisualElement();
            edge.AddToClassList("office-row__edge");
            row.Add(edge);

            // ---- the left: what the place is, then what it costs -----------------------------

            var body = new VisualElement();
            body.AddToClassList("office-row__body");

            var kicker = new Label(here
                ? Loc.T("offices.level_here", place.Level)
                : Loc.T("offices.level", place.Level));
            kicker.AddToClassList("office-row__kicker");
            kicker.EnableInClassList("office-row__kicker--here", here);
            body.Add(kicker);

            var name = new Label(place.DisplayName);
            name.AddToClassList("office-row__name");
            body.Add(name);

            var blurb = new Label(place.Description);
            blurb.AddToClassList("office-row__blurb");
            body.Add(blurb);

            var figures = new VisualElement();
            figures.AddToClassList("office-row__figures");
            var owned = company.Staff.Owns(place.Tier);

            figures.Add(Figure(Loc.T("offices.rent"), owned
                ? Loc.T("offices.owned")
                : Loc.T("offices.per_month", UiFormat.Money(place.MonthlyRentUsd))));

            if (place.CanBeBought)
            {
                figures.Add(Figure(Loc.T("offices.to_buy"), owned
                    ? "yours"
                    : UiFormat.Money(place.PurchasePriceUsd)));
            }
            figures.Add(Figure(Loc.T("offices.desks"),
                place.Desks == 0 ? Loc.T("offices.none") : place.Desks.ToString()));
            figures.Add(Figure(Loc.T("offices.fitout"), moveBill == 0
                ? Loc.T("offices.nothing")
                : UiFormat.Money(moveBill)));

            body.Add(figures);

            body.Add(BuildActions(place, company, here, affordable, openYet));
            row.Add(body);

            // ---- the right: the place itself --------------------------------------------------

            var photo = new VisualElement();
            photo.AddToClassList("office-row__photo");

            // A place with no picture says so. It used to fall back to the office icon from the
            // bottom bar, which put a 64px interface glyph where a photograph of the house belongs
            // and made the first row of the screen look like a missing asset.
            var art = Resources.Load<Texture2D>("Offices/" + place.Art);
            if (art != null)
            {
                photo.style.backgroundImage = new StyleBackground(art);

                // A soft edge into the card rather than a two pixel cream rule between them.
                var seam = new VisualElement();
                seam.AddToClassList("office-row__seam");
                photo.Add(seam);
            }
            else
            {
                var pending = new Label(Loc.T("offices.photo_here"));
                pending.AddToClassList("office-row__pending");
                photo.Add(pending);
            }

            row.Add(photo);
            return row;
        }

        private static VisualElement Figure(string label, string value)
        {
            var figure = new VisualElement();
            figure.AddToClassList("office-figure");

            var caption = new Label(label);
            caption.AddToClassList("office-figure__label");
            figure.Add(caption);

            var amount = new Label(value);
            amount.AddToClassList("office-figure__value");
            figure.Add(amount);

            return figure;
        }

        /// <summary>
        /// The two ways in, side by side.
        ///
        /// **Renting and buying are different decisions and the screen has to say so.** A monthly
        /// bill is something a struggling company walks away from; a purchase is capital that never
        /// comes back and ends the rent forever. Putting them in one button with a toggle would hide
        /// exactly the comparison the player is here to make.
        /// </summary>
        private VisualElement BuildActions(OfficeDefinition place, CompanyState company, bool here,
            bool affordable, bool openYet)
        {
            var owned = company.Staff.Owns(place.Tier);

            if (!openYet)
            {
                var blocked = new Label(Loc.T("offices.not_until", place.EarliestDate));
                blocked.AddToClassList("office-row__blocked");
                return blocked;
            }

            var row = new VisualElement();
            row.AddToClassList("office-actions");

            if (here && owned)
            {
                var settled = new Label(Loc.T("offices.yours"));
                settled.AddToClassList("office-row__here");
                return settled;
            }

            if (!here)
            {
                row.Add(BuildAction(place, company, false, affordable, true));
            }
            else
            {
                var staying = new Label(
                    Loc.T("offices.a_day", UiFormat.Money(place.DailyRentUsd)));

                staying.AddToClassList("office-row__here");
                row.Add(staying);
            }

            if (place.CanBeBought && !owned)
            {
                row.Add(BuildBuy(place, company, here));
            }

            return row;
        }

        /// <summary>
        /// The purchase button. Two clicks like the move, because the money never comes back.
        /// </summary>
        private VisualElement BuildBuy(OfficeDefinition place, CompanyState company, bool here)
        {
            var owed = place.PurchasePriceUsd
                + (here ? 0L : place.FitOutCostUsd + FurnishingOn(place.Tier));
            var canAfford = company.CashUsd >= owed;

            var buy = new Button(() => Open(place.Tier))
            {
                text = (here ? Loc.T("offices.buy_here") : Loc.T("offices.buy_outright"))
                    + UiFormat.Money(owed)
            };

            buy.AddToClassList("office-row__move");
            buy.AddToClassList("office-row__buy");
            buy.SetEnabled(canAfford);

            if (!canAfford)
            {
                buy.text = Loc.T("offices.needs_to_buy", UiFormat.Money(owed));
            }

            return buy;
        }

        /// <summary>
        /// Opens the deal card for a place, or closes it when it is already open on that place.
        ///
        /// Public so a test can reach the card without a panel to click into: an EditMode test
        /// dispatches no pointer events, and a card that could only be opened by pressing a button
        /// would make both decisions on this screen untestable.
        /// </summary>
        public void Open(OfficeTier? tier)
        {
            deal = deal == tier ? null : tier;
            problem = string.Empty;
            Refresh();
        }

        /// <summary>Which place's deal card is open, or null when none is.</summary>
        public OfficeTier? OpenDeal => deal;

        /// <summary>Signs for the place outright. The card is the confirmation.</summary>
        public void Buy(OfficeTier tier)
        {
            deal = null;
            problem = tryBuy(tier, Furnished && CanBeFurnished(tier)) ?? string.Empty;
            Refresh();
        }

        private VisualElement BuildAction(OfficeDefinition place, CompanyState company, bool here,
            bool affordable, bool openYet)
        {
            if (here)
            {
                // The kicker already says it. This line says what it costs to stay, which is the
                // number the other rows are being compared against.
                var label = new Label(
                    Loc.T("offices.a_day", UiFormat.Money(place.DailyRentUsd)));

                label.AddToClassList("office-row__here");
                return label;
            }

            if (!openYet)
            {
                var label = new Label(Loc.T("offices.not_until", place.EarliestDate));
                label.AddToClassList("office-row__blocked");
                return label;
            }

            var bill = place.FitOutCostUsd + FurnishingOn(place.Tier);

            var move = new Button(() => Open(place.Tier))
            {
                text = Loc.T("offices.move_here", UiFormat.Money(bill))
            };

            move.AddToClassList("office-row__move");
            move.SetEnabled(affordable);

            if (!affordable)
            {
                move.text = place.RequiredCashUsd > company.CashUsd
                    ? Loc.T("offices.needs_in_bank", UiFormat.Money(place.RequiredCashUsd))
                    : Loc.T("offices.needs_fit_out", UiFormat.Money(bill));
            }

            return move;
        }

        /// <summary>
        /// Takes the place on rent. The card is the confirmation: it has already named the fit-out
        /// that is not refunded and the rent that runs for the rest of the campaign.
        /// </summary>
        public void Move(OfficeTier tier)
        {
            deal = null;
            problem = tryMove(tier, Furnished && CanBeFurnished(tier)) ?? string.Empty;
            Refresh();
        }
    }
}
