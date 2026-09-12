using System;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Who owns the company, drawn as one bar with the names under it.
    ///
    /// **Reported: there was a percentage and nothing else.** The screen said the founders held
    /// ninety three per cent and never said who had the other seven, what they paid for it, or what
    /// it is earning them, so an investor was an abstraction the player diluted themselves against
    /// rather than somebody sitting on their board.
    ///
    /// The bar is the whole company, always, which is why the remainder has a segment of its own
    /// rather than being left off: a bar that does not reach the end is a bar the reader has to do
    /// arithmetic on. On a fresh campaign it reads ninety eight per cent the player and two per cent
    /// Emil, which is a line worth noticing on day one.
    /// </summary>
    public static class OwnershipBar
    {
        /// <summary>Below this a slice is a hairline and its label will not fit in it.</summary>
        private const double SmallestLabelledShare = 0.06;

        public static VisualElement Build(CompanySimulation simulation)
        {
            var block = new VisualElement();
            block.AddToClassList("ownbar");

            if (simulation == null)
            {
                return block;
            }

            var table = simulation.State.CapTable;
            var valuation = simulation.CurrentValuationUsd();

            var heading = new Label(Loc.T("own.who_owns_it"));
            heading.AddToClassList("ownbar__heading");
            block.Add(heading);

            var track = new VisualElement();
            track.AddToClassList("ownbar__track");

            track.Add(Segment(simulation.State.CompanyName, table.FounderEquity,
                new Color(0.89f, 0.47f, 0.38f)));

            foreach (var holding in table.Holders)
            {
                if (!InvestorCatalog.TryGet(holding.Investor, out var definition))
                {
                    continue;
                }

                track.Add(Segment(definition.DisplayName, holding.Fraction,
                    ColorUtility.TryParseHtmlString(definition.AccentHex, out var accent)
                        ? accent
                        : new Color(0.45f, 0.55f, 0.7f)));
            }

            // Whatever no name accounts for. On a campaign loaded from before the register existed
            // this is most of what was sold, and saying so is better than quietly rounding the
            // founders up to own it.
            var unnamed = 1.0 - table.FounderEquity - table.InvestorEquity;
            if (unnamed > 0.0005)
            {
                track.Add(Segment(Loc.T("own.other_holders"), unnamed,
                    new Color(0.32f, 0.38f, 0.48f)));
            }

            block.Add(track);

            block.Add(Row(simulation, simulation.State.CompanyName, table.FounderEquity,
                valuation, null));

            foreach (var holding in table.Holders)
            {
                if (!InvestorCatalog.TryGet(holding.Investor, out var definition))
                {
                    continue;
                }

                block.Add(Row(simulation, definition.DisplayName, holding.Fraction, valuation,
                    holding));
            }

            return block;
        }

        private static VisualElement Segment(string name, double share, Color colour)
        {
            var slice = new VisualElement();
            slice.AddToClassList("ownbar__slice");
            slice.style.width = Length.Percent((float)(Math.Clamp(share, 0.0, 1.0) * 100.0));
            slice.style.backgroundColor = colour;
            slice.tooltip = name + "  " + UiFormat.Percent(share, 1);

            // A two per cent slice is eleven pixels wide and a name will not go in it. The tooltip
            // carries it, and the row underneath says it in full.
            if (share >= SmallestLabelledShare)
            {
                var label = new Label(UiFormat.Percent(share, 0));
                label.AddToClassList("ownbar__slicelabel");
                slice.Add(label);
            }

            return slice;
        }

        /// <summary>
        /// One holder: what they own, what it is worth, and what it earned them.
        ///
        /// **Their share of the profit, not of the revenue.** A shareholder owns what is left rather
        /// than what came in, so a loss-making month really does cost them, and a row that only ever
        /// counted up would be telling the player their investors do well whatever happens.
        /// </summary>
        private static VisualElement Row(CompanySimulation simulation, string name, double share,
            long valuation, Holding? holding)
        {
            var row = new VisualElement();
            row.AddToClassList("ownrow");

            var who = new Label(name);
            who.AddToClassList("ownrow__who");
            row.Add(who);

            var percent = new Label(UiFormat.Percent(share, 1));
            percent.AddToClassList("ownrow__share");
            row.Add(percent);

            var worth = new Label(UiFormat.Money(
                SimUnits.ToDollars(Math.Max(0L, valuation) * Math.Clamp(share, 0.0, 1.0))));

            worth.AddToClassList("ownrow__worth");
            row.Add(worth);

            if (holding is not { } stake)
            {
                // The founders are not an investor: they did not buy in and nothing is owed to them
                // that is not already the company's own profit.
                var self = new Label(Loc.T("own.that_is_you"));
                self.AddToClassList("ownrow__earned");
                row.Add(self);
                return row;
            }

            var earnings = simulation.HolderEarnings(stake);

            var earned = new Label(Loc.T("own.earned_day_month",
                UiFormat.Money(earnings.Day), UiFormat.Money(earnings.Month)));

            earned.AddToClassList("ownrow__earned");
            earned.EnableInClassList("ownrow__earned--down", earnings.Month < 0L);
            row.Add(earned);

            return row;
        }
    }
}
