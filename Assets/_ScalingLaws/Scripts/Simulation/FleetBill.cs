using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// What one day of running the fleet is made of.
    ///
    /// These four numbers were already being computed one at a time inside <see cref="ComputePool"/>
    /// and then summed into a single figure, which is why the financial report could only say
    /// "operating cost" and ended up filing cloud rent under the cost of serving paying users. They
    /// are separate facts and the player pays them for different reasons: rent stops the moment you
    /// stop renting, electricity scales with what you own and run, housing is the floor space, and
    /// maintenance is the hardware wearing out while it works.
    /// </summary>
    public readonly struct FleetBill
    {
        public FleetBill(double cloudRentUsd, double electricityUsd, double housingUsd,
            double maintenanceUsd)
        {
            CloudRentUsd = Math.Max(0.0, SimUnits.Finite(cloudRentUsd));
            ElectricityUsd = Math.Max(0.0, SimUnits.Finite(electricityUsd));
            HousingUsd = Math.Max(0.0, SimUnits.Finite(housingUsd));
            MaintenanceUsd = Math.Max(0.0, SimUnits.Finite(maintenanceUsd));
        }

        /// <summary>Capacity hired from a cloud. Stops the day it is released.</summary>
        public double CloudRentUsd { get; }

        /// <summary>Power for hardware the company owns.</summary>
        public double ElectricityUsd { get; }

        /// <summary>Floor space, cooling and connectivity for that hardware.</summary>
        public double HousingUsd { get; }

        /// <summary>Hardware wearing out while it works. Cash, unlike depreciation.</summary>
        public double MaintenanceUsd { get; }

        public double TotalUsd => CloudRentUsd + ElectricityUsd + HousingUsd + MaintenanceUsd;

        public override string ToString() =>
            $"rent {CloudRentUsd:N0}, power {ElectricityUsd:N0}, "
            + $"housing {HousingUsd:N0}, upkeep {MaintenanceUsd:N0}";
    }
}
