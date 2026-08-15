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

            var title = new Label("WHERE THE COMPANY IS");
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

            // ---- the left three thirds: the bar, then the figures ---------------------------

            var body = new VisualElement();
            body.AddToClassList("office-row__body");

            var bar = new VisualElement();
            bar.AddToClassList("office-bar");

            var name = new Label($"LVL {place.Level}   -   {place.DisplayName.ToUpperInvariant()}");
            name.AddToClassList("office-bar__name");
            bar.Add(name);

            // The diagonal at the end of the title bar. USS has no skew, so it is a square turned
            // forty five degrees and clipped by the bar's own overflow, which is the same trick the
            // hosting switch uses for its slant.
            var wedge = new VisualElement();
            wedge.AddToClassList("office-bar__wedge");
            bar.Add(wedge);

            body.Add(bar);

            var rent = new Label($"KOSZT WYNAJMU: {UiFormat.Money(place.MonthlyRentUsd)} / MIESIĄC");
            rent.AddToClassList("office-row__figure");
            body.Add(rent);

            var desks = new Label($"ILOŚĆ STANOWISK: {place.Desks}");
            desks.AddToClassList("office-row__figure");
            body.Add(desks);

            var blurb = new Label(place.Description);
            blurb.AddToClassList("office-row__blurb");
            body.Add(blurb);

            body.Add(BuildAction(place, company, here, affordable, openYet));
            row.Add(body);

            // ---- the right third: the place itself -------------------------------------------

            var photo = new VisualElement();
            photo.AddToClassList("office-row__photo");

            var art = Resources.Load<Texture2D>("Offices/" + place.Art);
            if (art != null)
            {
                photo.style.backgroundImage = new StyleBackground(art);
            }
            else
            {
                var pending = new Label(place.Level == 0
                    ? "PHOTOGRAPH OF THE GARAGE GOES HERE"
                    : "PHOTOGRAPH OF THIS PLACE GOES HERE");

                pending.AddToClassList("office-row__pending");
                photo.Add(pending);
            }

            row.Add(photo);
            return row;
        }

        private VisualElement BuildAction(OfficeDefinition place, CompanyState company, bool here,
            bool affordable, bool openYet)
        {
            if (here)
            {
                var label = new Label("YOU ARE HERE");
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
