using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Turns things that happened into things the player reads.
    ///
    /// **This is the layer the game was missing.** Thirty four kinds of event were being raised every
    /// day and drained into a list nothing read, so a rival could ship, a loan could default and a
    /// regulator could pull the company's flagship off the market without a single word appearing
    /// anywhere. The systems all worked. Nobody was told.
    ///
    /// The rule this file obeys: **it translates, it does not decide.** Every headline is built from
    /// an event that already happened or from a rival's actual state. Nothing here rolls dice, sets
    /// state or invents a number, so news can never disagree with the simulation it describes. The
    /// one exception is the paid desks, where being wrong is the product being sold, and that
    /// randomness lives in <see cref="IntelligenceService"/> where it already did.
    /// </summary>
    public static class NewsDesk
    {
        /// <summary>Days between dossiers from a desk that is being paid.</summary>
        public const int DossierIntervalDays = 21;

        /// <summary>
        /// Where an event belongs on the page, or null when it is not news.
        ///
        /// Some events exist for the interface rather than for the reader. A skill levelling is a
        /// number going up in a panel the player is already looking at; printing it in a newspaper
        /// would bury the fine that arrived the same morning. **Filtering is the job here.** A feed
        /// that prints everything is the drained list with extra steps.
        /// </summary>
        public static bool TryFile(in CompanyEvent raised, string companyName, out NewsItem item)
        {
            item = default;
            var company = string.IsNullOrWhiteSpace(companyName) ? Loc.T("news.company.anon") : companyName;

            switch (raised.Type)
            {
                // ---- who is working with whom -------------------------------------------------
                //
                // **These are public and the rest of the relations system is not.** An alliance and
                // a joint programme are announced by everybody who signs one, which is what the
                // Frontier Model Forum was; an offer sent and an offer refused are a letter and its
                // answer, and a wire that reported those would be reading the player's post.
                //
                // Premieres rather than the Wire, because this section is what shipped and a
                // partnership is a thing two companies shipped together.
                case CompanyEventType.AllianceSigned:
                    item = new NewsItem(raised.Date, NewsSection.Premieres,
                        Loc.T("news.h.ally_signed", company), raised.Message, Loc.T("news.outlet.wire"), true,
                        NewsWeight.Notable);

                    return true;

                case CompanyEventType.AllianceBroken:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        Loc.T("news.h.ally_lost", company), raised.Message, Loc.T("news.outlet.wire"), true,
                        NewsWeight.Notable);

                    return true;

                case CompanyEventType.CampaignStarted:
                    item = new NewsItem(raised.Date, NewsSection.Premieres,
                        Loc.T("news.h.joint_open", company), raised.Message, Loc.T("news.outlet.wire"), true,
                        NewsWeight.Notable);

                    return true;

                case CompanyEventType.CampaignFinished:
                    item = new NewsItem(raised.Date, NewsSection.Wire,
                        Loc.T("news.h.joint_closed", company), raised.Message, Loc.T("news.outlet.wire"), true,
                        NewsWeight.Routine);

                    return true;

                // **Walking out is the one of these that is a story.** A programme that ran its
                // whole term is a line on the wire; one abandoned halfway is something the other
                // side has an opinion about.
                case CompanyEventType.CampaignLeft:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        Loc.T("news.h.joint_walked", company), raised.Message, Loc.T("news.outlet.wire"), true,
                        NewsWeight.Notable);

                    return true;

                // ---- trouble, ours and theirs -------------------------------------------------
                case CompanyEventType.SafetyIncident:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        Loc.T("news.h.scrutiny", company), raised.Message, Loc.T("news.outlet.wire"),
                        true, NewsWeight.Loud);
                    return true;

                case CompanyEventType.LoanDefaulted:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        Loc.T("news.h.default", company),
                        raised.Message + " " + Loc.T("news.h.default_note"),
                        Loc.T("news.outlet.wire"), true, NewsWeight.Loud);
                    return true;

                case CompanyEventType.LoanMissed:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        Loc.T("news.h.missed", company), raised.Message, Loc.T("news.outlet.wire"),
                        true, NewsWeight.Notable);
                    return true;

                case CompanyEventType.CreditLineBreached:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        Loc.T("news.h.no_room", company), raised.Message, Loc.T("news.outlet.wire"), true,
                        NewsWeight.Loud);
                    return true;

                case CompanyEventType.Bankrupt:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        Loc.T("news.h.folds", company), raised.Message, Loc.T("news.outlet.wire"),
                        true, NewsWeight.Loud);
                    return true;

                // **Both halves of a smear, and they are opposite stories.** One that lands is
                // about the target and carries no name, because the wire does not know who paid for
                // it; one that is traced back is a scandal about this company and says so. Until
                // this arm existed, the loudest thing a player can do to a rival reached the reader
                // as nothing at all.
                case CompanyEventType.SmearLaunched:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        Loc.T("news.smear.landed"), raised.Message, "Wire", false,
                        NewsWeight.Notable);
                    return true;

                case CompanyEventType.SmearBackfired:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        Loc.T("news.smear.traced", company), raised.Message, "Wire", true,
                        NewsWeight.Loud);
                    return true;

                case CompanyEventType.SmearThreatened:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        Loc.T("news.smear.threat", company), raised.Message, "Wire", true,
                        NewsWeight.Notable);
                    return true;

                // Not a scandal in the moral sense, and it belongs here anyway: being unable to serve
                // the demand you attracted is the weakness a reader would notice first.
                case CompanyEventType.DemandUnserved:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        Loc.T("news.h.turning_away", company), raised.Message, Loc.T("news.outlet.wire"), true,
                        NewsWeight.Notable);
                    return true;

                // ---- what shipped ----------------------------------------------------------------
                case CompanyEventType.ModelReleased:
                    item = new NewsItem(raised.Date, NewsSection.Premieres,
                        Loc.T("news.h.ships", company), raised.Message, Loc.T("news.outlet.wire"),
                        true, NewsWeight.Loud);
                    return true;

                case CompanyEventType.RivalReleased:
                    item = new NewsItem(raised.Date, NewsSection.Premieres,
                        raised.Message, Loc.T("news.rival.body"), Loc.T("news.outlet.wire"), false,
                        NewsWeight.Notable);
                    return true;

                case CompanyEventType.ModelShelved:
                    item = new NewsItem(raised.Date, NewsSection.Premieres,
                        Loc.T("news.h.finishes_run", company), raised.Message, Loc.T("news.outlet.wire"),
                        true, NewsWeight.Routine);
                    return true;

                // ---- the company's own business --------------------------------------------------
                case CompanyEventType.TrainingCompleted:
                case CompanyEventType.UpgradeCompleted:
                case CompanyEventType.ResearchCompleted:
                case CompanyEventType.ArchitectureResearchCompleted:
                    item = new NewsItem(raised.Date, NewsSection.Wire,
                        Loc.T("news.h.work_done", company), raised.Message, Loc.T("news.outlet.wire"),
                        true, NewsWeight.Notable);
                    return true;

                case CompanyEventType.FundingClosed:
                case CompanyEventType.LoanTaken:
                case CompanyEventType.LoanSettled:
                    item = new NewsItem(raised.Date, NewsSection.Wire,
                        Loc.T("news.h.money", company), raised.Message, Loc.T("news.outlet.wire"),
                        true, NewsWeight.Notable);
                    return true;

                case CompanyEventType.FundingOffered:
                case CompanyEventType.FundingExpired:
                case CompanyEventType.ComputeTierUnlocked:
                case CompanyEventType.HardwareDelivered:
                case CompanyEventType.HardwareSold:
                case CompanyEventType.OfficeMoved:
                case CompanyEventType.StaffLeft:
                case CompanyEventType.MarketingFinished:
                    item = new NewsItem(raised.Date, NewsSection.Wire,
                        Loc.T("news.h.plain", company), raised.Message, Loc.T("news.outlet.wire"), true,
                        NewsWeight.Routine);
                    return true;

                // ---- deliberately not printed -----------------------------------------------------
                // Training started, hardware ordered, staff hired, skills levelled, intel received,
                // architecture adopted, data acquired. Each of these is the player's own click coming
                // back at them a second later, and a feed that reports the player to themselves is
                // noise that hides the events they did not cause.
                default:
                    return false;
            }
        }

        /// <summary>
        /// A signal from a paid desk, written up under that desk's own masthead.
        ///
        /// Which section it lands in follows the outlet, so a player who pays for one thing reads one
        /// column. The confidence printed is the desk's own claim, never the truth, exactly as
        /// <see cref="IntelSignal"/> intends.
        /// </summary>
        /// <summary>
        /// A chapter from a rival's history, on the day it happens.
        ///
        /// **This is free news and it is the point of the whole dossier layer.** A player who never
        /// buys a membership still watches four companies rise and three of them come apart, and
        /// the ones that come apart do so for reasons the player is themselves exposed to: a data
        /// question that arrives in court eighteen months late, a team that walks, a bill nobody
        /// worked out how to pay. Reading that happen to somebody else is the cheapest possible way
        /// to learn that safety and cost are not side quests.
        ///
        /// The desk still only translates. The chapter already happened, the date is authored, and
        /// nothing here rolls a die or changes a number.
        /// </summary>
        public static NewsItem FromLabChapter(in LabDossier lab, in LabChapter chapter)
        {
            var section = chapter.Kind switch
            {
                LabChapterKind.Scandal => NewsSection.Scandals,
                LabChapterKind.Setback => NewsSection.Scandals,
                LabChapterKind.Exit => NewsSection.Scandals,
                LabChapterKind.Milestone => NewsSection.Premieres,
                _ => NewsSection.Wire
            };

            // An exit or a scandal at this scale is the loudest thing that happens on a given day,
            // and the corner banner shows the loudest story rather than the most recent one.
            var weight = chapter.Kind switch
            {
                LabChapterKind.Exit => NewsWeight.Loud,
                LabChapterKind.Scandal => NewsWeight.Loud,
                LabChapterKind.Setback => NewsWeight.Notable,
                LabChapterKind.Funding => NewsWeight.Notable,
                _ => NewsWeight.Notable
            };

            var body = chapter.Body;
            if (chapter.IsProjection)
            {
                // The honesty flag, in the one place a player will actually read it. A dated event
                // past what is known is the game's guess and has to say so.
                body += "\n\n" + Loc.T("news.chapter.projection");
            }

            return new NewsItem(chapter.On, section, Loc.T("news.chapter.head", lab.Name, chapter.Headline), body,
                lab.Name, isAboutPlayer: false, weight);
        }

        /// <summary>
        /// A world event, on the day it starts.
        ///
        /// **The loudest thing on the wire, because it is the only kind of story that happens to
        /// everybody.** A rival's collapse is about one company; a shortage or a price war is about
        /// the market the player is standing in, and it will be moving their numbers for months
        /// after the headline scrolls away.
        ///
        /// The desk still only translates. The date is in the catalog, the magnitude is in the
        /// catalog, and nothing here rolls anything or changes a number.
        /// </summary>
        public static NewsItem FromWorldEvent(in WorldEvent world)
        {
            var body = Loc.T(world.Key + ".body");

            if (world.IsProjection)
            {
                // The honesty flag, where a player will actually read it. Everything in this game
                // dated past the record has to say which side of that line it is on.
                body += "\n\n" + Loc.T("world.projection");
            }

            return new NewsItem(world.On, NewsSection.Wire, Loc.T(world.Key + ".head"), body,
                Loc.T("world.source"), isAboutPlayer: false, NewsWeight.Loud);
        }

        public static NewsItem FromSignal(in IntelSignal signal)
        {
            var section = signal.Tier switch
            {
                IntelTier.TrendSearch => NewsSection.TotalTrueNews,
                IntelTier.KnownWords => NewsSection.ItSpy,
                IntelTier.NationalPress => NewsSection.EventHunter,
                _ => NewsSection.Wire
            };

            var body = signal.Detail
                + "\n\n" + Loc.T("news.signal.filed", signal.IssuedOn.ToString(),
                    signal.LeadTimeDays.ToString(Invariant),
                    signal.Confidence.ToString("P0", Invariant));

            return new NewsItem(signal.IssuedOn, section, signal.Headline, body,
                NewsCatalog.OutletName(signal.Tier), false,
                signal.Confidence >= 0.85 ? NewsWeight.Loud : NewsWeight.Notable);
        }

        /// <summary>
        /// KnownWords on one rival: what they sell, how it is doing, and what they are sitting on.
        ///
        /// **Every figure is read, not estimated.** Revenue is the users the market says that lab
        /// holds at the price the market says they charge, which is the same arithmetic that bills
        /// the player. Model counts are what the lab has actually shipped. A dossier that guessed
        /// would be a second opinion about a number the game already knows.
        /// </summary>
        public static NewsItem Dossier(CompetitorAgent lab, GameDate date, double users,
            double revenuePerYearUsd, int shipped)
        {
            var headline = revenuePerYearUsd >= 1_000_000.0
                ? Loc.T("news.spy.earning", lab.LabName,
                    "$" + (revenuePerYearUsd / 1_000_000.0).ToString("N0", Invariant) + "M")
                : Loc.T("news.spy.barely", lab.LabName);

            var lines = new List<string>
            {
                lab.HasShipped
                    ? Loc.T("news.spy.onsale", lab.LiveModelName,
                        lab.LiveCapability.ToString("0.0", Invariant))
                    : Loc.T("news.spy.onsale_none"),
                Loc.T("news.spy.built",
                    shipped == 1
                        ? Loc.T("news.spy.model_one")
                        : Loc.T("news.spy.models", shipped.ToString(Invariant)),
                    (lab.HasShipped ? 1 : 0).ToString(Invariant)),
                Loc.T("news.spy.focus", ModelTypeCatalog.Get(lab.LiveType).DisplayName),
                Loc.T("news.spy.audience", users.ToString("N0", Invariant)),
                lab.LivePrice > 1.05
                    ? Loc.T("news.spy.dear", (lab.LivePrice - 1.0).ToString("P0", Invariant))
                    : lab.LivePrice < 0.95
                        ? Loc.T("news.spy.cheap", (1.0 - lab.LivePrice).ToString("P0", Invariant))
                        : Loc.T("news.spy.par")
            };

            if (lab.IsWaitingForHardware)
            {
                // The single most actionable thing this desk can find. A lab sitting out a hardware
                // cycle is a lab that will come back with something better than what is on sale now.
                lines.Add(Loc.T("news.spy.stalled", lab.WaitingFor,
                    lab.AccumulatedDelayDays.ToString(Invariant)));
            }

            return new NewsItem(date, NewsSection.ItSpy, headline, string.Join("\n", lines),
                NewsCatalog.OutletName(IntelTier.KnownWords), false,
                lab.IsWaitingForHardware ? NewsWeight.Loud : NewsWeight.Routine);
        }

        /// <summary>
        /// The one culture every figure filed by this desk is formatted in.
        ///
        /// **`Simulation/` may not reach into `UI/`, so `UiFormat` is out of bounds here**, and the
        /// rule it exists to enforce applies all the same: a raw `:N0` or `:P0` follows the
        /// machine's own language, and this project has shipped `$20,00` and `0,70x` on that fault
        /// five separate times.
        /// </summary>
        private static readonly System.Globalization.CultureInfo Invariant =
            System.Globalization.CultureInfo.InvariantCulture;
    }
}
