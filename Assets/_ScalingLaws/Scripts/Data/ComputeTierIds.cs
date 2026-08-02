namespace ScalingLaws.Data
{
    /// <summary>
    /// Where the company's compute physically lives. Each tier is a different balance sheet, not a
    /// different speed: the same accelerator does the same FLOPs in all three.
    /// Explicit values, written into saves, never renumbered.
    /// </summary>
    public enum ComputeTier
    {
        None = 0,

        /// <summary>Somebody else's cluster, billed by the hour. No capital, no lead time, no exit cost.</summary>
        RentedCloud = 1,

        /// <summary>Owned hardware in a rented hall. Capital plus rack fees, and it is yours whether you use it or not.</summary>
        ColocatedServers = 2,

        /// <summary>Owned building, owned power contract. The cheapest FLOP and the slowest mistake to undo.</summary>
        OwnDatacenter = 3
    }
}
