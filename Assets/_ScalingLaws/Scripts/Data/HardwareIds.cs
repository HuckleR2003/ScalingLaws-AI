namespace ScalingLaws.Data
{
    /// <summary>
    /// What a piece of hardware does in a cluster. Only <see cref="Accelerator"/> produces FLOPs;
    /// the other three feed it, and a cluster runs at the speed of whatever is missing.
    /// </summary>
    public enum HardwareClass
    {
        Accelerator = 0,
        Cpu = 1,
        Memory = 2,
        Network = 3
    }

    /// <summary>
    /// Stable identity for one hardware generation. Values are explicit and grouped by class
    /// (1xx accelerators, 2xx hosts, 3xx memory, 4xx fabric) because they are written into saves.
    /// Never renumber an existing entry; append instead.
    /// </summary>
    public enum HardwareGenerationId
    {
        None = 0,

        AcceleratorV100 = 101,
        AcceleratorA100 = 102,
        AcceleratorH100 = 103,
        AcceleratorH200 = 104,
        AcceleratorB200 = 105,
        AcceleratorGb300 = 106,
        AcceleratorVr200 = 107,
        AcceleratorNext = 108,

        CpuIceLake = 201,
        CpuMilan = 202,
        CpuGenoa = 203,
        CpuEmeraldRapids = 204,
        CpuTurin = 205,
        CpuNext = 206,

        MemoryDdr4 = 301,
        MemoryDdr5 = 302,
        MemoryDdr5Dense = 303,
        MemoryCxl = 304,

        NetworkIb200 = 401,
        NetworkIb400 = 402,
        NetworkIb800 = 403,
        NetworkOptical1600 = 404
    }
}
