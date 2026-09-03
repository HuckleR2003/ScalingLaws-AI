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
            var company = string.IsNullOrWhiteSpace(companyName) ? "The company" : companyName;

            switch (raised.Type)
            {
                // ---- trouble, ours and theirs -------------------------------------------------
                case CompanyEventType.SafetyIncident:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        $"{company} under scrutiny", raised.Message, "Wire", true, NewsWeight.Loud);
                    return true;

                case CompanyEventType.LoanDefaulted:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        $"{company} defaults on a loan",
                        raised.Message + " Lenders price the next one accordingly.",
                        "Wire", true, NewsWeight.Loud);
                    return true;

                case CompanyEventType.LoanMissed:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        $"{company} misses a repayment", raised.Message, "Wire", true, NewsWeight.Notable);
                    return true;

                case CompanyEventType.CreditLineBreached:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        $"{company} is out of room at the bank", raised.Message, "Wire", true,
                        NewsWeight.Loud);
                    return true;

                case CompanyEventType.Bankrupt:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        $"{company} folds", raised.Message, "Wire", true, NewsWeight.Loud);
                    return true;

                // Not a scandal in the moral sense, and it belongs here anyway: being unable to serve
                // the demand you attracted is the weakness a reader would notice first.
                case CompanyEventType.DemandUnserved:
                    item = new NewsItem(raised.Date, NewsSection.Scandals,
                        $"{company} is turning customers away", raised.Message, "Wire", true,
                        NewsWeight.Notable);
                    return true;

                // ---- what shipped ----------------------------------------------------------------
                case CompanyEventType.ModelReleased:
                    item = new NewsItem(raised.Date, NewsSection.Premieres,
                        $"{company} ships", raised.Message, "Wire", true, NewsWeight.Loud);
                    return true;

                case CompanyEventType.RivalReleased:
                    item = new NewsItem(raised.Date, NewsSection.Premieres,
                        raised.Message, "A new entry on the board. Par moves whether or not anything "
                        + "of yours changed today.", "Wire", false, NewsWeight.Notable);
                    return true;

                case CompanyEventType.ModelShelved:
                    item = new NewsItem(raised.Date, NewsSection.Premieres,
                        $"{company} finishes a run", raised.Message, "Wire", true, NewsWeight.Routine);
                    return true;

                // ---- the company's own business --------------------------------------------------
                case CompanyEventType.TrainingCompleted:
                case CompanyEventType.UpgradeCompleted:
                case CompanyEventType.ResearchCompleted:
                case CompanyEventType.ArchitectureResearchCompleted:
                    item = new NewsItem(raised.Date, NewsSection.Wire,
                        $"{company}: work finished", raised.Message, "Wire", true, NewsWeight.Notable);
                    return true;

                case CompanyEventType.FundingClosed:
                case CompanyEventType.LoanTaken:
                case CompanyEventType.LoanSettled:
                    item = new NewsItem(raised.Date, NewsSection.Wire,
                        $"{company}: money", raised.Message, "Wire", true, NewsWeight.Notable);
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
                        $"{company}", raised.Message, "Wire", true, NewsWeight.Routine);
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
                body += "\n\nProjection. This is where the game expects this to go, not something "
                    + "that has been announced.";
            }

            return new NewsItem(chapter.On, section, $"{lab.Name}: {chapter.Headline}", body,
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
                + $"\n\nFiled {signal.IssuedOn}, looking {signal.LeadTimeDays} days out. "
                + $"The desk puts {signal.Confidence:P0} on this.";

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
                ? $"{lab.LabName} is running at ${revenuePerYearUsd / 1_000_000.0:N0}M a year"
                : $"{lab.LabName} is barely earning";

            var lines = new List<string>
            {
                $"On sale: {(lab.HasShipped ? lab.LiveModelName : "nothing")}"
                    + (lab.HasShipped ? $", scoring {lab.LiveCapability:0.0}." : "."),
                $"Built to date: {shipped} model{(shipped == 1 ? string.Empty : "s")}, "
                    + $"{(lab.HasShipped ? 1 : 0)} of them still on sale.",
                $"Focus: {ModelTypeCatalog.Get(lab.LiveType).DisplayName}.",
                $"Audience: about {users:N0} people.",
                lab.LivePrice > 1.05
                    ? $"They charge {(lab.LivePrice - 1.0):P0} over the going rate."
                    : lab.LivePrice < 0.95
                        ? $"They undercut the going rate by {(1.0 - lab.LivePrice):P0}."
                        : "They price at the going rate."
            };

            if (lab.IsWaitingForHardware)
            {
                // The single most actionable thing this desk can find. A lab sitting out a hardware
                // cycle is a lab that will come back with something better than what is on sale now.
                lines.Add($"They have stopped shipping and are waiting on {lab.WaitingFor}. "
                    + $"Their next launch has slipped {lab.AccumulatedDelayDays} days so far, which "
                    + "means whatever lands will be trained on newer silicon than yours.");
            }

            return new NewsItem(date, NewsSection.ItSpy, headline, string.Join("\n", lines),
                "KnownWords", false,
                lab.IsWaitingForHardware ? NewsWeight.Loud : NewsWeight.Routine);
        }
    }
}
