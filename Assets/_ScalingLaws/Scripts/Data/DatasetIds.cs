using System;

namespace ScalingLaws.Data
{
    /// <summary>
    /// Training corpora the company can hold, as a bitmask so a blueprint stores its data mix in a
    /// single int. Values are powers of two and must never be renumbered.
    /// </summary>
    [Flags]
    public enum DatasetSource
    {
        None = 0,
        WebCrawl = 1 << 0,
        CuratedWeb = 1 << 1,
        CodeCorpus = 1 << 2,
        LicensedBooks = 1 << 3,
        AcademicArchive = 1 << 4,
        HumanFeedback = 1 << 5,
        Synthetic = 1 << 6,
        VideoAndAudio = 1 << 7
    }
}
