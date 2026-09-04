using System;
using System.Collections.Generic;
using System.Linq;

namespace ScalingLaws.Data
{
    /// <summary>
    /// What the shop sells. The order is the order it is listed in, cheapest first.
    /// </summary>
    public enum FurnitureKind
    {
        Desk = 0,
        Plant = 1,
        Whiteboard = 2,
        Sofa = 3,
        CoffeeBar = 4,
        Bookshelf = 5,
        StandingDesk = 6,
        ArtPiece = 7,
        Aquarium = 8,
        SleepPod = 9
    }

    /// <summary>
    /// One thing the player can buy for the office.
    ///
    /// **The bonuses are additive from zero, not multipliers around 1.0.** The neutral option rule
    /// says the middle of a catalog must be exactly neutral so a new mechanic does not retune the
    /// economy; here the neutral case is owning nothing at all, which is where every campaign starts
    /// and which contributes zero. A player who never opens the shop plays the game that was
    /// balanced without it.
    /// </summary>
    public sealed class FurniturePiece
    {
        public FurniturePiece(FurnitureKind kind, double priceUsd,
            int deskSeats, double moraleBonus, double researchBonus,
            float sizeX, float sizeY, float sizeZ, string tint)
        {
            Kind = kind;
            PriceUsd = priceUsd;
            DeskSeats = deskSeats;
            MoraleBonus = moraleBonus;
            ResearchBonus = researchBonus;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            Tint = tint;
        }

        public FurnitureKind Kind { get; }

        private static string KeyFor(FurnitureKind kind) => kind switch
        {
            FurnitureKind.Plant => "piece.plant",
            FurnitureKind.Whiteboard => "piece.whiteboard",
            FurnitureKind.Desk => "piece.desk",
            FurnitureKind.Bookshelf => "piece.bookshelf",
            FurnitureKind.Sofa => "piece.sofa",
            FurnitureKind.StandingDesk => "piece.standingdesk",
            FurnitureKind.CoffeeBar => "piece.coffeebar",
            FurnitureKind.ArtPiece => "piece.art",
            FurnitureKind.Aquarium => "piece.aquarium",
            _ => "piece.sleeppod"
        };

        /// <summary>Read from the book at access time. See `PrecisionDefinition`.</summary>
        public string DisplayName => Loc.T(KeyFor(Kind));

        /// <summary>What it does, in the player's terms rather than the simulation's.</summary>
        public string Blurb => Loc.T(KeyFor(Kind) + ".blurb");

        public double PriceUsd { get; }

        /// <summary>Extra seats, which is the only thing that raises the hiring cap.</summary>
        public int DeskSeats { get; }

        /// <summary>Added to morale while it is placed. Small: a sofa is not a pay rise.</summary>
        public double MoraleBonus { get; }

        /// <summary>Added to the daily research rate as a fraction. Also small, and also capped.</summary>
        public double ResearchBonus { get; }

        public float SizeX { get; }
        public float SizeY { get; }
        public float SizeZ { get; }

        /// <summary>Hex colour the room builder paints it. Data so the shop can show a swatch too.</summary>
        public string Tint { get; }

        /// <summary>
        /// What selling it back returns.
        ///
        /// Thirty per cent, which is deliberately punishing: furniture is a decision the player
        /// should think about before clicking, not a savings account they park cash in.
        /// </summary>
        public double ResaleValueUsd => Math.Round(PriceUsd * FurnitureCatalog.ResaleFraction);
    }

    public static class FurnitureCatalog
    {
        /// <summary>Selling something back returns this much of what it cost.</summary>
        public const double ResaleFraction = 0.30;

        /// <summary>
        /// The most morale every placed piece together can add.
        ///
        /// Without it a player with cash could buy forty sofas and never lose anybody again. The cap
        /// is what keeps the shop a decoration rather than a difficulty setting.
        /// </summary>
        public const double MoraleCeiling = 0.12;

        /// <summary>Same reasoning, for the research rate.</summary>
        public const double ResearchCeiling = 0.10;

        private static readonly List<FurniturePiece> Pieces = new()
        {
            // The words are `piece.*` in the phrase book.
            new FurniturePiece(FurnitureKind.Plant,
                900, 0, 0.010, 0.000, 0.7f, 1.1f, 0.7f, "#38703C"),

            new FurniturePiece(FurnitureKind.Whiteboard,
                2_400, 0, 0.005, 0.012, 1.8f, 1.2f, 0.1f, "#D6D6D2"),

            new FurniturePiece(FurnitureKind.Desk,
                6_500, 1, 0.000, 0.000, 1.3f, 0.75f, 0.7f, "#70522F"),

            new FurniturePiece(FurnitureKind.Bookshelf,
                7_800, 0, 0.008, 0.010, 1.6f, 1.9f, 0.4f, "#4A3524"),

            new FurniturePiece(FurnitureKind.Sofa,
                12_000, 0, 0.022, 0.000, 2.1f, 0.8f, 0.9f, "#B78C38"),

            new FurniturePiece(FurnitureKind.StandingDesk,
                14_500, 1, 0.014, 0.008, 1.4f, 1.1f, 0.75f, "#2F5C6E"),

            new FurniturePiece(FurnitureKind.CoffeeBar,
                19_000, 0, 0.028, 0.014, 1.5f, 1.4f, 0.8f, "#8C4A2A"),

            new FurniturePiece(FurnitureKind.ArtPiece,
                46_000, 0, 0.030, 0.000, 2.2f, 1.6f, 0.12f, "#7A3E86"),

            new FurniturePiece(FurnitureKind.Aquarium,
                72_000, 0, 0.040, 0.006, 2.4f, 1.5f, 0.7f, "#1E6E8C"),

            new FurniturePiece(FurnitureKind.SleepPod,
                95_000, 0, 0.036, 0.028, 2.2f, 1.3f, 1.2f, "#3A3F52")
        };

        /// <summary>Cheapest first, which is the order a player wants to shop in.</summary>
        public static IReadOnlyList<FurniturePiece> All =>
            Pieces.OrderBy(piece => piece.PriceUsd).ToList();

        public static FurniturePiece Get(FurnitureKind kind) =>
            Pieces.FirstOrDefault(piece => piece.Kind == kind)
            ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "No such piece.");
    }
}
