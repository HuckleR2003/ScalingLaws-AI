using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The things you can do to a competitor that are not offering their staff a job.
    ///
    /// Three of them, and they are deliberately unlike each other: paying for a story is cheap,
    /// fast and dishonest; a court case is expensive, slow and legitimate; being bought is the one
    /// that ends the campaign. Putting them on one card is what makes them read as a menu of ways
    /// to compete rather than as three unrelated features.
    ///
    /// **Nothing here decides anything.** Every path goes through `CompanySimulation`, because that
    /// is where money moves and where a relationship is recorded.
    /// </summary>
    public sealed class RivalActionsPanel
    {
        private readonly Func<CompanySimulation> company;
        private readonly Action changed;

        /// <summary>Which tier's card is expanded, or null.</summary>
        private SmearTier? openTier;

        /// <summary>The demand on the lawsuit form, kept across repaints.</summary>
        private long demandUsd;

        /// <summary>True once the court form has been opened, so the demand is not reset.</summary>
        private bool courtOpen;

        private string outcomeNote = string.Empty;

        public RivalActionsPanel(Func<CompanySimulation> company, Action changed)
        {
            this.company = company;
            this.changed = changed;
        }

        public void Reset()
        {
            openTier = null;
            demandUsd = 0L;
            courtOpen = false;
            outcomeNote = string.Empty;
        }

        public VisualElement Build(CompetitorId lab)
        {
            var simulation = company();

            var block = new VisualElement();
            block.AddToClassList("acts");

            if (!string.IsNullOrEmpty(outcomeNote))
            {
                var said = new Label(outcomeNote);
                said.AddToClassList("rival__outcome");
                block.Add(said);
            }

            block.Add(BuildSmear(simulation, lab));
            block.Add(BuildCourt(simulation, lab));

            return block;
        }

        // ---- paying for a story -------------------------------------------------------------------

        private VisualElement BuildSmear(CompanySimulation simulation, CompetitorId lab)
        {
            var panel = new VisualElement();
            panel.AddToClassList("acts__block");

            var heading = new Label(Loc.T("smear.title"));
            heading.AddToClassList("dossier__heading");
            panel.Add(heading);

            var warning = new Label(Loc.T("smear.warning"));
            warning.AddToClassList("rival__warning");
            panel.Add(warning);

            var open = simulation.CanSmear(lab, out var until);

            if (!open)
            {
                var quiet = new Label(
                    Loc.T("smear.too_soon", Math.Max(0, until - simulation.State.Date.DayIndex)));

                quiet.AddToClassList("rival__note");
                panel.Add(quiet);

                return panel;
            }

            foreach (var tier in SmearCatalog.All)
            {
                panel.Add(BuildSmearTier(simulation, lab, tier));
            }

            return panel;
        }

        private VisualElement BuildSmearTier(CompanySimulation simulation, CompetitorId lab,
            SmearDefinition tier)
        {
            var expanded = openTier.HasValue && openTier.Value == tier.Tier;

            var card = new VisualElement();
            card.AddToClassList("act");
            card.EnableInClassList("act--open", expanded);

            var head = new Button(() =>
            {
                openTier = expanded ? null : tier.Tier;
                outcomeNote = string.Empty;
                changed?.Invoke();
            });

            head.AddToClassList("act__head");

            var name = new Label(tier.DisplayName);
            name.AddToClassList("act__name");
            head.Add(name);

            var cost = new Label(UiFormat.Money(tier.CostUsd));
            cost.AddToClassList("act__cost");
            head.Add(cost);

            // The chance of being caught is on the closed row, not hidden inside it. This is the one
            // system where the effective play and the honest play come apart, and burying the risk
            // in a fold would be the interface taking a side.
            var risk = new Label(UiFormat.Percent(tier.BackfireChance));
            risk.AddToClassList("act__risk");
            risk.EnableInClassList("act__risk--high", tier.BackfireChance >= 0.25);
            head.Add(risk);

            card.Add(head);

            if (!expanded)
            {
                return card;
            }

            var note = new Label(tier.Note);
            note.AddToClassList("act__note");
            card.Add(note);

            var figures = new VisualElement();
            figures.AddToClassList("act__figures");
            figures.Add(Pair(Loc.T("smear.damage"), UiFormat.Percent(tier.BrandDamage)));
            figures.Add(Pair(Loc.T("smear.backfire"), UiFormat.Percent(tier.BackfireChance)));
            figures.Add(Pair(Loc.T("smear.quiet"), Loc.Counted(tier.QuietDays, "noun.day")));
            card.Add(figures);

            var pay = new Button(() =>
            {
                simulation.TrySmear(lab, tier.Tier, out _, out var note2);

                outcomeNote = note2;
                openTier = null;
                changed?.Invoke();
            })
            { text = Loc.T("smear.send") };

            pay.AddToClassList("button");
            pay.AddToClassList("button--armed");
            pay.SetEnabled(simulation.State.CashUsd >= tier.CostUsd);
            card.Add(pay);

            return card;
        }

        // ---- taking them to court -----------------------------------------------------------------

        private VisualElement BuildCourt(CompanySimulation simulation, CompetitorId lab)
        {
            var panel = new VisualElement();
            panel.AddToClassList("acts__block");

            var heading = new Label(Loc.T("suit.title"));
            heading.AddToClassList("dossier__heading");
            panel.Add(heading);

            var running = OpenCaseAgainst(simulation, lab);

            if (running != null)
            {
                panel.Add(BuildOpenCase(running));
                return panel;
            }

            if (!simulation.CanSue(lab, out var ceiling, out var groundsKey))
            {
                var none = new Label(Loc.T("suit.no_grounds"));
                none.AddToClassList("rival__note");
                panel.Add(none);

                return panel;
            }

            // The demand opens at a quarter of the ceiling rather than at zero or at the top. Zero
            // is not a case anybody would file, and the top is the worst odds in the game handed to
            // a player who has not yet read what the slider does.
            if (!courtOpen)
            {
                demandUsd = ceiling / 4;
                courtOpen = true;
            }

            demandUsd = Math.Clamp(demandUsd, 0L, ceiling);

            var grounds = new Label(Loc.T(groundsKey));
            grounds.AddToClassList("act__note");
            panel.Add(grounds);

            var warning = new Label(Loc.T("suit.warning"));
            warning.AddToClassList("rival__warning");
            panel.Add(warning);

            var demandLine = new Label($"{Loc.T("suit.demand")}   {UiFormat.Money(demandUsd)}");
            demandLine.AddToClassList("person__caption");
            panel.Add(demandLine);

            var odds = Pair(Loc.T("suit.odds"),
                UiFormat.Percent(LawsuitBook.OddsFor(demandUsd, ceiling)));

            var costs = Pair(Loc.T("suit.costs"), UiFormat.Money(LawsuitBook.CostOf(demandUsd)));

            var figures = new VisualElement();
            figures.AddToClassList("act__figures");
            figures.Add(odds);
            figures.Add(costs);
            panel.Add(figures);

            var slider = new Slider(0f, ceiling) { value = demandUsd };
            slider.AddToClassList("person__slider");
            slider.RegisterValueChangedCallback(change =>
            {
                demandUsd = (long)change.newValue;

                demandLine.text = $"{Loc.T("suit.demand")}   {UiFormat.Money(demandUsd)}";

                // Both readings move with the slider rather than on release, because the whole
                // decision is watching the odds fall as the demand climbs. Updating on commit
                // would hide the trade the control exists to show.
                odds.Q<Label>(className: "pair__value").text =
                    UiFormat.Percent(LawsuitBook.OddsFor(demandUsd, ceiling));

                costs.Q<Label>(className: "pair__value").text =
                    UiFormat.Money(LawsuitBook.CostOf(demandUsd));
            });

            panel.Add(slider);

            var file = new Button(() =>
            {
                if (!simulation.TryFileLawsuit(lab, demandUsd, out var why))
                {
                    outcomeNote = why;
                }

                courtOpen = false;
                changed?.Invoke();
            })
            { text = Loc.T("suit.file") };

            file.AddToClassList("button");
            file.AddToClassList("button--primary");
            file.SetEnabled(simulation.State.CashUsd >= LawsuitBook.CostOf(demandUsd));
            panel.Add(file);

            return panel;
        }

        private static Lawsuit OpenCaseAgainst(CompanySimulation simulation, CompetitorId lab)
        {
            foreach (var suit in simulation.State.Lawsuits)
            {
                if (suit.Target == lab && !suit.IsClosed)
                {
                    return suit;
                }
            }

            return null;
        }

        private static VisualElement BuildOpenCase(Lawsuit suit)
        {
            var card = new VisualElement();
            card.AddToClassList("act");

            var head = new VisualElement();
            head.AddToClassList("act__head");

            var name = new Label(Loc.T("suit.pending"));
            name.AddToClassList("act__name");
            head.Add(name);

            var amount = new Label(UiFormat.Money(suit.DamagesDemandedUsd));
            amount.AddToClassList("act__cost");
            head.Add(amount);

            card.Add(head);

            var left = new Label(
                Loc.T("suit.days_left", Loc.Counted(suit.DaysLeft, "noun.day")));

            left.AddToClassList("act__note");
            card.Add(left);

            var track = new VisualElement();
            track.AddToClassList("rival__track");

            var fill = new VisualElement();
            fill.AddToClassList("rival__fill");
            fill.AddToClassList("rival--neutral-fill");
            fill.style.left = Length.Percent(0f);
            fill.style.width = Length.Percent((float)(suit.Progress * 100.0));

            track.Add(fill);
            card.Add(track);

            return card;
        }

        private static VisualElement Pair(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("pair");

            var name = new Label(label);
            name.AddToClassList("pair__label");
            row.Add(name);

            var reading = new Label(value);
            reading.AddToClassList("pair__value");
            row.Add(reading);

            return row;
        }
    }
}
