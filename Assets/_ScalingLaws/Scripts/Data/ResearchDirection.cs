namespace ScalingLaws.Data
{
    /// <summary>
    /// The five directions a family programme can lean in.
    ///
    /// **Moved out of `Simulation/` when `ArchitectureCeiling` needed it.** `Data/` may not depend
    /// on `Simulation/` (the arrow only points the other way), and a direction is a category rather
    /// than a rule: nothing about the list of five is economics, and the catalogs that gate them
    /// have to be able to name them.
    ///
    /// Explicit values. They go into saves and must never be renumbered.
    /// </summary>
    public enum ResearchDirection
    {
        /// <summary>Fewer parameters firing per token. The biggest lever on what a run costs.</summary>
        Sparsity = 0,

        /// <summary>Better utilisation of the cluster during training. Shortens the calendar.</summary>
        Throughput = 1,

        /// <summary>More quality out of each parameter. Raises the ceiling on every model in the family.</summary>
        Quality = 2,

        /// <summary>Cheaper tokens once the model is live. Invisible until the price war.</summary>
        Serving = 3,

        /// <summary>Structural reasoning gains that scaling alone does not buy.</summary>
        Reasoning = 4
    }

}
