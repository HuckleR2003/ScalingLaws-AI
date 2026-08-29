using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What the company owes a funding body, in the corner of the office.
    ///
    /// **Under the wire, in the same column, and that is a layout decision rather than a
    /// coordinate.** Both banners used to be positioned absolutely against the window, so keeping
    /// them apart meant guessing the height of whichever one was taller and the guess was wrong the
    /// moment a headline wrapped to three lines. They share a column now, so overlapping is not
    /// something that can happen.
    ///
    /// It is a button whether or not anything is held. With no award it says so and opens the board,
    /// because a player who has never seen a grant offer has no reason to know the screen exists.
    /// </summary>
    public sealed class GrantBanner
    {
        /// <summary>The most awards drawn. The simulation caps holdings at the same number.</summary>
        public const int MostShown = GrantCatalog.MostHeldAtOnce;

        private readonly Func<CompanySimulation> company;

        public GrantBanner(Func<CompanySimulation> company, Action open)
        {
            this.company = company;

            Root = new Button(() => open?.Invoke());
            Root.AddToClassList("gb");
        }

        public Button Root { get; }

        public void SetHidden(bool hidden) =>
            Root.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;

        public void Refresh()
        {
            Root.Clear();

            var simulation = company();
            var held = simulation?.HeldGrants() ?? (IReadOnlyList<Grant>)Array.Empty<Grant>();

            var head = new VisualElement();
            head.AddToClassList("gb__head");

            var title = new Label(Loc.T("grant.banner"));
            title.AddToClassList("gb__masthead");
            head.Add(title);

            var count = new Label(held.Count > 0
                ? $"{held.Count}/{MostShown}"
                : Loc.T("grant.banner.tier", simulation?.GrantTierReached() ?? 1));

            count.AddToClassList("gb__count");
            head.Add(count);

            Root.Add(head);

            var body = new VisualElement();
            body.AddToClassList("gb__body");

            if (held.Count == 0)
            {
                var none = new Label(Loc.T("grant.banner.none"));
                none.AddToClassList("gb__none");
                body.Add(none);
            }
            else
            {
                foreach (var grant in held)
                {
                    body.Add(BuildRow(simulation, grant));
                }
            }

            Root.Add(body);
        }

        /// <summary>
        /// One award: what it is, how long is left, and whether it is already lost.
        ///
        /// The days are the headline rather than the progress, for the same reason `WorkInFlight`
        /// leads on days left: that is the number a player plans around.
        /// </summary>
        private static VisualElement BuildRow(CompanySimulation simulation, Grant grant)
        {
            var definition = grant.Definition;

            var row = new VisualElement();
            row.AddToClassList("gb__row");
            row.EnableInClassList("gb__row--broken", grant.IsBroken);

            var name = new Label(Loc.T(definition.NameKey));
            name.AddToClassList("gb__name");
            row.Add(name);

            var status = new Label(grant.IsBroken
                ? Loc.T("grant.broken")
                : Loc.T("grant.days_left", grant.DaysLeft));

            status.AddToClassList("gb__status");
            status.EnableInClassList("gb__status--broken", grant.IsBroken);
            row.Add(status);

            var track = new VisualElement();
            track.AddToClassList("gb__track");

            var fill = new VisualElement();
            fill.AddToClassList("gb__fill");
            fill.style.width = Length.Percent((float)(grant.Progress * 100.0));
            track.Add(fill);

            row.Add(track);
            return row;
        }
    }
}
