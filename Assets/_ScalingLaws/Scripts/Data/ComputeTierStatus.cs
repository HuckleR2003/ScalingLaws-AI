namespace ScalingLaws.Data
{
    /// <summary>
    /// Whether a tier is open to the company yet, and if not, what is missing. Locked tiers are
    /// always shown: the point of the gate is that the player can see the ladder from day one and
    /// plan the climb, not discover it by accident.
    /// </summary>
    public readonly struct ComputeTierStatus
    {
        public ComputeTierStatus(ComputeTier tier, bool isUnlocked, string lockReason)
        {
            Tier = tier;
            IsUnlocked = isUnlocked;
            LockReason = isUnlocked ? string.Empty : lockReason ?? string.Empty;
        }

        public ComputeTier Tier { get; }
        public bool IsUnlocked { get; }

        /// <summary>Empty when unlocked. Otherwise a plain sentence naming every unmet requirement.</summary>
        public string LockReason { get; }

        public static ComputeTierStatus Unlocked(ComputeTier tier) => new(tier, true, string.Empty);

        public override string ToString() => IsUnlocked ? $"{Tier}: open" : $"{Tier}: locked ({LockReason})";
    }
}
