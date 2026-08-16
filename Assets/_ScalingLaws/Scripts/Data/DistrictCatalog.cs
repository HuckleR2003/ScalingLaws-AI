using System;
using System.Collections.Generic;
using System.Linq;

namespace ScalingLaws.Data
{
    /// <summary>
    /// The eight colours the map is read in.
    ///
    /// **Eight, not fifteen.** The author's note is the whole design rule here: fifteen colours
    /// after a few hours looks like Google Maps on LSD. Eight is enough to give every decision on
    /// the map a family, and few enough that a player learns them without a legend.
    ///
    /// They are also the filter set. Normally the map is calm and these are barely visible; picking
    /// one dims everything else and lights its own points.
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
    /// Metres from the terrain's south-west corner, which is where every other builder in this
    /// project puts its origin. Kept in Data with no UnityEngine, so the terrain builder, the map
    /// screen and a test all read one description of the city rather than three.
    /// </summary>
    public sealed class DistrictDefinition
    {
        public DistrictDefinition(string id, string displayName, MapCategory category,
            float centreX, float centreZ, float radius, float groundHeight, string blurb)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            CentreX = centreX;
            CentreZ = centreZ;
            Radius = radius;
            GroundHeight = groundHeight;
            Blurb = blurb;
        }

        public string Id { get; }
        public string DisplayName { get; }

        /// <summary>What the district is mostly about. Decides its colour on the map.</summary>
        public MapCategory Category { get; }

        public float CentreX { get; }
        public float CentreZ { get; }

        /// <summary>How far the district reaches. Also the radius the terrain is flattened over.</summary>
        public float Radius { get; }

        /// <summary>
        /// The height its ground is levelled to, in metres.
        ///
        /// **Districts are flattened on purpose.** Buildings placed on a slope either float or sink,
        /// and every building in this game is a box builder that assumes a flat floor. The hills are
        /// scenery between the districts, not under them.
        /// </summary>
        public float GroundHeight { get; }

        public string Blurb { get; }
    }

    public static class DistrictCatalog
    {
        /// <summary>
        /// Side of the terrain in metres.
        ///
        /// Eight districts at four to six hundred metres across, plus the bay and the energy belt on
        /// the outskirts, comes to about eighteen hundred. Two thousand and forty-eight leaves a
        /// margin without leaving three quarters of the map as empty hills.
        /// </summary>
        public const float TerrainSize = 2048f;

        /// <summary>Vertical range. Enough for the hills on the reference without stretching texture.</summary>
        public const float TerrainHeight = 400f;

        /// <summary>
        /// Where the sea sits.
        ///
        /// Forty metres up rather than at zero, so there is room underneath for the bay floor and
        /// the harbour to be shaped rather than being a flat plane meeting a flat plane.
        /// </summary>
        public const float SeaLevel = 40f;

        /// <summary>Heightmap samples per side. At 2048 m this is one sample every two metres.</summary>
        public const int HeightmapResolution = 1025;

        private static readonly List<DistrictDefinition> Entries = new()
        {
            // West of the channel and clear of it. The distances here were checked against the
            // rendered plan, not worked out on paper: the first layout put both of these on top of
            // the bay and the map came out with a puddle instead of a harbour.
            new DistrictDefinition("greendale", "Greendale", MapCategory.Business,
                265f, 1420f, 250f, 62f,
                "Small houses, long driveways, garages and gardens. The company starts in one of "
                + "them, and the founder walks out to the car from here."),

            new DistrictDefinition("downtown", "Downtown Financial", MapCategory.Finance,
                1210f, 985f, 250f, 54f,
                "Banks, venture capital, the exchange and the expensive offices. Everything here "
                + "either lends you money or takes a share of you."),

            new DistrictDefinition("innovation", "Innovation District", MapCategory.Research,
                1560f, 1330f, 235f, 68f,
                "Universities, laboratories and the conference halls that go with them. Sponsor "
                + "the research or hire what comes out of it."),

            new DistrictDefinition("industrial", "Compute & Industrial", MapCategory.Compute,
                1600f, 1740f, 240f, 58f,
                "Data centres, substations and land big enough to build your own. Cheap power and "
                + "nobody to complain about the noise."),

            new DistrictDefinition("media", "Media District", MapCategory.Media,
                700f, 760f, 210f, 50f,
                "Radio, television, newsrooms, agencies and the studios the creators work out of. "
                + "Where a reputation is made and unmade."),

            new DistrictDefinition("waterfront", "Waterfront & Port", MapCategory.Events,
                1010f, 400f, 230f, 44f,
                "Hardware comes through here, and so does everybody attending whatever is on at "
                + "the expo halls. Expensive views."),

            new DistrictDefinition("energy", "Energy Belt", MapCategory.Energy,
                330f, 235f, 260f, 46f,
                "Flat land on the edge of the city, which is the only kind worth putting a solar "
                + "farm on. The river comes down to the sea past it."),

            new DistrictDefinition("civic", "Civic & Government", MapCategory.Regulation,
                1330f, 690f, 190f, 56f,
                "The AI authority, the tax office and the compliance desk. Nothing here makes you "
                + "money and all of it can cost you everything.")
        };

        public static IReadOnlyList<DistrictDefinition> All => Entries;

        public static DistrictDefinition Get(string id) =>
            Entries.FirstOrDefault(entry => entry.Id == id)
            ?? throw new ArgumentOutOfRangeException(nameof(id), id, "No such district.");

        public static bool TryGet(string id, out DistrictDefinition definition)
        {
            definition = Entries.FirstOrDefault(entry => entry.Id == id);
            return definition != null;
        }

        /// <summary>Where the founder lives, and where the campaign opens.</summary>
        public static DistrictDefinition Home => Get("greendale");

        /// <summary>
        /// The district a point falls inside, or null for the countryside between them.
        ///
        /// Nearest centre within its own radius rather than first match, so overlapping edges
        /// resolve to whichever district the point is actually more inside.
        /// </summary>
        public static DistrictDefinition At(float x, float z)
        {
            DistrictDefinition best = null;
            var bestDistance = float.MaxValue;

            foreach (var entry in Entries)
            {
                var dx = x - entry.CentreX;
                var dz = z - entry.CentreZ;
                var distance = MathF.Sqrt(dx * dx + dz * dz);

                if (distance <= entry.Radius && distance < bestDistance)
                {
                    best = entry;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
