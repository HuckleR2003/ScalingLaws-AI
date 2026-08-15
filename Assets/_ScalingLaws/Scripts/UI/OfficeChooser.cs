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
        private readonly Func<OfficeTier, string> tryMove;
        private readonly Action closed;

        // Nullable rather than a None member, because OfficeTier values live in saves
        // and Garage is legitimately zero.
        private OfficeTier? armed;
        private string problem = string.Empty;

        public OfficeChooser(Func<CompanyState> state, Func<OfficeTier, string> tryMove, Action closed)
        {
            this.state = state;
            this.tryMove = tryMove;
            this.closed = closed;

            Root = new VisualElement();
            Root.AddToClassList("offices");
        }

        public VisualElement Root { get; }

        public void Refresh()
        {
            Root.Clear();
            var company = state();

            var head = new VisualElement();
            head.AddToClassList("offices__head");

            var left = new VisualElement();

            var kicker = new Label("PREMISES");
            kicker.AddToClassList("offices__kicker");
            left.Add(kicker);

            var title = new Label("Where the company is");
            title.AddToClassList("offices__title");
            left.Add(title);

            var strap = new Label(
                $"{company.Staff.Headcount} of {company.Staff.OfficeDefinition.Desks} desks taken. "
                + "Rent is paid whether or not they are full, and desks are what caps hiring.");

            strap.AddToClassList("offices__strap");
            left.Add(strap);
            head.Add(left);

            var close = new Button(closed) { text = "CLOSE" };
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

            var note = new Label("More places are being built. A tier with nowhere to move into is "
                + "not offered here, because moving to nowhere is not a decision.");

            note.AddToClassList("field__hint");
            Root.Add(note);
        }

        private VisualElement BuildRow(OfficeDefinition place, CompanyState company)
        {
            var here = company.Staff.Office == place.Tier;
            var affordable = company.CashUsd >= place.FitOutCostUsd
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

            var kicker = new Label(here ? $"LVL {place.Level}   ·   YOU ARE HERE" : $"LVL {place.Level}");
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
            figures.Add(Figure("RENT", $"{UiFormat.Money(place.MonthlyRentUsd)} / mo"));
            figures.Add(Figure("DESKS", place.Desks == 0 ? "none" : place.Desks.ToString()));
            figures.Add(Figure("FIT-OUT", place.FitOutCostUsd == 0
                ? "nothing"
                : UiFormat.Money(place.FitOutCostUsd)));

            body.Add(figures);

            body.Add(BuildAction(place, company, here, affordable, openYet));
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
                var pending = new Label("PHOTOGRAPH\nOF THIS PLACE\nGOES HERE");
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

        private VisualElement BuildAction(OfficeDefinition place, CompanyState company, bool here,
            bool affordable, bool openYet)
        {
            if (here)
            {
                // The kicker already says it. This line says what it costs to stay, which is the
                // number the other rows are being compared against.
                var label = new Label(
                    $"{UiFormat.Money(place.DailyRentUsd)} a day, whatever happens.");

                label.AddToClassList("office-row__here");
                return label;
            }

            if (!openYet)
            {
                var label = new Label($"Not available until {place.EarliestDate}.");
                label.AddToClassList("office-row__blocked");
                return label;
            }

            var isArmed = armed == place.Tier;

            var move = new Button(() => Move(place.Tier))
            {
                text = isArmed
                    ? "CONFIRM THE MOVE"
                    : $"MOVE HERE   {UiFormat.Money(place.FitOutCostUsd)} TO FIT OUT"
            };

            move.AddToClassList("office-row__move");
            move.EnableInClassList("office-row__move--armed", isArmed);
            move.SetEnabled(affordable);

            if (!affordable)
            {
                move.text = place.RequiredCashUsd > company.CashUsd
                    ? $"NEEDS {UiFormat.Money(place.RequiredCashUsd)} IN THE BANK"
                    : $"NEEDS {UiFormat.Money(place.FitOutCostUsd)} TO FIT OUT";
            }

            return move;
        }

        /// <summary>
        /// Two clicks, because a move costs a fit out that is not refunded and changes the rent for
        /// the rest of the campaign.
        /// </summary>
        public void Move(OfficeTier tier)
        {
            if (armed != tier)
            {
                armed = tier;
                problem = string.Empty;
                Refresh();
                return;
            }

            armed = null;
            problem = tryMove(tier) ?? string.Empty;
            Refresh();
        }
    }
}
