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

            foreach (Style style in Enum.GetValues(typeof(Style)))
            {
                var probe = Run(style);

                foreach (var kind in probe.Seen)
                {
                    everySeen.Add(kind);
                }

                lines.Add($"## {style}");
                lines.Add(string.Empty);
                lines.Add(probe.Bankrupt >= 0
                    ? $"- Insolvent in year {probe.Bankrupt}."
                    : "- Survived the whole campaign.");

                lines.Add($"- Models released: {probe.Models}");
                lines.Add($"- Research nodes finished: {probe.Nodes}");
                lines.Add($"- Safety incidents: {probe.Incidents}, inspections opened: {probe.Inspections}");
                lines.Add($"- Pressed by the operator: {probe.Approaches} approaches, "
                    + $"{probe.Campaigns} campaigns, {probe.Upgrades} upgrades, "
                    + $"{probe.RacksPlaced} racks, {probe.HardwareBought} accelerators, "
                    + $"{probe.Signed} people signed, {probe.GrantsTaken} grants taken, "
                    + $"{probe.Rounds} rounds raised");

                foreach (var refusal in probe.Refusals)
                {
                    lines.Add($"- Refused: {refusal}");
                }
                lines.Add(string.Empty);
                lines.Add("| Year | Cash | Capability | Frontier |");
                lines.Add("|---|---|---|---|");

                foreach (var row in probe.Track)
                {
                    lines.Add(
                        $"| {row.Year} | {Money(row.Cash)} | "
                        + $"{row.Capability.ToString("0.0", CultureInfo.InvariantCulture)} | "
                        + $"{row.Frontier.ToString("0.0", CultureInfo.InvariantCulture)} |");
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
                        if (simulation.TryPlaceRack(column, row, ServerRack.Enclosed, out _))
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

            foreach (var offer in simulation.GrantOffers().ToList())
            {
                if (simulation.TryAcceptGrant(offer.Id, out var why))
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
