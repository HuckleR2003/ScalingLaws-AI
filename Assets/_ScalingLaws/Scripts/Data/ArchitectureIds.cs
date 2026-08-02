namespace ScalingLaws.Data
{
    /// <summary>
    /// Model architecture families, in the order the field discovered them. Values are explicit
    /// because they go into saves. Never renumber, only append.
    /// </summary>
    public enum ArchitectureId
    {
        None = 0,

        /// <summary>Plain decoder-only transformer. Every parameter runs on every token.</summary>
        DenseTransformer = 1,

        /// <summary>Memory-efficient attention and grouped queries. Same quality, better throughput.</summary>
        EfficientAttention = 2,

        /// <summary>Sparse mixture of experts. Most parameters sit idle on any given token.</summary>
        SparseMixture = 3,

        /// <summary>Mixture tuned for long context. Costs throughput, sells to enterprise.</summary>
        LongContextMixture = 4,

        /// <summary>Mixture with reinforcement-learned reasoning. Smarter per parameter, far dearer to serve.</summary>
        ReasoningMixture = 5,

        /// <summary>Attention and state-space hybrid. Cheap long context without the quality tax.</summary>
        HybridStateSpace = 6,

        // Six slots for families the company designs itself. These never appear in
        // ArchitectureCatalog: they live on the company and resolve through IArchitectureSource.
        // Six because a research org that maintains more than six families maintains none of them.
        CustomFamilyA = 1001,
        CustomFamilyB = 1002,
        CustomFamilyC = 1003,
        CustomFamilyD = 1004,
        CustomFamilyE = 1005,
        CustomFamilyF = 1006
    }

    /// <summary>
    /// Where an architecture definition comes from. The catalog knows the six public families; a
    /// company also knows whatever it designed itself. Everything that needs to resolve an
    /// architecture takes one of these rather than reaching for the catalog directly, which is what
    /// lets a custom family behave exactly like a published one everywhere downstream.
    /// </summary>
    public interface IArchitectureSource
    {
        bool TryGetArchitecture(ArchitectureId id, out ArchitectureDefinition definition);
    }
}
