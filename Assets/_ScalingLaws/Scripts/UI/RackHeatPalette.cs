using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The five cabinet states as five colours, decided once.
    ///
    /// **There were four copies of this mapping and they had already drifted.** The 3D room painted
    /// the strip down each cabinet, the corner banner painted its heat bar, the floor tile used to
    /// work its own ratio out inline, and the cabinet panel said in words something the other three
    /// were saying in colour. The banner's copy was the worst of them: it compared a ratio that had
    /// already been divided by <see cref="ServerRackCatalog.ThrottleFreeHeadroom"/> against
    /// thresholds written for the undivided one, so its amber began in a different place from the
    /// room's.
    ///
    /// That is SF-07 out of the author's own silent-failure catalogue, in his own game, for the
    /// second time in this room. A cabinet that is green on the floor beside a banner calling it
    /// critical is a disagreement with no owner and nothing that can fail.
    ///
    /// The stylesheet carries the same five for the 2D bands, and
    /// <c>ServerRoomChainTests.TheRoomPaintsOneColourPerState</c> reads them back out of the sheet
    /// and compares, because a comment asking two files to agree is not a guarantee.
    /// </summary>
    public static class RackHeatPalette
    {
        /// <summary>Nothing in it. White, because an empty cabinet is an invitation, not an alarm.</summary>
        public static readonly Color Idle = new(0.929f, 0.941f, 0.960f);

        /// <summary>Working, with room for half as many cards again.</summary>
        public static readonly Color Cool = new(0.470f, 0.741f, 0.929f);

        /// <summary>Working normally.</summary>
        public static readonly Color Comfortable = new(0.490f, 0.780f, 0.600f);

        /// <summary>Full output and no headroom left.</summary>
        public static readonly Color Warm = new(0.929f, 0.819f, 0.301f);

        /// <summary>Past what it can shed, and losing work that is being paid for.</summary>
        public static readonly Color Cooking = new(0.850f, 0.309f, 0.278f);

        /// <summary>The one lookup. Everything that draws a cabinet's condition comes through here.</summary>
        public static Color Of(ServerRackCatalog.RackHeat state) => state switch
        {
            ServerRackCatalog.RackHeat.Cool => Cool,
            ServerRackCatalog.RackHeat.Comfortable => Comfortable,
            ServerRackCatalog.RackHeat.Warm => Warm,
            ServerRackCatalog.RackHeat.Cooking => Cooking,
            _ => Idle
        };
    }
}
