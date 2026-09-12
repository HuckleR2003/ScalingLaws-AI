using System;
using System.Collections.Generic;
using System.Linq;

namespace ScalingLaws.Data
{
    /// <summary>
    /// Who is offering. Written into saves, so append and never renumber.
    ///
    /// **Parodies, like the labs, and for the same reason.** This project already decided that
    /// competitors are recognisable parodies rather than real company names, because the joke
    /// survives and the legal risk does not. A screen naming real venture firms as writing cheques
    /// to the player is the same exposure wearing a friendlier face.
    /// </summary>
    public enum InvestorId
    {
        None = 0,

        /// <summary>Emil's firm. The one holding a slice before anybody is asked.</summary>
        ESolutions = 1,

        SequelPartners = 2,
        BenchmarqVentures = 3,
        IndexPointCapital = 4,
        LightspireGrowth = 5,
        TigrisGlobal = 6,
        FoundersFoundry = 7,
        NorthGateEndowment = 8,
        MeridianPension = 9,
        StateInnovationFund = 10,
        HalversonFamilyOffice = 11,
        AtlasCorporateVentures = 12
    }

    /// <summary>What kind of money it is, which is most of how it behaves.</summary>
    public enum InvestorKind
    {
        Venture = 0,
        Growth = 1,
        Institution = 2,
        Corporate = 3,
        Angel = 4
    }

    /// <summary>
    /// One name that might put money in, and the three numbers that make it a different decision
    /// from the name beside it.
    ///
    /// **None of them is simply better.** Appetite is what they will pay against the market price,
    /// ticket is how much they want to write, and patience is how long the offer stands. A firm that
    /// pays up wants more of the company for it; one that lowballs leaves the offer on the table for
    /// months. `InvestorCatalogTests` walks every pair and fails if one is ever better on all three,
    /// because at that moment twelve names collapse into one.
    /// </summary>
    public sealed class InvestorDefinition
    {
        public InvestorDefinition(InvestorId id, InvestorKind kind, double appetite,
            double ticketShare, int patienceDays, string accentHex)
        {
            Id = id;
            Kind = kind;
            Appetite = Math.Clamp(appetite, 0.4, 1.6);
            TicketShare = Math.Clamp(ticketShare, 0.15, 1.6);
            PatienceDays = Math.Clamp(patienceDays, 7, 180);
            AccentHex = accentHex;
        }

        public InvestorId Id { get; }

        public InvestorKind Kind { get; }

        /// <summary>What they will pay against what the market says the company is worth.</summary>
        public double Appetite { get; }

        /// <summary>How large a cheque, against the stage's own target raise.</summary>
        public double TicketShare { get; }

        /// <summary>How long their offer stands before it is withdrawn.</summary>
        public int PatienceDays { get; }

        public string AccentHex { get; }

        /// <summary>
        /// The phrase-book stem.
        ///
        /// Every arm written out and no default, which is the Statecraft lesson: a `_` arm is a
        /// valid answer with real words behind it, so twelve investors quietly share one name and
        /// nothing fails.
        /// </summary>
        private static string KeyFor(InvestorId id) => id switch
        {
            InvestorId.ESolutions => "investor.esolutions",
            InvestorId.SequelPartners => "investor.sequel",
            InvestorId.BenchmarqVentures => "investor.benchmarq",
            InvestorId.IndexPointCapital => "investor.indexpoint",
            InvestorId.LightspireGrowth => "investor.lightspire",
            InvestorId.TigrisGlobal => "investor.tigris",
            InvestorId.FoundersFoundry => "investor.foundry",
            InvestorId.NorthGateEndowment => "investor.northgate",
            InvestorId.MeridianPension => "investor.meridian",
            InvestorId.StateInnovationFund => "investor.stateinnovation",
            InvestorId.HalversonFamilyOffice => "investor.halverson",
            InvestorId.AtlasCorporateVentures => "investor.atlas",
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "No such investor.")
        };

