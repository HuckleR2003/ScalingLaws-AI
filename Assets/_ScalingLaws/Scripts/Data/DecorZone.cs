namespace ScalingLaws.Data
{
    /// <summary>
    /// The patch of floor a room leaves clear for furniture.
    ///
    /// A rectangle rather than the room's outer walls, because most of a floor is already taken: a
    /// placement rule that only knew where the walls were would stand a sofa on top of somebody's
    /// workstation. Each room states its own open ground, and each room states the part nearest the
    /// camera, so anything the player buys is somewhere they can see it.
    ///
    /// Floats and no UnityEngine, so the simulation can place furniture without a scene.
    /// </summary>
    public readonly struct DecorZone
    {
        public DecorZone(float x, float z, float width, float depth)
        {
            X = x;
            Z = z;
            Width = width;
            Depth = depth;
        }

        /// <summary>The corner nearest the room's origin.</summary>
        public float X { get; }

        /// <inheritdoc cref="X"/>
        public float Z { get; }

        public float Width { get; }
        public float Depth { get; }
    }
}
