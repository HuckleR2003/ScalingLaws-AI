using System;

namespace ScalingLaws.Data
{
    /// <summary>
    /// What the company has learned about running a room full of machines.
    ///
    /// **Derived from the research the company holds, never stored.** The fifth mechanism in this
    /// project built that way, after `RivalExpansion`, `LabTraits`, `WorldEventCatalog` and
    /// `SafetyRecord`: a pure function of what has been researched, so it replays identically, needs
    /// no migration, and cannot drift from the node that granted it.
    ///
    /// **Passed into the hall rather than read by it.** `ServerHall` is the placement rules and the
    /// heat arithmetic and it has no business knowing what a research node is; the simulation reads
    /// the tree and hands the answer down. That is the same split the hall already has with the till:
    /// it knows where a cabinet can stand and nothing about what one costs.
    ///
    /// Every field is a number that already existed. Nothing here is a new mechanic, which is the
    /// rule every node in this game follows.
    /// </summary>
    public readonly struct RoomUpgrades
    {
        public RoomUpgrades(double extraCoolingKilowatts, double immersionThrottlePenalty,
            double tariffUsd, bool showsTelemetry)
        {
            ExtraCoolingKilowatts = Math.Clamp(extraCoolingKilowatts, 0.0, 20.0);
            ImmersionThrottlePenalty = Math.Clamp(immersionThrottlePenalty, 0.2, 4.0);
            TariffUsd = Math.Clamp(tariffUsd, 0.01, 1.0);
            ShowsTelemetry = showsTelemetry;
        }

        /// <summary>
        /// What every cabinet sheds on top of its own rating, from airflow modelling.
        ///
        /// Wide and shallow on purpose, against the fan's narrow and deep: a fan buys one cabinet a
        /// lot of headroom and costs it a slot, and this buys every cabinet a little and costs
        /// nothing but the research. Two answers to heat that are worth having at the same time.
        /// </summary>
        public double ExtraCoolingKilowatts { get; }

        /// <summary>
        /// How steeply an immersion tank throttles once it is over its rating.
        ///
        /// **Immersion only, and that is the whole trade.** The tank is the dearest cabinet in the
        /// catalog and it currently ages exactly like the cheapest one: past its rating the same
        /// curve applies. Liquid loops make the expensive cabinet the one that survives 2027, which
        /// is the reason to buy it that the catalog was missing.
        /// </summary>
        public double ImmersionThrottlePenalty { get; }

        /// <summary>
        /// What a kilowatt hour costs in the basement.
        ///
        /// The room runs on a domestic tariff because it is a basement, and the power bill is the
        /// one cost that grows with the thing the player is proudest of. A substation is the company
        /// stopping being a household.
        /// </summary>
        public double TariffUsd { get; }

        /// <summary>
        /// Whether the cabinet panel shows what one more card would do before it is fitted.
        ///
        /// **The only node in this game that buys information rather than a number.** Everything
        /// else in the tree makes something bigger, cheaper or safer; this makes a decision
        /// visible, and the decision was already there.
        /// </summary>
        public bool ShowsTelemetry { get; }

        /// <summary>A company that has researched none of it. The defaults every caller had before.</summary>
        public static RoomUpgrades None => new(
            0.0,
            ServerRackCatalog.ThrottlePenalty,
            ComputePoolTariff.DomesticUsd,
            false);

        /// <summary>How much cooling this cabinet has, once the room's own learning is counted.</summary>
        public double CoolingFor(ServerRackDefinition definition, int fans) =>
            definition.CoolingCapacityKilowatts
            + fans * ServerRackCatalog.FanCoolingKilowatts
            + ExtraCoolingKilowatts;

        /// <summary>How steeply this kind of cabinet throttles.</summary>
        public double PenaltyFor(ServerRack rack) =>
            rack == ServerRack.Immersion
                ? ImmersionThrottlePenalty
                : ServerRackCatalog.ThrottlePenalty;

        /// <summary>
        /// What the company knows today.
        ///
        /// Takes the same `HasResearch` predicate `ScaleCeiling` and `ArchitectureCeiling` take, so
        /// a caller with a company hands it `State.HasResearch` and a test hands it a lambda.
        /// </summary>
        public static RoomUpgrades For(Func<ResearchNodeId, bool> has)
        {
            if (has == null)
            {
                return None;
            }

            return new RoomUpgrades(
                has(ResearchNodeId.AirflowModelling) ? AirflowCoolingKilowatts : 0.0,
                has(ResearchNodeId.LiquidLoops)
                    ? LiquidLoopPenalty
                    : ServerRackCatalog.ThrottlePenalty,
                has(ResearchNodeId.OwnSubstation)
                    ? ComputePoolTariff.SubstationUsd
                    : ComputePoolTariff.DomesticUsd,
                has(ResearchNodeId.RackTelemetry));
        }

        /// <summary>
        /// What airflow modelling adds to every cabinet.
        ///
        /// Half a fan, roughly, and no slot. Measured against `FanCoolingKilowatts` of 2.4: a fan
        /// stays clearly the stronger answer for one cabinet in trouble, and this is the better one
        /// for a floor that is all slightly warm.
        /// </summary>
        public const double AirflowCoolingKilowatts = 1.2;

        /// <summary>
        /// How steeply an immersion tank throttles with liquid loops fitted.
        ///
        /// Against the catalog's 2.2. Not zero and not close to it: a tank twice over its rating is
        /// still losing most of its output, because a node that removed heat as a constraint would
        /// delete the mechanic the whole room exists for.
        /// </summary>
        public const double LiquidLoopPenalty = 1.1;
    }

    /// <summary>
    /// What the basement pays for power.
    ///
    /// Its own type because two places need the figure and one of them is `Simulation/`, which may
    /// not reference a pool that lives beside it. Both numbers are real: a domestic rate and a small
    /// industrial connection.
    /// </summary>
    public static class ComputePoolTariff
    {
        /// <summary>A basement is a house, and a house pays a house's rate.</summary>
        public const double DomesticUsd = 0.19;

        /// <summary>What the same room pays once it stops being on the household meter.</summary>
        public const double SubstationUsd = 0.11;
    }
}