        /// <summary>
        /// What they are called.
        ///
        /// **E-Solutions is the one exception and it has to be.** Emil's firm is on the competitor
        /// board, in the news and in the tutorial, and a second spelling of its name here would be
        /// the same company under two names the first time somebody renames it.
        /// </summary>
        public string DisplayName => Id == InvestorId.ESolutions
            ? CompetitorCatalog.NameOf(CompetitorId.ESolutions)
            : Loc.T(KeyFor(Id));

        /// <summary>One line on what this money wants, for the row in the book.</summary>
        public string Pitch => Loc.T(KeyFor(Id) + ".pitch");

        public string KindName => Loc.T(Kind switch
        {
            InvestorKind.Venture => "investor.kind.venture",
            InvestorKind.Growth => "investor.kind.growth",
            InvestorKind.Institution => "investor.kind.institution",
            InvestorKind.Corporate => "investor.kind.corporate",
            _ => "investor.kind.angel"
        });
    }

    public static class InvestorCatalog
    {
        public const string CatalogVersion = "2026.09.12";

        /// <summary>
        /// What Emil's firm holds before the player has been asked about anything.
        ///
        /// **Small enough to be a favour rather than a stake.** Two per cent says somebody believed
        /// in this before there was anything to believe in, and it is the kind of line a player
        /// notices and wants to do something about, which is the point of it being there.
        /// </summary>
        public const double FriendsAndFamilyStake = 0.02;

        private static readonly InvestorDefinition[] Entries =
        {
            new(InvestorId.ESolutions, InvestorKind.Angel, 0.95, 0.20, 120, "#7FBF5F"),
            new(InvestorId.SequelPartners, InvestorKind.Venture, 1.15, 1.00, 21, "#5B8DEF"),
            new(InvestorId.BenchmarqVentures, InvestorKind.Venture, 1.00, 0.70, 35, "#A66BE0"),
            new(InvestorId.IndexPointCapital, InvestorKind.Venture, 0.88, 0.55, 60, "#3FB6A8"),
            new(InvestorId.LightspireGrowth, InvestorKind.Growth, 1.08, 1.30, 28, "#E0883C"),
            new(InvestorId.TigrisGlobal, InvestorKind.Growth, 1.25, 1.50, 14, "#E06B6B"),
            // 0.80 on the first pass, and `NoInvestorIsSimplyBetterThanAnother` caught it: the
            // state fund paid more, wrote more and waited longer, so nobody would ever have
            // taken this one. People who have run one of these back founders generously in
            // small amounts, which is the axis it should win on.
            new(InvestorId.FoundersFoundry, InvestorKind.Angel, 0.90, 0.25, 90, "#D6A03C"),
            new(InvestorId.NorthGateEndowment, InvestorKind.Institution, 0.78, 0.90, 120, "#8FA3C4"),
            new(InvestorId.MeridianPension, InvestorKind.Institution, 0.72, 1.10, 150, "#6E86AC"),
            new(InvestorId.StateInnovationFund, InvestorKind.Institution, 0.85, 0.60, 180, "#4FA3C7"),
            new(InvestorId.HalversonFamilyOffice, InvestorKind.Angel, 0.92, 0.35, 75, "#C98FB0"),
            new(InvestorId.AtlasCorporateVentures, InvestorKind.Corporate, 0.96, 0.80, 45, "#9AA7B8")
        };

        public static IReadOnlyList<InvestorDefinition> All => Entries;

        /// <summary>Everybody who turns up unasked. Emil is already in, so he is not on this list.</summary>
        public static IReadOnlyList<InvestorDefinition> Approachable =>
            Entries.Where(entry => entry.Id != InvestorId.ESolutions).ToList();

        public static InvestorDefinition Get(InvestorId id) =>
            Entries.FirstOrDefault(entry => entry.Id == id)
            ?? throw new ArgumentOutOfRangeException(nameof(id), id, "No such investor.");

        public static bool TryGet(InvestorId id, out InvestorDefinition definition)
        {
            definition = Entries.FirstOrDefault(entry => entry.Id == id);
            return definition != null;
        }
    }
}
