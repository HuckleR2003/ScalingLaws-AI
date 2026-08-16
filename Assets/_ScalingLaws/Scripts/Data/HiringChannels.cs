using System;
using System.Collections.Generic;
using System.Linq;

namespace ScalingLaws.Data
{
    /// <summary>Where a person was found. Decides what they cost and what they are worth.</summary>
    public enum HireSource
    {
        /// <summary>Working from somewhere else. Cheap, weak, and available with no office at all.</summary>
        Remote = 0,

        /// <summary>Sent over by an employment agency. Ordinary people at an ordinary price.</summary>
        Agency = 1,

        /// <summary>Headhunted to a specification. Costs more and is worth more than it costs.</summary>
        Specialist = 2
    }

    /// <summary>
    /// One way of finding people.
    ///
    /// **The three channels are one trade-off stated three times: price against quality.** Remote is
    /// the cheap end and is the answer when there is no office and no money. The agency is the
    /// middle and is the answer when there are desks to fill and no time. The specialist is the
    /// expensive end and is the only channel that can be told what to find, which is what actually
    /// makes it worth a fifth more salary.
    ///
    /// The multipliers are on wage and on quality separately on purpose. A channel that scaled both
    /// together would be a pure price tag and the choice would collapse into whoever has more cash.
    /// </summary>
    public sealed class HiringChannel
    {
        public HiringChannel(HireSource source, string displayName, string siteName, string tagline,
            double wageMultiplier, double qualityMultiplier, string accentHex)
        {
            Source = source;
            DisplayName = displayName;
            SiteName = siteName;
            Tagline = tagline;
            WageMultiplier = wageMultiplier;
            QualityMultiplier = qualityMultiplier;
            AccentHex = accentHex;
        }

        public HireSource Source { get; }
        public string DisplayName { get; }

        /// <summary>The address the player types. Half the character of the channel is in it.</summary>
        public string SiteName { get; }

        public string Tagline { get; }

        /// <summary>What they ask, against the position's ordinary rate.</summary>
        public double WageMultiplier { get; }

        /// <summary>
        /// What they are worth, against the level on the advert.
        ///
        /// Applied to the level itself rather than to the effect, so the player sees the honest
        /// number: a remote candidate advertised at 60 arrives at 24 and the letter says so. A
        /// multiplier hidden inside the simulation would be the game lying on the tin.
        /// </summary>
        public double QualityMultiplier { get; }

        public string AccentHex { get; }
    }

    public static class HiringChannels
    {
        /// <summary>Remote people the company can carry before it has to buy the partnership.</summary>
        public const int FreeRemoteSeats = 5;

        /// <summary>And after. Still a ceiling: remote is a bridge, not a company.</summary>
        public const int PartneredRemoteSeats = 14;

        /// <summary>What IThand.hck charges to lift the cap. Roughly a quarter of a year's remote payroll.</summary>
        public const long PartnershipCostUsd = 180_000;

        /// <summary>Shortest and longest a contact takes to come back. Always a wait, never instant.</summary>
        public const int FastestContactDays = 2;

        /// <inheritdoc cref="FastestContactDays"/>
        public const int SlowestContactDays = 4;

        private static readonly List<HiringChannel> Entries = new()
        {
            new HiringChannel(HireSource.Remote, "Remote", "IThand.hck",
                "Contract work, worldwide. No desk required.",
                0.70, 0.40, "#4FA3C7"),

            new HiringChannel(HireSource.Agency, "Agency", "Regional Employment Register",
                "State employment records. Form E-11/b.",
                1.00, 0.70, "#8E8A72"),

            new HiringChannel(HireSource.Specialist, "Specialist", "get-admin.hck",
                "Search by discipline. Hire by specification.",
                1.20, 1.50, "#C9A227")
        };

        public static IReadOnlyList<HiringChannel> All => Entries;

        public static HiringChannel Get(HireSource source) =>
            Entries.FirstOrDefault(entry => entry.Source == source)
            ?? throw new ArgumentOutOfRangeException(nameof(source), source, "No such channel.");

        /// <summary>How many remote people can be on the books at once.</summary>
        public static int RemoteSeats(bool hasPartnership) =>
            hasPartnership ? PartneredRemoteSeats : FreeRemoteSeats;
    }
}
