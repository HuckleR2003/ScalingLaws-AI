using System;
using System.Collections.Generic;
using System.Globalization;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>Which way in the player took. Decides which site they are looking at.</summary>
    public enum HiringPortal
    {
        /// <summary>Nothing open.</summary>
        None = 0,

        /// <summary>IThand.hck. A contract job board, reached from the remote button.</summary>
        Remote = 1,

        /// <summary>A state employment register that has not been redesigned since 1998.</summary>
        Agency = 2,

        /// <summary>get-admin.hck. A search tool that finds one person to a specification.</summary>
        Specialist = 3
    }

    /// <summary>
    /// The three places the company can find people, drawn as three websites.
    ///
    /// **They look different on purpose, and the difference is the mechanic.** A player who
    /// remembers that the agency is the beige one with the form numbers already remembers that
    /// agency people arrive thirty per cent worse than advertised. Making all three channels share
    /// one screen with a dropdown would have been less code and would have thrown that away.
    ///
    /// The class owns the shortlists rather than regenerating them per redraw, because the
    /// shortlist costs draws from the company's random stream and a screen that rerolled on every
    /// repaint would let the player fish for a good candidate by pressing a tab twice.
    /// </summary>
    public sealed class HiringPortals
    {
        /// <summary>Candidates one search returns. Enough to choose from, few enough to read.</summary>
        public const int ShortlistSize = 6;

        private readonly Func<CompanyState> state;
        private readonly CompanySimulation simulation;
        private readonly Action refresh;
        private readonly Action openMail;

        private readonly Dictionary<HireSource, List<Candidate>> shortlists = new();

        private PlayerSkill chosen = PlayerSkill.Development;
        private int minimumLevel = 55;
        private string problem = string.Empty;

        public HiringPortals(Func<CompanyState> state, CompanySimulation simulation, Action refresh,
            Action openMail)
        {
            this.state = state;
            this.simulation = simulation;
            this.refresh = refresh;
            this.openMail = openMail;
        }

        /// <summary>Which site is open. Set by the shell before it asks for a page.</summary>
        public HiringPortal Open { get; set; }

        /// <summary>Forgets every shortlist. Called when a save is loaded or the player moves on.</summary>
        public void Reset()
        {
            shortlists.Clear();
            problem = string.Empty;
        }

        public VisualElement Build() => Open switch
        {
            HiringPortal.Remote => BuildRemote(),
            HiringPortal.Agency => BuildAgency(),
            HiringPortal.Specialist => BuildSpecialist(),
            _ => BuildChooser()
        };

        /// <summary>
        /// The three ways to find somebody, when none of them is open yet.
        ///
        /// **This screen used to render nothing at all.** `Open` starts at `None`, and every route
        /// into hiring went through a button on the team page that set it first, so the empty case
        /// was assumed unreachable. It is reachable: coming back from a portal, and from anything
        /// that opens the screen without choosing a channel first. What the player got was a blank
        /// page with one BACK button on it, which reads as a broken game rather than as a screen
        /// waiting for a decision.
        ///
        /// It is also the better screen. The three channels differ on cost, on quality and on how
        /// long they take, and the old flow made that comparison somewhere else and then threw it
        /// away. Here they sit side by side.
        /// </summary>
        private VisualElement BuildChooser()
        {
            var page = new VisualElement();
            page.AddToClassList("hchoose");

            var title = new Label(Loc.T("hire.where_to_look"));
            title.AddToClassList("hchoose__title");
            page.Add(title);

            var strap = new Label(
                Loc.T("hiring.three_ways"));

            strap.AddToClassList("hchoose__strap");
            page.Add(strap);

            var row = new VisualElement();
            row.AddToClassList("hchoose__row");

            foreach (var channel in HiringChannels.All)
            {
                row.Add(BuildChannelCard(channel));
            }

            page.Add(row);
            return page;
        }

        private VisualElement BuildChannelCard(HiringChannel channel)
        {
            var card = new Button(() =>
            {
                Open = channel.Source switch
                {
                    HireSource.Remote => HiringPortal.Remote,
                    HireSource.Agency => HiringPortal.Agency,
                    _ => HiringPortal.Specialist
                };

                problem = string.Empty;
                refresh();
            });

            card.AddToClassList("hcard");

            // The channel's own colour down the left edge, so the three read as three places rather
            // than as three copies of the same card.
            if (ColorUtility.TryParseHtmlString(channel.AccentHex, out var accent))
            {
                card.style.borderLeftColor = accent;
            }

            var address = new Label(channel.SiteName);
            address.AddToClassList("hcard__address");
            card.Add(address);

            var tagline = new Label(channel.Tagline);
            tagline.AddToClassList("hcard__tagline");
            card.Add(tagline);

            var figures = new VisualElement();
            figures.AddToClassList("hcard__figures");

            
            // The two numbers that make this a choice rather than a price list: what they ask, and
            // what they are actually worth against the level on the advert.
            figures.Add(Figure("WAGE",
                channel.WageMultiplier.ToString("0.00", CultureInfo.InvariantCulture) + "x"));
            figures.Add(Figure("WORTH",
                channel.QualityMultiplier.ToString("0.00", CultureInfo.InvariantCulture) + "x"));

            card.Add(figures);
            return card;
        }

        private static VisualElement Figure(string caption, string value)
        {
            var block = new VisualElement();
            block.AddToClassList("hcard__figure");

            var label = new Label(caption);
            label.AddToClassList("hcard__figurelabel");
            block.Add(label);

            var reading = new Label(value);
            reading.AddToClassList("hcard__figurevalue");
            block.Add(reading);

            return block;
        }

        // ---- IThand.hck -----------------------------------------------------------------------

        private VisualElement BuildRemote()
        {
            var company = state();
            var channel = HiringChannels.Get(HireSource.Remote);

            var page = Site("ithand", channel.SiteName, channel.Tagline);

            var seats = company.Hiring.RemoteSeats;
            var used = company.Staff.CountFrom(HireSource.Remote);

            var bar = new VisualElement();
            bar.AddToClassList("ithand__bar");

            var count = new Label(Loc.T("hiring.contracts_running", used, seats));
            count.AddToClassList("ithand__count");
            bar.Add(count);

            if (!company.Hiring.HasRemotePartnership)
            {
                var partner = new Button(() =>
                {
                    problem = simulation.TryBuyRemotePartnership();
                    refresh();
                })
                {
                    text = Loc.T("hiring.become_partner", UiFormat.Money(HiringChannels.PartnershipCostUsd))
                };

                partner.AddToClassList("ithand__partner");
                bar.Add(partner);

                var why = new Label(
                    Loc.T("hiring.partner_note", HiringChannels.PartneredRemoteSeats,
                    HiringChannels.FreeRemoteSeats));

                why.AddToClassList("ithand__why");
                page.Add(bar);
                page.Add(why);
            }
            else
            {
                var badge = new Label(Loc.T("hire.partner"));
                badge.AddToClassList("ithand__badge");
                bar.Add(badge);
                page.Add(bar);
            }

            page.Add(Warning(
                $"Contract rates are {1.0 - channel.WageMultiplier:P0} below staff rates and the "
                + $"people are {1.0 - channel.QualityMultiplier:P0} weaker than their profile says. "
                + "This is the cheap end of the market and it is cheap for a reason."));

            page.Add(SkillPicker());
            page.Add(SearchButton(HireSource.Remote, "SEARCH CONTRACTORS"));
            page.Add(Results(HireSource.Remote));

            return page;
        }

        // ---- the register ---------------------------------------------------------------------

        private VisualElement BuildAgency()
        {
            var channel = HiringChannels.Get(HireSource.Agency);
            var page = Site("register", channel.SiteName, channel.Tagline);

            // The whole character of this screen is that it was built by somebody who had to,
            // twenty years ago, and has been maintained by nobody since.
            var masthead = new Label(
                Loc.T("hiring.register_header"));

            masthead.AddToClassList("register__masthead");
            page.Add(masthead);

            var notice = new Label(
                Loc.T("hiring.register_notice"));

            notice.AddToClassList("register__notice");
            page.Add(notice);

            page.Add(SkillPicker());
            page.Add(SearchButton(HireSource.Agency, "SUBMIT QUERY  [F4]"));
            page.Add(Results(HireSource.Agency));

            var footer = new Label(
                Loc.T("hiring.register_footer"));

            footer.AddToClassList("register__footer");
            page.Add(footer);

            return page;
        }

        // ---- get-admin.hck ---------------------------------------------------------------------

        private VisualElement BuildSpecialist()
        {
            var company = state();
            var channel = HiringChannels.Get(HireSource.Specialist);

            var page = Site("getadmin", channel.SiteName, channel.Tagline);

            page.Add(SkillPicker());

            // The slider is the reason this channel is worth a fifth more salary: it is the only
            // one that can be told what to find, and the fee scales with how much is being asked.
            var band = new VisualElement();
            band.AddToClassList("getadmin__band");

            var bandTitle = new Label(Loc.T("hire.minimum_level"));
            bandTitle.AddToClassList("getadmin__label");
            band.Add(bandTitle);

            var slider = new SliderInt(10, PlayerSkillLimits.MaximumLevel) { value = minimumLevel };
            slider.AddToClassList("getadmin__slider");
            slider.RegisterValueChangedCallback(change =>
            {
                minimumLevel = change.newValue;
                refresh();
            });

            band.Add(slider);

            var reading = new Label(
                Loc.T("hiring.specialist_line", minimumLevel,
                (int)Math.Round(minimumLevel * channel.QualityMultiplier),
                UiFormat.Money(CompanySimulation.SpecialistFeeUsd(chosen, minimumLevel))));

            reading.AddToClassList("getadmin__reading");
            band.Add(reading);

            page.Add(band);

            var fee = CompanySimulation.SpecialistFeeUsd(chosen, minimumLevel);

            var commission = new Button(() =>
            {
                if (company.CashUsd < fee)
                {
                    problem = $"The search fee is {UiFormat.Money(fee)} and the company has "
                        + $"{UiFormat.Money(company.CashUsd)}.";
                    refresh();
                    return;
                }

                // Paid whether or not they sign. That is what a retained search is.
                company.PostCash(LedgerLine.Salaries, fee);

                shortlists[HireSource.Specialist] = new List<Candidate>(
                    simulation.Shortlist(chosen, HireSource.Specialist, minimumLevel, 1));

                problem = string.Empty;
                refresh();
            })
            { text = Loc.T("hiring.commission_search", UiFormat.Money(fee)) };

            commission.AddToClassList("getadmin__go");
            commission.SetEnabled(company.CashUsd >= fee);
            page.Add(commission);

            page.Add(Results(HireSource.Specialist));
            return page;
        }

        // ---- the pieces all three share ---------------------------------------------------------

        private VisualElement Site(string skin, string address, string tagline)
        {
            var page = new VisualElement();
            page.AddToClassList("portal");
            page.AddToClassList($"portal--{skin}");

            var bar = new VisualElement();
            bar.AddToClassList("portal__urlbar");

            var url = new Label(address);
            url.AddToClassList("portal__url");
            bar.Add(url);

            page.Add(bar);

            var line = new Label(tagline);
            line.AddToClassList("portal__tagline");
            page.Add(line);

            if (!string.IsNullOrEmpty(problem))
            {
                var trouble = new Label(problem);
                trouble.AddToClassList("portal__problem");
                page.Add(trouble);
            }

            return page;
        }

        private VisualElement SkillPicker()
        {
            var row = new VisualElement();
            row.AddToClassList("portal__skills");

            foreach (var position in PositionCatalog.All)
            {
                var captured = position.Skill;

                var chip = new Button(() =>
                {
                    chosen = captured;
                    refresh();
                });

                chip.AddToClassList("skillchip");
                chip.EnableInClassList("skillchip--on", chosen == captured);

                var icon = SkillIcons.Badge(position.Skill, 26);
                icon.AddToClassList("skillchip__icon");
                chip.Add(icon);

                var name = new Label(position.Title);
                name.AddToClassList("skillchip__name");
                chip.Add(name);

                row.Add(chip);
            }

            return row;
        }

        private VisualElement SearchButton(HireSource source, string caption)
        {
            var button = new Button(() =>
            {
                shortlists[source] = new List<Candidate>(
                    simulation.Shortlist(chosen, source, source == HireSource.Remote ? 34 : 48,
                        ShortlistSize));

                problem = string.Empty;
                refresh();
            })
            { text = caption };

            button.AddToClassList("portal__search");
            return button;
        }

        private VisualElement Results(HireSource source)
        {
            var list = new VisualElement();
            list.AddToClassList("portal__results");

            if (!shortlists.TryGetValue(source, out var found) || found.Count == 0)
            {
                return list;
            }

            foreach (var candidate in found)
            {
                if (candidate.Position != chosen)
                {
                    continue;
                }

                list.Add(Row(candidate));
            }

            return list;
        }

        private VisualElement Row(Candidate candidate)
        {
            var channel = HiringChannels.Get(candidate.Source);
            var definition = candidate.Definition;

            var row = new VisualElement();
            row.AddToClassList("candidate");

            row.Add(CandidateFaces.Frame(candidate, 54, channel.AccentHex));

            var text = new VisualElement();
            text.AddToClassList("candidate__text");

            var name = new Label(candidate.Name);
            name.AddToClassList("candidate__name");
            text.Add(name);

            var line = new Label(
                $"{definition.Title}  ·  profile {candidate.AdvertisedLevel}  ·  "
                + $"assessed {candidate.TrueLevel}");

            line.AddToClassList("candidate__line");
            text.Add(line);

            var asking = new Label(Loc.T("hiring.asking_hourly", UiFormat.Number(candidate.AskingHourlyUsd, 2),
                UiFormat.Money(candidate.AnnualSalaryUsd(candidate.AskingHourlyUsd))));

            asking.AddToClassList("candidate__asking");
            text.Add(asking);

            row.Add(text);

            var contact = new Button(() =>
            {
                problem = simulation.TryApproach(candidate);

                if (string.IsNullOrEmpty(problem))
                {
                    // They are gone from the board the moment the company writes to them, so the
                    // player cannot approach the same person twice and hold two conversations.
                    foreach (var pair in shortlists)
                    {
                        pair.Value.Remove(candidate);
                    }
                }

                refresh();
            })
            { text = Loc.T("hire.get_in_touch") };

            contact.AddToClassList("candidate__contact");
            row.Add(contact);

            return row;
        }

        private static VisualElement Warning(string text)
        {
            var label = new Label(text);
            label.AddToClassList("portal__warning");
            return label;
        }

        /// <summary>The way from a portal back to the inbox, once somebody has written.</summary>
        public VisualElement InboxLink()
        {
            var button = new Button(openMail) { text = Loc.T("hire.open_inbox") };
            button.AddToClassList("portal__inbox");
            return button;
        }
    }
}
