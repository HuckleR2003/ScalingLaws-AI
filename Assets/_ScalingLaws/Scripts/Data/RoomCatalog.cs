using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// Which room the player is looking at, and how to frame it.
    ///
    /// The office view is one camera pointed at one room, and the room changes when the lease does.
    /// This is the table that says which. It is here rather than in the view because the numbers are
    /// a design decision — how much of a floor the player sees, and where the eye lands — and design
    /// decisions live in Data where a test can read them without a scene.
    ///
    /// Floats rather than Vector3, because Data never imports UnityEngine.
    /// </summary>
    public readonly struct RoomView
    {
        public RoomView(string resourcePath, float cameraSize, float focusX, float focusZ,
            int fixedDesks, float decorX, float decorZ, float decorWidth, float decorDepth)
        {
            ResourcePath = resourcePath;
            CameraSize = cameraSize;
            FocusX = focusX;
            FocusZ = focusZ;
            FixedDesks = fixedDesks;
            DecorX = decorX;
            DecorZ = decorZ;
            DecorWidth = decorWidth;
            DecorDepth = decorDepth;
        }

        /// <summary>
        /// Path under Resources, or null for the garage.
        ///
        /// The garage is null because it is baked into the game scene rather than loaded: it is the
        /// room the campaign starts in, and a first frame that has to wait on a Resources load is a
        /// first frame with an empty office in it.
        /// </summary>
        public string ResourcePath { get; }

        /// <summary>Half the height the orthographic camera sees, in metres.</summary>
        public float CameraSize { get; }

        /// <summary>Where the camera looks, on the floor.</summary>
        public float FocusX { get; }
        public float FocusZ { get; }

        /// <summary>
        /// Desks the lease comes with, before anything the player buys.
        ///
        /// Kept alongside the room because it is a property of the geometry: it is how many desks the
        /// builder actually put on that floor, and a number here that disagreed with the room would
        /// seat people at desks that are not there.
        /// </summary>
        public int FixedDesks { get; }

        /// <summary>
        /// The patch of floor furniture may stand on.
        ///
        /// **Stated rather than derived from the room's size.** Most of a floor is already occupied
        /// by desks, a meeting room and a kitchen, and a placement rule that only knew the outer
        /// walls would put a sofa on top of a workstation. This is the open ground in front of the
        /// desks, which is also the part of the room nearest the camera, so anything bought is
        /// somewhere the player can actually see it.
        /// </summary>
        public float DecorX { get; }

        /// <inheritdoc cref="DecorX"/>
        public float DecorZ { get; }

        /// <inheritdoc cref="DecorX"/>
        public float DecorWidth { get; }

        /// <inheritdoc cref="DecorX"/>
        public float DecorDepth { get; }

        /// <summary>
        /// Whether anything can be bought for this room at all.
        ///
        /// **False for the garage, and that is a design decision rather than a gap.** The starter
        /// room is a sofa, a workbench, a rack and a staircase in twelve metres by nine; the render
        /// showed every candidate spot standing a coffee bar through the workbench. A shop that can
        /// only place things inside the existing furniture is worse than no shop, and the first
        /// rented floor is a few months away. Furnishing is one of the things moving out buys you.
        /// </summary>
        public bool AllowsFurniture => DecorWidth > 0f && DecorDepth > 0f;

        /// <summary>True when the room has to be loaded rather than being already in the scene.</summary>
        public bool IsLoaded => !string.IsNullOrEmpty(ResourcePath);
    }

    /// <summary>
    /// The five tiers and the three rooms that stand in for them.
    ///
    /// **Campus and MultiSite reuse the big floor.** Those two tiers unlock late and have no art yet;
    /// pointing them at a room that exists is honest about that, and a player who reaches them sees a
    /// working office rather than an empty frame. When their own rooms are built this table is the
    /// only thing that changes.
    /// </summary>
    public static class RoomCatalog
    {
        /// <summary>What the camera is pointed at in the game scene, and must stay pointed at.</summary>
        public const float GarageCameraSize = 7.5f;

        private static readonly Dictionary<OfficeTier, RoomView> Rooms = new()
        {
            [OfficeTier.Garage] =
                new RoomView(null, GarageCameraSize, 6.0f, 4.5f, 4, 0f, 0f, 0f, 0f),

            // The zones below start where the back row of desks ends and stop short of the front
            // wall. They were checked against the rendered frames rather than worked out on paper.
            [OfficeTier.Loft] =
                new RoomView("Rooms/SmallHub", 7.0f, 8.0f, 5.5f, 10, 1.6f, 6.9f, 8.0f, 3.6f),

            [OfficeTier.Floor] =
                new RoomView("Rooms/BigHub", 9.0f, 11.0f, 7.0f, 20, 2.2f, 8.8f, 11.0f, 4.4f),

            [OfficeTier.Campus] =
                new RoomView("Rooms/BigHub", 9.0f, 11.0f, 7.0f, 20, 2.2f, 8.8f, 11.0f, 4.4f),

            [OfficeTier.MultiSite] =
                new RoomView("Rooms/BigHub", 9.0f, 11.0f, 7.0f, 20, 2.2f, 8.8f, 11.0f, 4.4f)
        };

        public static RoomView For(OfficeTier tier) =>
            Rooms.TryGetValue(tier, out var room) ? room : Rooms[OfficeTier.Garage];

        /// <summary>Every tier has a room. A test asserts it, so a new tier cannot ship without one.</summary>
        public static IEnumerable<OfficeTier> Tiers => Rooms.Keys;
    }
}
