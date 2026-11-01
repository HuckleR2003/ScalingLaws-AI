using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Long campaigns played several different ways, measured rather than asserted.
    ///
    /// **This is not a pass/fail fixture and it must not become one.** `PlayabilityTests` already
    /// asserts that a competent player survives, and `SoakTests` already asserts that a decade
    /// replays identically. Both answer "is it broken". Neither answers the question that matters
    /// before a public build: **which mechanisms never fire in a real campaign.**
    ///
    /// A system can be complete, reachable from the interface, covered by tests, and still never
    /// happen to anybody, because its trigger sits somewhere a player never reaches. That is the
    /// same failure as an unreachable button wearing different clothes, and nothing here was
    /// watching for it.
    ///
    /// The report lands in `CampaignProbe~/report.md` so it can be read, diffed between builds and
    /// argued with. The one assertion is that the run completed, because a probe that throws
    /// halfway measures nothing.
    /// </summary>
    public sealed class CampaignProbeTests
    {
        private const int Years = 14;

        /// <summary>How a scripted player behaves. Each one is a real way people play.</summary>
        private enum Style
        {
            /// <summary>Ships steadily, hires, keeps the fleet fresh, advertises, borrows once.</summary>
            Balanced = 0,

            /// <summary>
            /// Never rents and never borrows: buys its own accelerators and runs them in the
            /// basement.
            ///
            /// This is the style that answers a question no other fixture asks, which is whether
            /// owning compute is a route through the game at all or merely a thing the interface
            /// lets you do. The first version of this style simply refused to rent and therefore
            /// never trained anything, which measured nothing except the obvious.
            /// </summary>
            Frugal = 1,

            /// <summary>Borrows early and hard, chases the frontier, ignores safety.</summary>
            Reckless = 2,

            /// <summary>Ships once and mostly watches. The failure case, on purpose.</summary>
            Passive = 3,

            /// <summary>
            /// A real company that stops trying.
            ///
            /// It ships one product and then keeps paying for everything a going concern pays for:
            /// the people, the office, the fleet it already rented. **`Passive` is a hermit and this
            /// is a coaster**, and the difference is the whole point. The design says a company that
            /// ships one model and coasts is bankrupt inside three years, and a hermit with no
            /// payroll is not the company that claim is about.
            /// </summary>
            Coaster = 4
        }

        private sealed class Probe
        {
            public readonly HashSet<CompanyEventType> Seen = new();
            public readonly List<string> Notes = new();
            public readonly List<(int Year, long Cash, double Capability, double Frontier)> Track = new();

            public int Bankrupt = -1;

            /// <summary>
            /// The highest cash and the largest following the campaign ever reached.
            ///
            /// **Peaks, not final values**, because an achievement fires the moment a number is
            /// touched. A company that reached sixty billion and spent it on a datacenter has
            /// earned the cash achievement and finishes the campaign reading nearly nothing.
            ///
            /// Here because three thresholds in `AchievementCatalog` were written without anybody
            /// finishing a campaign to see whether they are reachable: fifty billion, two hundred
            /// and fifty billion, and a million fans.
            /// </summary>
            public long PeakCash;

            public double PeakFans;

            public int Models;
            public int Nodes;
            public int Incidents;
            public int Inspections;

            // What the operator actually pressed. Counted because the first version of this probe
            // reported thirty four event kinds as never firing, and most of them were mechanisms
            // the script never reached rather than mechanisms the game fails to run.
            public int Approaches;
            public int Campaigns;
            public int Upgrades;
            public int RacksPlaced;
            public int HardwareBought;
            public int Signed;
            public int GrantsTaken;
            public int Rounds;

            /// <summary>
            /// Why the game said no, each distinct answer once.
            ///
            /// **A counter that reads zero cannot tell you whether the game refused or the script
            /// never asked**, and that ambiguity wasted a pass: hiring read zero because the house
            /// the company starts in has no desks, which is the game working exactly as designed.
            /// </summary>
            public readonly SortedSet<string> Refusals = new();

            /// <summary>Added 2026-11-01 with the mechanisms the operator had never touched.</summary>
            public int PriceMoves;

            public int Alliances;
            public int Packages;
            public int Offers;

            /// <summary>
            /// Files a refusal with the figures taken out.
            ///
            /// The same refusal carrying a different cash balance is the same refusal, and keeping
            /// them apart filled the report with twenty near-identical lines that said one thing.
            /// </summary>
            public void Refused(string what, string why)
            {
                var shape = new StringBuilder();

                foreach (var character in why ?? string.Empty)
                {
                    if (!char.IsDigit(character))
                    {
                        shape.Append(character);
                    }
                }

                Refusals.Add($"{what}: {shape.ToString().Trim()}");
            }
        }

        [Test]
        public void AProbeOfSeveralCampaignsWritesAReport()
        {
            var lines = new List<string>
            {
                "# Campaign probe",
                string.Empty,
                $"Generated {DateTime.Now:yyyy-MM-dd HH:mm}, "
                + $"{Years} years per run, {Enum.GetValues(typeof(Style)).Length} playing styles.",
                string.Empty,
                "This file is measured, not asserted. It exists to answer one question: which "
                + "mechanisms never fire in a real campaign. A system can be complete, reachable "
                + "and tested and still never happen to anybody.",
                string.Empty
            };

            var everySeen = new HashSet<CompanyEventType>();

            // Kept rather than discarded, so the summary at the foot can compare the styles against
            // each other. Each run is fourteen years and there is no re-running one to look again.
            var probes = new List<(Style Style, Probe Probe)>();

            foreach (Style style in Enum.GetValues(typeof(Style)))
            {
                var probe = Run(style);
                probes.Add((style, probe));

                foreach (var kind in probe.Seen)
                {
                    everySeen.Add(kind);
                }

                lines.Add($"## {style}");
                lines.Add(string.Empty);
                // **The horizon, said out loud.** AdvanceDay returns an empty report once the
                // company is insolvent, so every year after the one it died in is a copy of the
                // row above it. The table printed those as data and therefore read as a flat,
                // measured late game, which is exactly the part of the game a tester has asked
                // about twice. Nine of fourteen years were never simulated for any style that
                // plays, and nothing on the page said so.
                lines.Add(probe.Bankrupt >= 0
                    ? $"- Insolvent in year {probe.Bankrupt}, so **{probe.Bankrupt - 2021} of "
                      + $"{Years} years were simulated** and the rest of this run measures nothing."
                    : $"- Survived the whole campaign, all {Years} years simulated.");

                lines.Add($"- Models released: {probe.Models}");
                lines.Add($"- Research nodes finished: {probe.Nodes}");
                lines.Add($"- Safety incidents: {probe.Incidents}, inspections opened: {probe.Inspections}");
                lines.Add($"- Pressed by the operator: {probe.Approaches} approaches, "
                    + $"{probe.Campaigns} campaigns, {probe.Upgrades} upgrades, "
                    + $"{probe.RacksPlaced} racks, {probe.HardwareBought} accelerators, "
                    + $"{probe.Signed} people signed, {probe.GrantsTaken} grants taken, "
                    + $"{probe.Rounds} rounds raised, {probe.PriceMoves} price moves, "
                    + $"{probe.Alliances} alliances, {probe.Packages} reserved packages, "
                    + $"{probe.Offers} offers sent");

                foreach (var refusal in probe.Refusals)
                {
                    lines.Add($"- Refused: {refusal}");
                }
                lines.Add(string.Empty);
                lines.Add("| Year | Cash | Capability | Frontier | Parity |");
                lines.Add("|---|---|---|---|---|");

                foreach (var row in probe.Track)
                {
                    if (probe.Bankrupt >= 0 && row.Year > probe.Bankrupt)
                    {
                        lines.Add($"| {row.Year} | insolvent, not simulated | | |");
                        continue;
                    }

                    // Parity with the frontier, because every funding gate in the game is
                    // expressed as a share of it and no other column answers the question the
                    // late-game report is about.
                    var parity = row.Frontier <= 0.0 ? 0.0 : row.Capability / row.Frontier;

                    lines.Add(
                        $"| {row.Year} | {Money(row.Cash)} | "
                        + $"{row.Capability.ToString("0.0", CultureInfo.InvariantCulture)} | "
                        + $"{row.Frontier.ToString("0.0", CultureInfo.InvariantCulture)} | "
                        + $"{parity.ToString("P0", CultureInfo.InvariantCulture)} |");
                }

                lines.Add(string.Empty);

                if (probe.Notes.Count > 0)
                {
                    lines.Add("Worth looking at:");
                    lines.Add(string.Empty);

                    foreach (var note in probe.Notes)
                    {
                        lines.Add($"- {note}");
                    }

                    lines.Add(string.Empty);
                }
            }

            // ---- the part this fixture exists for ----------------------------------------------

            var never = Enum.GetValues(typeof(CompanyEventType))
                .Cast<CompanyEventType>()
                .Where(kind => !everySeen.Contains(kind))
                .ToList();

            lines.Add("## Event kinds that never fired in any of the runs");
            lines.Add(string.Empty);
            lines.Add("Some of these are correct: a scripted player never poaches anybody, so the "
                + "poaching events cannot appear. The ones worth reading are the systems a normal "
                + "campaign should have run into on its own.");
            lines.Add(string.Empty);

            foreach (var kind in never)
            {
                lines.Add($"- `{kind}`");
            }

            lines.Add(string.Empty);
            lines.Add($"{everySeen.Count} of {Enum.GetValues(typeof(CompanyEventType)).Length} "
                + "event kinds were raised at least once.");

            // **The world with no company in it**, which is the table report 15 is really about.
            //
            // A tester said twice that the late game is unsurvivable and tokens are too cheap, and
            // nothing anywhere measured the market on its own. Price decays at 0.80 a year from
            // $20 and floors at four cents, which is a fall of five hundred times; demand grows by
            // its own curve. Whether the late game can pay for anything is the product of those
            // two and nobody had multiplied them.
            //
            // No company, no operator, no seed: this is the same for every campaign ever played.
            lines.Add(string.Empty);
            lines.Add("## The market itself, with nobody trading in it");
            lines.Add(string.Empty);
            lines.Add("| Year | $/M tokens | Demand Btok/day | Whole market, $/day | vs 2022 |");
            lines.Add("|---|---|---|---|---|");

            var openingMarket = MarketModel.Evaluate(GameDate.FromCalendar(2022, 1, 1))
                .DailyMarketRevenueUsd;

            for (var year = 2022; year <= 2022 + Years; year++)
            {
                var world = MarketModel.Evaluate(GameDate.FromCalendar(year, 1, 1));
                var against = openingMarket <= 0.0 ? 0.0 : world.DailyMarketRevenueUsd / openingMarket;

                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | {1:0.####} | {2:N0} | {3:N0} | {4:0.00}x |",
                    year, world.PricePerMillionTokensUsd, world.TotalDemandBillionTokensPerDay,
                    world.DailyMarketRevenueUsd, against));
            }

            // **The three thresholds nobody had checked.** Written into the report rather than
            // asserted: whether a number is right is a design call, and this fixture measures.
            // **Report 16, both halves, measured rather than argued about.**
            //
            // He asked for two things: that model types carry different market prices, and that a
            // better model should be able to charge more. One of those is already true and was
            // never shown to anybody; the other is half true in a way that matters.
            // **Report 15, the half the funding gate cannot reach.** The price of a token falls
            // 500 times across the campaign and the market's serving recipes get 80 times cheaper
            // and then stop improving in 2030, so whether the late game can pay for anything is
            // the ratio of those two and nobody had divided them.
            //
            // The only measurement of this in the repository is a comment on
            // MarketModel.ServingEfficiencyOn: a 200B-active model cost 0.22x the market rate in
            // 2022, 2x in 2025 and 5.7x from 2030. **Those figures are from before that curve
            // existed**, which is to say before the fix, and nothing has measured it since.
            //
            // Assumptions, stated because the figure is meaningless without them: one 200B-active
            // dense model, no distillation, a founder at the neutral skill baseline, served on
            // rented compute at the market's own price per petaflop-day. The same 200B the old
            // measurement used, so the two are comparable.
            // **What falling behind costs in the price of money, and how fast that fades.**
            //
            // Written 2026-11-01 when the parity gate was removed from the funding ladder. The
            // argument for removing it was that the cost is already charged in the valuation,
            // where the story half is raised to the fourth power of parity. That is true and it
            // was overstated: the valuation has a second half, revenue at a multiple of twenty,
            // which does not read parity at all. A company at 44 per cent of the frontier with
            // $20M of revenue sells about three times more of itself than a frontier lab, not
            // twenty times, and the gap closes as revenue grows.
            //
            // **Whether a large company should still be punished for falling behind is a design
            // call**, so this is measured here and not asserted anywhere.
            // **The late game, and the measurement that found none of the suspects guilty.**
            //
            // A tester said the late game is unsurvivable and the tokens are too cheap. Three
            // candidates were offered and the tables above acquit all three: serving a token costs
            // between two and nine per cent of what one sells for in every year of the campaign,
            // so the margin is healthy, and the whole market does contract by 49 per cent between
            // 2027 and 2030 but a player holding 0.038 per cent of it is not killed by that.
            //
            // What is left is share, and share is an exponential. SegmentMarket scores every model
            // on exp(Utility), and Utility carries capability at CapabilityWeight a point, so a
            // capability gap is multiplied rather than subtracted. This table is that gap, read off
            // the same Utility the market uses, with everything except capability held equal.
            //
            // **Computed here rather than in a sentence**, because this number has been got wrong
            // by hand twice in one day.
            lines.Add(string.Empty);
            lines.Add("## What a capability gap does to how attractive a model is");
            lines.Add(string.Empty);
            lines.Add("Same brand, same price, same age on both sides, so capability is the only "
                + "difference. Read off `MarketShareModel.Utility`, the one the demand split "
                + "uses. The player column is the Balanced style's measured capability from the "
                + "track above.");
            lines.Add(string.Empty);
            lines.Add("| Year | Player | Frontier | Gap | Frontier model is |");
            lines.Add("|---|---|---|---|---|");

            var balanced = probes.FirstOrDefault(entry => entry.Style == Style.Balanced).Probe;

            if (balanced != null)
            {
                foreach (var row in balanced.Track)
                {
                    if (balanced.Bankrupt >= 0 && row.Year > balanced.Bankrupt)
                    {
                        continue;
                    }

                    var mine = MarketShareModel.Utility(row.Capability, 0.5, 1.0, 0.0);
                    var theirs = MarketShareModel.Utility(row.Frontier, 0.5, 1.0, 0.0);
                    var times = Math.Exp(theirs - mine);

                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "| {0} | {1:0.0} | {2:0.0} | {3:0.0} | **{4:N0}x more attractive** |",
                        row.Year, row.Capability, row.Frontier, row.Frontier - row.Capability,
                        times));
                }
            }

            lines.Add(string.Empty);
            lines.Add("## What falling behind costs in the price of money");
            lines.Add(string.Empty);
            lines.Add("A $250M round in January 2026, same reputation and following on both sides, "
                + "so parity is the only difference. **The penalty fades as the revenue half of "
                + "the valuation grows**, because that half does not read parity.");
            lines.Add(string.Empty);
            lines.Add("| Annual revenue | Pre-money at 44% | Pre-money at parity | Sold behind | Sold at front | Times worse |");
            lines.Add("|---|---|---|---|---|---|");

            var priceDate = GameDate.FromCalendar(2026, 1, 1);
            const double theFrontier = 77.0;
            const long round = 250_000_000L;

            foreach (var revenue in new[] { 0L, 20_000_000L, 100_000_000L, 300_000_000L, 1_000_000_000L })
            {
                var behindValue = FundingMarket.PreMoneyValuationUsd(
                    priceDate, theFrontier * 0.44, theFrontier, revenue, 0.5, 100_000.0);

                var frontValue = FundingMarket.PreMoneyValuationUsd(
                    priceDate, theFrontier, theFrontier, revenue, 0.5, 100_000.0);

                var soldBehind = round / (double)(behindValue + round);
                var soldFront = round / (double)(frontValue + round);
                var worse = soldFront <= 0.0 ? 0.0 : soldBehind / soldFront;

                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3:P1} | {4:P1} | **{5:0.0}x** |",
                    Money(revenue), Money(behindValue), Money(frontValue),
                    soldBehind, soldFront, worse));
            }

            lines.Add(string.Empty);
            lines.Add("## What it costs to serve a token against what one sells for");
            lines.Add(string.Empty);
            lines.Add("A 200B-active dense model, no distillation, neutral skills, on rented "
                + "compute at the market rate. **Above 1.00 means pricing at the market is a loss "
                + "on every token served**, whatever else the company does right.");
            lines.Add(string.Empty);
            lines.Add("| Year | Serving recipes | $/M to serve | $/M to sell | Cost over price |");
            lines.Add("|---|---|---|---|---|");

            const double activeParameters = 200e9;
            const double flopsPerPetaflopDay = 1e15 * 86_400.0;

            for (var year = 2022; year <= 2022 + Years; year++)
            {
                var when = GameDate.FromCalendar(year, 1, 1);
                var world = MarketModel.Evaluate(when);

                var recipes = MarketModel.ServingEfficiencyOn(when);
                var flopsPerToken = 2.0 * activeParameters / recipes;
                var tokensPerPetaflopDay = flopsPerPetaflopDay / flopsPerToken;

                var costPerMillion = world.RentPricePerPetaflopDayUsd
                    / tokensPerPetaflopDay * 1e6;

                var price = world.PricePerMillionTokensUsd;
                var over = price <= 0.0 ? 0.0 : costPerMillion / price;

                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | {1:0.0}x | {2:0.####} | {3:0.####} | **{4:0.00}x** |",
                    year, recipes, costPerMillion, price, over));
            }

            lines.Add(string.Empty);
            lines.Add("## What a capability point is worth in price");
            lines.Add(string.Empty);
            // **Invariant, because the first version of this printed 0,16 and 1,1.** A raw
            // interpolation follows the machine culture and this one is Polish, which is the sixth
            // time that fault has been written in this project, this time into the document that
            // answers a tester about numbers.
            lines.Add("Read straight out of `MarketShareModel.Utility`: capability enters as "
                + $"`{MarketShareModel.CapabilityWeight.ToString(CultureInfo.InvariantCulture)}` a point and price as "
                + $"`{MarketShareModel.PriceWeight.ToString(CultureInfo.InvariantCulture)}` times the log of it, so the price a model can "
                + "carry at the same appeal is `exp(capability gain * weight / price weight)`. "
                + "**Quality already buys price.** Nothing on any screen says so.");
            lines.Add(string.Empty);
            lines.Add("| Capability ahead | Can charge |");
            lines.Add("|---|---|");

            foreach (var ahead in new[] { 1.0, 3.0, 5.0, 10.0, 20.0 })
            {
                var multiple = Math.Exp(ahead * MarketShareModel.CapabilityWeight
                    / MarketShareModel.PriceWeight);

                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "| +{0:0} | {1:0.00}x |", ahead, multiple));
            }

            lines.Add(string.Empty);
            lines.Add("## What a model type is worth in price");
            lines.Add(string.Empty);
            lines.Add("Tolerance is how much less that type's audience minds the price, against a "
                + "general model on the same date, and it already divides the price in the demand "
                + "term. **The till does not read it.** Revenue is served tokens times "
                + "`Monetization.RatePerMillionTokensUsd`, which takes one market price for every "
                + "type, so a specialist can hold an audience at a higher price and still be paid "
                + "the general rate per token. That is the half of report 16 that is a real gap, "
                + "and closing it is a design call rather than a patch.");
            lines.Add(string.Empty);
            lines.Add("| Type | Tolerance 2024 | Tolerance 2030 | Reach 2024 | Reach 2030 |");
            lines.Add("|---|---|---|---|---|");

            var early = GameDate.FromCalendar(2024, 1, 1);
            var late = GameDate.FromCalendar(2030, 1, 1);

            foreach (var definition in ModelTypeCatalog.All)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "| {0} | {1:0.00} | {2:0.00} | {3:0.00} | {4:0.00} |",
                    definition.Type,
                    MarketShareModel.ToleranceFactor(definition.Type, early),
                    MarketShareModel.ToleranceFactor(definition.Type, late),
                    MarketShareModel.ReachFactor(definition.Type, early),
                    MarketShareModel.ReachFactor(definition.Type, late)));
            }

            lines.Add(string.Empty);
            lines.Add("## Achievement thresholds nobody had measured");
            lines.Add(string.Empty);
            // **Read off the catalog rather than written here.** The first version printed the
            // thresholds as literals in the header and they were stale within the hour, which is a
            // report telling you the wrong thing with a table around it.
            var rungs = AchievementCatalog.All
                .Where(entry => entry.Metric == AchievementMetric.CashUsd)
                .OrderBy(entry => entry.Threshold)
                .ToList();

            var fans = AchievementCatalog.All
                .First(entry => entry.Metric == AchievementMetric.Fans);

            lines.Add("| style | peak cash | "
                + string.Join(" | ", rungs.Select(r => $"${r.Threshold / 1_000_000.0:N0}M"))
                + $" | peak fans | {fans.Threshold:N0} |");

            lines.Add("|---|---|" + string.Concat(rungs.Select(_ => "---|")) + "---|---|");

            foreach (var (style, probe) in probes)
            {
                lines.Add(string.Format(CultureInfo.InvariantCulture, "| {0} | ${1:N0} | {2} | {3:N0} | {4} |",
                    style, probe.PeakCash,
                    string.Join(" | ", rungs.Select(r => probe.PeakCash >= r.Threshold ? "yes" : "no")),
                    probe.PeakFans,
                    probe.PeakFans >= fans.Threshold ? "yes" : "no"));
            }

            var folder = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName, "CampaignProbe~");

            Directory.CreateDirectory(folder);

            File.WriteAllText(
                Path.Combine(folder, "report.md"),
                string.Join(Environment.NewLine, lines),
                Encoding.UTF8);

            Debug.Log($"[Scaling Laws] Campaign probe written to {folder}");

            Assert.That(everySeen, Is.Not.Empty,
                "Every scripted campaign raised no events at all, so the probe measured "
                + "nothing and the report is worthless.");
        }

        // ---- the scripted players ---------------------------------------------------------------

        /// <summary>
        /// Plays one campaign and records what happened, without asserting anything about it.
        ///
        /// The operator is deliberately crude. It is a model of a person who understands the game
        /// rather than a solver, and making it cleverer would measure the solver rather than the
        /// game.
        /// </summary>
        private static Probe Run(Style style)
        {
            var probe = new Probe();

            var state = new CompanyState($"Probe {style}", (uint)(0x51A11 + (int)style));
            var simulation = new CompanySimulation(state);

            while (state.TryDequeueEvent(out _))
            {
            }

            for (var year = 0; year < Years; year++)
            {
                for (var month = 0; month < 12; month++)
                {
                    Act(simulation, style, probe);

                    simulation.Advance(30);

                    Drain(state, probe);

                    // Monthly, which is close enough: an achievement fires the moment the number is
                    // touched, and no campaign reaches fifty billion and spends it inside thirty
                    // days.
                    probe.PeakCash = Math.Max(probe.PeakCash, state.CashUsd);
                    probe.PeakFans = Math.Max(probe.PeakFans, state.Fans);

                    if (state.IsBankrupt && probe.Bankrupt < 0)
                    {
                        probe.Bankrupt = 2022 + year;
                    }
                }

                probe.Track.Add((
                    2022 + year,
                    state.CashUsd,
                    simulation.Flagship()?.Capability ?? 0.0,
                    simulation.Market.FrontierCapability));
            }

            probe.Models = state.ReleasedModelCount;

            Review(simulation, style, probe);
            return probe;
        }

        private static void Drain(CompanyState state, Probe probe)
        {
            while (state.TryDequeueEvent(out var raised))
            {
                probe.Seen.Add(raised.Type);

                if (raised.Type == CompanyEventType.SafetyIncident)
                {
                    probe.Incidents++;
                }
            }

            if (state.PendingAction != null)
            {
                probe.Inspections++;
            }
        }

        /// <summary>
        /// One month of decisions, in the order a person makes them.
        ///
        /// Every call is a `Try`, and every failure is ignored on purpose: a player who cannot
        /// afford something simply does not buy it, and a probe that asserted success would be
        /// measuring its own script rather than the game.
        /// </summary>
        private static void Act(CompanySimulation simulation, Style style, Probe probe)
        {
            var state = simulation.State;

            // The hermit stops paying for anything at all. That is what makes it the floor.
            if (style == Style.Passive && state.ReleasedModelCount >= 1)
            {
                return;
            }

            // The coaster stops *deciding* and goes on paying, which is a different company.
            if (style == Style.Coaster && state.ReleasedModelCount >= 1)
            {
                Hire(simulation, style, probe);
                AnswerTheMail(simulation, style, probe);
                return;
            }

            RaiseCapital(simulation, style, probe);
            Compute(simulation, style, probe);
            Borrow(simulation, style);
            Hire(simulation, style, probe);
            AnswerTheMail(simulation, style, probe);
            ConsiderGrants(simulation, style, probe);
            Advertise(simulation, style, probe);
            Research(simulation, probe);
            Train(simulation, style);
            Ship(simulation, style, probe);

            // **Three mechanisms this operator had never touched**, added 2026-11-01 as step two of
            // the plan in CLAUDE.md. Pricing is the important one and the reason is measured rather
            // than guessed: the market rate falls five hundred times across the campaign and this
            // operator held the opening subscription price in every one of its fourteen years, so
            // its price relative to the market climbed the whole way. A relative price of five
            // hundred costs about 6.8 of utility, which is the same order as the forty point
            // capability gap that was being blamed for the player holding nothing.
            SetThePrice(simulation, style, probe);
            TakeAllies(simulation, probe);
            ReserveCapacity(simulation, style, probe);
        }

        /// <summary>
        /// Keeps the price somewhere near the market, which is what a company watching its own
        /// dashboard would do and what this operator had never done.
        ///
        /// **A subscription is priced in dollars a month and the market is priced per token**, so
        /// standing still is a decision: as the market rate falls and each subscriber gets through
        /// more tokens, a fixed monthly fee drifts further above the market every year. The
        /// conversion is read from MonetizationPolicy rather than repeated here, so the operator
        /// cannot aim at a figure the simulation computes differently.
        ///
        /// The styles differ in what they aim at, which is the whole point: undercutting is a real
        /// strategy the arithmetic allows and no fixture had ever used it.
        /// </summary>
        private static void SetThePrice(CompanySimulation simulation, Style style, Probe probe)
        {
            var state = simulation.State;

            if (state.ReleasedModelCount == 0)
            {
                return;
            }

            var target = style switch
            {
                Style.Reckless => 0.70,
                Style.Frugal => 1.30,
                _ => 1.00
            };

            var market = simulation.Market.PricePerMillionTokensUsd;

            if (market <= 0.0)
            {
                return;
            }

            var tokensPerSubscriber =
                MonetizationCatalog.TokensPerSubscriberPerMonthOn(state.Date) / 1_000_000.0;

            if (tokensPerSubscriber <= 0.0)
            {
                return;
            }

            var wanted = target * market * tokensPerSubscriber;
            var now = state.Monetization.SubscriptionPriceUsdPerMonth;

            // A tenth either way is under the resolution of anything the player would notice, and
            // a move a month in a fourteen year run would otherwise fill the report with noise.
            if (now > 0.0 && Math.Abs(wanted - now) / now < 0.10)
            {
                return;
            }

            state.Monetization.SubscriptionPriceUsdPerMonth = wanted;
            probe.PriceMoves++;
        }

        /// <summary>
        /// Signs a term with whichever lab is friendliest, which became worth doing on 2026-10-31.
        ///
        /// Allied capacity reached nothing in the rules until that day, so an operator signing
        /// deals would have been measuring a fee for a number on a card. It joins the fleet now.
        /// </summary>
        private static void TakeAllies(CompanySimulation simulation, Probe probe)
        {
            var state = simulation.State;

            if (state.IsBankrupt || state.CashUsd < 5_000_000L)
            {
                return;
            }

            // **Not Relations.Known, which was the first version of this and never fired once.**
            // Known is every lab anything has ever happened with, and nothing had ever happened,
            // because this operator had never contacted anybody. So the set was empty, the loop
            // never ran, and the report showed zero alliances and zero refusals, which reads
            // exactly like a mechanism that was tried and declined. A player clicks a lab card;
            // the operator has to do the same thing before it can ask for anything.
            foreach (CompetitorId lab in Enum.GetValues(typeof(CompetitorId)))
            {
                if (lab == CompetitorId.None)
                {
                    continue;
                }

                // A struct list, so this is a search for a live term rather than a null check.
                if (state.Deals.Any(deal => deal.Lab == lab && deal.IsLiveOn(state.Date)))
                {
                    continue;
                }

                if (simulation.TrySignAlliance(lab, out var why))
                {
                    probe.Alliances++;
                    return;
                }

                probe.Refused("sign alliance", why);

                // **Refused, so build the relationship, and the way in is an offer.**
                //
                // The first version of this reached for TryCallLab and found a closed loop: you
                // need a live deal to have their number, and you need to be Friendly to sign a
                // deal. Both refusals are in the report above, which is how the loop was found.
                // The player is not stuck in it, because RivalPanel sends offers, so this is a gap
                // in every fixture this project has rather than a fault in the game. Publishing a
                // finding is the cheap, public, small one, which is what somebody with no
                // relationship and no leverage actually has to offer.
                if (simulation.TrySendOffer(lab, RelationOffer.PublishFinding, out var offerWhy))
                {
                    probe.Offers++;
                    return;
                }

                probe.Refused("send offer", offerWhy);
                return;
            }
        }

        /// <summary>
        /// Buys reserved capacity, which is the half of the fleet that does not queue.
        ///
        /// Reliability multiplies into how attractive a product is, so a company serving its
        /// audience badly loses share for a reason that has nothing to do with its model. No
        /// fixture had ever bought a package in a long run.
        /// </summary>
        private static void ReserveCapacity(CompanySimulation simulation, Style style, Probe probe)
        {
            var state = simulation.State;

            if (style == Style.Frugal || state.ReleasedModelCount == 0)
            {
                return;
            }

            // **A readonly struct, so FirstOrDefault hands back a zero-cost entry rather than
            // null when nothing matches.** The price is what separates the two, the same trap the
            // upgrade grid in Ship is already commented for.
            var definition = HostingCatalog.All.FirstOrDefault(
                entry => entry.Id == HostingPackage.Standard);

            if (definition.DailyCostUsd <= 0L)
            {
                return;
            }

            // Only while the fleet is genuinely under pressure, and only out of money the company
            // can lose without dying. A package is a monthly bill, not a purchase.
            if (state.LastQuality.Utilisation < 0.80)
            {
                return;
            }

            if (state.CashUsd < definition.DailyCostUsd * 120L)
            {
                return;
            }

            var held = state.Pool.PackageCount(HostingPackage.Standard);

            // **Capped, and the first version of this was not.** Utilisation is measured daily and
            // this operator acts monthly, so an uncapped rule bought a package every month for as
            // long as the fleet was busy: Reckless took nineteen of them, about six million a
            // month of standing bills, and died four years earlier than it had without them. That
            // is not a model of a player under pressure, it is a model of nobody reading the
            // invoice. Three is enough to pull a busy fleet off the ceiling and small enough that
            // the bill stays a decision.
            if (held >= 3)
            {
                return;
            }

            state.Pool.SetPackageCount(HostingPackage.Standard, held + 1);
            probe.Packages++;
        }

        /// <summary>
        /// Where the petaflops come from, and this is the one decision the styles disagree about
        /// most.
        /// </summary>
        private static void Compute(CompanySimulation simulation, Style style, Probe probe)
        {
            var state = simulation.State;

            if (style != Style.Frugal)
            {
                if (state.Pool.RentedPetaflops <= 0.0)
                {
                    simulation.SetRentedAccelerators(style == Style.Reckless ? 64 : 24);
                }

                return;
            }

            // The frugal player owns instead. The basement first, because it is the cheapest place
            // to stand a cabinet, then cards to put in it.
            if (!state.HasServerRoom)
            {
                simulation.TryOpenServerRoom(false, out _);
            }

            if (state.HasServerRoom)
            {
                for (var column = 0; column < 4; column++)
                {
                    for (var row = 0; row < 4; row++)
                    {
                        if (simulation.TryBuyRack(ServerRack.Enclosed, out _)
                            && simulation.TryStandRack(column, row, ServerRack.Enclosed, out _))
                        {
                            probe.RacksPlaced++;
                            break;
                        }
                    }
                }
            }

            var best = HardwareCatalog
                .AvailableOn(state.Date, HardwareClass.Accelerator)
                .OrderByDescending(entry => entry.PetaflopsPerUnit)
                .FirstOrDefault();

            if (best.LaunchPriceUsd > 0 && state.CashUsd > best.LaunchPriceUsd * 12)
            {
                if (simulation.TryBuyHardware(best.Id, 8, ComputeTier.ColocatedServers, out var why))
                {
                    probe.HardwareBought += 8;
                }
                else
                {
                    probe.Refused("buy hardware", why);
                }
            }
        }

        /// <summary>
        /// Equity.
        ///
        /// **The omission that made every earlier reading unfair.** `PlayabilityTests`, the one
        /// fixture that asserts a company survives, raises rounds throughout, and this probe was
        /// not raising any at all. Comparing a company that never sold a share against a balance
        /// guarantee built on one that does is comparing two different games.
        /// </summary>
        private static void RaiseCapital(CompanySimulation simulation, Style style, Probe probe)
        {
            var state = simulation.State;

            if (style == Style.Passive)
            {
                return;
            }

            if (state.CurrentFundingOffer.IsOpen)
            {
                if (simulation.TryAcceptFundingOffer(out var why))
                {
                    probe.Rounds++;
                }
                else
                {
                    probe.Refused("sign round", why);
                }

                return;
            }

            // Only when the money is actually wanted. A company opening a round it does not need is
            // selling equity for nothing, which no player does twice.
            if (state.CashUsd > 25_000_000)
            {
                return;
            }

            var availability = simulation.NextRoundAvailability();

            if (availability.IsAvailable)
            {
                simulation.TryOpenFundingRound(out _);
            }
            else
            {
                probe.Refused("open round", availability.Reason);
            }
        }

        private static void Borrow(CompanySimulation simulation, Style style)
        {
            var state = simulation.State;

            if (style == Style.Frugal)
            {
                return;
            }

            if (state.Loans.OpenCount > 0 || state.CashUsd >= 30_000_000)
            {
                return;
            }

            // Cheapest facility first, so a young company is refused the large ones rather than
            // the probe deciding on its behalf that it cannot have them.
            var wanted = style == Style.Reckless
                ? new[] { LoanProduct.VentureDebt, LoanProduct.BridgeFacility, LoanProduct.EquipmentFinance }
                : new[] { LoanProduct.EquipmentFinance, LoanProduct.BridgeFacility };

            foreach (var product in wanted)
            {
                if (simulation.TryTakeLoan(product, out _))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// People. **Nothing in this project measured hiring over a campaign before this**, so the
        /// payroll had never been weighed against what the staff produce.
        /// </summary>
        private static void Hire(CompanySimulation simulation, Style style, Probe probe)
        {
            var state = simulation.State;

            if (style == Style.Passive)
            {
                return;
            }

            var wanted = WantedHeadcount(style);

            if (state.Staff.Headcount + state.Hiring.OpenCount >= wanted)
            {
                return;
            }

            if (!state.Hiring.CanApproach || state.CashUsd < 4_000_000)
            {
                return;
            }

            // **The house has no desks at all**, so a company that never moves can never employ
            // anybody. The first pass of this probe missed that and reported zero hires as though
            // the hiring desk were broken.
            if (!state.Staff.HasFreeSeat && !simulation.TryMoveOffice(NextOfficeUp(state), out var moveWhy))
            {
                probe.Refused("move office", moveWhy);
                return;
            }

            var shortlist = simulation.Shortlist(PlayerSkill.Development, HireSource.Agency, 45, 3);

            if (shortlist.Count == 0)
            {
                return;
            }

            var why = simulation.TryApproach(shortlist[0]);

            if (string.IsNullOrEmpty(why))
            {
                probe.Approaches++;
            }
            else
            {
                probe.Refused("approach", why);
            }
        }

        /// <summary>
        /// Settles the job offers sitting in the post.
        ///
        /// **This is the step that was missing, and its absence looked like a bug in the hiring
        /// desk.** Approaching somebody does not employ them: it produces a letter, and a person
        /// only joins once a wage has been agreed. The probe was reporting fifty three approaches
        /// and an empty roster, which is what a broken hiring desk would also look like.
        ///
        /// It offers exactly what the candidate asked for, because the probe is measuring whether
        /// the chain completes rather than whether a scripted negotiator can haggle.
        /// </summary>
        private static void AnswerTheMail(CompanySimulation simulation, Style style, Probe probe)
        {
            var state = simulation.State;

            if (style == Style.Passive)
            {
                return;
            }

            foreach (var letter in state.Mail.All.ToList())
            {
                if (letter.IsClosed || letter.Kind != MailKind.JobOffer || letter.Candidate == null)
                {
                    continue;
                }

                // **Only up to the plan.** People write in unprompted as well as in reply to a
                // search, and the first version of this said yes to every one of them at the wage
                // they asked for. That is a real thing a new player does and it bankrupted every
                // style inside three years, but it measures the doormat rather than the game.
                if (state.Staff.Headcount >= WantedHeadcount(style))
                {
                    continue;
                }

                var verdict = simulation.Negotiate(
                    letter, letter.Candidate.AskingHourlyUsd, 0L, out var note);

                if (verdict == OfferVerdict.Accepted)
                {
                    probe.Signed++;
                }
                else
                {
                    probe.Refused("hire", note);
                }
            }
        }

        /// <summary>
        /// Signs for whatever is on the grant board.
        ///
        /// Takes everything offered rather than choosing, because a probe that picked the winnable
        /// ones would be measuring the pick. Missing a term is a real outcome and the report should
        /// see it happen.
        /// </summary>
        private static void ConsiderGrants(CompanySimulation simulation, Style style, Probe probe)
        {
            if (style == Style.Passive)
            {
                return;
            }

            foreach (var definition in simulation.AvailableGrants())
            {
                if (simulation.TryAcceptGrant(definition.Id, out var why))
                {
                    probe.GrantsTaken++;
                }
                else
                {
                    probe.Refused("grant", why);
                }
            }
        }

        /// <summary>How many people this style is trying to employ. One number, two readers.</summary>
        private static int WantedHeadcount(Style style) => style switch
        {
            Style.Reckless => 12,
            Style.Coaster => 4,
            _ => 6
        };

        /// <summary>The next tier up the ladder, or the one the company is in at the top.</summary>
        private static OfficeTier NextOfficeUp(CompanyState state)
        {
            var here = (int)state.Staff.Office;

            foreach (var tier in OfficeCatalog.All.Select(entry => entry.Tier).OrderBy(tier => (int)tier))
            {
                if ((int)tier > here)
                {
                    return tier;
                }
            }

            return state.Staff.Office;
        }

        private static void Advertise(CompanySimulation simulation, Style style, Probe probe)
        {
            var state = simulation.State;

            if (style is Style.Passive or Style.Frugal || state.ReleasedModelCount == 0)
            {
                return;
            }

            if (state.Campaigns.Count > 0 || state.CashUsd < 8_000_000)
            {
                return;
            }

            var channel = style == Style.Reckless
                ? MarketingChannel.Television
                : MarketingChannel.Creators;

            state.AddCampaign(new MarketingCampaign(
                new[] { channel }, AudienceSegment.Developer, 3, state.Date));

            probe.Campaigns++;
        }

        /// <summary>
        /// Research whatever is affordable, cheapest first. A real player does not, but a probe
        /// that picked well would be measuring the pick.
        /// </summary>
        private static void Research(CompanySimulation simulation, Probe probe)
        {
            var state = simulation.State;

            if (state.ActiveResearch != null)
            {
                return;
            }

            foreach (var node in ResearchTree.All.OrderBy(entry => entry.CostUsd))
            {
                if (state.UnlockedResearch.Contains(node.Id))
                {
                    continue;
                }

                if (simulation.TryStartResearch(node.Id, out _))
                {
                    probe.Nodes++;
                    return;
                }
            }
        }

        private static void Train(CompanySimulation simulation, Style style)
        {
            var state = simulation.State;

            if (state.ActiveRun != null || state.Shelf.Count > 0)
            {
                return;
            }

            // Compute-days rather than cash: the planner shapes a run against the cluster it will
            // actually run on, and a budget in dollars means nothing to it.
            var profile = simulation.Profile;
            var days = style == Style.Reckless ? 120.0 : 200.0;
            var budget = profile.EffectivePetaflops * state.TrainingComputeShare * days;

            var blueprint = TrainingPlanner.OptimalBlueprintForBudget(
                $"Probe {state.ReleasedModelCount + 1}",
                ArchitectureId.DenseTransformer,
                budget,
                state.OwnedDataSources);

            // `ModelBlueprint` is a struct, so there is nothing to null-check. A shape the planner
            // could not build comes back with no parameters, and TryStartTraining refuses it the
            // same way it refuses a player asking for something impossible.
            if (blueprint.ParameterCountBillions > 0.0)
            {
                simulation.TryStartTraining(blueprint, out _);
            }
        }

        private static void Ship(CompanySimulation simulation, Style style, Probe probe)
        {
            var state = simulation.State;

            if (state.Shelf.Count > 0)
            {
                simulation.TryReleaseModel(0, 1.0, out _);
            }

            if (style == Style.Passive || state.ReleasedModelCount == 0)
            {
                return;
            }

            // Keep the newest product improving rather than only ever replacing it. The upgrade
            // path is the other half of the release loop and no long run had exercised it.
            var grid = simulation.UpgradeGrid(0);

            // `TraitStanding` is a struct, so `FirstOrDefault` yields a zero-cost entry rather
            // than null when nothing is affordable. The cost is what separates the two.
            var affordable = grid
                .Where(standing => standing.UpgradeCostUsd > 0
                    && standing.UpgradeCostUsd < state.CashUsd / 8)
                .OrderBy(standing => standing.UpgradeCostUsd)
                .FirstOrDefault();

            if (affordable.UpgradeCostUsd > 0
                && simulation.TryStartUpgrades(0, new[] { affordable.Trait }, out _))
            {
                probe.Upgrades++;
            }
        }

        /// <summary>What the finished campaign looks like from outside, in sentences.</summary>
        private static void Review(CompanySimulation simulation, Style style, Probe probe)
        {
            var state = simulation.State;

            if (probe.Models == 0)
            {
                probe.Notes.Add("Never released a model in fourteen years.");
            }

            if (probe.Nodes == 0)
            {
                probe.Notes.Add("Never finished a research node.");
            }

            var last = probe.Track.Count > 0 ? probe.Track[^1] : default;

            if (last.Frontier > 0 && last.Capability > 0 && last.Capability > last.Frontier * 1.6)
            {
                probe.Notes.Add(
                    $"Finished far ahead of the frontier ({last.Capability:0.0} against "
                    + $"{last.Frontier:0.0}), which is a balance question rather than a bug.");
            }

            if (state.CashUsd > 500_000_000_000L)
            {
                probe.Notes.Add(
                    $"Ended with {Money(state.CashUsd)}, which is past the point where money "
                    + "means anything and worth a look.");
            }

            if (probe.Incidents == 0 && style != Style.Passive)
            {
                probe.Notes.Add(
                    "No safety incident in fourteen years, so the whole safety and regulator "
                    + "chain never ran for this player.");
            }

            if (state.Fans <= 0.0 && probe.Models > 0)
            {
                probe.Notes.Add("Shipped models and never gained a single fan.");
            }

            if (probe.Signed > 0 && state.Staff.Headcount == 0)
            {
                probe.Notes.Add(
                    $"Signed {probe.Signed} people and employs nobody, so accepted hires are not "
                    + "reaching the roster.");
            }

            if (probe.Approaches > 0 && probe.Signed == 0)
            {
                probe.Notes.Add(
                    $"Approached {probe.Approaches} candidates and agreed terms with none of them, "
                    + "at the wage each one asked for.");
            }

            // The spine, measured. `CLAUDE.md` states that a company which ships one model and
            // coasts is bankrupt inside three years. This is the only style shaped to test that
            // claim, so it is the only one that reports on it.
            if (style == Style.Coaster)
            {
                probe.Notes.Add(probe.Bankrupt >= 0
                    ? $"Coasting ended the company in {probe.Bankrupt}, which is the design working."
                    : $"**Coasted for fourteen years and never went insolvent**, ending on "
                        + $"{Money(state.CashUsd)}. The design says this company should be gone "
                        + "inside three years.");
            }
        }

        private static string Money(long usd)
        {
            var sign = usd < 0 ? "-" : string.Empty;
            var value = Math.Abs(usd);

            return value switch
            {
                >= 1_000_000_000_000 => $"{sign}${value / 1_000_000_000_000.0:0.##}T",
                >= 1_000_000_000 => $"{sign}${value / 1_000_000_000.0:0.##}B",
                >= 1_000_000 => $"{sign}${value / 1_000_000.0:0.##}M",
                >= 1_000 => $"{sign}${value / 1_000.0:0.#}k",
                _ => $"{sign}${value}"
            };
        }
    }
}
