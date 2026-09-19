using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Surface textures for the offices, drawn by code.
    ///
    /// **Generated rather than downloaded, for now and on purpose.** The repository is public and the
    /// Store packs this project uses cannot be redistributed, so a floor that depended on one would
    /// be a grey slab on every clone. These are the stand-ins the rooms are laid out against until
    /// the asset pass the author has planned replaces them with photographed materials; a material
    /// that names one of these can be repointed at a bought texture without touching the room.
    ///
    /// Every texture tiles: each pattern is built on a lattice that divides the image exactly, and
    /// the noise is sampled on a torus, so a floor of forty repeats shows no seam.
    ///
    /// Deterministic: the same call writes the same bytes, so rebuilding a room does not show up as
    /// a change to twelve binary files.
    /// </summary>
    public static class TextureForge
    {
        public const string Folder = "Assets/_ScalingLaws/Art/Generated";

        /// <summary>
        /// Herringbone parquet. <paramref name="baseColour"/> is the average board; each board gets
        /// its own shade and grain, and the joints are a thin dark line.
        /// </summary>
        public static Texture2D Herringbone(string name, Color baseColour, float spread, int size = 1024)
        {
            return Forge(name, size, (u, v) =>
            {
                // Eight cells across; a board is two cells long and one wide.
                const float cells = 8f;
                var x = u * cells;
                var y = v * cells;

                // Herringbone: each class of (x - y) mod 4 is one half of a board. 0 and 1 are the
                // two halves of a board running along x; 2 and 3 of one running along y. The
                // pattern repeats every four cells on both axes, and eight divides by four, so the
                // image tiles.
                var ix = Mathf.FloorToInt(x);
                var iy = Mathf.FloorToInt(y);
                var d = Mod(ix - iy, 4);

                int board;
                float inBoardLength;
                float inBoardWidth;

                switch (d)
                {
                    case 0:
                        board = Hash(Mod(ix, 8), Mod(iy, 8), 1);
                        inBoardLength = (x - ix) / 2f;
                        inBoardWidth = y - iy;
                        break;
                    case 1:
                        board = Hash(Mod(ix - 1, 8), Mod(iy, 8), 1);
                        inBoardLength = (x - (ix - 1)) / 2f;
                        inBoardWidth = y - iy;
                        break;
                    case 2:
                        board = Hash(Mod(ix, 8), Mod(iy - 1, 8), 2);
                        inBoardLength = (y - (iy - 1)) / 2f;
                        inBoardWidth = x - ix;
                        break;
                    default:
                        board = Hash(Mod(ix, 8), Mod(iy, 8), 2);
                        inBoardLength = (y - iy) / 2f;
                        inBoardWidth = x - ix;
                        break;
                }

                var shade = 1f + (Rand(board) - 0.5f) * spread;
                var grain = 0.92f + 0.08f * Mathf.Sin((inBoardWidth * 9f + Rand(board * 7) * 6f)
                    + Mathf.Sin(inBoardLength * 3.1f + board) * 0.8f);
                var joint = Mathf.Min(Edge(inBoardWidth, 0.035f), Edge(inBoardLength, 0.018f));

                var colour = baseColour * shade * grain;
                return Color.Lerp(colour * 0.45f, colour, joint);
            });
        }

        /// <summary>Square tiles with a grout line. The kitchen floor in both references.</summary>
        public static Texture2D Tiles(string name, Color baseColour, int perSide, float spread,
            int size = 512)
        {
            return Forge(name, size, (u, v) =>
            {
                var x = u * perSide;
                var y = v * perSide;
                var tile = Hash(Mathf.FloorToInt(x), Mathf.FloorToInt(y), 3);

                var shade = 1f + (Rand(tile) - 0.5f) * spread;
                var mottle = 0.95f + 0.05f * Noise(u * 12f, v * 12f, 12);
                var grout = Mathf.Min(Edge(x - Mathf.Floor(x), 0.025f), Edge(y - Mathf.Floor(y), 0.025f));

                var colour = baseColour * shade * mottle;
                return Color.Lerp(baseColour * 0.35f, colour, grout);
            });
        }

        /// <summary>Vertical boards, for the warm walls of the small office.</summary>
        public static Texture2D Planks(string name, Color baseColour, int boards, int size = 512)
        {
            return Forge(name, size, (u, v) =>
            {
                var x = u * boards;
                var board = Mathf.FloorToInt(x);
                var shade = 1f + (Rand(board * 13 + 5) - 0.5f) * 0.22f;
                var grain = 0.9f + 0.1f * Mathf.Sin(v * 40f + Rand(board) * 20f + Mathf.Sin(v * 7f) * 2f);
                var joint = Edge(x - board, 0.03f);

                var colour = baseColour * shade * grain;
                return Color.Lerp(colour * 0.4f, colour, joint);
            });
        }

        /// <summary>Soft stone: marble-ish veining, low contrast, for lobby floors and counters.</summary>
        public static Texture2D Stone(string name, Color baseColour, int size = 512)
        {
            return Forge(name, size, (u, v) =>
            {
                var n = Noise(u * 4f, v * 4f, 4) * 0.6f + Noise(u * 16f, v * 16f, 16) * 0.4f;
                var vein = Mathf.Abs(Mathf.Sin((u + n * 0.35f) * Mathf.PI * 6f));
                var line = Mathf.SmoothStep(0f, 0.08f, vein);
                var colour = baseColour * (0.9f + n * 0.2f);
                return Color.Lerp(colour * 0.78f, colour, line);
            });
        }

        /// <summary>
        /// A decorative 3D panel wall: rows of shallow domes, lit from above. The textured wall behind
        /// the television in the big office reference.
        /// </summary>
        public static Texture2D DomePanels(string name, Color baseColour, int perSide, int size = 512)
        {
            return Forge(name, size, (u, v) =>
            {
                var x = u * perSide;
                var y = v * perSide;
                var fx = x - Mathf.Floor(x) - 0.5f;
                var fy = y - Mathf.Floor(y) - 0.5f;
                var r = Mathf.Sqrt(fx * fx + fy * fy) * 2f;

                // Light from the top of the dome, shadow at its foot.
                var height = r < 0.9f ? Mathf.Sqrt(1f - r * r / 0.81f) : 0f;
                var slope = r < 0.9f ? -fy * 2.2f : 0f;
                var light = 0.72f + height * 0.18f + slope * 0.25f;

                return baseColour * Mathf.Clamp(light, 0.4f, 1.2f);
            });
        }

        // ---- plumbing -----------------------------------------------------------------------------

        private static Texture2D Forge(string name, int size, System.Func<float, float, Color> shade)
        {
            Directory.CreateDirectory(Folder);
            var path = $"{Folder}/{name}.png";

            var image = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var colour = shade((x + 0.5f) / size, (y + 0.5f) / size);
                    colour.a = 1f;
                    pixels[y * size + x] = colour;
                }
            }

            image.SetPixels(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.filterMode = FilterMode.Trilinear;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static int Mod(int value, int by) => ((value % by) + by) % by;

        private static int Hash(int a, int b, int salt)
        {
            unchecked
            {
                var h = a * 374761393 + b * 668265263 + salt * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                return h ^ (h >> 16);
            }
        }

        private static float Rand(int seed) => (Hash(seed, seed * 31, 7) & 0xFFFF) / 65535f;

        /// <summary>0 on a joint, 1 away from it, with a soft edge of <paramref name="width"/>.</summary>
        private static float Edge(float t, float width)
        {
            var distance = Mathf.Min(t, 1f - t);
            return Mathf.SmoothStep(0f, 1f, distance / width);
        }

        /// <summary>Value noise on a lattice that wraps at <paramref name="period"/>, so it tiles.</summary>
        private static float Noise(float x, float y, int period)
        {
            var ix = Mathf.FloorToInt(x);
            var iy = Mathf.FloorToInt(y);
            var fx = x - ix;
            var fy = y - iy;

            float Corner(int cx, int cy) => Rand(Hash(Mod(cx, period), Mod(cy, period), 11));

            var a = Mathf.Lerp(Corner(ix, iy), Corner(ix + 1, iy), Smooth(fx));
            var b = Mathf.Lerp(Corner(ix, iy + 1), Corner(ix + 1, iy + 1), Smooth(fx));
            return Mathf.Lerp(a, b, Smooth(fy));
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
