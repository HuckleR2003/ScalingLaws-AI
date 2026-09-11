using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// Something the company can put engineering work into, whether or not anybody can buy it yet.
    ///
    /// **The screen had no way to say "the one that finished last week".** `TryStartUpgrades` has
    /// carried an `onShelf` argument since it was written, and the comment above it says plainly
    /// that a finished run waiting to ship is exactly when a real lab does its evaluation work. No
    /// caller ever passed it. So a player whose second model had just finished training could only
    /// improve the first one, and the only way to reach the new one was to release it first, which
    /// is the decision the upgrades were supposed to inform.
    ///
    /// A view, not a thing the state owns: every figure on it is read off the model at the moment
    /// it was asked for. It exists so the panel does not have to ask which of two types it is
    /// holding, which is how the two would drift into drawing different things.
    /// </summary>
    public readonly struct UpgradeSubject
    {
        public UpgradeSubject(int index, bool onShelf, string name, string versionName,
            ModelTraitSet traits, ModelType type, double capability, double brand,
            double efficiency, int daysWaiting)
        {
            Index = index;
            OnShelf = onShelf;
            Name = name ?? string.Empty;
            VersionName = versionName ?? string.Empty;
            Traits = traits;
            Type = type;
            Capability = capability;
            Brand = brand;
            Efficiency = efficiency;
            DaysWaiting = daysWaiting;
        }

        /// <summary>
        /// Index into the deployed list, or into the shelf when <see cref="OnShelf"/> is set.
        ///
        /// The pair travels together and must keep travelling together: the two lists are numbered
        /// separately, so an index without its flag addresses a different model.
        /// </summary>
        public int Index { get; }

        /// <summary>Finished, not released. Work commissioned on it ships with the model.</summary>
        public bool OnShelf { get; }

        public string Name { get; }

        /// <summary>The version on sale. Empty on the shelf, because nothing has been published.</summary>
        public string VersionName { get; }

        public ModelTraitSet Traits { get; }
        public ModelType Type { get; }

        /// <summary>Capability as it stands today, upgrades included.</summary>
        public double Capability { get; }

        public double Brand { get; }
        public double Efficiency { get; }

        /// <summary>Days it has sat on the shelf. Zero for anything on sale.</summary>
        public int DaysWaiting { get; }
    }
}
