using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Marketing: the six channels, booking them, and what is currently running.
    ///
    /// Part of <see cref="GameShell"/>, split out on 2026-08-29. `partial` is a file boundary and
    /// nothing else: the compiler builds the same type either way, so no field changed lifetime and
    /// no call site moved. The shell had reached 5,800 lines because every screen it ever grew was
    /// written into it rather than beside it, and that is the only thing being corrected here.
    /// </summary>
    public sealed partial class GameShell
    {
        /// <summary>
        /// Marketing: pick up to three channels, an audience and a term, then book it.
        ///
        /// The tiles are the screen. Each one is a picture and a name, because the decision is
        /// "which of these feels right for what I am selling" rather than a table of coefficients,
        /// and the numbers that back it are underneath for the player who wants them.
        ///
        /// Three at once is the cap, and the reason is that channels cover each other's weaknesses:
        /// television is broad and slow, social is fast and forgets, press hardly moves the numbers
        /// and is the only thing that reliably builds standing. Allowing all six would make the
        /// combination meaningless.
        /// </summary>
        private VisualElement BuildMarketingScreen()
        {
            var state = simulation.State;

            var page = NewPage(Loc.T("page.marketing"), Loc.T("page.marketing.strap"));
UiParts.ExplainPage(page, TechNotes.CampaignLength);

            page.Add(BuildAwarenessPanel());

            var channels = new VisualElement();
            channels.AddToClassList("panel");

            var heading = new Label(Loc.T("panel.channels"));
            heading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(heading, TechNotes.Channels);
            channels.Add(heading);

            var grid = new VisualElement();
            grid.AddToClassList("chan-grid");

            foreach (var definition in MarketingCatalog.All)
            {
                grid.Add(BuildChannelTile(definition));
            }

            channels.Add(grid);
            page.Add(channels);
            page.Add(BuildRunningPanel());

            // **Out of the flow, pinned to the bottom right.** Booking used to be a panel stacked
            // under the channels, which meant picking a channel scrolled the thing you pick it
            // into off the screen. It is a control surface, not a section, so it behaves like one:
            // it stays where it is while the page moves behind it.
            page.Add(BuildBookingPanel());

            return page;
        }

        /// <summary>How well known the company is, audience by audience.</summary>
        private VisualElement BuildAwarenessPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var head = new VisualElement();
            head.AddToClassList("rfund__head");

            var heading = new Label(Loc.T("panel.heard_of_you"));
            heading.AddToClassList("panel__heading");
            UiParts.ExplainHeading(heading, TechNotes.Awareness);
            heading.style.marginBottom = 0;
            head.Add(heading);

            var overall = new Label(UiFormat.Percent(simulation.State.Awareness.Overall, 0)
                + " overall");

            overall.AddToClassList("rfund__banked");
            head.Add(overall);
            panel.Add(head);

            foreach (var audience in AudienceCatalog.All)
            {
                var known = simulation.State.Awareness.In(audience.Segment);
                panel.Add(UiParts.ThinBarRow(audience.DisplayName, UiFormat.Percent(known, 0), known));
            }

            var note = new Label(
                "Being used counts as being known. A company people already have on the service does "
                + "not become anonymous, so this floor rises with the audience you hold.");

            note.AddToClassList("field__hint");
            panel.Add(note);

            return panel;
        }

        /// <summary>
        /// One channel: a darkened photograph, a name across the bottom, and what it actually does.
        /// </summary>
        private VisualElement BuildChannelTile(MarketingChannelDefinition definition)
        {
            var picked = pickedChannels.Contains(definition.Id);

            var tile = new Button(() =>
            {
                if (picked)
                {
                    pickedChannels.Remove(definition.Id);
                }
                else if (pickedChannels.Count < MarketingCatalog.MostChannelsAtOnce)
                {
                    pickedChannels.Add(definition.Id);
                }

                Show(Screen.Marketing);
            });

            tile.AddToClassList("chan");
            tile.EnableInClassList("chan--on", picked);

            var art = new VisualElement();
            art.AddToClassList("chan__art");

            var picture = Resources.Load<Texture2D>("Marketing/" + definition.Art);
            if (picture != null)
            {
                art.style.backgroundImage = new StyleBackground(picture);
            }
            else
            {
                art.AddToClassList("chan__art--missing");
            }

            tile.Add(art);

            // The scrim is what makes a realistic photograph sit inside a dark interface instead of
            // shouting over it. Same rule as the card art everywhere else in the game.
            var scrim = new VisualElement();
            scrim.AddToClassList("chan__scrim");
            tile.Add(scrim);

            var name = new Label(definition.DisplayName.ToUpperInvariant());
            name.AddToClassList("chan__name");
            tile.Add(name);

            var price = new Label(UiFormat.Money(definition.DailyCostUsd) + " a day");
            price.AddToClassList("chan__price");
            tile.Add(price);

            tile.tooltip = definition.Pitch
                + $"\n\nBest with: {AudienceCatalog.Get(definition.Favours).DisplayName}."
                + $"\nReach {definition.Reach:0.00}, speed {definition.Speed:P0}, "
                + $"sticks {definition.Persistence:P0}, swings {definition.Volatility:P0}.";

            return tile;
        }

        /// <summary>Audience, term, the bill, and the button that commits to it.</summary>
        private VisualElement BuildBookingPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("mkbook");

            var heading = new Label(Loc.T("panel.new_campaign"));
            heading.AddToClassList("mkbook__heading");
            panel.Add(heading);

            var audiences = new VisualElement();
            audiences.AddToClassList("mkbook__chips");

            foreach (var audience in AudienceCatalog.All)
            {
                var segment = audience.Segment;
                var chip = new Button(() => { pickedAudience = segment; Show(Screen.Marketing); })
                { text = audience.DisplayName.ToUpperInvariant() };

                chip.AddToClassList("chip");
                chip.AddToClassList("mkchip");
                chip.EnableInClassList("chip--on", pickedAudience == segment);
                audiences.Add(chip);
            }

            panel.Add(audiences);

            var terms = new VisualElement();
            terms.AddToClassList("mkbook__chips");

            foreach (var months in MarketingCatalog.TermsInMonths)
            {
                var term = months;
                var label = months <= 0 ? "OPEN ENDED" : $"{months} MONTH" + (months > 1 ? "S" : string.Empty);

                var chip = new Button(() => { pickedTerm = term; Show(Screen.Marketing); })
                { text = label };

                chip.AddToClassList("chip");
                chip.AddToClassList("mkchip");
                chip.EnableInClassList("chip--on", pickedTerm == term);
                terms.Add(chip);
            }

            panel.Add(terms);

            var draft = new MarketingCampaign(pickedChannels, pickedAudience, pickedTerm,
                simulation.State.Date);

            var daily = draft.DailyCostUsd;
            var total = draft.IsOpenEnded ? 0L : daily * draft.DaysBooked;

            if (pickedChannels.Count == 0)
            {
                var pick = new Label(Loc.T("mk.pick_channel"));
                pick.AddToClassList("mkbook__pick");
                panel.Add(pick);
            }
            else
            {
                // Two blocks, because the player is answering two questions: what am I buying, and
                // what does it cost. The old single sentence made them read a paragraph to find a
                // number they were going to compare against another number.
                panel.Add(BookRow("AUDIENCE",
                    AudienceCatalog.Get(pickedAudience).DisplayName));

                panel.Add(BookRow("CHANNELS", string.Join(" + ", pickedChannels
                    .Select(channel => MarketingCatalog.Get(channel).DisplayName))));

                panel.Add(BookRow("RUNS FOR", draft.IsOpenEnded
                    ? "until you stop it"
                    : $"{draft.DaysBooked} days"));

                var split = new VisualElement();
                split.AddToClassList("mkbook__split");
                panel.Add(split);

                panel.Add(BookRow("PER DAY", UiFormat.Money(daily), true));

                panel.Add(BookRow("TOTAL", draft.IsOpenEnded
                    ? "open ended"
                    : UiFormat.Money(total), true));

                if (draft.IsOpenEnded)
                {
                    var why = new Label(
                        $"An open contract costs {MarketingCatalog.OpenEndedSurcharge:P0} of the "
                        + "committed rate. Nobody sells one at the price of a booked one.");

                    why.AddToClassList("mkbook__why");
                    panel.Add(why);
                }
            }

            var book = new Button(() =>
            {
                if (pickedChannels.Count == 0)
                {
                    return;
                }

                simulation.State.AddCampaign(new MarketingCampaign(
                    pickedChannels, pickedAudience, pickedTerm, simulation.State.Date));

                pickedChannels.Clear();
                Show(Screen.Marketing);
            })
            { text = Loc.T("mk.book_it") };

            book.AddToClassList("mkbook__go");
            book.SetEnabled(pickedChannels.Count > 0);
            panel.Add(book);

            return panel;
        }

        /// <summary>One caption and one reading, on a line. The whole booker is made of these.</summary>
        private static VisualElement BookRow(string caption, string value, bool loud = false)
        {
            var row = new VisualElement();
            row.AddToClassList("mkrow");

            var label = new Label(caption);
            label.AddToClassList("mkrow__caption");
            row.Add(label);

            var reading = new Label(value);
            reading.AddToClassList("mkrow__value");
            reading.EnableInClassList("mkrow__value--loud", loud);
            row.Add(reading);

            return row;
        }

        private VisualElement BuildRunningPanel()
        {
            var state = simulation.State;

            var panel = new VisualElement();
            panel.AddToClassList("panel");

            var heading = new Label(Loc.T("panel.running"));
            heading.AddToClassList("panel__heading");
            panel.Add(heading);

            if (state.Campaigns.Count == 0)
            {
                var none = new Label("Nothing booked. Only the people already using the service have "
                    + "heard of you.");

                none.AddToClassList("field__hint");
                panel.Add(none);
                return panel;
            }

            foreach (var campaign in state.Campaigns)
            {
                var row = new VisualElement();
                row.AddToClassList("run-row");

                var names = new List<string>();
                foreach (var channel in campaign.Channels)
                {
                    names.Add(MarketingCatalog.Get(channel).DisplayName);
                }

                var words = new VisualElement();
                words.AddToClassList("run-row__words");

                var what = new Label(string.Join(" + ", names));
                what.AddToClassList("run-row__what");
                words.Add(what);

                var who = new Label($"to {AudienceCatalog.Get(campaign.Target).DisplayName}");
                who.AddToClassList("run-row__who");
                words.Add(who);

                row.Add(words);

                // The term as a bar rather than a sentence: a campaign three days from ending and
                // one three months from it read identically as text.
                if (!campaign.IsOpenEnded)
                {
                    var track = new VisualElement();
                    track.AddToClassList("run-row__track");

                    var fill = new VisualElement();
                    fill.AddToClassList("run-row__fill");

                    var run = Math.Max(1, campaign.DaysBooked);
                    var gone = Math.Clamp(1.0 - campaign.DaysLeft(state.Date) / (double)run, 0.0, 1.0);
                    fill.style.width = Length.Percent((float)(gone * 100.0));

                    track.Add(fill);
                    row.Add(track);
                }

                var left = new Label(campaign.IsOpenEnded
                    ? "OPEN ENDED"
                    : $"{campaign.DaysLeft(state.Date)} DAYS LEFT");

                left.AddToClassList("run-row__left");
                row.Add(left);

                var cost = new Label(UiFormat.Money(campaign.DailyCostUsd) + "/day");
                cost.AddToClassList("run-row__cost");
                row.Add(cost);

                var stop = new Button(() =>
                {
                    simulation.State.RemoveCampaign(campaign);
                    Show(Screen.Marketing);
                })
                { text = Loc.T("common.stop") };

                stop.AddToClassList("run-row__stop");
                row.Add(stop);

                panel.Add(row);
            }

            return panel;
        }

    }
}
