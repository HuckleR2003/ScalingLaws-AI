using System.Collections.Generic;
using System.IO;
using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// The map from above, drawn straight from the data rather than photographed by a camera.
    ///
    /// **Because the camera cannot be trusted in batch mode.** `CitySnapshot` renders through a real
    /// camera and Unity dies on `camera.Render` in this project; this reads terrain heights and
    /// renderer bounds and writes the pixels itself, so it works with `-nographics` and cannot crash
    /// the editor. It is a working drawing, not a picture of the game: heights as shading, water flat
    /// blue, and everything standing on the ground as its footprint, coloured by what it is.
    ///
    /// The grid is the point. Every 100 m there is a tick, every 500 m a full line, so a place seen
    /// here can be named in metres and handed to a builder.
    /// </summary>
    public static class CityPlanImage
    {
        private const string OutputFolder = "CityProof~";
        private const string ScenePath = "Assets/_ScalingLaws/Scenes/City.unity";

        /// <summary>Metres per pixel. Four keeps the whole map, both tiles, under 1100 px.</summary>
        private const float MetresPerPixel = 4f;

        private static readonly Color Water = new(0.14f, 0.28f, 0.44f);
        private static readonly Color Shore = new(0.80f, 0.76f, 0.58f);
        private static readonly Color Low = new(0.42f, 0.56f, 0.32f);
        private static readonly Color High = new(0.55f, 0.50f, 0.38f);
        private static readonly Color Peak = new(0.72f, 0.72f, 0.70f);
        private static readonly Color Road = new(0.62f, 0.63f, 0.66f);
        private static readonly Color House = new(0.85f, 0.62f, 0.42f);
        private static readonly Color Building = new(0.93f, 0.93f, 0.96f);
        private static readonly Color Tree = new(0.16f, 0.40f, 0.20f);
        private static readonly Color Site = new(1f, 0.35f, 0.75f);
        private static readonly Color Grid = new(0f, 0f, 0f);
        private static readonly Color GridStrong = new(1f, 1f, 1f);

        /// <summary>
        /// One corner of the map, close up: `-plan x:z:size` in metres, written at a metre a pixel.
        /// The whole-map drawing is four metres a pixel, where a road is one pixel wide and a bend in
        /// it is invisible.
        /// </summary>
        public static void Zoom()
        {
            var args = System.Environment.GetCommandLineArgs();
            var centre = new Vector2(200f, 1900f);
            var size = 500f;

            for (var index = 0; index < args.Length - 1; index++)
            {
                if (args[index] != "-plan")
                {
                    continue;
                }

                var parts = args[index + 1].Split(':');
                centre = new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
                size = float.Parse(parts[2]);
            }

            Draw(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size, 1f,
                $"plan_{centre.x:0}_{centre.y:0}.png");
        }

        [MenuItem("Scaling Laws/Plan of the city (no camera)")]
        public static void Run() =>
            Draw(0f, CityLayout.SouthEdge, CityLayout.Size, CityLayout.Size - CityLayout.SouthEdge,
                MetresPerPixel, "plan_z_danych.png");

        private static void Draw(float minX, float minZ, float metresWide, float metresTall,
            float metresPerPixel, string fileName)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"No scene at {ScenePath}.");
                return;
            }

            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var width = Mathf.RoundToInt(metresWide / metresPerPixel);
            var height = Mathf.RoundToInt(metresTall / metresPerPixel);

            var pixels = new Color[width * height];

            for (var py = 0; py < height; py++)
            {
                for (var px = 0; px < width; px++)
                {
                    var x = minX + px * metresPerPixel + metresPerPixel * 0.5f;
                    var z = minZ + py * metresPerPixel + metresPerPixel * 0.5f;

                    pixels[py * width + px] = Ground(terrains, x, z);
                }
            }

            // What stands on the ground, painted as footprints. Ordered so the small and interesting
            // things land on top of the big dull ones.
            var layers = new List<(string Name, Color Colour, int Order)>
            {
                ("road", Road, 0), ("tree", Tree, 1), ("house", House, 2),
                ("building", Building, 3), ("site", Site, 4)
            };

            foreach (var (name, colour, _) in layers)
            {
                foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (KindOf(renderer.transform) != name)
                    {
                        continue;
                    }

                    var bounds = renderer.bounds;

                    // The sea is one map-sized quad; painting it would cover the whole drawing.
                    if (bounds.size.x > 600f || bounds.size.z > 600f)
                    {
                        continue;
                    }

                    Fill(pixels, width, height, minX, minZ, metresPerPixel, bounds, colour);
                }
            }

            DrawGrid(pixels, width, height, minX, minZ, metresPerPixel);

            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.SetPixels(pixels);
            image.Apply();

            Directory.CreateDirectory(OutputFolder);
            var path = Path.Combine(OutputFolder, fileName);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);

            Debug.Log($"[Scaling Laws] Wrote {path}: {width}x{height} px, {metresPerPixel} m per pixel, " +
                      $"x from {minX} to {minX + metresWide}, z from {minZ} to {minZ + metresTall}.");
        }

        private static Color Ground(Terrain[] terrains, float x, float z)
        {
            var found = false;
            var metres = 0f;

            foreach (var terrain in terrains)
            {
                var origin = terrain.transform.position;
                var size = terrain.terrainData.size;

                if (x < origin.x || x > origin.x + size.x || z < origin.z || z > origin.z + size.z)
                {
                    continue;
                }

                metres = terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
                found = true;
                break;
            }

            if (!found)
            {
                return Color.black;
            }

            if (metres <= CityLayout.SeaLevel)
            {
                return Water;
            }

            if (metres <= CityLayout.SeaLevel + 2f)
            {
                return Shore;
            }

            if (metres < 80f)
            {
                return Color.Lerp(Low, High, Mathf.InverseLerp(CityLayout.SeaLevel + 2f, 80f, metres));
            }

            return Color.Lerp(High, Peak, Mathf.InverseLerp(80f, 200f, metres));
        }

        private static void Fill(Color[] pixels, int width, int height, float minX, float minZ,
            float metresPerPixel, Bounds bounds, Color colour)
        {
            var x0 = Mathf.FloorToInt((bounds.min.x - minX) / metresPerPixel);
            var x1 = Mathf.CeilToInt((bounds.max.x - minX) / metresPerPixel);
            var y0 = Mathf.FloorToInt((bounds.min.z - minZ) / metresPerPixel);
            var y1 = Mathf.CeilToInt((bounds.max.z - minZ) / metresPerPixel);

            for (var py = Mathf.Max(0, y0); py <= Mathf.Min(height - 1, y1); py++)
            {
                for (var px = Mathf.Max(0, x0); px <= Mathf.Min(width - 1, x1); px++)
                {
                    pixels[py * width + px] = colour;
                }
            }
        }

        private static void DrawGrid(Color[] pixels, int width, int height, float minX, float minZ,
            float metresPerPixel)
        {
            for (var py = 0; py < height; py++)
            {
                var z = minZ + py * metresPerPixel;

                for (var px = 0; px < width; px++)
                {
                    var x = minX + px * metresPerPixel;
                    var onFive = Mathf.Abs(x % 500f) < metresPerPixel || Mathf.Abs(z % 500f) < metresPerPixel;
                    var onOne = Mathf.Abs(x % 100f) < metresPerPixel || Mathf.Abs(z % 100f) < metresPerPixel;

                    if (onFive)
                    {
                        pixels[py * width + px] = Color.Lerp(pixels[py * width + px], GridStrong, 0.55f);
                    }
                    else if (onOne)
                    {
                        pixels[py * width + px] = Color.Lerp(pixels[py * width + px], Grid, 0.25f);
                    }
                }
            }
        }

        /// <summary>
        /// What a thing in the scene is, by the name of the branch it hangs from. The city is built by
        /// tools that name their groups (`Streets`, `Subdivision_*`, `Grid_*`, `MapSites`), so this
        /// reads the same names those tools wrote.
        /// </summary>
        private static string KindOf(Transform transform)
        {
            for (var node = transform; node != null; node = node.parent)
            {
                var name = node.name;

                if (name == "MapSites" || name.StartsWith("Pin_"))
                {
                    return "site";
                }

                if (name == "Streets" || name.StartsWith("Road") || name.Contains("Road") || name.Contains("Bridge"))
                {
                    return "road";
                }

                if (name.StartsWith("Subdivision_") || name.StartsWith("House") || name.StartsWith("Villa"))
                {
                    return "house";
                }

                if (name.Contains("Tree") || name.Contains("Woods") || name.StartsWith("Pine"))
                {
                    return "tree";
                }

                if (name == "Sea" || name.Contains("Terrain"))
                {
                    return "water";
                }
            }

            return "building";
        }
    }
}
