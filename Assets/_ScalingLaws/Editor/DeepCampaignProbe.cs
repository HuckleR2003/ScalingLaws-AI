using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// A whole campaign, played competently, and a report on what happened.
    ///
    /// **This is not a test and deliberately not one.** `PlayabilityTests` asserts that a scripted
    /// operator survives, which is a floor; what nobody could see was the shape of a campaign: how
    /// many models a real player gets out, how much of the tree they open, whether the field reacts,
    /// whether the ending is ever a win. Those are questions with numbers for answers rather than
    /// pass or fail, and a number that is quietly wrong for two years is exactly what this project
    /// keeps finding.
    ///
    /// The operator here plays better than the one in `PlayabilityTests`: it sizes a run to the
    /// fleet it actually has, ships and prices, keeps something on the tree at all times, upgrades
    /// what is on sale, hires against its desks, moves when it can afford to, and borrows before it
    /// runs out rather than after. It is still not a good player. It is a competent one, and the
    /// distance between it and the author is the margin the design has.
    /// </summary>
    public static class DeepCampaignProbe
    {
        /// <summary>Fourteen years, which is the span the catalogues are written for.</summary>
        private const int Days = 14 * 365;

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>
        /// Whether the operator keeps one product line or starts a new one every time.
        ///
        /// **This is the difference between a strategy and the game.** A release that is given a
        /// name of its own starts a line of its own, and a line is a product: nothing supersedes
        /// anything, so fourteen years of shipping leaves three hundred products dividing one
        /// company's audience between them. That is what the first operator did, and reading its
        /// last place as "the game cannot be won" would have been wrong.
        /// </summary>
        private static bool oneLine;

        /// <summary>
        /// Whether the operator buys its compute or only rents it.
        ///
        /// **Both earlier operators rented, always, and it hid a third of the game.** Fourteen
        /// years of every seed reported `kW 0` and a power bill of nothing, because nothing was
        /// ever owned: no accelerators, no server room, no cabinets, no heat, and therefore no
        /// answer at all to whether electricity is a burden. The one part of this economy the
        /// author was asking about was the part the probe could not see.
        /// </summary>
        private static bool ownsCompute;

        /// <summary>
        /// Whether the operator plays the way somebody trying to win would.
        ///
        /// **The three above never aimed their research, never paid for it and never advertised.**
        /// They took the first node the board offered, so a company with two billion in the bank sat
        /// at capability 53 for four years behind a parameter ceiling it never researched, and
        /// awareness sat at the word of mouth floor for fourteen years. Reading "the player is
        /// erased" off that operator would have been reading its own bad choices. This one aims the
        /// tree at scale, data and families, funds it, keeps a campaign running and picks the family
        /// that projects best. It is still a script; it is a script with a plan.
        /// </summary>
        private static bool ambitious;

        [MenuItem("Scaling Laws/Play a deep campaign")]
        public static void Play()
        {
            var report = new StringBuilder();
            var clock = Stopwatch.StartNew();
            var campaigns = 0;

            // Three operators, and the third is the one that owns anything.
            foreach (var (disciplined, owner, aims, heading) in new[]
            {
                (false, false, false, "A NEW LINE EVERY TIME, nothing ever superseded, renting"),
                (true, false, false, "ONE PRODUCT LINE, each release replacing the last, renting"),
                (true, true, false, "ONE PRODUCT LINE, and it owns its own silicon and a server room"),
                (true, false, true, "AMBITIOUS: one line, research aimed and funded, advertising, renting")
            })
            {
                // `PROBE_ONLY=ambitious` runs the last operator on one seed, for iterating on it
                // without waiting two minutes for eleven campaigns nobody is looking at.
                var only = Environment.GetEnvironmentVariable("PROBE_ONLY");
                if (only == "ambitious" && !aims)
                {
                    continue;
                }

                oneLine = disciplined;
                ownsCompute = owner;
                ambitious = aims;

                report.AppendLine();
                report.AppendLine("================ " + heading);

                foreach (var seed in only == "ambitious" ? new[] { 4242 } : new[] { 4242, 9001, 1337 })
                {
                    RunOne(seed, report);
                    campaigns++;
                }
            }

            clock.Stop();

            report.AppendLine();
            // Nine, not three: three operators over three seeds. The count is derived from the
            // run rather than written beside it, because it said "three" for a week after it
            // became six.
            report.AppendLine($"{campaigns} campaigns of {Days} days in "
                + $"{clock.ElapsedMilliseconds} ms "
                + $"({clock.ElapsedMilliseconds / (double)Math.Max(1, campaigns * Days):0.00} ms a day)");

            Debug.Log(report.ToString());
        }

        /// <summary>The largest share any one company holds of the people being served.</summary>
        private static double LeaderShare(CompanySimulation simulation)
        {
            var breakdown = simulation.MarketByType();
            var best = 0.0;

            for (var owner = 0; owner < breakdown.OwnerUsersOverall.Count; owner++)
            {
                best = Math.Max(best, breakdown.OverallShareOf(owner));
            }

            return best;
        }

        private static void RunOne(int seed, StringBuilder report)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI", (uint)seed));
            var state = simulation.State;

            var shipped = 0;
            var released = new List<string>();
            var researched = 0;
            var upgrades = 0;
            var hires = 0;
            var offices = new List<string>();
            var loans = 0;
            var smears = 0;
            var backfires = 0;
            var threats = 0;
            var scandalsAgainstUs = 0;
            var lawsuits = 0;
            var incidents = 0;
            var bankrupt = -1;

            var peakCash = state.CashUsd;
            var peakUsers = 0.0;
            var peakCapability = 0.0;

            var yearly = new List<string>();
            var events = new List<string>();

            // **Why a campaign stops, in its own words.** The first run showed a company that
            // shipped thirty models in seven years and then nothing at all for another seven, and
            // there was no way to tell whether that was the operator giving up or the game
            // refusing it. Every refusal is now counted by its reason.
            var refusals = new Dictionary<string, int>();

            void Refused(string what, string why)
            {
                var key = what + ": " + Trim(why ?? string.Empty);
                refusals[key] = refusals.TryGetValue(key, out var count) ? count + 1 : 1;
            }

            // The events are a queue the shell drains each tick. Draining it here is what the
            // game does, and leaving it undrained would grow it for fourteen years.
            void Drain()
            {
                while (state.TryDequeueEvent(out var entry))
                {
                    switch (entry.Type)
                    {
                        case CompanyEventType.SafetyIncident:
                            incidents++;
                            break;

                        case CompanyEventType.SmearBackfired:
                            backfires++;
                            break;
                    }

                    // The loud ones, a few of each, so the report says what a campaign felt
                    // like rather than printing four thousand lines.
                    if (events.Count < 30 && IsLoud(entry.Type))
                    {
                        events.Add($"      day {state.Date.DayIndex,5}  {entry.Type}: "
                            + Trim(entry.Message));
                    }
                }
            }

            for (var day = 0; day < Days; day++)
            {
                simulation.AdvanceDay();
                Drain();

                if (state.CashUsd < 0 && bankrupt < 0)
                {
                    bankrupt = day;
                }

                peakCash = Math.Max(peakCash, state.CashUsd);
                peakCapability = Math.Max(peakCapability, state.BestCapability);

                var standing = simulation.Product();
                peakUsers = Math.Max(peakUsers, standing.Subscribers);

                if (state.SmearThreat != null)
                {
                    threats++;
                }

                Operate(simulation, ref shipped, ref researched, ref upgrades, ref hires,
                    ref loans, ref smears, ref lawsuits, released, offices, Refused);

                if (day % 365 == 364 || (ambitious && day < 1100 && day % 91 == 90))
                {
                    var rank = simulation.Ranking().FirstOrDefault(entry => entry.IsPlayer);

                    // **The power bill, because nobody could say whether it bites.** The room is
                    // billed at a domestic tariff and a datacenter at a contract one, and the only
                    // honest way to know whether either one is a real burden is to read it against
                    // what the fleet costs and against what the company took that month.
                    var fleet = simulation.Profile;
                    var powerShare = fleet.Bill.TotalUsd > 0.0
                        ? fleet.Bill.ElectricityUsd / fleet.Bill.TotalUsd
                        : 0.0;

                    yearly.Add(string.Format(Culture,
                        "      {0}  cash {1,14}  cap {2,6:0.0}  rank {3,2}  users {4,12:N0}  "
                        + "rep {5:0.00}  nodes {6,2}  live {7,3}  load {8,5:P0}  ms {9,5:0}  "
                        + "marketed {10,3}  WORLD {11,15:N0}  our share {12,6:P1}  "
                        + "kW {13,9:N0}  power/day {14,12}  = {15,6:P1} of fleet, "
                        + "fleet {16,12}/day, revenue {17,12}/day, "
                        + "SERVED {18,15:N0} of the world, unserved {19,6:P1}, leader {20,6:P1}, "
                        + "frontier {21,5:0.0}, demanded {22:N1}B, capacity {23:N1}B, "
                        + "rate {24:0.000}/M, market {25:0.000}/M, free {26:P0}, training share {27:P0}",
                        state.Date, Money(state.CashUsd), state.BestCapability,
                        rank.Position, standing.Subscribers, state.Reputation,
                        state.UnlockedResearch.Count, state.DeployedModels.Count,
                        state.LastQuality.Utilisation, state.LastQuality.ResponseMilliseconds,
                        simulation.MarketedModels().Count,
                        simulation.MarketByType().AddressableUsers,
                        simulation.MarketByType().OverallShareOf(0),
                        fleet.PowerDrawKilowatts,
                        Money((long)fleet.Bill.ElectricityUsd), powerShare,
                        Money((long)fleet.Bill.TotalUsd),
                        Money((long)(standing.MonthEarningsUsd / 30.0)),

                        // **Who actually holds the people, against how many people there are.**
                        // The five year guard reads the player's share of all demand and nothing
                        // said how much of that demand anybody at all was serving, so a share of
                        // nothing and a share of everything looked the same.
                        simulation.MarketByType().TotalUsersOverall,
                        simulation.MarketByType().UnservedShare,
                        LeaderShare(simulation),
                        simulation.Market.FrontierCapability,
                        state.LastQuality.Demanded, state.LastQuality.Capacity,
                        state.Monetization.RatePerMillionTokensUsd(simulation.Market.PricePerMillionTokensUsd),
                        simulation.Market.PricePerMillionTokensUsd,
                        state.Monetization.FreeShareOfTokens,
                        state.TrainingComputeShare));
                }
            }

            // Scandals are counted at the end from the wire, because they are filed as news rather
            // than as an event the company raises about itself.
            scandalsAgainstUs = state.News.All.Count(story =>
                story.Section == NewsSection.Scandals && story.IsAboutPlayer);

            var final = simulation.Ranking().FirstOrDefault(entry => entry.IsPlayer);

            report.AppendLine();
            report.AppendLine($"  ---- seed {seed} " + new string('-', 60));
            report.AppendLine($"      models trained {shipped}, released {released.Count}: "
                + string.Join(", ", released.Take(10)));
            report.AppendLine($"      research {researched} of {ResearchTree.All.Count} nodes, "
                + $"upgrades {upgrades}, hires {hires}, loans {loans}");
            report.AppendLine($"      offices: " + (offices.Count == 0 ? "never moved" : string.Join(" -> ", offices)));
            report.AppendLine($"      smears {smears} ({backfires} traced), threats {threats} days, "
                + $"lawsuits {lawsuits}, incidents {incidents}, scandals about us {scandalsAgainstUs}");
            report.AppendLine($"      peak cash {Money(peakCash)}, peak users {peakUsers:N0}, "
                + $"peak capability {peakCapability:0.0}");
            report.AppendLine($"      finished rank {final.Position} of {simulation.Ranking().Count}"
                + (bankrupt >= 0 ? $", insolvent from day {bankrupt}" : ", solvent throughout"));
            if (refusals.Count > 0)
            {
                report.AppendLine("      what refused us, by count:");

                foreach (var pair in refusals.OrderByDescending(entry => entry.Value).Take(8))
                {
                    report.AppendLine($"      {pair.Value,6}x  {pair.Key}");
                }
            }

            report.AppendLine("      year by year:");

            foreach (var line in yearly)
            {
                report.AppendLine(line);
            }

            if (events.Count > 0)
            {
                report.AppendLine("      what happened:");

                foreach (var line in events)
                {
                    report.AppendLine(line);
                }
            }
        }

        /// <summary>
        /// Buying the cluster instead of hiring it, the way a company with money actually does.
        ///
        /// Three purchases, in the order their returns arrive. The room is first because it is
        /// the cheapest housing this company will ever have and it is the one that needs a
        /// calendar to fill. Cabinets next, because silicon with nowhere to stand is silicon
        /// paying a datacenter to hold it. Then the cards.
        ///
        /// **A tenth of the balance a month, never more.** An operator that converts its whole
        /// account into accelerators is not measuring the economy, it is measuring one bad
        /// decision, and hardware is the one purchase in this game that cannot be undone at
        /// anything like its price.
        /// </summary>
        private static void OwnSomething(CompanySimulation simulation, Action<string, string> refused)
        {
            var state = simulation.State;

            if (state.Date.DayIndex % 30 != 0 || state.CashUsd < 20_000_000L)
            {
                return;
            }

            if (!state.HasServerRoom && !simulation.TryOpenServerRoom(false, out var roomWhy))
            {
                refused("open the room", roomWhy);
            }

            // One cabinet a month onto the first free square. High density, because the floor is
            // sixteen squares and the binding constraint down there is always the floor.
            if (state.HasServerRoom)
            {
                for (var column = 0; column < CompanyState.BasementColumns; column++)
                {
                    var placed = false;

                    for (var row = 0; row < CompanyState.BasementRows; row++)
                    {
                        if (!state.Hall.At(column, row).IsEmpty)
                        {
                            continue;
                        }

                        if (!simulation.TryBuyRack(ServerRack.HighDensity, out var buyWhy))
                        {
                            refused("a cabinet", buyWhy);
                        }
                        else if (!simulation.TryStandRack(column, row, ServerRack.HighDensity,
                                     out var standWhy))
                        {
                            refused("standing a cabinet", standWhy);
                        }
                        else
                        {
                            placed = true;
                        }

                        break;
                    }

                    if (placed)
                    {
                        break;
                    }
                }
            }

            // ---- and the silicon ---------------------------------------------------------------
            //
            // Newest first, which is what a shop opens on and what a company buying once a month
            // would take. The batch is whatever a tenth of the balance pays for.
            HardwareGeneration newest = default;
            var found = false;

            foreach (var generation in HardwareCatalog.All)
            {
                if (generation.Class != HardwareClass.Accelerator
                    || !generation.IsAvailableOn(state.Date))
                {
                    continue;
                }

                if (!found || generation.ReleaseDate.DayIndex > newest.ReleaseDate.DayIndex)
                {
                    newest = generation;
                    found = true;
                }
            }

            if (!found)
            {
                return;
            }

            var tier = ComputeTierCatalog.Get(ComputeTier.ColocatedServers);

            var unit = MarketModel.PurchasePricePerUnitUsd(
                    newest, tier, MarketModel.ScarcityOn(state.Date))
                * state.Founder.HardwarePriceMultiplier
                * state.Home.HardwarePriceMultiplier;

            if (unit <= 0.0)
            {
                return;
            }

            var units = (int)(state.CashUsd * 0.10 / unit);

            if (units <= 0)
            {
                return;
            }

            if (!simulation.TryBuyHardware(newest.Id, units, ComputeTier.ColocatedServers,
                    out var siliconWhy))
            {
                refused("silicon", siliconWhy);
            }
        }

        /// <summary>
        /// One day of decisions.
        ///
        /// Ordered the way a player would: keep the cluster busy, keep the tree moving, spend what
        /// is left. Every call is a `Try`, so a refusal is simply a day where that did not happen.
        /// </summary>
        private static void Operate(CompanySimulation simulation, ref int shipped, ref int researched,
            ref int upgrades, ref int hires, ref int loans, ref int smears, ref int lawsuits,
            List<string> released, List<string> offices, Action<string, string> refused)
        {
            var state = simulation.State;

            // ---- keep something training -------------------------------------------------------
            if (state.ActiveRun == null && state.Shelf.Count < 2)
            {
                var fleet = Math.Max(120.0, state.Pool.RentedPetaflops);

                // Sized to a season of the fleet, which is how a real plan is made: a budget of
                // compute-days rather than a parameter count picked out of the air.
                var budget = fleet * 90.0;

                var family = state.AdoptedArchitectures.Contains(ArchitectureId.SparseMixture)
                    ? ArchitectureId.SparseMixture
                    : ArchitectureId.DenseTransformer;

                if (ambitious)
                {
                    family = BestFamily(simulation, budget);
                }

                var blueprint = TrainingPlanner.OptimalBlueprintForBudget(
                    "Aurora " + (shipped + 1),
                    family,
                    budget,
                    state.OwnedDataSources);

                // The ceiling is a rule, not a suggestion, so the plan is clamped to it rather than
                // being refused every day for a year.
                var ceiling = simulation.ParameterCeilingBillions();

                if (blueprint.ParameterCountBillions > ceiling)
                {
                    blueprint = blueprint.WithParameters(ceiling * 0.95);
                }

                // **And the corpus is the other ceiling, which is the one that stopped the first
                // run of this probe dead.** A compute-optimal plan asks for twenty tokens a
                // parameter, and the company only has the tokens its corpora supply: the operator
                // asked for 2,627B against 1,800B owned every day for seven years and was refused
                // every time, which is the rule working and the operator not listening.
                var supply = DatasetCatalog.Blend(
                    blueprint.DataSources,
                    blueprint.TrainingTokensBillions,
                    state.Date,
                    state.BestCapability,
                    state.Founder.DataSupplyMultiplier).AvailableTokensBillions;

                if (blueprint.TrainingTokensBillions > supply)
                {
                    blueprint = blueprint.WithTokens(supply * 0.98);
                }

                // One line means each release replaces the one before it on the market, which is
                // what a product is. Without it every release is a separate product competing
                // with the company's own back catalogue.
                if (oneLine)
                {
                    blueprint = blueprint.WithFamily("Aurora");
                }

                if (simulation.TryStartTraining(blueprint, out var trainWhy))
                {
                    shipped++;
                }
                else
                {
                    refused("training", trainWhy);
                }
            }

            // ---- ship what is finished ---------------------------------------------------------
            if (state.Shelf.Count > 0)
            {
                if (simulation.TryReleaseModel(0, 1.0, out var shipWhy))
                {
                    released.Add(state.DeployedModels[^1].Name);
                }
                else
                {
                    refused("release", shipWhy);
                }
            }

            // ---- keep the tree moving ----------------------------------------------------------
            if (ambitious)
            {
                AimTheTree(simulation, ref researched);
                Advertise(simulation);
            }
            else if (state.ActiveResearch == null)
            {
                foreach (var node in simulation.ResearchBoard())
                {
                    if (node.CanStart && simulation.TryStartResearch(node.Node.Id, out _))
                    {
                        researched++;
                        break;
                    }
                }
            }

            // ---- rent to what the run needs ----------------------------------------------------
            if (state.Date.DayIndex % 30 == 0)
            {
                // An owner rents the shortfall rather than the whole cluster. Renting the same
                // amount and *also* buying would be a company with two clusters and one
                // workload, which is not a strategy anybody plays.
                var affordable = state.CashUsd / (ownsCompute ? 160_000.0 : 40_000.0);
                simulation.SetRentedPetaflops(Math.Clamp(affordable, 120.0, 60_000.0));
            }

            if (ownsCompute)
            {
                OwnSomething(simulation, refused);
            }

            // ---- spend what is left ------------------------------------------------------------
            if (state.Date.DayIndex % 90 != 0)
            {
                return;
            }

            if (state.DeployedModels.Count > 0
                && simulation.TryStartUpgrades(0, new[] { ModelTrait.Reasoning }, out _))
            {
                upgrades++;
            }

            if (state.Staff.Headcount < state.Staff.Desks)
            {
                state.Staff.Add(new Hire(StaffRole.ResearchScientist, 3, state.Date));
                hires++;
            }

            // Upwards only. The first version walked the list every quarter and moved Loft, then
            // Floor, then Loft again for fourteen years, paying the fit-out each time: the probe
            // reporting its own bug rather than the game's.
            var wanted = state.CashUsd > 400_000_000L ? OfficeTier.Floor
                : state.CashUsd > 40_000_000L ? OfficeTier.Loft
                : state.Staff.Office;

            if (wanted > state.Staff.Office && simulation.TryMoveOffice(wanted, out _))
            {
                offices.Add(wanted.ToString());
            }

            if (state.CashUsd < 2_000_000L && simulation.TryTakeLoan(LoanProduct.BridgeFacility, out _))
            {
                loans++;
            }

            // ---- and play dirty, once a year, to see whether anybody notices --------------------
            if (state.Date.DayIndex % 365 == 180)
            {
                var target = state.Rivals.Agents.FirstOrDefault();

                if (target != null
                    && simulation.TrySmear(target.Competitor, SmearTier.Whisper, out _, out _))
                {
                    smears++;
                }
            }

            if (state.SmearThreat != null)
            {
                simulation.TryAnswerSmearThreat(settle: false, out _);
            }
        }

        /// <summary>
        /// Every node that raises a ceiling, and everything they stand on. Computed once: the tree
        /// does not change during a campaign.
        /// </summary>
        private static HashSet<ResearchNodeId> aimedAt;

        private static HashSet<ResearchNodeId> AimedAt()
        {
            if (aimedAt != null)
            {
                return aimedAt;
            }

            var targets = new Stack<ResearchNodeId>();

            foreach (var (node, _) in ScaleCeiling.Ladder)
            {
                targets.Push(node);
            }

            foreach (var node in ResearchTree.All)
            {
                if (node.UnlocksData != DatasetSource.None
                    || node.UnlocksArchitecture != ArchitectureId.None)
                {
                    targets.Push(node.Id);
                }
            }

            aimedAt = new HashSet<ResearchNodeId>();

            while (targets.Count > 0)
            {
                var id = targets.Pop();

                if (!aimedAt.Add(id))
                {
                    continue;
                }

                foreach (var need in ResearchTree.Get(id).Prerequisites)
                {
                    targets.Push(need);
                }
            }

            return aimedAt;
        }

        /// <summary>
        /// Research aimed at what caps the next model, and paid for. The oldest open node on the
        /// path first, because the calendar opened it first; anything else only once the path is
        /// clear.
        /// </summary>
        private static void AimTheTree(CompanySimulation simulation, ref int researched)
        {
            var state = simulation.State;

            // A percent of the balance a month, within sane bounds, reviewed monthly.
            if (state.Date.DayIndex % 30 == 0)
            {

                state.ResearchFunding = ResearchFundingMode.Fixed;
                state.ResearchMonthlyUsd = Math.Clamp(state.CashUsd / 100L,
                    ResearchBudget.MinimumMonthlyUsd, 6_000_000L);
            }

            if (state.ActiveResearch != null)
            {
                return;
            }

            var aimed = AimedAt();
            ResearchStanding? pick = null;

            foreach (var node in simulation.ResearchBoard())
            {
                if (!node.CanStart)
                {
                    continue;
                }

                var better = pick == null
                    || (aimed.Contains(node.Node.Id) && !aimed.Contains(pick.Value.Node.Id))
                    || (aimed.Contains(node.Node.Id) == aimed.Contains(pick.Value.Node.Id)
                        && node.Node.EarliestDate.DayIndex < pick.Value.Node.EarliestDate.DayIndex);

                if (better)
                {
                    pick = node;
                }
            }

            if (pick != null && simulation.TryStartResearch(pick.Value.Node.Id, out _))
            {
                researched++;
            }
        }

        /// <summary>
        /// One campaign at a time, three channels, aimed at the largest audience, six months at a
        /// stretch. What a company that means to be known does, at a price it can carry.
        /// </summary>
        private static void Advertise(CompanySimulation simulation)
        {
            var state = simulation.State;

            if (state.DeployedModels.Count == 0 || state.CashUsd < 30_000_000L)
            {
                return;
            }

            foreach (var campaign in state.Campaigns)
            {
                if (!campaign.HasFinished(state.Date))
                {
                    return;
                }
            }

            state.ClearCampaigns();
            state.AddCampaign(new MarketingCampaign(
                new[] { MarketingChannel.Press, MarketingChannel.Creators, MarketingChannel.Social },
                AudienceSegment.Consumer, 6, state.Date));
        }

        /// <summary>The adopted family that projects best at this budget.</summary>
        private static ArchitectureId BestFamily(CompanySimulation simulation, double budget)
        {
            var state = simulation.State;
            var best = ArchitectureId.DenseTransformer;
            var bestCapability = double.MinValue;

            foreach (var family in state.AdoptedArchitectures)
            {
                var blueprint = TrainingPlanner.OptimalBlueprintForBudget("probe", family, budget,
                    state.OwnedDataSources);

                var ceiling = simulation.ParameterCeilingBillions();
                if (blueprint.ParameterCountBillions > ceiling)
                {
                    blueprint = blueprint.WithParameters(ceiling * 0.95);
                }

                var capability = simulation.Project(blueprint).ProjectedCapability;

                if (capability > bestCapability)
                {
                    bestCapability = capability;
                    best = family;
                }
            }

            return best;
        }

        private static bool IsLoud(CompanyEventType type) =>
            type == CompanyEventType.SafetyIncident
            || type == CompanyEventType.SmearBackfired
            || type == CompanyEventType.ModelReleased;

        private static string Trim(string message) =>
            string.IsNullOrEmpty(message) ? string.Empty
                : message.Length <= 90 ? message : message[..90] + "...";

        private static string Money(long usd) => usd.ToString("C0", Culture);
    }
}
