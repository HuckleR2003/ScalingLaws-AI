using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Where the lights are on a drawn part, in the picture's own coordinates.
    ///
    /// **The art carries no lit indicators and it must not.** Heat, throttling and occupancy are
    /// simulation state, so a green LED baked into a texture is a lie the moment a cabinet
    /// overheats. Every picture in `Resources/Racks` is drawn dark and the game lights it.
    ///
    /// The numbers are normalised to the image, so they survive the art being re-exported at a
    /// different size, and they are measured from the drawings rather than assumed. That
    /// distinction cost this project two wrong guesses about where a pair of glasses sat.
    /// </summary>
    public readonly struct LightGrid
    {
        public LightGrid(int columns, int rows, float left, float right, float top, float bottom,
            float radius = 0f, bool hollow = false)
        {
            Hollow = hollow;
            Columns = columns;
            Rows = rows;
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
            Radius = radius;
        }

        public int Columns { get; }
        public int Rows { get; }

        public float Left { get; }
        public float Right { get; }
        public float Top { get; }
        public float Bottom { get; }

        /// <summary>Non-zero draws discs of this radius, as a share of the cell. Zero draws bars.</summary>
        public float Radius { get; }

        /// <summary>
        /// Draws the disc as a ring rather than filling it.
        ///
        /// A fan guard is a circle you can see through. Filling it put a solid blue ball over the
        /// hub, which reads as a light bulb rather than as a fan that is turning.
        /// </summary>
        public bool Hollow { get; }

        public bool IsEmpty => Columns <= 0 || Rows <= 0;
    }

    /// <summary>
    /// The pictures the server room is drawn from, and where each one's indicators sit.
    ///
    /// Every loader here returns null rather than throwing, and every caller draws a plain plate
    /// when it gets one. That is why the room worked before any of this art existed and why the
    /// next part can be added on its own.
    /// </summary>
    public static class RackArt
    {
        private static readonly Dictionary<string, Texture2D> Cache = new();

        /// <summary>
        /// The empty interior of each cabinet, normalised, measured off the drawings.
        ///
        /// The three vertical cabinets agree closely. The immersion tank does not and should not:
        /// it is a bath rather than a cupboard, so its parts lie in fluid across the lower half.
        /// </summary>
        public static Rect InteriorOf(ServerRack rack) => rack switch
        {
            ServerRack.OpenFrame => new Rect(0.190f, 0.080f, 0.620f, 0.828f),
            ServerRack.HighDensity => new Rect(0.205f, 0.072f, 0.590f, 0.872f),
            ServerRack.Immersion => new Rect(0.170f, 0.300f, 0.660f, 0.560f),
            _ => new Rect(0.196f, 0.075f, 0.608f, 0.880f)
        };

        public static Texture2D Cabinet(ServerRack rack) => Load(rack switch
        {
            ServerRack.OpenFrame => "Racks/rack_openframe",
            ServerRack.HighDensity => "Racks/rack_highdensity",
            ServerRack.Immersion => "Racks/rack_immersion",
            _ => "Racks/rack_enclosed"
        });

        /// <summary>
        /// Which of the four accelerator drawings a generation wears.
        ///
        /// **Four pictures for twenty two generations, on purpose.** The difference between an A100
        /// and an H100 at ninety six pixels tall is a sticker, and four eras is a difference
        /// anybody can see at a glance across a full cabinet.
        /// </summary>
        public static Texture2D Sled(int year)
        {
            if (year >= 2029)
            {
                return Load("Racks/sled_era4");
            }

            if (year >= 2026)
            {
                return Load("Racks/sled_era3");
            }

            return Load(year >= 2023 ? "Racks/sled_era2" : "Racks/sled_era1");
        }

        public static Texture2D Fan() => Load("Racks/part_fan");
        public static Texture2D Blank() => Load("Racks/part_blank");
        public static Texture2D Storage() => Load("Racks/shelf_storage");

        public static Texture2D Support(HardwareClass hardwareClass) => Load(hardwareClass switch
        {
            HardwareClass.Cpu => "Racks/node_cpu",
            HardwareClass.Memory => "Racks/node_memory",
            _ => "Racks/node_fabric"
        });

        /// <summary>
        /// The three status apertures at the left end of every accelerator sled.
        ///
        /// Measured from the drawings, which put them in the same place on all four eras.
        /// </summary>
        public static LightGrid SledLights =>
            new(3, 1, 0.088f, 0.165f, 0.135f, 0.225f, 0.36f);

        /// <summary>
        /// The switch's thirty two port cages, sixteen across and two down.
        ///
        /// This is the one the author asked for by name. A port lights when the fleet is using it,
        /// so a half-empty cabinet has a half-lit switch, and that is a fact about the company
        /// rather than a decoration.
        /// </summary>
        public static LightGrid FabricPorts =>
            new(16, 2, 0.070f, 0.962f, 0.190f, 0.812f);

        /// <summary>The twelve drive carriers on the storage shelf, four across and three down.</summary>
        public static LightGrid StorageBays =>
            new(4, 3, 0.045f, 0.955f, 0.060f, 0.940f);

        /// <summary>The two fan guards, lit as rings when the cabinet is working hard.</summary>
        public static LightGrid FanRings =>
            new(2, 1, 0.215f, 0.785f, 0.150f, 0.850f, 0.40f, hollow: true);

        private static Texture2D Load(string path)
        {
            if (Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var texture = Resources.Load<Texture2D>(path);
            Cache[path] = texture;

            return texture;
        }
    }
}
