using System;
using ScalingLaws.Core;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// One entry in the archive: a model, and where it stands today.
    ///
    /// Three states, and they are not the same thing:
    ///
    /// - **Marketed.** On sale, and the strongest in its line, so the market is choosing it.
    /// - **Live but superseded.** Not withdrawn, and earning nothing, because one line is one product
    ///   and something newer in the same line is the one buyers see.
    /// - **Retired.** Taken off sale, by the player or by a regulator.
    ///
    /// The middle one is the one worth drawing, because a player looking at a list of live models
    /// would otherwise expect all of them to be earning and be wrong about most of them.
    /// </summary>
    public readonly struct ModelRecord
    {
        public ModelRecord(int index, DeployedModel model, bool isLive, bool isMarketed, double users,
            double capabilityToday)
        {
            Index = index;
            Model = model;
            IsLive = isLive;
            IsMarketed = isMarketed;
            Users = Math.Max(0.0, SimUnits.Finite(users));
            CapabilityToday = Math.Max(0.0, SimUnits.Finite(capabilityToday));
        }

        /// <summary>Position in <see cref="CompanyState.DeployedModels"/>, for the upgrade screen.</summary>
        public int Index { get; }

        public DeployedModel Model { get; }

        /// <summary>Not withdrawn and past its release date.</summary>
        public bool IsLive { get; }

        /// <summary>Live and not beaten by something newer in its own line.</summary>
        public bool IsMarketed { get; }

        /// <summary>People on it right now. Zero unless it is the one its line is selling.</summary>
        public double Users { get; }

        /// <summary>Capability including everything the upgrade programmes added since release.</summary>
        public double CapabilityToday { get; }

        /// <summary>What the shutdown control needs to know: is there anything to shut down.</summary>
        public bool CanRetire => IsLive;

        public string StateWord => !IsLive
            ? "RETIRED"
            : IsMarketed
                ? "ON SALE"
                : "SUPERSEDED";
    }
}
