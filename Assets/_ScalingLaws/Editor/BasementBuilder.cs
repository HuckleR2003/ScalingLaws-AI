using System.IO;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Builds the basement under Emil's building: a grey box with a grid of squares on the floor.
    ///
    /// **Generated rather than hand-placed, for the reason the office room already records.** The
    /// grid has to line up with <see cref="BasementFloor"/> to the centimetre or a cabinet is drawn
    /// on a square the player did not click, and hand-placing sixteen markers is sixteen chances to
    /// be one square out in a way no test can see. The builder reads the same numbers the
    /// simulation and the interface read.
    ///
    /// Deliberately ugly. It is a basement: concrete, one strip light, pipes along the ceiling and
    /// a drain in the corner. The first real server hall has to feel like somewhere else, and it
    /// cannot if this one already looks like a datacentre.
    ///
    /// `Scaling Laws > Snapshot basement` renders it to `basement_preview.png`. **Use it.** Every
    /// layout fault in this project was found by looking, and this is the only way to look without
    /// opening the editor.
    /// </summary>
    public static class BasementBuilder
    {
        /// <summary>
        /// Under Resources, like the two office hubs and for the same reason: the room is loaded
        /// when the player opens it rather than baked into the generated game scene, which has a
        /// fixed prefab count the project checks before every commit.
        /// </summary>
        private const string PrefabFolder = "Assets/_ScalingLaws/Resources/Rooms";
        private const string MaterialFolder = "Assets/_ScalingLaws/Materials";
        private const string ScenesFolder = "Assets/_ScalingLaws/Scenes";

        private const string PrefabPath = PrefabFolder + "/Basement.prefab";
        private const string ScenePath = ScenesFolder + "/Basement.unity";

        /// <summary>The group the runtime stands cabinets in. Empty here, always.</summary>
        public const string RackGroup = "Racks";

        /// <summary>The group holding one empty marker per square.</summary>
        public const string SquareGroup = "Squares";

        private const float SlabThickness = 0.3f;
        private const float WallThickness = 0.25f;

        /// <summary>How far a floor tile stands proud of the slab. Enough to catch the light.</summary>
        private const float TileRise = 0.02f;

        [MenuItem("Scaling Laws/Build basement")]
        public static void Build()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(ScenesFolder);

            var palette = new BasementPalette();
            var root = new GameObject("Basement");

            BuildShell(root.transform, palette);
            BuildServices(root.transform, palette);
            BuildFloorGrid(root.transform, palette);

            // Filled at runtime by the stage. Anything the builder put here would be destroyed the
            // next time the room was dressed, which is the same contract the office Furniture group
            // has had since it was written.
            Group(root.transform, RackGroup);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            BuildViewingScene(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Scaling Laws] Basement built: {BasementFloor.Columns} x {BasementFloor.Rows} "
                + $"squares on a {BasementFloor.RoomWidth:0.0} x {BasementFloor.RoomDepth:0.0} m floor. "
                + $"Prefab at {PrefabPath}, scene at {ScenePath}.");
        }

        // ---- the room ---------------------------------------------------------------------------

        private static void BuildShell(Transform parent, BasementPalette palette)
        {
            var shell = Group(parent, "Shell");

            var width = BasementFloor.RoomWidth;
            var depth = BasementFloor.RoomDepth;
            var height = BasementFloor.CeilingHeight;

            Box(shell, "Floor",
                new Vector3(width / 2f, -SlabThickness / 2f, depth / 2f),
                new Vector3(width, SlabThickness, depth),
                palette.Concrete);

            // Only the two far walls, same rule the hubs follow: the camera sits high on x and low
            // on z, so these two are behind the room and the other two would fill the frame with
            // their own backs.
            Box(shell, "WallBack",
                new Vector3(WallThickness / 2f, height / 2f, depth / 2f),
                new Vector3(WallThickness, height, depth),
                palette.ConcreteDark);

            Box(shell, "WallSide",
                new Vector3(width / 2f, height / 2f, depth - WallThickness / 2f),
                new Vector3(width, height, WallThickness),
                palette.ConcreteDark);

            // **There is no ceiling, and the first render is why.**
            //
            // A slab over the room seemed right for a basement: an orthographic camera looking into
            // an open box reads as a courtyard. What it actually produced was a closed grey lid
            // filling the whole frame with four floor tiles peeking out from under one edge. At
            // thirty degrees of elevation anything overhead hides about 1.7 metres of floor behind
            // it for every metre of height, and a full-width lid at 2.35m hides all of it.
            //
            // Both hub builders reached the same answer before this one and kept only the two far
            // walls. The pipes and the strip light stay, and doing the work the slab was meant to
            // do: they are thin, so they read as things overhead without taking the room with them.

            // A damp patch and an old repair. Two boxes, and they are most of what stops the walls
            // reading as a grey-box placeholder rather than as a basement.
            Box(shell, "Damp",
                new Vector3(WallThickness + 0.02f, height * 0.62f, depth * 0.34f),
                new Vector3(0.03f, height * 0.5f, depth * 0.22f),
                palette.Damp);

            Box(shell, "Patch",
                new Vector3(width * 0.62f, height * 0.44f, depth - WallThickness - 0.02f),
                new Vector3(width * 0.18f, height * 0.3f, 0.03f),
                palette.Render);
        }

        /// <summary>
        /// Pipes, the consumer unit and one strip light.
        ///
        /// This is the whole art budget for the room and it is spent on the ceiling on purpose: at
        /// this camera angle the ceiling is the largest uninterrupted surface in frame, and a blank
        /// one is what makes a generated room look unfinished rather than plain.
        /// </summary>
        private static void BuildServices(Transform parent, BasementPalette palette)
        {
            var services = Group(parent, "Services");

            var width = BasementFloor.RoomWidth;
            var depth = BasementFloor.RoomDepth;
            var height = BasementFloor.CeilingHeight;

            // **The pipes run along the back wall, not across the room, and the first render is why.**
            //
            // At thirty degrees of elevation anything at height h draws where the floor point
            // `(x - 1.22h, z + 1.22h)` is, so a pipe under a 2.2m ceiling lands 2.7 metres nearer
            // the camera than it hangs. Three of them spanning the width crossed the grid exactly
            // where the cabinets stand, and the racks would have been drawn under a set of bars.
            //
            // Running them front to back at low x puts their drawn position off the floor
            // altogether, over the back wall, which is also where the pipes in a real basement are.
            for (var index = 0; index < 3; index++)
            {
                var across = 0.45f + index * 0.30f;

                Box(services, $"Pipe{index}",
                    new Vector3(across, height - 0.16f, depth / 2f),
                    new Vector3(0.12f, 0.12f, depth),
                    index == 1 ? palette.Copper : palette.Steel);
            }

            Box(services, "Trunking",
                new Vector3(0.34f, height * 0.55f, depth / 2f),
                new Vector3(0.16f, 0.16f, depth * 0.86f),
                palette.Steel);

            Box(services, "ConsumerUnit",
                new Vector3(0.42f, height * 0.6f, depth * 0.82f),
                new Vector3(0.3f, 0.44f, 0.6f),
                palette.Steel);

            // The drain. A basement has one, and it is the detail that says which room this is.
            Box(services, "Drain",
                new Vector3(width - 0.9f, 0.005f, 0.9f),
                new Vector3(0.42f, 0.02f, 0.42f),
                palette.Steel);

            // One fitting, kept over the room on purpose. The pipes had to move because there were
            // four long bars crossing the grid; a single thin light overhead is the thing that says
            // the room is lit rather than daylit, and one line is not clutter.
            Box(services, "StripLight",
                new Vector3(width / 2f, height - 0.06f, depth / 2f),
                new Vector3(0.20f, 0.06f, depth * 0.42f),
                palette.Lamp);

            AddPointLight(services, "Strip",
                new Vector3(width / 2f, height - 0.25f, depth / 2f), 1.05f, 11f);

            // A second, dimmer source at the far end, or the corners go to black and the two squares
            // in them cannot be read at all.
            AddPointLight(services, "Corner",
                new Vector3(width * 0.78f, height - 0.4f, depth * 0.24f), 0.42f, 7f);
        }

        /// <summary>
        /// The grid: one tile and one empty marker per square.
        ///
        /// **The marker is the load-bearing half.** The tile is paint; the marker is what the
        /// runtime stage parents a cabinet to and what the interface projects through the camera to
        /// find the square under the cursor. Both look it up by
        /// <see cref="BasementFloor.MarkerName"/>, so the name is never typed twice.
        /// </summary>
        private static void BuildFloorGrid(Transform parent, BasementPalette palette)
        {
            var tiles = Group(parent, "FloorTiles");
            var squares = Group(parent, SquareGroup);

            for (var row = 0; row < BasementFloor.Rows; row++)
            {
                for (var column = 0; column < BasementFloor.Columns; column++)
                {
                    var centre = new Vector3(
                        BasementFloor.CentreX(column), 0f, BasementFloor.CentreZ(row));

                    Box(tiles, $"Tile_{column}_{row}",
                        centre + new Vector3(0f, TileRise / 2f, 0f),
                        new Vector3(BasementFloor.SquareSize, TileRise, BasementFloor.SquareSize),
                        (column + row) % 2 == 0 ? palette.TileA : palette.TileB);

                    Marker(squares, BasementFloor.MarkerName(column, row), centre);
                }
            }
        }

        // ---- the scene, and the picture ----------------------------------------------------------

        private static void BuildViewingScene(GameObject prefab)
        {
            if (!ScalingLawsSceneBuilder.MayOverwriteScene(ScenePath))
            {
                Debug.LogWarning($"Kept {ScenePath} as it is. The prefab was still rebuilt.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = BasementFloor.CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.035f, 0.05f, 1f);

            // The house's angle, kept. Every other room in this game is drawn at 30/-45 and a
            // basement at a different angle would read as a different game.
            cameraObject.transform.rotation = Quaternion.Euler(30f, -45f, 0f);
            cameraObject.transform.position = CameraPositionFor(camera);

            var sunObject = new GameObject("Fill");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;

            // Weak, and cool. There is no window down here; this is spill from the stairwell.
            sun.intensity = 0.35f;
            sun.color = new Color(0.72f, 0.78f, 0.9f);
            sunObject.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            PrefabUtility.InstantiatePrefab(prefab);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>Pulled back far enough that nothing in the room crosses the near plane.</summary>
        public static Vector3 CameraPositionFor(Camera camera)
        {
            var focus = new Vector3(BasementFloor.FocusX, 1.1f, BasementFloor.FocusZ);
            return focus - camera.transform.forward * 26f;
        }

        [MenuItem("Scaling Laws/Snapshot basement")]
        public static void Snapshot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (prefab == null)
            {
                Debug.LogError("[Scaling Laws] No basement prefab yet. Run Build basement first.");
                return;
            }

            var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            StandSampleRacks(room.transform);

            var cameraObject = new GameObject("BasementSnapshotCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = BasementFloor.CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.035f, 0.05f, 1f);
            cameraObject.transform.rotation = Quaternion.Euler(30f, -45f, 0f);
            cameraObject.transform.position = CameraPositionFor(camera);

            var lightObject = new GameObject("BasementSnapshotFill");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.35f;
            light.color = new Color(0.72f, 0.78f, 0.9f);
            lightObject.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var target = new RenderTexture(1280, 720, 24) { name = "BasementSnapshot" };
            camera.targetTexture = target;
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;

            var picture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            picture.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;

            var path = Path.Combine(Directory.GetCurrentDirectory(), "basement_preview.png");
            File.WriteAllBytes(path, picture.EncodeToPNG());

            Object.DestroyImmediate(picture);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(room);

            Debug.Log($"[Scaling Laws] Basement snapshot written to {path}.");
        }

        /// <summary>
        /// Stands one of each cabinet on the grid, for the snapshot only.
        ///
        /// **The empty room is not the picture worth looking at.** A floor with nothing on it says
        /// nothing about whether the cabinets read, whether they occlude each other at this angle,
        /// or whether the heat strip is visible from the camera, which are the three things this
        /// preview exists to answer.
        ///
        /// The box comes from <see cref="BasementStage.RackScale"/>, the same call the game makes.
        /// A preview drawing its own idea of the shape would stop being evidence the moment either
        /// side changed.
        /// </summary>
        private static void StandSampleRacks(Transform room)
        {
            var group = room.Find(RackGroup);
            var squares = room.Find(SquareGroup);

            if (group == null || squares == null)
            {
                return;
            }

            var kinds = new[]
            {
                ServerRack.OpenFrame, ServerRack.Enclosed,
                ServerRack.HighDensity, ServerRack.Immersion
            };

            // Four tones, so the picture answers whether a red cabinet is findable at a glance.
            var heats = new[]
            {
                new Color(0.49f, 0.78f, 0.60f), new Color(0.89f, 0.75f, 0.27f),
                new Color(0.91f, 0.55f, 0.24f), new Color(0.85f, 0.31f, 0.29f)
            };

            var palette = new BasementPalette();

            for (var index = 0; index < kinds.Length; index++)
            {
                var marker = squares.Find(BasementFloor.MarkerName(index, index));

                if (marker == null)
                {
                    continue;
                }

                var scale = BasementStage.RackScale(kinds[index]);

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Sample_" + kinds[index];
                body.transform.SetParent(marker, false);
                body.transform.localPosition = new Vector3(0f, scale.y / 2f, 0f);
                body.transform.localScale = scale;
                Object.DestroyImmediate(body.GetComponent<BoxCollider>());
                body.GetComponent<MeshRenderer>().sharedMaterial = palette.Cabinet;

                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = "Heat";
                strip.transform.SetParent(body.transform, false);
                strip.transform.localPosition = new Vector3(0.52f, 0.04f, 0f);
                strip.transform.localScale = new Vector3(0.06f, 0.72f, 0.5f);
                Object.DestroyImmediate(strip.GetComponent<BoxCollider>());

                strip.GetComponent<MeshRenderer>().sharedMaterial =
                    BasementPalette.Heat(heats[index], index);
            }
        }

        // ---- helpers ------------------------------------------------------------------------------

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void Marker(Transform parent, string name, Vector3 position)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
        }

        private static void Box(Transform parent, string name, Vector3 centre, Vector3 size,
            Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = centre;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;

            Object.DestroyImmediate(box.GetComponent<BoxCollider>());
        }

        private static void AddPointLight(Transform parent, string name, Vector3 position,
            float intensity, float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = intensity;
            light.range = range;

            // Fluorescent, not tungsten. A warm bulb makes the room cosy, which is the one thing
            // this room must not be.
            light.color = new Color(0.88f, 0.94f, 1f);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// The basement's own materials.
        ///
        /// The shader is looked up rather than named, for the reason both other builders record: a
        /// URP shader under the built-in pipeline draws magenta rather than failing, which is how
        /// the first office room came out.
        /// </summary>
        private sealed class BasementPalette
        {
            public BasementPalette()
            {
                // Darker than the first pass. The room came out near white under two point lights
                // and read as an empty office, and the cabinets standing on it have to be the
                // brightest thing in the frame rather than competing with their own floor.
                Concrete = Make("BasementConcrete", new Color(0.22f, 0.22f, 0.23f), 0.08f);
                ConcreteDark = Make("BasementConcreteDark", new Color(0.15f, 0.155f, 0.17f), 0.05f);
                TileA = Make("BasementTileA", new Color(0.26f, 0.27f, 0.29f), 0.16f);
                TileB = Make("BasementTileB", new Color(0.22f, 0.23f, 0.25f), 0.16f);
                Steel = Make("BasementSteel", new Color(0.36f, 0.38f, 0.41f), 0.55f, 0.6f);
                Copper = Make("BasementCopper", new Color(0.52f, 0.33f, 0.20f), 0.5f, 0.5f);
                Damp = Make("BasementDamp", new Color(0.24f, 0.26f, 0.25f), 0.3f);
                Render = Make("BasementRender", new Color(0.40f, 0.39f, 0.37f), 0.05f);
                Lamp = Make("BasementLamp", new Color(0.90f, 0.95f, 1f), 0.7f);

                // Only the snapshot stands cabinets. The game paints its own at runtime.
                Cabinet = Make("BasementCabinet", new Color(0.20f, 0.21f, 0.24f), 0.25f);
            }

            /// <summary>A heat strip for the preview, one asset per tone.</summary>
            public static Material Heat(Color colour, int index) =>
                Make("BasementHeat" + index, colour, 0.4f);

            public Material Concrete { get; }
            public Material ConcreteDark { get; }
            public Material TileA { get; }
            public Material TileB { get; }
            public Material Steel { get; }
            public Material Copper { get; }
            public Material Damp { get; }
            public Material Render { get; }
            public Material Lamp { get; }
            public Material Cabinet { get; }

            private static Shader CurrentLitShader()
            {
                var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                var shader = pipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : null;
                return shader != null ? shader : Shader.Find("Standard");
            }

            private static Material Make(string name, Color colour, float smoothness = 0.15f,
                float metallic = 0f)
            {
                var path = MaterialFolder + "/" + name + ".mat";
                var shader = CurrentLitShader();
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (existing != null)
                {
                    existing.shader = shader;
                    Paint(existing, colour, smoothness, metallic);
                    EditorUtility.SetDirty(existing);
                    return existing;
                }

                var material = new Material(shader) { name = name };
                Paint(material, colour, smoothness, metallic);
                AssetDatabase.CreateAsset(material, path);
                return material;
            }

            private static void Paint(Material material, Color colour, float smoothness,
                float metallic)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", colour);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", colour);
                }

                if (material.HasProperty("_Glossiness"))
                {
                    material.SetFloat("_Glossiness", smoothness);
                }

                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", smoothness);
                }

                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", metallic);
                }
            }
        }
    }
}
