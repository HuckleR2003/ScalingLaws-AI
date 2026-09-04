using System;
using System.Globalization;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Grants and donations, on the bank screen.
    ///
    /// **Here rather than on a screen of its own**, for the same reason borrowing sits beside
    /// equity: these are three answers to one question. A round costs a share of everything the
    /// company ever earns. A facility costs a fixed sum on a fixed date. A grant costs the freedom
    /// to run the company however you like for as long as the term lasts. Putting them on one page
    /// is what makes that a choice instead of three unrelated errands.
    /// </summary>
    public sealed partial class GameShell
    {
        private VisualElement BuildGrantsPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("grant.section"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            var strap = new Label(Loc.T("grant.subtitle"));
            strap.AddToClassList("panel__strap");
            panel.Add(strap);

            // Where the company sits on the ladder, and what opens the next rung. Without this the
            // player has no way of knowing that finishing one award is what makes the larger
            // bodies write at all.
            var reached = simulation.GrantTierReached();

            var rung = new Label(reached < GrantCatalog.TopTier
                ? Loc.T("grant.tier", reached) + "  ·  " + Loc.T("grant.tier.locked", reached)
                : Loc.T("grant.tier", reached));

            rung.AddToClassList("grant__rung");
            panel.Add(rung);

            BuildHeldGrants(panel);
            BuildOfferedGrants(panel);

            return panel;
        }

        /// <summary>
        /// What the company is already working off.
        ///
        /// Held awards come first because they carry a deadline the player is running against, and
        /// an offer they have not accepted cannot be more urgent than a term already ticking.
        /// </summary>
        private void BuildHeldGrants(VisualElement panel)
        {
            var held = simulation.HeldGrants();

            var title = new Label(Loc.T("grant.held"));
            title.AddToClassList("grant__section");
            panel.Add(title);

            if (held.Count == 0)
            {
                panel.Add(Hint(Loc.T("grant.none_held")));
                return;
            }

            foreach (var grant in held)
            {
                panel.Add(BuildHeldGrantCard(grant));
            }
        }

        private VisualElement BuildHeldGrantCard(Grant grant)
        {
            var definition = grant.Definition;

            var card = new VisualElement();
            card.AddToClassList("gcard");
            card.EnableInClassList("gcard--broken", grant.IsBroken);

            var head = new VisualElement();
            head.AddToClassList("gcard__head");

            var name = new Label(Loc.T(definition.NameKey));
            name.AddToClassList("gcard__name");
            head.Add(name);

            var status = new Label(grant.IsBroken
                ? Loc.T("grant.broken")
                : Loc.T("grant.days_left", grant.DaysLeft));

            status.AddToClassList("gcard__status");
            status.EnableInClassList("gcard__status--broken", grant.IsBroken);
            head.Add(status);

            card.Add(head);

            var goal = new Label(GoalSentence(definition, grant.Baseline));
            goal.AddToClassList("gcard__goal");
            card.Add(goal);

            // Where the measured quantity actually stands, which is the one thing a calendar bar
            // cannot say. A player watching a term run down needs to know whether they are winning.
            card.Add(UiParts.ThinBarRow(
                Loc.T("grant.standing",
                    ReadingText(definition.Goal, simulation.GrantReading(grant)),
                    TargetText(definition, grant.Baseline)),
                Loc.T("grant.days_left", grant.DaysLeft),
                grant.Progress));

            return card;
        }

        private void BuildOfferedGrants(VisualElement panel)
        {
            var open = simulation.AvailableGrants();

            var title = new Label(Loc.T("grant.open"));
            title.AddToClassList("grant__section");
            panel.Add(title);

            if (open.Count == 0)
            {
                panel.Add(Hint(Loc.T("grant.none_open")));
                return;
            }

            var canAccept = simulation.CanAcceptGrant(out var why);

            foreach (var definition in open)
            {
                panel.Add(BuildOfferCard(definition, canAccept, why));
            }

            panel.Add(Hint(Loc.T("grant.warning")));
        }

        /// <summary>
        /// A heading on a torn plate.
        ///
        /// **Drawn, because USS has neither a clip path nor a mask.** Same trick as the cousin's
        /// name block: a Painter2D shape behind the words, with the tear derived from the vertex
        /// index so it does not reshuffle on every repaint. The grants page repaints every
        /// simulated day, and a flickering edge would be the loudest thing on the screen.
        /// </summary>
        private static VisualElement TornHeading(string text, Color plate, Color ink,
            string extraClass = null)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("gplate");

            if (extraClass != null)
            {
                wrap.AddToClassList(extraClass);
            }

            wrap.Add(new TornPlate(plate));

            var label = new Label(text);
            label.AddToClassList("gplate__text");
            label.style.color = ink;
            wrap.Add(label);

            return wrap;
        }

        /// <summary>One figure on its own coloured plate: what it is, and how much.</summary>
        private static VisualElement TornFigure(string caption, string value, Color plate, Color ink,
            bool torn)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("gfig");

            if (torn)
            {
                wrap.Add(new TornPlate(plate));
            }
            else
            {
                wrap.style.backgroundColor = plate;
            }

            var words = new VisualElement();
            words.AddToClassList("gfig__words");

            var kicker = new Label(caption);
            kicker.AddToClassList("gfig__kicker");
            kicker.style.color = new Color(ink.r, ink.g, ink.b, 0.72f);
            words.Add(kicker);

            var figure = new Label(value);
            figure.AddToClassList("gfig__value");
            figure.style.color = ink;
            words.Add(figure);

            wrap.Add(words);
            return wrap;
        }

        private static readonly Color Paper = new(0.94f, 0.94f, 0.92f);
        private static readonly Color PaperInk = new(0.06f, 0.07f, 0.09f);
        private static readonly Color AdvanceGreen = new(0.42f, 0.72f, 0.50f);
        private static readonly Color CompletionGreen = new(0.10f, 0.33f, 0.22f);
        private static readonly Color PointsBlue = new(0.16f, 0.36f, 0.64f);
        private static readonly Color OnDark = new(0.95f, 0.98f, 0.96f);

        private VisualElement BuildOfferCard(GrantDefinition definition, bool canAccept, string why)
        {
            var card = new VisualElement();
            card.AddToClassList("gcard");
            card.AddToClassList("gcard--offer");

            // The name on torn paper, black on white, which is the one thing on the card that has
            // to be read before anything else.
            card.Add(TornHeading(Loc.T(definition.NameKey), Paper, PaperInk, "gplate--title"));

            // The rung rather than a countdown. A programme on the board does not expire: it is
            // there until it is taken, finished, or put away, which is what a register is.
            var rung = new Label(Loc.T("grant.tier", definition.Tier));
            rung.AddToClassList("gcard__status");
            card.Add(rung);

            // **Who is offering, with weight.** This line is the difference between "a grant" and
            // "the Ministry of Digitisation is offering you a grant", and it was set at the same
            // size as the terms underneath it.
            var body = new Label(simulation.BodyOf(definition));
            body.AddToClassList("gcard__body");
            body.AddToClassList("gcard__body--strong");
            card.Add(body);

            var goal = new Label(GoalSentence(definition, CurrentBaseline(definition)));
            goal.AddToClassList("gcard__goal");
            card.Add(goal);

            var terms = new Label(Loc.T(definition.TermsKey));
            terms.AddToClassList("gcard__terms");
            card.Add(terms);

            // **Four plates rather than four rows of small grey text.** These are the terms, and
            // the terms are the decision: what arrives now, what arrives if it is finished, what
            // cannot be bought with money, and how long there is. Each on its own ground so the
            // shape of an offer is readable before a word of it.
            var figures = new VisualElement();
            figures.AddToClassList("gcard__plates");

            figures.Add(TornFigure(Loc.T("grant.advance"),
                UiFormat.Money(definition.AdvanceUsd), AdvanceGreen, PaperInk, true));

            figures.Add(TornFigure(Loc.T("grant.completion"),
                UiFormat.Money(definition.CompletionUsd), CompletionGreen, OnDark, false));

            figures.Add(TornFigure(Loc.T("grant.points"),
                UiFormat.Number(definition.ResearchPoints, 0), PointsBlue, OnDark, false));

            figures.Add(TornFigure(Loc.T("grant.term"),
                Loc.T("grant.days", definition.TermDays), Paper, PaperInk, true));

            card.Add(figures);

            var buttons = new VisualElement();
            buttons.AddToClassList("gcard__buttons");

            var sign = new Button(() =>
            {
                simulation.TryAcceptGrant(definition.Id, out _);
                Show(Screen.Funding);
            })
            { text = Loc.T("grant.accept") };

            sign.AddToClassList("button");
            sign.AddToClassList("button--primary");
            sign.style.marginLeft = 0;
            sign.style.marginTop = 0;
            sign.SetEnabled(canAccept);
            buttons.Add(sign);

            // Asked for by name: a board the player cannot clear is a board they stop reading.
            var dismiss = new Button(() =>
            {
                simulation.TryDismissGrant(definition.Id);
                Show(Screen.Funding);
            })
            { text = Loc.T("grant.dismiss") };

            dismiss.AddToClassList("button");
            dismiss.AddToClassList("button--quiet");
            dismiss.style.marginLeft = 8;
            dismiss.style.marginTop = 0;
            buttons.Add(dismiss);

            card.Add(buttons);

            if (!canAccept)
            {
                card.Add(Hint(why));
            }

            return card;
        }

        /// <summary>
        /// Where the measured quantity stands today, so an offer can say what it would be asking
        /// for if it were signed this minute.
        /// </summary>
        private double CurrentBaseline(GrantDefinition definition) =>
            GrantConditions.Reading(definition.Goal, simulation.State,
                simulation.Flagship()?.Capability ?? 0.0,
                simulation.State.LastQuality.Utilisation);

        /// <summary>
        /// The condition in a sentence.
        ///
        /// **Written as a switch rather than assembled from the goal's name.** A phrase-book key
        /// built by concatenation is invisible to `LocalisationTests`, which can only read literals,
        /// and this project has already shipped one screen of raw keys that way.
        /// </summary>
        private static string GoalSentence(GrantDefinition definition, double baseline) =>
            definition.Goal switch
            {
                GrantGoal.ReleaseModels => Loc.T("grant.goal.release",
                    (int)Math.Round(definition.Target)),

                GrantGoal.ReachCapability => Loc.T("grant.goal.capability",
                    UiFormat.Number(definition.Target, 0)),

                GrantGoal.FinishResearch => Loc.T("grant.goal.research",
                    (int)Math.Round(definition.Target)),

                GrantGoal.EmployPeople => Loc.T("grant.goal.employ",
                    (int)Math.Round(definition.Target)),

                GrantGoal.SustainFreeTier => Loc.T("grant.goal.freetier",
                    UiFormat.Percent(definition.Target, 0)),

                GrantGoal.SustainHeadroom => Loc.T("grant.goal.headroom",
                    UiFormat.Percent(definition.Target, 0)),

                GrantGoal.SustainReputation => Loc.T("grant.goal.reputation",
                    UiFormat.Percent(definition.Target, 0)),

                GrantGoal.SustainOnSale => Loc.T("grant.goal.onsale",
                    (int)Math.Round(definition.Target)),

                _ => Loc.T("grant.goal.protected", (int)Math.Round(definition.Target))
            };

        /// <summary>Today's reading, in whatever unit the goal is measured in.</summary>
        private static string ReadingText(GrantGoal goal, double reading) => goal switch
        {
            GrantGoal.SustainFreeTier => UiFormat.Percent(reading, 0),
            GrantGoal.SustainHeadroom => UiFormat.Percent(reading, 0),
            GrantGoal.SustainReputation => UiFormat.Percent(reading, 0),
            GrantGoal.ReachCapability => UiFormat.Number(reading, 1),
            _ => ((int)Math.Round(reading)).ToString(CultureInfo.InvariantCulture)
        };

        /// <summary>
        /// What the reading has to get to.
        ///
        /// The counting goals are measured from the day the award was signed, so the figure shown
        /// has to have the baseline added back in. Printing the raw target would tell a company
        /// that had already shipped four models that it needs three.
        /// </summary>
        private static string TargetText(GrantDefinition definition, double baseline) =>
            definition.Goal switch
            {
                GrantGoal.ReleaseModels => ((int)Math.Round(baseline + definition.Target))
                    .ToString(CultureInfo.InvariantCulture),

                GrantGoal.FinishResearch => ((int)Math.Round(baseline + definition.Target))
                    .ToString(CultureInfo.InvariantCulture),

                GrantGoal.ReachCapability => UiFormat.Number(baseline + definition.Target, 1),

                GrantGoal.SustainFreeTier => UiFormat.Percent(definition.Target, 0),
                GrantGoal.SustainHeadroom => UiFormat.Percent(definition.Target, 0),
                GrantGoal.SustainReputation => UiFormat.Percent(definition.Target, 0),

                _ => ((int)Math.Round(definition.Target)).ToString(CultureInfo.InvariantCulture)
            };
    }
}
