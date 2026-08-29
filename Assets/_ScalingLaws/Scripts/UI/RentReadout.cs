using System;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What the rented fleet is, what it costs, and roughly who it holds.
    ///
    /// **The slider had one line of prose over it and nothing else.** Petaflops, price per PF-day
    /// and the current generation were run together in a sentence, so the number a player is
    /// actually deciding — the daily bill — was a fragment in the middle of it, and the question
    /// they are really asking, "is this enough for the people I have", was not answered anywhere on
    /// the screen.
    ///
    /// Three meters and a band. The meters are the facts with a bar behind each, because a figure
    /// with no scale cannot say whether it is a lot; the band is the one derived number worth having
    /// in large type, and it is derived by `HostingCatalog` rather than here so the capacity claimed
    /// on the reserved packages and the capacity claimed on the slider come from the same rule.
    /// </summary>
    public static class RentReadout
    {
        /// <summary>
        /// The largest fleet the interface will ever offer. One billion ordinary accounts.
        ///
        /// This used to be the slider's range on day one as well as its absolute ceiling, which is
        /// the fault this class now exists to fix. It is the cap, not the scale.
        /// </summary>
        public const double FullScalePetaflops = 40_000.0;

        /// <summary>
        /// What the slider offers a company that holds nobody: about fifty million accounts.
        ///
        /// Enough to be generous for a first product and small enough that the control has useful
        /// resolution. At the old range a single pixel was seven hundred thousand accounts.
        /// </summary>
        public const double OpeningCeilingPetaflops = 2_000.0;

        /// <summary>How many times today's audience the slider will let the player buy for.</summary>
        public const double Headroom = 4.0;

        /// <summary>
        /// The top of the slider for this company, today.
        ///
        /// **It ratchets and never falls.** `current` is passed in and taken as a floor, because a
        /// ceiling that dropped when an audience shrank would clamp the player's own setting down
        /// without being asked, change the daily bill they had chosen, and do it on the worst day
        /// they were having. A ceiling that only ever rises cannot surprise anybody.
        /// </summary>
        public static double CeilingPetaflops(double heldUsers, double current)
        {
            var forAudience = Math.Max(0.0, SimUnits.Finite(heldUsers)) * Headroom
                / 1_000_000.0 * HostingCatalog.PetaflopsPerMillionConsumers;

            var ceiling = Math.Max(OpeningCeilingPetaflops, forAudience);

            ceiling = Math.Max(ceiling, Math.Max(0.0, SimUnits.Finite(current)));

            // Rounded up to a readable step, so the end of the travel is a number rather than the
            // arithmetic that produced it.
            var step = ceiling <= 5_000.0 ? 250.0 : 1_000.0;
            ceiling = Math.Ceiling(ceiling / step) * step;

            return Math.Clamp(ceiling, OpeningCeilingPetaflops, FullScalePetaflops);
        }

        /// <summary>A day's rent at which the bar is full. Above the point most companies survive.</summary>
        public const double FullScaleDailyUsd = 1_000_000.0;

        /// <summary>
        /// Builds the three meters.
        ///
        /// Nothing computes: every figure comes off the profile and the market the fleet is billed
        /// from, so the strip cannot claim a capacity the simulation does not deliver.
        /// </summary>
        public static VisualElement Meters(ComputeProfile profile, MarketConditions market,
            double rentedPetaflops, double scalePetaflops = FullScalePetaflops)
        {
            // The bars answer "is this a lot" and the answer depends on what this company could
            // sensibly hold, not on the largest fleet in the game. A meter measured against a
            // billion accounts reads as empty for every company that is not one.
            var scale = Math.Max(1.0, scalePetaflops);

            var strip = new VisualElement();
            strip.AddToClassList("rmeters");

            var perDay = rentedPetaflops * market.RentPricePerPetaflopDayUsd;

            strip.Add(Meter(
                Loc.T("rent.meter.capacity"),
                UiFormat.Petaflops(rentedPetaflops),
                Loc.T("rent.meter.capacity_note",
                    HardwareCatalog.Get(market.RentableGeneration).DisplayName),
                rentedPetaflops / scale,
                "rmeters__fill--capacity"));

            strip.Add(Meter(
                Loc.T("rent.meter.bill"),
                UiFormat.Money((long)Math.Round(perDay)),
                Loc.T("rent.meter.bill_note",
                    UiFormat.Money((long)Math.Round(market.RentPricePerPetaflopDayUsd))),
                perDay / FullScaleDailyUsd,
                "rmeters__fill--bill"));

            // Delivered, not contracted. The pool never converts every rented petaflop into work,
            // and the gap between these two bars is the whole reason `RealizedEfficiency` exists.
            strip.Add(Meter(
                Loc.T("rent.meter.delivered"),
                UiFormat.Petaflops(profile.EffectivePetaflops),
                Loc.T("rent.meter.delivered_note", UiFormat.Percent(profile.RealizedEfficiency)),
                profile.EffectivePetaflops / scale,
                "rmeters__fill--delivered"));

            return strip;
        }

        /// <summary>
        /// The band under the slider: roughly how many everyday accounts this much capacity holds.
        ///
        /// **The one question the rent slider is really being asked.** A player moving it is not
        /// choosing petaflops, they are choosing whether the product stays up, and until this
        /// existed the only way to find out was to release something and watch the queue form.
        ///
        /// "About", in words as well as in the number, because it is a rule of thumb over an average
        /// account and a heavy audience will not fit in it.
        /// </summary>
        public static VisualElement CapacityBand(double rentedPetaflops, double heldUsers)
        {
            var band = new VisualElement();
            band.AddToClassList("rband");

            var accounts = HostingCatalog.CoversAccounts(rentedPetaflops);

            var caption = new Label(Loc.T("rent.band.caption"));
            caption.AddToClassList("rband__caption");
            band.Add(caption);

            var figure = new Label(UiFormat.Count(accounts));
            figure.AddToClassList("rband__figure");
            band.Add(figure);

            // Against what the company actually holds, when it holds anybody. A capacity figure on
            // its own is a fact; the same figure beside today's audience is a decision.
            var verdict = new Label(Verdict(accounts, heldUsers));
            verdict.AddToClassList("rband__verdict");
            verdict.EnableInClassList("rband__verdict--short", heldUsers > accounts);
            band.Add(verdict);

            return band;
        }

        private static string Verdict(double accounts, double heldUsers) =>
            heldUsers <= 0.0
                ? Loc.T("rent.band.nobody")
                : heldUsers > accounts
                    ? Loc.T("rent.band.short", UiFormat.Count(heldUsers))
                    : Loc.T("rent.band.room", UiFormat.Count(heldUsers));

        private static VisualElement Meter(string caption, string value, string note,
            double fraction, string fillClass)
        {
            var block = new VisualElement();
            block.AddToClassList("rmeters__block");

            var head = new VisualElement();
            head.AddToClassList("rmeters__head");

            var label = new Label(caption);
            label.AddToClassList("rmeters__caption");
            head.Add(label);

            var figure = new Label(value);
            figure.AddToClassList("rmeters__value");
            head.Add(figure);

            block.Add(head);

            var track = new VisualElement();
            track.AddToClassList("rmeters__track");

            var fill = new VisualElement();
            fill.AddToClassList("rmeters__fill");
            fill.AddToClassList(fillClass);

            // Clamped rather than allowed to overrun, because a fill wider than its track is drawn
            // outside it and reads as a broken element rather than as a large number.
            fill.style.width = Length.Percent((float)(Math.Clamp(fraction, 0.0, 1.0) * 100.0));

            track.Add(fill);
            block.Add(track);

            var hint = new Label(note);
            hint.AddToClassList("rmeters__note");
            block.Add(hint);

            return block;
        }
    }
}
