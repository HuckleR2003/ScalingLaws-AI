using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The year's corporation tax, in the corner, counting down.
    ///
    /// **The one bill in this game a player can plan for, and nothing outside the inbox said so.**
    /// The liability accrues all year, the demand lands on the second of January, and until now it
    /// sat among the letters with no clock on it anywhere. A company that missed it found out
    /// through a number quietly growing at thirty five per cent a year.
    ///
    /// Orange rather than the grants panel's own colour, and directly under it, because they are the
    /// two halves of one subject: what the state gives you and what it asks back.
    ///
    /// **It reads the letter rather than keeping its own copy.** `OutstandingTaxDemand` is a view
    /// over the inbox, so the banner cannot go on saying money is owed after it has been paid, which
    /// is what a second record of the same fact always ends up doing.
    /// </summary>
    public sealed class TaxBanner
    {
        /// <summary>Days left at which the strip starts reading as urgent.</summary>
        public const int UrgentWithin = 14;

        private readonly Func<CompanySimulation> company;

        public TaxBanner(Func<CompanySimulation> company, Action open)
        {
            this.company = company;

            Root = new Button(() => open?.Invoke());
            Root.AddToClassList("tb");
        }

        public Button Root { get; }

        /// <summary>Whether anything is owed. The shell asks before it shows the strip.</summary>
        public bool HasSomethingToSay => company()?.OutstandingTaxDemand() != null;

        public void SetHidden(bool hidden) =>
            Root.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;

        public void Refresh()
        {
            Root.Clear();

            var simulation = company();
            var letter = simulation?.OutstandingTaxDemand();

            if (letter == null)
            {
                SetHidden(true);
                return;
            }

            var today = simulation.State.Date;
            var left = letter.DaysLeft(today);

            Root.EnableInClassList("tb--urgent", left <= UrgentWithin);

            // The year the tax is for, not the year it is being paid in. A demand issued on the
            // second of January is about the twelve months that just ended, and printing the
            // current year on it would be wrong every time.
            var title = new Label(Loc.T("tax.banner", today.Year - 1));
            title.AddToClassList("tb__masthead");
            Root.Add(title);

            var amount = new Label(UiFormat.Money(letter.AmountUsd));
            amount.AddToClassList("tb__amount");
            Root.Add(amount);

            var clock = new Label(left <= 0
                ? Loc.T("tax.banner.today")
                : Loc.T("tax.banner.left", UiFormat.Days(left)));

            clock.AddToClassList("tb__clock");
            clock.EnableInClassList("tb__clock--urgent", left <= UrgentWithin);
            Root.Add(clock);

            var track = new VisualElement();
            track.AddToClassList("tb__track");

            var fill = new VisualElement();
            fill.AddToClassList("tb__fill");

            // How much of the grace has gone, so the bar fills up as the time runs out rather than
            // draining, which is the direction every other countdown in this game reads.
            var whole = Math.Max(1, letter.DueDayIndex - letter.Arrived.DayIndex);
            var gone = Math.Clamp((today.DayIndex - letter.Arrived.DayIndex) / (double)whole, 0.0, 1.0);

            fill.style.width = Length.Percent((float)(gone * 100.0));
            track.Add(fill);
            Root.Add(track);
        }
    }
}
