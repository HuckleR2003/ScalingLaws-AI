using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The inbox: a list on the left, the letter on the right, and the answer at the bottom of it.
    ///
    /// **The wire reports and the inbox asks.** That split is the whole reason this is a second
    /// screen rather than another news section: a story is something the player may read, and a
    /// letter is something waiting on them. So a letter that carries no action and no deadline
    /// almost certainly should have been a news story, and the desk that decides that lives in the
    /// simulation rather than here.
    ///
    /// The screen dispatches and never decides. `TryActOnMail` is the only thing that can spend the
    /// company's money; this file knows a demand has a Pay button and does not know what paying does.
    /// </summary>
    public sealed class MailScreen
    {
        private readonly CompanySimulation simulation;
        private readonly Action repaint;

        private int selected;
        private Filter filter = Filter.All;
        private string problem = string.Empty;

        private enum Filter
        {
            All,
            Unread,
            NeedsAnswer
        }

        public MailScreen(CompanySimulation simulation, Action repaint)
        {
            this.simulation = simulation;
            this.repaint = repaint;

            Root = new VisualElement();
            Root.AddToClassList("content");
            Root.AddToClassList("mail");
        }

        public VisualElement Root { get; }

        /// <summary>Letter whose wage panel is open, or zero. One at a time.</summary>
        private int openOffer;

        /// <summary>What the player is currently proposing, an hour.</summary>
        private double offerHourly;

        /// <summary>And as a lump sum on signing.</summary>
        private double offerBonus;

        /// <summary>What the candidate said about the last offer. Cleared when a new one opens.</summary>
        private string negotiationNote = string.Empty;

        /// <summary>Opens one letter. Public so a notification can deep link into it later.</summary>
        public void Select(int mailId)
        {
            selected = mailId;
            problem = string.Empty;
            Refresh();
        }

        public void Refresh()
        {
            Root.Clear();

            // **After the clear, not in the constructor.** This screen rebuilds itself into the same
            // root on every refresh, so anything added once at construction survives exactly until
            // the first message arrives. It went in at construction first and the banner was gone
            // from every frame, which looks identical to art that failed to load.
            var strip = PageArt.BannerFor("background_mail");

            if (strip != null)
            {
                Root.Add(strip);
            }
            Root.Add(BuildHeader());

            var letters = Visible();

            // Land on something rather than on an empty reading pane. The newest open letter is what
            // a person opening their mail is actually looking for.
            if (letters.Count > 0 && !letters.Exists(letter => letter.Id == selected))
            {
                selected = letters[0].Id;
            }

            var columns = new VisualElement();
            columns.AddToClassList("mail__columns");
            columns.Add(BuildList(letters));
            columns.Add(BuildReader(letters));
            Root.Add(columns);
        }

        private List<MailItem> Visible()
        {
            var all = simulation.State.Mail.All;
            var found = new List<MailItem>(all.Count);

            for (var index = all.Count - 1; index >= 0; index--)
            {
                var letter = all[index];

                var keep = filter switch
                {
                    Filter.Unread => !letter.IsRead,
                    Filter.NeedsAnswer => letter.NeedsAnswer,
                    _ => true
                };

                if (keep)
                {
                    found.Add(letter);
                }
            }

            return found;
        }

        private VisualElement BuildHeader()
        {
            var box = simulation.State.Mail;

            var bar = new VisualElement();
            bar.AddToClassList("mail__header");

            var left = new VisualElement();

            var title = new Label(Loc.T("mail.title"));
            title.AddToClassList("mail__title");
            left.Add(title);

            var owed = box.OwedUsd;
            var strap = new Label(owed > 0L
                ? Loc.T("mail.owed_across", UiFormat.Money(owed),
                    Loc.Counted(box.Open, "noun.open_letter"))
                : Loc.T("mail.nothing_owed"));

            strap.AddToClassList("mail__strap");
            strap.EnableInClassList("mail__strap--owing", owed > 0L);
            left.Add(strap);

            bar.Add(left);

            var filters = new VisualElement();
            filters.AddToClassList("mail__filters");

            filters.Add(FilterChip(Loc.T("mail.filter_all"), Filter.All, box.All.Count));
            filters.Add(FilterChip(Loc.T("mail.filter_unread"), Filter.Unread, box.Unread));
            filters.Add(FilterChip(Loc.T("mail.filter_answer"), Filter.NeedsAnswer, NeedingAnswer()));

            bar.Add(filters);
            return bar;
        }

        private int NeedingAnswer()
        {
            var count = 0;
            foreach (var letter in simulation.State.Mail.All)
            {
                if (letter.NeedsAnswer)
                {
                    count++;
                }
            }

            return count;
        }

        private Button FilterChip(string text, Filter which, int count)
        {
            var chip = new Button(() =>
            {
                filter = which;
                problem = string.Empty;
                Refresh();
            })
            { text = count > 0 ? $"{text}  {count}" : text };

            chip.AddToClassList("chip");
            chip.EnableInClassList("chip--on", filter == which);
            return chip;
        }

        // ---- the list -----------------------------------------------------------------------------

        private VisualElement BuildList(IReadOnlyList<MailItem> letters)
        {
            var column = new VisualElement();
            column.AddToClassList("mail-list");

            if (letters.Count == 0)
            {
                var none = new Label(filter == Filter.All
                    ? Loc.T("mail.empty_box")
                    : Loc.T("mail.empty_filter"));

                none.AddToClassList("mail-empty");
                column.Add(none);
                return column;
            }

            var scroll = new ScrollView();
            scroll.AddToClassList("mail-scroll");

            foreach (var letter in letters)
            {
                scroll.Add(BuildRow(letter));
            }

            column.Add(scroll);
            return column;
        }

        private VisualElement BuildRow(MailItem letter)
        {
            var today = simulation.State.Date;

            var row = new Button(() => Select(letter.Id));
            row.AddToClassList("mail-row");
            row.EnableInClassList("mail-row--on", letter.Id == selected);
            row.EnableInClassList("mail-row--unread", !letter.IsRead);
            row.EnableInClassList("mail-row--closed", letter.IsClosed);

            var dot = new VisualElement();
            dot.AddToClassList("mail-row__dot");
            dot.AddToClassList(DotClass(letter, today));
            row.Add(dot);

            var text = new VisualElement();
            text.AddToClassList("mail-row__text");

            var top = new VisualElement();
            top.AddToClassList("mail-row__top");

            var sender = new Label(letter.Sender);
            sender.AddToClassList("mail-row__sender");
            top.Add(sender);

            var when = new Label(letter.Arrived.ToString());
            when.AddToClassList("mail-row__date");
            top.Add(when);

            text.Add(top);

            var subject = new Label(letter.Subject);
            subject.AddToClassList("mail-row__subject");
            text.Add(subject);

            // The one line that makes the list scannable without opening anything: what it wants.
            var wants = new Label(WantsLine(letter, today));
            wants.AddToClassList("mail-row__wants");
            wants.EnableInClassList("mail-row__wants--late", letter.IsOverdue(today));
            text.Add(wants);

            row.Add(text);
            return row;
        }

        private static string DotClass(MailItem letter, GameDate today) =>
            letter.IsClosed
                ? "mail-row__dot--done"
                : letter.IsOverdue(today)
                    ? "mail-row__dot--late"
                    : letter.Kind switch
                    {
                        MailKind.TaxDemand or MailKind.Fine => "mail-row__dot--money",
                        MailKind.JobOffer => "mail-row__dot--people",
                        _ => "mail-row__dot--plain"
                    };

        /// <summary>
        /// The one line on the row that says what a letter wants, without opening it.
        ///
        /// **Public so the agreement can be tested.** This line, the NEEDS AN ANSWER count above it
        /// and the buttons in the reader were three separate readings of one state, and they
        /// disagreed: a feedback letter said "No reply needed" on the row, was counted as needing
        /// one by the filter, and carried two answer buttons in the body. An EditMode test has no
        /// panel to dispatch a click into, so the only way to hold the three together is to call
        /// this the way the row does.
        /// </summary>
        public static string WantsLine(MailItem letter, GameDate today)
        {
            if (letter.IsClosed)
            {
                return letter.Outcome.Length > 0 ? letter.Outcome : Loc.T("mail.closed");
            }

            if (letter.AmountUsd > 0L)
            {
                return letter.IsOverdue(today)
                    ? Loc.T("mail.wants_overdue", UiFormat.Money(letter.AmountUsd))
                    : Loc.T("mail.wants_due", UiFormat.Money(letter.AmountUsd),
                        Loc.Counted(letter.DaysLeft(today), "noun.day"));
            }

            if (letter.Kind == MailKind.JobOffer)
            {
                return Loc.T("mail.wants_salary", UiFormat.Money(letter.AskingSalaryUsd));
            }

            // Checked before the fall-through rather than after it. A letter with buttons under it
            // is waiting on an answer whatever else it does or does not carry.
            return letter.NeedsAnswer
                ? Loc.T("mail.wants_answer")
                : Loc.T("mail.wants_nothing");
        }

        // ---- the letter ----------------------------------------------------------------------------

        private VisualElement BuildReader(IReadOnlyList<MailItem> letters)
        {
            var pane = new VisualElement();
            pane.AddToClassList("mail-read");

            MailItem letter = null;
            foreach (var candidate in letters)
            {
                if (candidate.Id == selected)
                {
                    letter = candidate;
                    break;
                }
            }

            if (letter == null)
            {
                var none = new Label(Loc.T("common.nothing_selected"));
                none.AddToClassList("mail-empty");
                pane.Add(none);
                return pane;
            }

            // Opening it is reading it. Marked here rather than on the click, so arriving at a letter
            // by any route still clears its unread mark.
            letter.IsRead = true;

            var subject = new Label(letter.Subject);
            subject.AddToClassList("mail-read__subject");
            pane.Add(subject);

            var from = new VisualElement();
            from.AddToClassList("mail-read__from");

            var avatar = new Label(Initial(letter.Sender));
            avatar.AddToClassList("mail-read__avatar");
            avatar.AddToClassList(DotClass(letter, simulation.State.Date));
            from.Add(avatar);

            var who = new VisualElement();

            var name = new Label(letter.Sender);
            name.AddToClassList("mail-read__sender");
            who.Add(name);

            var when = new Label(letter.Arrived.ToString()
                + (letter.DueDayIndex > 0 && !letter.IsClosed
                    ? "   ·   " + Loc.T("mail.due_on", new GameDate(letter.DueDayIndex).ToString())
                    : string.Empty));

            when.AddToClassList("mail-read__date");
            who.Add(when);

            from.Add(who);
            pane.Add(from);

            var body = new Label(letter.Body);
            body.AddToClassList("mail-read__body");
            pane.Add(body);

            if (letter.AmountUsd > 0L && !letter.IsClosed)
            {
                pane.Add(BuildAmount(letter));
            }

            if (problem.Length > 0)
            {
                var trouble = new Label(problem);
                trouble.AddToClassList("mail-read__problem");
                pane.Add(trouble);
            }

            if (letter.IsClosed)
            {
                var done = new Label(letter.Outcome.Length > 0 ? letter.Outcome : Loc.T("mail.closed"));
                done.AddToClassList("mail-read__closed");
                pane.Add(done);
                return pane;
            }

            // The candidate card sits above the buttons, and the wage panel below them, so the
            // order down the page is who they are, what you can do, and what you are proposing.
            if (letter.Candidate != null)
            {
                pane.Add(BuildCandidateCard(letter));
            }

            pane.Add(BuildActions(letter));

            if (letter.Candidate != null && !letter.IsClosed && openOffer == letter.Id)
            {
                pane.Add(BuildNegotiator(letter));
            }
            else if (!string.IsNullOrEmpty(negotiationNote) && letter.Candidate != null)
            {
                var note = new Label(negotiationNote);
                note.AddToClassList("haggle__note");
                pane.Add(note);
            }
            return pane;
        }

        private VisualElement BuildAmount(MailItem letter)
        {
            var late = letter.IsOverdue(simulation.State.Date);

            var block = new VisualElement();
            block.AddToClassList("mail-amount");
            block.EnableInClassList("mail-amount--late", late);

            var caption = new Label(late ? "OVERDUE AND GROWING" : "AMOUNT DUE");
            caption.AddToClassList("mail-amount__label");
            block.Add(caption);

            var figure = new Label(UiFormat.Money(letter.AmountUsd));
            figure.AddToClassList("mail-amount__value");
            block.Add(figure);

            var against = new Label(Loc.T("mail.in_account", UiFormat.Money(simulation.State.CashUsd)));
            against.AddToClassList("mail-amount__foot");
            block.Add(against);

            return block;
        }

        /// <summary>
        /// The face and the papers of somebody who wants a job.
        ///
        /// **A letter about a person should show the person.** The inbox is where hiring is
        /// actually decided and the difference between "Data Engineer, band 3" and a name with a
        /// face and a number attached is the difference between a spreadsheet and a decision.
        /// </summary>
        private VisualElement BuildCandidateCard(MailItem letter)
        {
            var candidate = letter.Candidate;
            var channel = HiringChannels.Get(candidate.Source);
            var definition = candidate.Definition;

            var card = new VisualElement();
            card.AddToClassList("applicant");

            card.Add(CandidateFaces.Frame(candidate, 118, channel.AccentHex));

            var facts = new VisualElement();
            facts.AddToClassList("applicant__facts");

            var name = new Label(candidate.Name);
            name.AddToClassList("applicant__name");
            facts.Add(name);

            var job = new Label(definition.Title.ToUpperInvariant());
            job.AddToClassList("applicant__job");
            facts.Add(job);

            var source = new Label($"{channel.DisplayName.ToUpperInvariant()}  ·  {channel.SiteName}");
            source.AddToClassList("applicant__source");

            if (ColorUtility.TryParseHtmlString(channel.AccentHex, out var accent))
            {
                source.style.color = accent;
            }

            facts.Add(source);

            var grid = new VisualElement();
            grid.AddToClassList("applicant__grid");
            grid.Add(Fact(Loc.T("mail.profile_says"), candidate.AdvertisedLevel.ToString()));
            grid.Add(Fact(Loc.T("mail.assessed_at"), candidate.TrueLevel.ToString()));
            grid.Add(Fact(Loc.T("mail.asking"), Loc.T("mail.per_hour",
                UiFormat.Number(candidate.AskingHourlyUsd, 2))));
            grid.Add(Fact(Loc.T("mail.a_year"),
                UiFormat.Money(candidate.AnnualSalaryUsd(candidate.AskingHourlyUsd))));

            facts.Add(grid);

            var rounds = Negotiation.Patience - letter.NegotiationRounds;
            var patience = new Label(rounds <= 1
                ? Loc.T("mail.close_to_walking")
                : Loc.T("mail.offers_left", Loc.Counted(rounds, "noun.offer")));

            patience.AddToClassList("applicant__patience");
            patience.EnableInClassList("applicant__patience--thin", rounds <= 1);
            facts.Add(patience);

            card.Add(facts);
            return card;
        }

        private static VisualElement Fact(string caption, string value)
        {
            var cell = new VisualElement();
            cell.AddToClassList("applicant__cell");

            var label = new Label(caption);
            label.AddToClassList("applicant__cellcaption");
            cell.Add(label);

            var reading = new Label(value);
            reading.AddToClassList("applicant__cellvalue");
            cell.Add(reading);

            return cell;
        }

        /// <summary>
        /// Wage and signing bonus, and the button that puts them in front of the candidate.
        ///
        /// Two numbers rather than one because they are not the same decision: the wage is a bill
        /// the company pays for years and the bonus is cash it pays once. A candidate two per cent
        /// short can be closed with a bonus without moving the payroll, which is a real trick and
        /// the reason the second field exists.
        /// </summary>
        private VisualElement BuildNegotiator(MailItem letter)
        {
            var panel = new VisualElement();
            panel.AddToClassList("haggle");

            var heading = new Label(Loc.T("hire.your_offer"));
            heading.AddToClassList("haggle__heading");
            panel.Add(heading);

            panel.Add(Field(Loc.T("mail.hourly_wage"), offerHourly, value =>
            {
                offerHourly = Math.Max(0.0, value);
                Refresh();
            }, 5.0, "$"));

            panel.Add(Field(Loc.T("mail.signing_bonus"), offerBonus, value =>
            {
                offerBonus = Math.Max(0.0, value);
                Refresh();
            }, 2500.0, "$"));

            // The face. It reads the same numbers the verdict does, so it can never promise an
            // answer the send button then refuses.
            var mood = Negotiation.MoodFor(letter.Candidate, offerHourly, (long)offerBonus);
            var (face, says) = Negotiation.Portrait(mood);

            var read = new VisualElement();
            read.AddToClassList("haggle__mood");
            read.AddToClassList($"haggle__mood--{mood.ToString().ToLowerInvariant()}");

            var glyph = new Label(face);
            glyph.AddToClassList("haggle__face");
            read.Add(glyph);

            var saying = new Label(says);
            saying.AddToClassList("haggle__says");
            read.Add(saying);

            panel.Add(read);

            var worth = new Label(
                Loc.T("mail.worth_hourly",
                UiFormat.Number(offerHourly + Negotiation.HourlyValueOfBonus((long)offerBonus), 2),
                UiFormat.Money((long)Math.Round(offerHourly * PositionCatalog.PaidHoursPerYear))));

            worth.AddToClassList("haggle__worth");
            panel.Add(worth);

            var buttons = new VisualElement();
            buttons.AddToClassList("haggle__buttons");

            var decline = new Button(() => Act(letter.Id, MailAction.Decline)) { text = Loc.T("hire.decline") };
            decline.AddToClassList("haggle__decline");
            buttons.Add(decline);

            var send = new Button(() => SendOffer(letter)) { text = Loc.T("hire.send_offer") };
            send.AddToClassList("haggle__send");
            buttons.Add(send);

            panel.Add(buttons);

            if (!string.IsNullOrEmpty(negotiationNote))
            {
                var note = new Label(negotiationNote);
                note.AddToClassList("haggle__note");
                panel.Add(note);
            }

            return panel;
        }

        private VisualElement Field(string caption, double value, Action<double> set, double step,
            string prefix)
        {
            var row = new VisualElement();
            row.AddToClassList("haggle__field");

            var label = new Label(caption);
            label.AddToClassList("haggle__label");
            row.Add(label);

            var down = new Button(() => set(value - step)) { text = "-" };
            down.AddToClassList("haggle__step");
            row.Add(down);

            var reading = new Label(prefix + UiFormat.Number(value, 2));
            reading.AddToClassList("haggle__value");
            row.Add(reading);

            var up = new Button(() => set(value + step)) { text = "+" };
            up.AddToClassList("haggle__step");
            row.Add(up);

            return row;
        }

        /// <summary>Public so a test can negotiate without a panel to dispatch clicks into.</summary>
        public OfferVerdict SendOffer(MailItem letter)
        {
            var verdict = simulation.Negotiate(letter, offerHourly, (long)Math.Round(offerBonus),
                out var note);

            negotiationNote = note;

            if (verdict == OfferVerdict.Accepted || verdict == OfferVerdict.WalkedAway)
            {
                openOffer = 0;
            }

            Refresh();
            repaint?.Invoke();
            return verdict;
        }

        /// <summary>Opens the wage panel for a letter, seeded with what they asked for.</summary>
        public void BeginNegotiation(MailItem letter)
        {
            if (letter?.Candidate == null)
            {
                return;
            }

            openOffer = letter.Id;
            offerHourly = Math.Round(letter.Candidate.AskingHourlyUsd, 2);
            offerBonus = 0.0;
            negotiationNote = string.Empty;

            Refresh();
        }

        private VisualElement BuildActions(MailItem letter)
        {
            var bar = new VisualElement();
            bar.AddToClassList("mail-actions");

            foreach (var action in letter.Actions)
            {
                var captured = action;

                // A letter with a person behind it does not use the old one-shot haggle. Accepting
                // signs at their asking rate; haggling opens the wage panel instead of firing a
                // fixed counter, because the whole point is that the player picks the number.
                var button = new Button(() =>
                {
                    if (letter.Candidate != null && captured == MailAction.Accept)
                    {
                        simulation.AcceptAsking(letter, out var note);
                        negotiationNote = note;
                        openOffer = 0;
                        Refresh();
                        repaint?.Invoke();
                        return;
                    }

                    if (letter.Candidate != null && captured == MailAction.Haggle)
                    {
                        BeginNegotiation(letter);
                        return;
                    }

                    // The one action that leaves the game. Handled here rather than in the
                    // simulation, which never learns the address: opening a browser is
                    // `Application.OpenURL` and `Simulation/` does not import UnityEngine.
                    if (captured == MailAction.OpenLink)
                    {
                        FeedbackLink.Open(simulation.State.Date);
                        Act(letter.Id, MailAction.Decline);
                        return;
                    }

                    Act(letter.Id, captured);
                })
                {
                    text = Caption(action, letter)
                };

                button.AddToClassList("mail-action");
                button.AddToClassList(action switch
                {
                    MailAction.Pay => "mail-action--pay",
                    MailAction.Accept => "mail-action--yes",
                    MailAction.Haggle or MailAction.Defer => "mail-action--haggle",
                    MailAction.OpenLink => "mail-action--yes",
                    _ => "mail-action--no"
                });

                bar.Add(button);
            }

            return bar;
        }

        private static string Caption(MailAction action, MailItem letter) => action switch
        {
            MailAction.Pay => Loc.T("mail.act_pay", UiFormat.Money(letter.AmountUsd)),
            MailAction.Accept => letter.Candidate != null
                ? Loc.T("mail.act_accept_hourly",
                    UiFormat.Number(letter.Candidate.AskingHourlyUsd, 2))
                : Loc.T("mail.act_hire_at", UiFormat.Money(letter.AskingSalaryUsd)),

            MailAction.Haggle => letter.Candidate != null
                ? Loc.T("mail.act_negotiate")
                : Loc.T("mail.act_offer_less"),
            MailAction.Defer => Loc.T("mail.act_defer",
                Loc.Counted(CompanySimulation.DeferralStepDays, "noun.day"),
                UiFormat.Percent(CompanySimulation.DeferralInterest, 1)),

            MailAction.OpenLink => Loc.T("feedback.open"),

            // A feedback letter is not being turned down, it is being put away, and a button
            // reading DECLINE over a request for help is a small unkindness.
            _ => letter.Kind == MailKind.Feedback
                ? Loc.T("feedback.dismiss")
                : Loc.T("mail.act_decline")
        };

        /// <summary>Public so a test can drive the button without a panel to dispatch clicks into.</summary>
        public void Act(int mailId, MailAction action)
        {
            problem = simulation.TryActOnMail(mailId, action, out var reason) ? string.Empty : reason;
            selected = mailId;

            Refresh();
            repaint?.Invoke();
        }

        private static string Initial(string sender) =>
            string.IsNullOrWhiteSpace(sender) ? "?" : sender.Substring(0, 1).ToUpperInvariant();
    }
}
