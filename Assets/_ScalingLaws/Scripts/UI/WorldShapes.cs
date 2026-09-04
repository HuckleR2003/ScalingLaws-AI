using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The real world, read once from a baked asset.
    ///
    /// **Baked rather than parsed, and the reason is not performance.** The source is 725 kB of
    /// GeoJSON whose coordinates are arrays nested four deep, and `JsonUtility` has no way to express
    /// that at all. Writing a JSON parser into the runtime to read a file that has not changed since
    /// 2012 would be a parser to maintain and a second place for the projection to live. The bake is
    /// `Tools/bake_world_map.py`, it is one command, and the format is documented at the top of it.
    ///
    /// The data is public domain (Natural Earth, 1:110m), which is the whole reason this map can be
    /// in a public repository at all. Everything else considered was either too large for GitHub's
    /// hundred megabyte limit, needed a network the game deliberately does not have, or carried a
    /// licence that would follow the project around.
    ///
    /// Loaded once into static fields. There are 177 countries, they do not change during a
    /// campaign, and the creator can be opened and closed a dozen times in a session.
    /// </summary>
    public static class WorldShapes
    {
        private const int Magic = 0x504D4C53;
        private const int Format = 2;

        /// <summary>Where the asset lives, without its extension, the way `Resources.Load` wants it.</summary>
        public const string AssetPath = "Map/world";

        public sealed class Shape
        {
            public Shape(Country member, WorldRegion region, string name, Vector2 pin,
                Vector2[][] rings)
            {
                Member = member;
                Region = region;
                Name = name;
                Pin = pin;
                Rings = rings;

                var area = 0.0;

                foreach (var ring in rings)
                {
                    for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
                    {
                        area += ring[j].x * ring[i].y - ring[i].x * ring[j].y;
                    }
                }

                DrawnArea = (float)Math.Abs(area) * 0.5f;
            }

            /// <summary>Which of the sixteen this is, or `None` when it is only scenery.</summary>
            public Country Member { get; }

            /// <summary>
            /// Which of the three regions it sits in, for every country rather than only the
            /// sixteen. Selecting a region lights its whole neighbourhood, which is what makes the
            /// region read as a place rather than as four highlighted countries.
            /// </summary>
            public WorldRegion Region { get; }

            /// <summary>The English name from the source. Shown on hover; never translated.</summary>
            public string Name { get; }

            /// <summary>Area centroid of the largest ring, which is where a marker belongs.</summary>
            public Vector2 Pin { get; }

            /// <summary>Outer rings only, in map space: x from 0 to 1, y from 0 to <see cref="Aspect"/>.</summary>
            public Vector2[][] Rings { get; }

            /// <summary>
            /// How much of the map this country covers, in map space.
            ///
            /// Computed once at load rather than per draw, and used for one question: is this
            /// country big enough for a cursor to find without help. Not a real area - the
            /// projection is not equal-area and this is a sum over outer rings only - which is
            /// exactly right for a question about pixels on a screen.
            /// </summary>
            public float DrawnArea { get; }

            /// <summary>
            /// Is this point inside the country.
            ///
            /// Ray casting, per ring, counting crossings. Not a general point-in-polygon: these are
            /// outer rings with no holes, so a point inside any ring is inside the country, and two
            /// rings of the same country never overlap.
            /// </summary>
            public bool Contains(Vector2 point)
            {
                foreach (var ring in Rings)
                {
                    var inside = false;

                    for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
                    {
                        if (ring[i].y > point.y != ring[j].y > point.y
                            && point.x < (ring[j].x - ring[i].x) * (point.y - ring[i].y)
                            / (ring[j].y - ring[i].y) + ring[i].x)
                        {
                            inside = !inside;
                        }
                    }

                    if (inside)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private static Shape[] shapes;
        private static bool tried;

        /// <summary>
        /// How tall the map is when its width is one.
        ///
        /// Carried by the asset rather than assumed, because it is a property of the projection. The
        /// first bake normalised both axes by the longer one, which put a map nearly twice as wide as
        /// it is tall inside a square and drew the world across the middle third of an empty box.
        /// </summary>
        public static float Aspect { get; private set; } = 0.4234f;

        /// <summary>
        /// Every country, or an empty list.
        ///
        /// **Never throws and never returns null.** A missing asset draws no map, which is a screen
        /// with a hole in it and recoverable; an exception here happens inside the creator and is
        /// not. Same rule every loader in this project follows.
        /// </summary>
        public static IReadOnlyList<Shape> All
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    shapes = Load();
                }

                return shapes ?? Array.Empty<Shape>();
            }
        }

        /// <summary>The shape for one of the sixteen, or null.</summary>
        public static Shape For(Country country)
        {
            foreach (var shape in All)
            {
                if (shape.Member == country)
                {
                    return shape;
                }
            }

            return null;
        }

        /// <summary>Which country is under this point in map space, or `None`.</summary>
        public static Country PlayableAt(Vector2 point)
        {
            foreach (var shape in All)
            {
                if (shape.Member != Country.None && shape.Contains(point))
                {
                    return shape.Member;
                }
            }

            return Country.None;
        }

        /// <summary>
        /// Which region is under this point, or `None`.
        ///
        /// Separate from <see cref="PlayableAt"/> because the two answer different questions and the
        /// map asks both: clicking Spain picks Europe even though Spain is not one of the sixteen.
        /// </summary>
        public static WorldRegion RegionAt(Vector2 point)
        {
            foreach (var shape in All)
            {
                if (shape.Region != WorldRegion.None && shape.Contains(point))
                {
                    return shape.Region;
                }
            }

            return WorldRegion.None;
        }

        private static Shape[] Load()
        {
            var asset = Resources.Load<TextAsset>(AssetPath);

            if (asset == null)
            {
                Debug.LogWarning($"World map asset missing at Resources/{AssetPath}. "
                                 + "Run Tools/bake_world_map.py.");
                return null;
            }

            try
            {
                return Read(asset.bytes);
            }
            catch (Exception exception)
            {
                // A truncated or stale asset is a bad build, not a reason to take the creator down
                // with it. The message names the tool that fixes it.
                Debug.LogWarning($"World map asset could not be read ({exception.Message}). "
                                 + "Re-run Tools/bake_world_map.py.");
                return null;
            }
        }

        private static Shape[] Read(byte[] bytes)
        {
            var at = 0;

            var magic = ReadInt(bytes, ref at);
            var version = ReadInt(bytes, ref at);

            if (magic != Magic || version != Format)
            {
                throw new InvalidOperationException(
                    $"expected magic {Magic:X} version {Format}, found {magic:X} version {version}");
            }

            Aspect = ReadFloat(bytes, ref at);

            var count = ReadInt(bytes, ref at);
            var list = new Shape[count];

            for (var index = 0; index < count; index++)
            {
                var member = (Country)ReadShort(bytes, ref at);
                var region = (WorldRegion)bytes[at++];

                var nameLength = bytes[at++];
                var name = System.Text.Encoding.UTF8.GetString(bytes, at, nameLength);
                at += nameLength;

                var pin = new Vector2(ReadFloat(bytes, ref at), ReadFloat(bytes, ref at));

                var ringCount = ReadShort(bytes, ref at);
                var rings = new Vector2[ringCount][];

                for (var ring = 0; ring < ringCount; ring++)
                {
                    var points = ReadShort(bytes, ref at);
                    var ringPoints = new Vector2[points];

                    for (var point = 0; point < points; point++)
                    {
                        ringPoints[point] =
                            new Vector2(ReadFloat(bytes, ref at), ReadFloat(bytes, ref at));
                    }

                    rings[ring] = ringPoints;
                }

                // An unknown enum value is treated as scenery rather than trusted, the same rule the
                // save loader follows: a stale asset must not put a country in the game that the
                // catalog has never heard of.
                if (!Enum.IsDefined(typeof(Country), member))
                {
                    member = Country.None;
                }

                if (!Enum.IsDefined(typeof(WorldRegion), region))
                {
                    region = WorldRegion.None;
                }

                list[index] = new Shape(member, region, name, pin, rings);
            }

            return list;
        }

        private static int ReadInt(byte[] bytes, ref int at)
        {
            var value = BitConverter.ToInt32(bytes, at);
            at += 4;
            return value;
        }

        private static short ReadShort(byte[] bytes, ref int at)
        {
            var value = BitConverter.ToInt16(bytes, at);
            at += 2;
            return value;
        }

        private static float ReadFloat(byte[] bytes, ref int at)
        {
            var value = BitConverter.ToSingle(bytes, at);
            at += 4;
            return value;
        }
    }
}
