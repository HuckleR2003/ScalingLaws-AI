using System;
using System.Collections.Generic;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// The contract with a government, and the sectors running under it.
    ///
    /// **The end of the game, and it is the same game.** Nothing here is a new economy. The fee joins
    /// the books as revenue, the capacity comes out of the pool that serves paying customers, the
    /// power joins the bill the fleet already pays, and a failure lands through the penalty path an
    /// incident already uses. What changes is the scale: a sector pays more in a day than most
    /// campaigns earn in a month, and one failure costs more than the company is worth.
    ///
    /// **The trap is capacity, and it is the spine of this game at national scale.** A state does not
    /// queue behind consumer traffic, so every sector adopted is petaflops the market no longer has.
    /// A company that signs for four sectors and stops building has not bought income, it has sold
    /// its own customers and put a government on a cluster that cannot serve it.
    ///
    /// **The risk is the player's own doing.** It is not a die rolled against a company doing
    /// everything right; it climbs when the fleet cannot cover what was promised. A programme that is
    /// fully served is very nearly safe. One at seventy per cent of what it promised is a company
    /// waiting for a nine-figure letter, and the screen says so every day beforehand.
    /// </summary>
    public sealed class StateProgramme
    {
        /// <summary>
        /// Daily chance of a failure per unit of sector weight, when everything is fully served.
        ///
        /// **Small, and it is not zero.** Perfect delivery makes the programme safe rather than
        /// certain: running a country's bureaucracy on models has a residual risk that no amount of
        /// capacity removes, and a zero here would make the endgame free money for anybody who
        /// overbuilt once. At weight 3.4 (Defence, alone, fully served) this is about one failure
        /// every eleven years.
        /// </summary>
        public const double BaseDailyRisk = 0.00007;

        /// <summary>
        /// How much worse a shortfall makes it, at the worst.
        ///
        /// A programme delivering nothing is thirty times more likely to fail than one delivering
        /// everything. Steep on purpose: the difference between ninety and a hundred per cent has to
        /// be worth building for, which is the same curve `ServiceQuality` already uses on latency
        /// and for the same reason.
        /// </summary>
        public const double WorstShortfallMultiplier = 30.0;

        /// <summary>
        /// The record a state keeps watching after it signs.
        ///
        /// Below this the contract is not torn up - governments are slower than that - but the
        /// programme is put on notice and the failure risk doubles. Deliberately lower than the
        /// threshold to sign: it is harder to win a contract than to keep one.
        /// </summary>
        public const double NoticeBelow = 0.72;

        /// <summary>How much the risk climbs while the programme is on notice.</summary>
        public const double OnNoticeMultiplier = 2.0;

        /// <summary>Days between failures at most. Nothing may fire twice in a fortnight.</summary>
        public const int QuietDaysAfterFailure = 45;

        private readonly HashSet<StateSector> running = new();

        /// <summary>Has the state signed. Nothing below matters until it has.</summary>
        public bool IsSigned { get; private set; }

        /// <summary>Which country signed. The player's own, since that is who knows them.</summary>
        public Country Signatory { get; private set; } = Country.None;

        /// <summary>The day the contract began, for the news and for the ledger.</summary>
        public GameDate SignedOn { get; private set; }

        /// <summary>Sectors the models are running. Order is not kept; the catalog orders them.</summary>
        public IReadOnlyCollection<StateSector> Running => running;

        /// <summary>The last day a failure landed, so they cannot arrive in a burst.</summary>
        public GameDate LastFailure { get; private set; } = new(-9999);

        /// <summary>How many failures the programme has had. Shown, and it never resets.</summary>
        public int Failures { get; private set; }

        /// <summary>Total the company has been billed for them.</summary>
        public long PaidOutUsd { get; private set; }

        /// <summary>
        /// Yesterday's delivery, from 0 to 1.
        ///
        /// **Saved, because tomorrow's risk reads it.** It looks like a display value and it is
        /// causal, which is the sixth time something in this project has turned out that way. The
        /// save replay tests are the guard.
        /// </summary>
        public double LastDelivery { get; private set; } = 1.0;

        public bool IsRunning(StateSector sector) => running.Contains(sector);

        /// <summary>What the state pays today, before anything is taken off for failing to deliver.</summary>
        public long FeeUsdPerDay
        {
            get
            {
                if (!IsSigned)
                {
                    return 0L;
                }

                var total = StateSectorCatalog.BaseFeeUsdPerDay;

                foreach (var sector in running)
                {
                    total += StateSectorCatalog.Get(sector).FeeUsdPerDay;
                }

                return total;
            }
        }

        /// <summary>Capacity the programme holds, whether or not the company has it.</summary>
        public double PetaflopsRequired
        {
            get
            {
                if (!IsSigned)
                {
                    return 0.0;
                }

                var total = StateSectorCatalog.BasePetaflops;

                foreach (var sector in running)
                {
                    total += StateSectorCatalog.Get(sector).PetaflopsRequired;
                }

                return total;
            }
        }

        /// <summary>Power the programme draws, into the same bill the fleet already pays.</summary>
        public double MegawattsRequired
        {
            get
            {
                var total = 0.0;

                foreach (var sector in running)
                {
                    total += StateSectorCatalog.Get(sector).MegawattsRequired;
                }

                return total;
            }
        }

        /// <summary>The sum of what the running sectors are worth in risk.</summary>
        public double FailureWeight
        {
            get
            {
                var total = 0.0;

                foreach (var sector in running)
                {
                    total += StateSectorCatalog.Get(sector).FailureWeight;
                }

                return total;
            }
        }

        /// <summary>
        /// What the state is actually paying, given what it is actually getting.
        ///
        /// **A shortfall costs money before it costs anything else**, and that is the warning shot.
        /// A programme at eighty per cent is paid at eighty per cent, which is visible in the books
        /// the same week, months before the failure it is also making more likely.
        /// </summary>
        public long EarnedUsdPerDay(double delivery) =>
            (long)Math.Round(FeeUsdPerDay * Math.Clamp(delivery, 0.0, 1.0));

        /// <summary>
        /// Today's chance of something going wrong at national scale.
        ///
        /// One method, so the number on the screen is the number the roll uses. That was worth
        /// stating explicitly the last time this project had two: telling the player a figure that
        /// governs nothing is worse than telling them nothing.
        /// </summary>
        public double DailyFailureRisk(double delivery, double safetyRecord, double incidentMultiplier)
        {
            if (!IsSigned || running.Count == 0)
            {
                return 0.0;
            }

            var shortfall = 1.0 - Math.Clamp(delivery, 0.0, 1.0);

            // Squared, so the last stretch of delivery is where the risk actually lives. Linear
            // would make ninety-five per cent and eighty per cent nearly the same decision.
            var pressure = 1.0 + (WorstShortfallMultiplier - 1.0) * shortfall * shortfall;

            var notice = safetyRecord < NoticeBelow ? OnNoticeMultiplier : 1.0;

            return FailureWeight * BaseDailyRisk * pressure * notice
                   * Math.Max(0.0, incidentMultiplier);
        }

        /// <summary>Is the state watching the company rather than trusting it.</summary>
        public static bool IsOnNotice(double safetyRecord) => safetyRecord < NoticeBelow;

        // ---- changing it -----------------------------------------------------------------------

        /// <summary>Signs. One-way: a contract with a government is not something you undo.</summary>
        public bool Sign(Country signatory, GameDate on)
        {
            if (IsSigned)
            {
                return false;
            }

            IsSigned = true;
            Signatory = signatory;
            SignedOn = on;
            LastDelivery = 1.0;

            return true;
        }

        /// <summary>
        /// Puts a sector live. The caller has already charged for it and checked the chain.
        ///
        /// Returns false when it is already running, so a double click cannot bill twice.
        /// </summary>
        public bool Start(StateSector sector) => IsSigned && running.Add(sector);

        /// <summary>
        /// Hands a sector back.
        ///
        /// **Allowed, and it costs everything paid to research it.** A company drowning in a
        /// programme it cannot serve has to have a way out that is not bankruptcy, and handing back
        /// Defence is a real decision with a real price: the fee stops, the capacity returns, and
        /// the research is gone.
        /// </summary>
        public bool Stop(StateSector sector) => running.Remove(sector);

        /// <summary>Records the day's delivery for tomorrow to read.</summary>
        public void RecordDelivery(double delivery) =>
            LastDelivery = Math.Clamp(delivery, 0.0, 1.0);

        /// <summary>Books a failure. The caller moves the money and files the story.</summary>
        public void RecordFailure(GameDate on, long costUsd)
        {
            Failures++;
            LastFailure = on;
            PaidOutUsd += Math.Max(0L, costUsd);
        }

        /// <summary>Has enough time passed since the last one.</summary>
        public bool CouldFailOn(GameDate today) =>
            today.DayIndex - LastFailure.DayIndex >= QuietDaysAfterFailure;

        /// <summary>
        /// Which sector fails, given a roll between zero and the total weight.
        ///
        /// **Weighted by risk, not chosen at random.** Defence is the most dangerous thing on the
        /// board and it has to be the most likely thing to go wrong, or the weights are decoration
        /// and the board's whole trade evaporates.
        /// </summary>
        public StateSector SectorForRoll(double roll)
        {
            var running = new List<StateSector>(this.running);

            // Catalog order, so the same roll always picks the same sector however the set was
            // built. A HashSet's order is not promised, and a replay that differs from its own save
            // is the bug class this project has been caught by more than any other.
            running.Sort((a, b) => ((int)a).CompareTo((int)b));

            var seen = 0.0;

            foreach (var sector in running)
            {
                seen += StateSectorCatalog.Get(sector).FailureWeight;

                if (roll <= seen)
                {
                    return sector;
                }
            }

            return running.Count > 0 ? running[running.Count - 1] : StateSector.None;
        }

        /// <summary>Puts a loaded programme back, clamped like everything else off disk.</summary>
        public void Restore(bool signed, Country signatory, int signedDay,
            IEnumerable<StateSector> sectors, int lastFailureDay, int failures, long paidOut,
            double lastDelivery)
        {
            running.Clear();

            IsSigned = signed;
            Signatory = Enum.IsDefined(typeof(Country), signatory) ? signatory : Country.None;
            SignedOn = new GameDate(Math.Max(0, signedDay));
            LastFailure = new GameDate(lastFailureDay);
            Failures = Math.Max(0, failures);
            PaidOutUsd = Math.Max(0L, paidOut);
            LastDelivery = Math.Clamp(lastDelivery, 0.0, 1.0);

            if (sectors == null)
            {
                return;
            }

            foreach (var sector in sectors)
            {
                if (sector != StateSector.None && Enum.IsDefined(typeof(StateSector), sector))
                {
                    running.Add(sector);
                }
            }
        }
    }
}
