using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A waypoint that is a seat: whoever arrives here turns to face the way it faces.
    ///
    /// **A component rather than a rotation that is not identity**, which was the first attempt and
    /// is wrong here for a specific reason: the chair in both hubs faces +z, which *is* identity, so
    /// "has somebody set a rotation on this" and "is this rotated" are not the same question and the
    /// second one answers no on the case that matters.
    ///
    /// Without it the founder sat down facing whichever direction they last walked in from. Coming
    /// to the boss corner along the front aisle, that is sideways to their own monitor.
    ///
    /// Nothing on it. It is a fact about a marker, and the marker's own transform carries the
    /// direction.
    /// </summary>
    public sealed class SeatFacing : MonoBehaviour
    {
    }
}
