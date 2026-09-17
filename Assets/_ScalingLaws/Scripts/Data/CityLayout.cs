using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// A point on the map, in metres from the terrain's south-west corner.
    ///
    /// Its own type rather than Vector2 because Data never imports UnityEngine, and every builder,
    /// screen and test in this project reads the city from here.
    /// </summary>
    public readonly struct MapPoint
    {
        public MapPoint(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float X { get; }
        public float Z { get; }
    }

    /// <summary>
    /// The eight colours the map is read in.
    ///
    /// **Eight, not fifteen.** The author's note is the design rule: fifteen colours after a few
    /// hours looks like Google Maps on LSD. Eight gives every decision a family and is few enough to
    /// learn without a legend. They are also the filter set — normally barely visible, and picking
    /// one dims everything else.
    /// </summary>
    public enum MapCategory
    {
        /// <summary>Cyan. Servers, accelerators, data centres.</summary>
        Compute = 0,

        /// <summary>Blue. Offices, headquarters, property.</summary>
        Business = 1,

        /// <summary>Violet. Universities, laboratories, R&amp;D.</summary>
        Research = 2,

        /// <summary>Amber. Fairs, conferences, exhibitions.</summary>
        Events = 3,

        /// <summary>Magenta. Radio, television, creators, advertising.</summary>
        Media = 4,

        /// <summary>Green. Power stations, grid, infrastructure.</summary>
        Energy = 5,

        /// <summary>Gold. Banks, investors, the exchange.</summary>
        Finance = 6,

        /// <summary>Red. Regulators, audits, incidents.</summary>
        Regulation = 7
    }

    /// <summary>
    /// One district: where it is on the terrain and what it is for.
    ///
    /// Metres from the terrain's south-west corner, which is where every builder in this project
    /// puts its origin.
    /// </summary>
    public sealed class DistrictDefinition
    {
        public DistrictDefinition(string id, string displayName, MapCategory category,
            float centreX, float centreZ, float radius, float groundHeight, string blurb,
            bool levelsGround = true)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            CentreX = centreX;
            CentreZ = centreZ;
            Radius = radius;
            GroundHeight = groundHeight;
            Blurb = blurb;
            LevelsGround = levelsGround;
        }

        /// <summary>
        /// False for a district laid out on terraces of its own (<c>CityBlocks.Terraces</c>) rather
        /// than on one round pad: its radius then only says how far it reaches.
        /// </summary>
        public bool LevelsGround { get; }

        public string Id { get; }
        public string DisplayName { get; }

        /// <summary>What the district is mostly about. Decides its colour on the map.</summary>
        public MapCategory Category { get; }

        public float CentreX { get; }
        public float CentreZ { get; }

        /// <summary>How far it reaches, and the radius the terrain is levelled over.</summary>
        public float Radius { get; }

        /// <summary>
        /// The height its ground is levelled to, in metres.
        ///
        /// **Districts are flattened on purpose.** Every building generator here assumes a flat
        /// floor, so sloped ground under a district is a building that floats later.
        /// </summary>
        public float GroundHeight { get; }

        public string Blurb { get; }
    }

    /// <summary>What a road is for, which decides how wide it is and how it is surfaced.</summary>
    public enum RoadClass
    {
        /// <summary>The ring and the arteries between districts. Four lanes, banked, fast.</summary>
        Highway = 0,

        /// <summary>Inside a district. Two lanes.</summary>
        Street = 1,

        /// <summary>Suburban loop. Narrow, curved, houses either side.</summary>
        Lane = 2
    }

    /// <summary>
    /// One run of road as a list of points the builder smooths through.
    ///
    /// Polylines rather than splines in the data, because a polyline is readable in a diff and the
    /// builder is the thing that needs the curve. Four points is usually enough to describe a road
    /// that reads as designed rather than drawn with a ruler.
    /// </summary>
    public sealed class RoadRun
    {
        public RoadRun(string id, RoadClass roadClass, params MapPoint[] points)
        {
            Id = id;
            Class = roadClass;
            Points = points;
        }

        public string Id { get; }
        public RoadClass Class { get; }
        public IReadOnlyList<MapPoint> Points { get; }

        /// <summary>Metres across, kerb to kerb.</summary>
        public float Width => Class switch
        {
            RoadClass.Highway => 26f,
            RoadClass.Street => 16f,
            _ => 11f
        };
    }

    /// <summary>
    /// A crossing. Two ends, a deck height and how many piers hold it up.
    ///
    /// **Bridges are stated rather than derived from where a road meets water.** Working out a span
    /// from a road and a coastline is a geometry problem that gets it wrong at every river mouth;
    /// naming the two ends is one line and always right.
    /// </summary>
    public sealed class BridgeSpan
    {
        public BridgeSpan(string id, MapPoint from, MapPoint to, float deckHeight, int piers,
            float width)
        {
            Id = id;
            From = from;
            To = to;
            DeckHeight = deckHeight;
            Piers = piers;
            Width = width;
        }

        public string Id { get; }
        public MapPoint From { get; }
        public MapPoint To { get; }

        /// <summary>Height of the road surface above sea level, in metres.</summary>
        public float DeckHeight { get; }

        public int Piers { get; }
        public float Width { get; }
    }

    /// <summary>
    /// The shape of the water, as centrelines with a width at each point.
    ///
    /// A coastline built from straight segments reads as a canal, which is exactly what the first
    /// pass of this terrain produced. Giving each control point its own half-width lets the bay open
    /// out into a basin and the river narrow as it climbs, which is what makes it look like water
    /// rather than like a road that happens to be blue.
    /// </summary>
    public sealed class WaterRun
    {
        public WaterRun(string id, params (MapPoint At, float HalfWidth, float Depth)[] points)
            : this(id, 1f, points)
        {
        }

        public WaterRun(string id, float wobble, params (MapPoint At, float HalfWidth, float Depth)[] points)
        {
            Id = id;
            Wobble = wobble;
            Points = points;
        }

        public string Id { get; }

        /// <summary>How much the noise pushes the shore about: one for a natural coast, less for a built one.</summary>
        public float Wobble { get; }
        public IReadOnlyList<(MapPoint At, float HalfWidth, float Depth)> Points { get; }
    }

    /// <summary>
    /// Everything about where Bayview is, in one file.
    ///
    /// **This is the second layout, drawn against the author's reference rather than invented.** The
    /// first was eight circles on a square and the render showed exactly that. The arrangement here
    /// is the one on the reference: hills ringing the north and east, a bay opening from the north
    /// between the suburbs and the university, the financial towers in the middle, the media strip
    /// on the west coast, and the cheaper housing away in the south east where the founder lives.
    /// </summary>
    public static class CityLayout
    {
        /// <summary>Side of the terrain in metres. Argued in Docs/CITY_MAP_PLAN.md.</summary>
        public const float Size = 2048f;

        /// <summary>Vertical range, in metres. The ridge on the reference is about this tall.</summary>
        public const float Height = 400f;

        /// <summary>Where the sea sits. Everything below this is under water.</summary>
        public const float SeaLevel = 40f;

        /// <summary>Heightmap samples per side. One every two metres at this size.</summary>
        public const int HeightmapResolution = 1025;

        /// <summary>Splatmap samples per side. Half the heightmap; roads are wider than two metres.</summary>
        public const int SplatResolution = 512;

        /// <summary>
        /// The ten districts: eight placed to the reference, River Works grown between two of them, and
        /// Silicon Valley on the ground added south of the port.
        ///
        /// Heights step down towards the water on purpose: the suburbs sit up on the shoulder of
        /// the hills, downtown is on the flat, and the port is barely above the sea.
        /// </summary>
        public static IReadOnlyList<DistrictDefinition> Districts { get; } = new List<DistrictDefinition>
        {
            // Top left on the reference: winding lanes, big gardens, expensive views over the bay.
            new("greendale", "Greendale Heights", MapCategory.Business,
                340f, 1620f, 270f, 96f,
                "Villas on the shoulder of the hills, long asphalt driveways and lawns nobody "
                + "walks on. Quiet, expensive, and half an hour from anything."),

            // Bottom right: the cheaper suburb, and where the founder's house is.
            new("riverdale", "Riverdale", MapCategory.Business,
                1610f, 400f, 250f, 58f,
                "Ordinary houses on ordinary streets, a garage each and a car in most of them. "
                + "The company starts in one of these."),

            // West coast strip.
            new("media", "Media District", MapCategory.Media,
                290f, 830f, 230f, 52f,
                "Radio, television, newsrooms and the studios the creators work out of, all of it "
                + "looking west over the water."),

            // Middle. The towers.
            new("downtown", "Downtown Financial", MapCategory.Finance,
                1000f, 1030f, 290f, 54f,
                "Banks, venture capital and the exchange, stacked as high as the ground will take. "
                + "Everything here either lends you money or takes a share of you."),

            // Between downtown and the bay.
            new("park", "Bayview Park", MapCategory.Events,
                945f, 1420f, 220f, 50f,
                "Lawns, a lake and the open ground the city holds its outdoor events on. The "
                + "cheapest place in Bayview to be seen."),

            // Upper right, across the bay from the suburbs.
            new("innovation", "Innovation District", MapCategory.Research,
                1570f, 1440f, 250f, 72f,
                "Universities, laboratories and the conference halls that go with them. Sponsor "
                + "the research or hire what walks out of it."),

            // South of downtown.
            new("civic", "Civic Center", MapCategory.Regulation,
                1075f, 660f, 190f, 52f,
                "City hall, the AI authority, the tax office and the compliance desk. Nothing here "
                + "makes you money and all of it can cost you everything."),

            // The flat land in the south west, and the port on the coast below the media strip.
            new("port", "Waterfront & Port", MapCategory.Compute,
                560f, 350f, 240f, 44f,
                "Hardware comes through here and so does everybody attending whatever is on at the "
                + "expo halls. Land is cheap and the power is already run in."),

            // The low flat bank between the civic centre and the port, on the city side of the river:
            // server halls that rent their cooling from the water running past them.
            new("riverworks", "River Works", MapCategory.Compute,
                747f, 639f, 150f, 46f,
                "Server halls on the river bank, two streets deep. The ones on the water pipe the river "
                + "through their cooling and charge for it; the ones behind are cheaper and run warmer."),

            // South of the port, on the ground the south tile added: the bay shore the frontier labs
            // keep their headquarters on. Not levelled as one pad — it is laid out on the terraces in
            // CityBlocks.Terraces, a waterfront and an upper bench against the hill.
            new("silicon", "Silicon Valley", MapCategory.Business,
                680f, -620f, 540f, 47f,
                "The most expensive street in Bayview. The frontier labs keep their headquarters on this "
                + "shore, and two trophy buildings between them are still waiting for a tenant who can "
                + "afford them.",
                levelsGround: false)
        };

        /// <summary>
        /// How far south the land runs: a second terrain tile, as large as the first, below it.
        ///
        /// The city was a single square with the port on its southern edge. Silicon Valley needed a
        /// waterfront of its own and there was none left inside that square that was not a suburb, a
        /// highway or the port, so the map grew south instead of being crammed. Everything keeps the
        /// same coordinates; the new ground simply has negative Z.
        /// </summary>
        public const float SouthEdge = -Size;

        /// <summary>
        /// The water, as three runs: the bay from the north, the river from the eastern hills, and the
        /// southern bay the river opens into below the port.
        ///
        /// The bay opens out as it goes north — a basin at the mouth, a channel where the bridges
        /// cross it — and the river narrows as it climbs, which is what stops both of them reading
        /// as canals.
        /// </summary>
        public static IReadOnlyList<WaterRun> Water { get; } = new List<WaterRun>
        {
            new("bay",
                (new MapPoint(760f, 2100f), 430f, 46f),
                (new MapPoint(830f, 1880f), 330f, 40f),
                (new MapPoint(900f, 1700f), 210f, 32f),
                (new MapPoint(1010f, 1560f), 120f, 24f),
                (new MapPoint(1180f, 1400f), 92f, 20f),
                (new MapPoint(1330f, 1180f), 78f, 16f),
                (new MapPoint(1400f, 980f), 70f, 14f)),

            new("river",
                (new MapPoint(1400f, 980f), 70f, 14f),
                (new MapPoint(1330f, 800f), 60f, 13f),
                (new MapPoint(1180f, 640f), 52f, 12f),
                (new MapPoint(980f, 520f), 48f, 12f),
                (new MapPoint(760f, 430f), 54f, 13f),
                (new MapPoint(520f, 330f), 70f, 15f),
                (new MapPoint(240f, 240f), 120f, 20f),
                (new MapPoint(-120f, 120f), 260f, 30f)),

            // The river mouth opening south into the bay Silicon Valley fronts. A quarter of the usual
            // wobble: this shore is built up with a promenade along it, and an engineered waterfront is
            // a long clean curve, not a coast of inlets.
            new("southbay", 0.25f,
                (new MapPoint(-120f, 120f), 260f, 30f),
                (new MapPoint(-110f, -300f), 480f, 34f),
                (new MapPoint(-70f, -700f), 490f, 36f),
                (new MapPoint(-90f, -1100f), 480f, 38f),
                (new MapPoint(-210f, -1600f), 520f, 42f),
                (new MapPoint(-420f, -2300f), 620f, 46f))
        };

        /// <summary>
        /// The road network. Highways between districts, streets inside them, lanes in the suburbs.
        ///
        /// Every run is drawn so that it reads as following the land rather than cutting across it,
        /// which on the reference is most of what makes the city look designed.
        /// </summary>
        public static IReadOnlyList<RoadRun> Roads { get; } = new List<RoadRun>
        {
            // The spine: suburbs, over the bay, past the park, into downtown, down to the civic
            // buildings and on to the far suburb.
            new("spine", RoadClass.Highway,
                new MapPoint(300f, 1780f), new MapPoint(520f, 1640f), new MapPoint(760f, 1520f),
                new MapPoint(950f, 1440f), new MapPoint(1000f, 1250f), new MapPoint(1010f, 1060f),
                new MapPoint(1060f, 830f), new MapPoint(1080f, 660f), new MapPoint(1240f, 540f),
                new MapPoint(1450f, 450f), new MapPoint(1620f, 410f)),

            // The west road: media strip down to the port.
            new("westroad", RoadClass.Highway,
                new MapPoint(300f, 1560f), new MapPoint(250f, 1200f), new MapPoint(280f, 900f),
                new MapPoint(360f, 640f), new MapPoint(520f, 430f), new MapPoint(700f, 360f)),

            // The east road: downtown out to the university across the bay's upper reach.
            new("eastroad", RoadClass.Highway,
                new MapPoint(1080f, 1120f), new MapPoint(1280f, 1240f), new MapPoint(1420f, 1360f),
                new MapPoint(1560f, 1440f), new MapPoint(1720f, 1500f)),

            // The southern link: port across to the civic centre and the river bridge.
            new("southroad", RoadClass.Highway,
                new MapPoint(560f, 400f), new MapPoint(760f, 500f), new MapPoint(940f, 600f),
                new MapPoint(1070f, 660f)),

            // Suburban loops. Two in Greendale, one in Riverdale, deliberately curved.
            new("greendale_loop", RoadClass.Lane,
                new MapPoint(230f, 1740f), new MapPoint(420f, 1760f), new MapPoint(520f, 1640f),
                new MapPoint(470f, 1500f), new MapPoint(300f, 1470f), new MapPoint(200f, 1560f),
                new MapPoint(230f, 1740f)),

            new("greendale_spur", RoadClass.Lane,
                new MapPoint(300f, 1470f), new MapPoint(360f, 1360f), new MapPoint(490f, 1330f)),

            new("riverdale_loop", RoadClass.Lane,
                new MapPoint(1500f, 300f), new MapPoint(1680f, 290f), new MapPoint(1760f, 400f),
                new MapPoint(1700f, 520f), new MapPoint(1530f, 520f), new MapPoint(1460f, 410f),
                new MapPoint(1500f, 300f)),

            // Downtown grid, kept short and straight because that is what a financial district is.
            new("downtown_a", RoadClass.Street,
                new MapPoint(830f, 900f), new MapPoint(830f, 1180f)),

            new("downtown_b", RoadClass.Street,
                new MapPoint(1160f, 900f), new MapPoint(1160f, 1180f)),

            new("downtown_c", RoadClass.Street,
                new MapPoint(790f, 1000f), new MapPoint(1210f, 1000f)),

            new("downtown_d", RoadClass.Street,
                new MapPoint(790f, 1130f), new MapPoint(1210f, 1130f)),

            // Park and media approaches.
            new("park_street", RoadClass.Street,
                new MapPoint(830f, 1370f), new MapPoint(1060f, 1470f)),

            new("media_street", RoadClass.Street,
                new MapPoint(200f, 930f), new MapPoint(390f, 860f), new MapPoint(400f, 730f)),

            // Silicon Valley. The boulevard carries the west road on across the port street and down
            // the bay shore, two hundred metres inland, so the campuses have the water in front of
            // them and the towers the boulevard. Four lanes because it is the district's one way in,
            // not because it is a highway: it is dressed as an avenue.
            new("valley_boulevard", RoadClass.Highway,
                new MapPoint(471f, 118f), new MapPoint(478f, 20f), new MapPoint(505f, -120f),
                new MapPoint(545f, -300f), new MapPoint(585f, -480f), new MapPoint(615f, -660f),
                new MapPoint(628f, -840f), new MapPoint(625f, -1000f), new MapPoint(605f, -1100f),
                new MapPoint(575f, -1200f)),

            // The bench street: leaves the boulevard at the north end, climbs onto the bench under the
            // hill, runs its length and comes back down to the boulevard at the south end.
            new("valley_bench", RoadClass.Street,
                new MapPoint(548f, -310f), new MapPoint(640f, -300f), new MapPoint(760f, -320f),
                new MapPoint(815f, -440f), new MapPoint(830f, -640f), new MapPoint(828f, -860f),
                new MapPoint(805f, -1060f), new MapPoint(720f, -1110f), new MapPoint(608f, -1105f)),

            // The two cross streets either side of the central plaza.
            new("valley_plaza_north", RoadClass.Street,
                new MapPoint(590f, -540f), new MapPoint(829f, -555f)),

            new("valley_plaza_south", RoadClass.Street,
                new MapPoint(622f, -790f), new MapPoint(830f, -795f)),

            // And one more below the canopy headquarters, so the southern half of the waterfront is
            // two blocks rather than one long one.
            new("valley_south_cross", RoadClass.Street,
                new MapPoint(627f, -915f), new MapPoint(827f, -915f)),

            // From the port street, south across the south bank and down to Silicon Valley's bench
            // street: the valley's second way in, and the south bank's way out.
            new("southbank_valley", RoadClass.Street,
                new MapPoint(960f, 189f), new MapPoint(952f, 60f), new MapPoint(925f, -80f),
                new MapPoint(875f, -220f), new MapPoint(774f, -350f)),

            // Valley Heights' way out, north to Southbank Hill. A second one west to the south bank road
            // was tried: its cutting left a slope too steep for the corner it had to meet.
            new("valley_heights_north_link", RoadClass.Street,
                new MapPoint(1150f, 15f), new MapPoint(1150f, -106f))
        };

        /// <summary>
        /// The crossings. Two over the bay, two over the river.
        ///
        /// Deck heights clear the water by enough that a boat reads as able to pass under, which is
        /// the detail that makes a bridge look like a bridge rather than a causeway.
        /// </summary>
        public static IReadOnlyList<BridgeSpan> Bridges { get; } = new List<BridgeSpan>
        {
            new("bay_north", new MapPoint(760f, 1520f), new MapPoint(1010f, 1420f), 74f, 3, 30f),
            new("bay_south", new MapPoint(1230f, 1200f), new MapPoint(1420f, 1120f), 66f, 2, 26f),
            new("river_civic", new MapPoint(1140f, 690f), new MapPoint(1300f, 590f), 60f, 2, 26f),
            new("river_port", new MapPoint(640f, 400f), new MapPoint(880f, 480f), 58f, 3, 26f)
        };

        /// <summary>Where the founder lives. Riverdale, and the scene puts the house on it.</summary>
        public static MapPoint FounderHome { get; } = new(1660f, 470f);
    }
}
