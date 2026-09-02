namespace ScalingLaws.Data
{
    /// <summary>
    /// The shape of the basement floor, in metres, as data.
    ///
    /// **Three things have to agree about where a square is and none of them can see the others.**
    /// The editor builder writes a marker per square, the runtime stage stands a cabinet on each,
    /// and the interface projects the same points through the camera to work out which square the
    /// cursor is over. Three copies of the arithmetic is three chances for a rack to be drawn one
    /// square away from the one the player clicked, and that is a fault nothing would report: the
    /// simulation would be right, the picture would be wrong, and every test would pass.
    ///
    /// So the geometry lives here, in `Data/`, next to <see cref="RoomCatalog"/> and for the same
    /// stated reason: it is a design decision, and a design decision belongs where a test can read
    /// it without loading a scene. Floats rather than Vector3, because `Data/` never imports
    /// UnityEngine.
    /// </summary>
    public static class BasementFloor
    {
        /// <summary>
        /// The grid, and it is the only place these two numbers are written.
        ///
        /// Four by four rather than the hall's own six by six default. It is a basement, not a
        /// hall, and the small floor is what makes moving into a real room mean something.
        /// </summary>
        public const int Columns = 4;

        /// <inheritdoc cref="Columns"/>
        public const int Rows = 4;

        /// <summary>
        /// How wide one square is.
        ///
        /// **Generous, and not the footprint of a real cabinet.** A rack is about 0.6 by 1.1 metres
        /// and a floor drawn to that scale is sixteen dark slabs touching each other, which is
        /// unreadable at the distance this camera sits at. The square is the space a cabinet plus
        /// the room to open its door and walk behind it occupies, which is also what a real room
        /// allocates.
        /// </summary>
        public const float SquareSize = 1.6f;

        /// <summary>The gap between squares. What the player reads as a walkway, and as a grid.</summary>
        public const float SquareGap = 0.24f;

        /// <summary>Floor kept clear around the grid, so nothing is jammed against a wall.</summary>
        public const float Margin = 1.5f;

        public const float Pitch = SquareSize + SquareGap;

        /// <summary>The whole room, walls included.</summary>
        public static float RoomWidth => Columns * Pitch - SquareGap + Margin * 2f;

        /// <inheritdoc cref="RoomWidth"/>
        public static float RoomDepth => Rows * Pitch - SquareGap + Margin * 2f;

        /// <summary>
        /// Low, and that is the point.
        ///
        /// A basement with the ceiling height of an office reads as an office with no windows. Two
        /// and a bit metres is what makes the first proper server hall feel like somewhere else.
        /// </summary>
        public const float CeilingHeight = 2.35f;

        /// <summary>Half the height the orthographic camera sees. Frames the floor with a little air.</summary>
        public const float CameraSize = 5.4f;

        public static int SquareCount => Columns * Rows;

        public static bool Contains(int column, int row) =>
            column >= 0 && row >= 0 && column < Columns && row < Rows;

        /// <summary>Where the middle of a square sits on the floor, measured from the room's corner.</summary>
        public static float CentreX(int column) => Margin + SquareSize / 2f + column * Pitch;

        /// <inheritdoc cref="CentreX"/>
        public static float CentreZ(int row) => Margin + SquareSize / 2f + row * Pitch;

        /// <summary>
        /// The name the builder gives a square's marker, and the name the stage looks it up by.
        ///
        /// One function rather than a format string written out twice. A marker named in the editor
        /// and looked up with a different pattern at runtime is a rack that never appears, silently,
        /// which is exactly how the founder's waypoints failed before a test read the names.
        /// </summary>
        public static string MarkerName(int column, int row) => $"Square_{column}_{row}";

        /// <summary>Where the camera looks: the middle of the grid, not the middle of the room.</summary>
        public static float FocusX => (CentreX(0) + CentreX(Columns - 1)) / 2f;

        /// <inheritdoc cref="FocusX"/>
        public static float FocusZ => (CentreZ(0) + CentreZ(Rows - 1)) / 2f;
    }
}
