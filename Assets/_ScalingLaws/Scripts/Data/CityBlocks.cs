using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// A patch of suburb, described the way a developer would describe it.
    ///
    /// **Streets and plots are generated from this rather than drawn by hand.** An American suburb
    /// is not a handful of curving lanes with houses sprinkled along them — it is a subdivision:
    /// parallel streets at a fixed spacing, a collector road along one edge, cul-de-sacs branching
    /// off it, and every plot the same width with every house the same distance back from the kerb.
    /// That regularity is exactly what makes it read as somewhere real on a map, and it is the one
    /// thing a random placement can never produce.
    ///
    /// So the data is the developer's brief and the builder does the surveying.
    /// </summary>
    public sealed class ResidentialBlock
    {
        public ResidentialBlock(string id, string districtId, float centreX, float centreZ,
            float width, float depth, float rotationDegrees, float streetSpacing, float lotWidth,
            float setback, int culDeSacs, bool grand)
        {
            Id = id;
            DistrictId = districtId;
            CentreX = centreX;
            CentreZ = centreZ;
            Width = width;
            Depth = depth;
            RotationDegrees = rotationDegrees;
            StreetSpacing = streetSpacing;
            LotWidth = lotWidth;
            Setback = setback;
            CulDeSacs = culDeSacs;
            Grand = grand;
        }

        public string Id { get; }
        public string DistrictId { get; }

        public float CentreX { get; }
        public float CentreZ { get; }

        /// <summary>Across the streets. The collector road runs along this edge.</summary>
        public float Width { get; }

        /// <summary>Along the streets.</summary>
        public float Depth { get; }

        /// <summary>
        /// Which way the streets run.
        ///
        /// Every block is turned a little differently, because a suburb where every subdivision
        /// faces the same way reads as one enormous estate rather than as a town that grew.
        /// </summary>
        public float RotationDegrees { get; }

        /// <summary>
        /// Metres between parallel streets.
        ///
        /// Twice the lot depth plus the road, so plots back onto each other the way they do
        /// everywhere: back gardens meeting down the middle of the block, never a street behind
        /// somebody's kitchen.
        /// </summary>
        public float StreetSpacing { get; }

        /// <summary>Frontage per plot. Twenty-two metres is an ordinary American lot.</summary>
        public float LotWidth { get; }

        /// <summary>Metres from the kerb to the front of the house. This is the driveway length.</summary>
        public float Setback { get; }

        /// <summary>Dead ends with a turning bulb, branching off the collector.</summary>
        public int CulDeSacs { get; }

        /// <summary>Villas rather than houses: bigger, further back, more trees.</summary>
        public bool Grand { get; }
    }

    /// <summary>
    /// A rectangle of city on a street grid: downtown, the campus, the civic blocks.
    ///
    /// The same idea as a residential block and for the same reason. A financial district is a grid
    /// with buildings filling the blocks between the streets, and generating it from a spacing means
    /// the streets and the buildings can never disagree about where the blocks are.
    /// </summary>
    public sealed class GridBlock
    {
        public GridBlock(string id, string districtId, float centreX, float centreZ,
            float width, float depth, float rotationDegrees, float blockSize,
            float lowBuilding, float highBuilding, bool skyline)
        {
            Id = id;
            DistrictId = districtId;
            CentreX = centreX;
            CentreZ = centreZ;
            Width = width;
            Depth = depth;
            RotationDegrees = rotationDegrees;
            BlockSize = blockSize;
            LowBuilding = lowBuilding;
            HighBuilding = highBuilding;
            Skyline = skyline;
        }

        public string Id { get; }
        public string DistrictId { get; }
        public float CentreX { get; }
        public float CentreZ { get; }
        public float Width { get; }
        public float Depth { get; }
        public float RotationDegrees { get; }

        /// <summary>Street to street. A hundred and twenty metres is a normal downtown block.</summary>
        public float BlockSize { get; }

        public float LowBuilding { get; }
        public float HighBuilding { get; }

        /// <summary>
        /// True when heights should fall off from the middle.
        ///
        /// It is what makes a cluster of boxes read as a skyline rather than as a bar chart, and it
        /// is wrong everywhere except the financial district.
        /// </summary>
        public bool Skyline { get; }
    }

    /// <summary>
    /// The shopping gallery and its car park, which is where the city holds indoor events.
    ///
    /// **The car park is the point, not the building.** The author asked for a gallery with a big
    /// parking lot for events; a mall with nowhere to put four thousand cars is a mall nobody drove
    /// to, and the lot is what an expo actually needs — it is where the marquees go.
    /// </summary>
    public sealed class MallSite
    {
        public MallSite(string id, string displayName, float centreX, float centreZ,
            float buildingWidth, float buildingDepth, float buildingHeight,
            float lotWidth, float lotDepth, float rotationDegrees)
        {
            Id = id;
            DisplayName = displayName;
            CentreX = centreX;
            CentreZ = centreZ;
            BuildingWidth = buildingWidth;
            BuildingDepth = buildingDepth;
            BuildingHeight = buildingHeight;
            LotWidth = lotWidth;
            LotDepth = lotDepth;
            RotationDegrees = rotationDegrees;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public float CentreX { get; }
        public float CentreZ { get; }
        public float BuildingWidth { get; }
        public float BuildingDepth { get; }
        public float BuildingHeight { get; }
        public float LotWidth { get; }
        public float LotDepth { get; }
        public float RotationDegrees { get; }
    }

    /// <summary>Green space: a lake, an event lawn, paths and a lot of trees.</summary>
    public sealed class ParkSite
    {
        public ParkSite(string id, string displayName, float centreX, float centreZ, float radius,
            bool hasLake, bool hasEventGround, int trees)
        {
            Id = id;
            DisplayName = displayName;
            CentreX = centreX;
            CentreZ = centreZ;
            Radius = radius;
            HasLake = hasLake;
            HasEventGround = hasEventGround;
            Trees = trees;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public float CentreX { get; }
        public float CentreZ { get; }
        public float Radius { get; }
        public bool HasLake { get; }
        public bool HasEventGround { get; }
        public int Trees { get; }
    }

    /// <summary>
    /// The built parts of Bayview: subdivisions, grids, the gallery and the parks.
    ///
    /// Separate from <see cref="CityLayout"/> because that file is the land — where the coast is,
    /// where the roads run — and this is what was put on it. The two change for different reasons
    /// and at different times.
    /// </summary>
    public static class CityBlocks
    {
        /// <summary>
        /// Six subdivisions across the two residential districts.
        ///
        /// Greendale gets the big lots on the hillside; Riverdale gets four tighter ones, which is
        /// what the author asked for and also what actually happens: the cheap suburb is denser.
        /// Rotations are all different so the town looks grown rather than stamped.
        /// </summary>
        public static IReadOnlyList<ResidentialBlock> Residential { get; } = new List<ResidentialBlock>
        {
            new("greendale_north", "greendale", 300f, 1720f, 330f, 300f, 12f,
                86f, 27f, 13f, 2, true),

            new("greendale_west", "greendale", 190f, 1470f, 260f, 280f, -18f,
                86f, 26f, 12f, 1, true),

            new("greendale_east", "greendale", 500f, 1560f, 240f, 260f, 34f,
                84f, 25f, 12f, 1, true),

            new("riverdale_core", "riverdale", 1600f, 400f, 340f, 300f, -8f,
                74f, 21f, 9f, 2, false),

            new("riverdale_north", "riverdale", 1690f, 620f, 260f, 220f, 22f,
                72f, 20f, 9f, 1, false),

            new("riverdale_west", "riverdale", 1400f, 330f, 230f, 240f, -32f,
                72f, 20f, 9f, 1, false)
        };

        /// <summary>The parts of the city that are on a grid rather than on a lane.</summary>
        public static IReadOnlyList<GridBlock> Grids { get; } = new List<GridBlock>
        {
            new("downtown_core", "downtown", 990f, 1040f, 460f, 420f, 6f,
                126f, 42f, 172f, true),

            new("media_core", "media", 290f, 830f, 330f, 300f, -12f,
                112f, 16f, 42f, false),

            new("innovation_core", "innovation", 1570f, 1440f, 350f, 320f, 18f,
                134f, 18f, 48f, false),

            new("civic_core", "civic", 1075f, 660f, 250f, 230f, -6f,
                118f, 14f, 32f, false),

            new("port_core", "port", 560f, 350f, 320f, 280f, 8f,
                128f, 9f, 20f, false)
        };

        /// <summary>
        /// The gallery, placed between downtown and the park.
        ///
        /// **That corner is the best address on the map and the reasoning is worth writing down.**
        /// It is inside the walk from the financial district, it is on the spine road so it can be
        /// driven to from either suburb, and it backs onto Bayview Park — so a convention in the
        /// hall and an open-air event on the lawn are the same weekend rather than two.
        /// </summary>
        public static IReadOnlyList<MallSite> Malls { get; } = new List<MallSite>
        {
            new("bayview_gallery", "Bayview Gallery", 790f, 1300f,
                190f, 120f, 26f, 250f, 170f, 8f)
        };

        /// <summary>
        /// Two parks, which is what the author asked for and what a city this size would have.
        ///
        /// The big one by the bay for the crowds, and a smaller square down between the civic
        /// buildings and the river — the kind that gets used for a press conference rather than a
        /// festival.
        /// </summary>
        public static IReadOnlyList<ParkSite> Parks { get; } = new List<ParkSite>
        {
            new("bayview_park", "Bayview Park", 990f, 1450f, 215f, true, true, 130),
            new("civic_gardens", "Civic Gardens", 930f, 690f, 120f, false, true, 55)
        };
    }
}
