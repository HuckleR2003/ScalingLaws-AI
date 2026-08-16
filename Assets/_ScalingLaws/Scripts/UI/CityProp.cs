using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>What a placed object in the city is, so a real asset can be dropped onto it later.</summary>
    public enum CityPropKind
    {
        None = 0,

        /// <summary>An ordinary suburban house. Faces its street.</summary>
        House = 1,

        /// <summary>A larger house on a larger plot.</summary>
        Villa = 2,

        /// <summary>Attached to a house, at the end of the driveway.</summary>
        Garage = 3,

        /// <summary>Mid-rise block: offices, studios, laboratories.</summary>
        Block = 4,

        /// <summary>A downtown tower.</summary>
        Tower = 5,

        Tree = 6,
        StreetLamp = 7,

        /// <summary>One straight run of road surface.</summary>
        RoadSegment = 8,

        /// <summary>A pavement strip beside a road.</summary>
        Sidewalk = 9,

        /// <summary>The asphalt between a kerb and a garage.</summary>
        Driveway = 10,

        /// <summary>The shopping gallery itself.</summary>
        Mall = 11,

        /// <summary>One row of parking bays.</summary>
        ParkingRow = 12,

        BridgeDeck = 13,
        BridgePier = 14,

        /// <summary>The founder's house. Exactly one of these exists.</summary>
        FounderHome = 15,

        /// <summary>A bench, a bin, a sign: the small things that make a street look inhabited.</summary>
        StreetFurniture = 16
    }

    /// <summary>
    /// A socket where a real asset goes.
    ///
    /// **This is the answer to "can you use the assets I find".** Every box the city builder places
    /// carries one of these, recording what it is meant to be and how big the space is. A grey box
    /// is not a placeholder in the sense of something to be deleted and redone — it is a surveyed
    /// position with a footprint, a facing and a name, and swapping it for a model is a transform
    /// copy rather than a redesign.
    ///
    /// <see cref="Footprint"/> matters as much as the position. An asset dropped in without checking
    /// it is the size of the hole is how a suburb ends up with houses overlapping their own
    /// driveways, so the swapper scales to fit and says so when the fit was bad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityProp : MonoBehaviour
    {
        [SerializeField] private CityPropKind kind = CityPropKind.None;

        [Tooltip("Metres. The space this prop was surveyed to fill: width, height, depth.")]
        [SerializeField] private Vector3 footprint = Vector3.one;

        [Tooltip("Which district it stands in, for filtering and for the map screen.")]
        [SerializeField] private string district = string.Empty;

        [Tooltip("A number the builder rolled for this prop. Lets a swapper vary models per house.")]
        [SerializeField] private int variant;

        public CityPropKind Kind => kind;
        public Vector3 Footprint => footprint;
        public string District => district;
        public int Variant => variant;

        /// <summary>Stamps a freshly placed box. Called by the builder and by nothing else.</summary>
        public void Describe(CityPropKind propKind, Vector3 size, string districtId, int rolled)
        {
            kind = propKind;
            footprint = size;
            district = districtId ?? string.Empty;
            variant = rolled;
        }
    }
}
