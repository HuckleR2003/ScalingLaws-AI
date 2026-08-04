using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Builds the level one room: a two floor doll's house with the bedroom on the mezzanine, a
    /// staircase down the left wall, and a living room sharing the ground floor with the workshop
    /// and the first server racks.
    ///
    /// It is generated rather than assembled by hand for the same reason the menu is built in C#:
    /// the numbers stay readable and a change is a diff instead of a drag. Everything here is grey
    /// box on a one metre grid, sized so real models can replace a box without moving anything else.
    /// The room is saved as a prefab because <see cref="ScalingLawsSceneBuilder"/> regenerates
    /// scenes and would delete anything placed directly in one.
    ///
    /// Two walls only, back and left. The camera looks down the open corner, which is what makes a
    /// closed room readable from a fixed angle.
    /// </summary>
    public static class OfficeRoomBuilder
    {
        private const string PrefabFolder = "Assets/_ScalingLaws/Prefabs";
        private const string MaterialFolder = "Assets/_ScalingLaws/Materials";
        private const string ScenesFolder = "Assets/_ScalingLaws/Scenes";
        private const string PrefabPath = PrefabFolder + "/OfficeRoom.prefab";
        private const string ScenePath = ScenesFolder + "/Office.unity";

        // The room, in metres. Changing these is meant to be the way the room changes.
        private const float RoomWidth = 12.0f;
        private const float RoomDepth = 9.0f;
        private const float WallThickness = 0.2f;
        private const float GroundHeight = 3.0f;
        private const float TotalHeight = 6.0f;
        private const float SlabThickness = 0.25f;

        /// <summary>Where the mezzanine starts. Everything behind this line has a floor above it.</summary>
        private const float MezzanineFront = 5.4f;

        /// <summary>The stair well. The railing stops here so the steps have somewhere to arrive.</summary>
        private const float StairLaneWidth = 3.4f;

        private const int StairSteps = 14;

        [MenuItem("Scaling Laws/Build office room")]
        public static void Build()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(ScenesFolder);

            var palette = new Palette();

            var root = new GameObject("OfficeRoom");
            BuildShell(root.transform, palette);
            BuildStairs(root.transform, palette);

            var furniture = Group(root.transform, "Furniture");
            BuildLivingRoom(furniture, palette);
            BuildWorkshop(furniture, palette);
            BuildBedroom(furniture, palette);

            // Filled at runtime from CompanyState, so they are empty on purpose.
            Group(root.transform, "Staff");
            BuildServerBay(Group(root.transform, "Servers"), palette);
            BuildWaypoints(Group(root.transform, "Waypoints"));

            // The practicals go in the prefab rather than in a scene, so instantiating the room
            // anywhere gives a lit room. They were in the viewing scene only, which meant the copy
            // that the game shows would have come out flat.
            AddPointLight(root.transform, "LampBedside",
                new Vector3(7.8f, GroundHeight + 1.3f, 8.2f), 2.2f, 4.5f);
            AddPointLight(root.transform, "LampLiving", new Vector3(4.6f, 2.2f, 3.4f), 1.8f, 5.5f);
            AddPointLight(root.transform, "LampWorkshop", new Vector3(9.6f, 2.2f, 4.4f), 1.6f, 5.0f);
            AddPointLight(root.transform, "RackGlow", new Vector3(7.7f, 1.4f, 7.2f), 1.2f, 3.5f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            BuildViewingScene(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Office room written to {PrefabPath}, viewable scene at {ScenePath}.");
        }

        /// <summary>
        /// Renders the room from its own camera to a PNG. Every layout fault in this project so far
        /// was found by looking at it rather than by a test, and a headless render is the only way
        /// to look at it without opening the editor.
        /// </summary>
        [MenuItem("Scaling Laws/Snapshot office room")]
        public static void Snapshot()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogError("No camera in the office scene. Build the room first.");
                return;
            }

            const int width = 1600;
            const int height = 900;

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            camera.targetTexture = target;
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;

            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;

            var path = Path.Combine(Directory.GetCurrentDirectory(), "office_preview.png");
            File.WriteAllBytes(path, image.EncodeToPNG());

            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);

            Debug.Log($"Office preview written to {path}.");
        }

        // ------------------------------------------------------------------ shell

        private static void BuildShell(Transform parent, Palette palette)
        {
            var shell = Group(parent, "Shell");

            Box(shell, "Floor",
                new Vector3(RoomWidth / 2f, -WallThickness / 2f, RoomDepth / 2f),
                new Vector3(RoomWidth, WallThickness, RoomDepth), palette.Concrete);

            Box(shell, "WallBack",
                new Vector3(RoomWidth / 2f, TotalHeight / 2f, RoomDepth - WallThickness / 2f),
                new Vector3(RoomWidth, TotalHeight, WallThickness), palette.WallCool);

            Box(shell, "WallLeft",
                new Vector3(WallThickness / 2f, TotalHeight / 2f, RoomDepth / 2f),
                new Vector3(WallThickness, TotalHeight, RoomDepth), palette.WallWarm);

            // The mezzanine slab is the bedroom floor and the living room ceiling at once.
            var slabDepth = RoomDepth - MezzanineFront;
            Box(shell, "MezzanineSlab",
                new Vector3(RoomWidth / 2f, GroundHeight + SlabThickness / 2f, MezzanineFront + slabDepth / 2f),
                new Vector3(RoomWidth, SlabThickness, slabDepth), palette.Timber);

            // Railing along the open edge, stopping where the stairs arrive.
            var railFrom = StairLaneWidth;
            var railLength = RoomWidth - railFrom;
            var railTop = GroundHeight + SlabThickness;

            Box(shell, "RailingTop",
                new Vector3(railFrom + railLength / 2f, railTop + 1.0f, MezzanineFront + 0.05f),
                new Vector3(railLength, 0.08f, 0.08f), palette.Metal);

            for (var index = 0; index <= 8; index++)
            {
                var x = railFrom + railLength * index / 8f;
                Box(shell, $"RailingPost{index:00}",
                    new Vector3(Mathf.Clamp(x, railFrom + 0.05f, RoomWidth - 0.05f), railTop + 0.5f, MezzanineFront + 0.05f),
                    new Vector3(0.06f, 1.0f, 0.06f), palette.Metal);
            }
        }

        /// <summary>
        /// A straight flight against the left wall. Steps are boxes rather than a ramp because the
        /// camera is close enough to see the treads and because a walker can stand on each one.
        /// </summary>
        private static void BuildStairs(Transform parent, Palette palette)
        {
            var stairs = Group(parent, "Stairs");

            var rise = (GroundHeight + SlabThickness) / StairSteps;
            var run = 0.32f;
            var startZ = MezzanineFront - StairSteps * run;

            for (var index = 0; index < StairSteps; index++)
            {
                var height = rise * (index + 1);
                Box(stairs, $"Step{index:00}",
                    new Vector3(WallThickness + 0.95f, height / 2f, startZ + run * index + run / 2f),
                    new Vector3(1.9f, height, run), palette.Timber);
            }
        }

        // ------------------------------------------------------------------ ground floor

        private static void BuildLivingRoom(Transform parent, Palette palette)
        {
            var room = Group(parent, "LivingRoom");

            Box(room, "Rug", new Vector3(4.6f, 0.02f, 2.4f), new Vector3(3.6f, 0.04f, 2.6f), palette.Fabric);

            Box(room, "SofaBase", new Vector3(4.6f, 0.35f, 1.4f), new Vector3(3.0f, 0.7f, 0.9f), palette.Sofa);
            Box(room, "SofaBack", new Vector3(4.6f, 0.85f, 1.05f), new Vector3(3.0f, 1.0f, 0.25f), palette.Sofa);
            Box(room, "SofaArmLeft", new Vector3(3.2f, 0.6f, 1.4f), new Vector3(0.25f, 1.2f, 0.9f), palette.Sofa);
            Box(room, "SofaArmRight", new Vector3(6.0f, 0.6f, 1.4f), new Vector3(0.25f, 1.2f, 0.9f), palette.Sofa);

            Box(room, "CoffeeTable", new Vector3(4.6f, 0.42f, 3.0f), new Vector3(1.5f, 0.08f, 0.7f), palette.Timber);
            Box(room, "CoffeeTableLegs", new Vector3(4.6f, 0.2f, 3.0f), new Vector3(1.3f, 0.4f, 0.5f), palette.TimberDark);

            Box(room, "MediaUnit", new Vector3(4.6f, 0.28f, 4.7f), new Vector3(2.6f, 0.56f, 0.45f), palette.Timber);
            Box(room, "Television", new Vector3(4.6f, 1.05f, 4.85f), new Vector3(1.9f, 1.0f, 0.08f), palette.Screen);

            Box(room, "PlantPotA", new Vector3(2.6f, 0.2f, 4.4f), new Vector3(0.4f, 0.4f, 0.4f), palette.Terracotta);
            Box(room, "PlantA", new Vector3(2.6f, 0.75f, 4.4f), new Vector3(0.7f, 0.8f, 0.7f), palette.Foliage);
            Box(room, "PlantPotB", new Vector3(6.6f, 0.18f, 4.5f), new Vector3(0.34f, 0.36f, 0.34f), palette.Terracotta);
            Box(room, "PlantB", new Vector3(6.6f, 0.62f, 4.5f), new Vector3(0.6f, 0.6f, 0.6f), palette.Foliage);

            Box(room, "Shelf", new Vector3(7.9f, 1.0f, 4.6f), new Vector3(1.4f, 2.0f, 0.4f), palette.Timber);
        }

        private static void BuildWorkshop(Transform parent, Palette palette)
        {
            var shop = Group(parent, "Workshop");

            Box(shop, "BenchTop", new Vector3(9.4f, 0.9f, 2.6f), new Vector3(2.6f, 0.1f, 1.2f), palette.Timber);
            Box(shop, "BenchFrame", new Vector3(9.4f, 0.45f, 2.6f), new Vector3(2.4f, 0.8f, 1.0f), palette.Metal);
            Box(shop, "BenchStool", new Vector3(10.9f, 0.32f, 2.6f), new Vector3(0.4f, 0.64f, 0.4f), palette.Metal);
            Box(shop, "PartsBinA", new Vector3(8.8f, 1.05f, 2.6f), new Vector3(0.4f, 0.2f, 0.3f), palette.Accent);
            Box(shop, "PartsBinB", new Vector3(9.3f, 1.05f, 2.6f), new Vector3(0.4f, 0.2f, 0.3f), palette.Accent);

            Box(shop, "DeskTop", new Vector3(9.8f, 0.75f, 5.6f), new Vector3(2.8f, 0.08f, 0.8f), palette.Timber);
            Box(shop, "DeskLegs", new Vector3(9.8f, 0.37f, 5.6f), new Vector3(2.6f, 0.74f, 0.6f), palette.Metal);
            Box(shop, "Monitor", new Vector3(9.6f, 1.05f, 5.85f), new Vector3(1.0f, 0.55f, 0.06f), palette.Screen);
            Box(shop, "Tower", new Vector3(11.0f, 0.3f, 5.6f), new Vector3(0.25f, 0.6f, 0.5f), palette.Metal);
            Box(shop, "Chair", new Vector3(9.6f, 0.45f, 4.9f), new Vector3(0.55f, 0.9f, 0.55f), palette.Fabric);

            Box(shop, "ToolBoard", new Vector3(10.2f, 2.1f, RoomDepth - WallThickness - 0.06f),
                new Vector3(2.6f, 1.2f, 0.05f), palette.Accent);
            Box(shop, "ToolChest", new Vector3(7.4f, 0.45f, 8.1f), new Vector3(1.0f, 0.9f, 0.6f), palette.Accent);

            Box(shop, "CrateA", new Vector3(11.4f, 0.25f, 1.0f), new Vector3(0.5f, 0.5f, 0.5f), palette.Cardboard);
            Box(shop, "CrateB", new Vector3(11.4f, 0.7f, 1.05f), new Vector3(0.42f, 0.42f, 0.42f), palette.Cardboard);
        }

        private static void BuildServerBay(Transform parent, Palette palette)
        {
            for (var index = 0; index < 3; index++)
            {
                var x = 7.0f + index * 0.75f;
                Box(parent, $"Rack{index:00}", new Vector3(x, 1.0f, 7.9f),
                    new Vector3(0.65f, 2.0f, 0.9f), palette.Metal);
                Box(parent, $"RackFace{index:00}", new Vector3(x, 1.0f, 7.44f),
                    new Vector3(0.55f, 1.8f, 0.04f), palette.Screen);
            }
        }

        // ------------------------------------------------------------------ mezzanine

        private static void BuildBedroom(Transform parent, Palette palette)
        {
            var room = Group(parent, "Bedroom");
            var floor = GroundHeight + SlabThickness;

            Box(room, "BedFrame", new Vector3(6.4f, floor + 0.25f, 7.4f), new Vector3(2.0f, 0.5f, 2.1f), palette.Timber);
            Box(room, "Mattress", new Vector3(6.4f, floor + 0.62f, 7.4f), new Vector3(1.9f, 0.25f, 2.0f), palette.Bedding);
            Box(room, "Pillows", new Vector3(6.4f, floor + 0.8f, 8.25f), new Vector3(1.7f, 0.18f, 0.5f), palette.Linen);
            Box(room, "Nightstand", new Vector3(7.8f, floor + 0.3f, 8.2f), new Vector3(0.5f, 0.6f, 0.5f), palette.Timber);
            Box(room, "Lamp", new Vector3(7.8f, floor + 0.78f, 8.2f), new Vector3(0.22f, 0.36f, 0.22f), palette.Lamp);

            Box(room, "DeskTop", new Vector3(3.4f, floor + 0.75f, 8.4f), new Vector3(2.2f, 0.08f, 0.7f), palette.Timber);
            Box(room, "DeskLegs", new Vector3(3.4f, floor + 0.37f, 8.4f), new Vector3(2.0f, 0.74f, 0.5f), palette.Metal);
            Box(room, "Monitor", new Vector3(3.4f, floor + 1.05f, 8.6f), new Vector3(0.95f, 0.52f, 0.06f), palette.Screen);
            Box(room, "Chair", new Vector3(3.4f, floor + 0.45f, 7.7f), new Vector3(0.55f, 0.9f, 0.55f), palette.Fabric);

            Box(room, "Bookshelf", new Vector3(1.4f, floor + 0.9f, 8.4f), new Vector3(1.2f, 1.8f, 0.4f), palette.Timber);
            Box(room, "PlantPot", new Vector3(9.2f, floor + 0.18f, 8.2f), new Vector3(0.34f, 0.36f, 0.34f), palette.Terracotta);
            Box(room, "Plant", new Vector3(9.2f, floor + 0.62f, 8.2f), new Vector3(0.6f, 0.6f, 0.6f), palette.Foliage);

            Box(room, "PictureA", new Vector3(5.2f, floor + 1.9f, RoomDepth - WallThickness - 0.06f),
                new Vector3(0.5f, 0.65f, 0.04f), palette.Accent);
            Box(room, "PictureB", new Vector3(8.6f, floor + 2.0f, RoomDepth - WallThickness - 0.06f),
                new Vector3(0.45f, 0.6f, 0.04f), palette.Accent);
        }

        /// <summary>
        /// Named points a walker can be sent to. Waypoints rather than a navmesh: at this camera
        /// distance the difference is invisible and the complexity is not.
        /// </summary>
        private static void BuildWaypoints(Transform parent)
        {
            Marker(parent, "Door", new Vector3(11.2f, 0f, 0.6f));
            Marker(parent, "Sofa", new Vector3(4.6f, 0f, 2.3f));
            Marker(parent, "Bench", new Vector3(9.4f, 0f, 1.8f));
            Marker(parent, "Desk", new Vector3(9.6f, 0f, 4.9f));
            Marker(parent, "Racks", new Vector3(7.7f, 0f, 7.1f));
            Marker(parent, "StairFoot", new Vector3(1.0f, 0f, 0.9f));
            Marker(parent, "StairHead", new Vector3(1.0f, GroundHeight + SlabThickness, 5.8f));
            Marker(parent, "Bed", new Vector3(6.4f, GroundHeight + SlabThickness, 6.2f));
            Marker(parent, "UpstairsDesk", new Vector3(3.4f, GroundHeight + SlabThickness, 7.7f));
        }

        // ------------------------------------------------------------------ scene

        private static void BuildViewingScene(GameObject prefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Fixed for good. Every placement above assumes this angle, so once it is set it must
            // not move: a different angle would put furniture behind the walls that were left out.
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Yaw is -45, not +45. The two walls that were kept are x=0 and z=max, so the camera
            // has to sit at high x and low z for both of them to be behind the room. At +45 it sat
            // outside the left wall and rendered the back of it filling the frame.
            cameraObject.transform.rotation = Quaternion.Euler(30f, -45f, 0f);
            cameraObject.transform.position = new Vector3(RoomWidth / 2f, GroundHeight, RoomDepth / 2f)
                - cameraObject.transform.forward * 30f;
            cameraObject.AddComponent<AudioListener>();

            var keyObject = new GameObject("KeyLight");
            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            key.color = new Color(1.0f, 0.96f, 0.90f);
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(45f, 200f, 0f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = Vector3.zero;


            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void AddPointLight(Transform parent, string name, Vector3 position, float intensity, float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = intensity;
            light.range = range;
            light.color = new Color(1.0f, 0.86f, 0.68f);
        }

        // ------------------------------------------------------------------ plumbing

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

        private static void Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = centre;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;

            // Nothing in the room is walked into yet, and colliders on a few hundred boxes cost
            // more than they are worth until something needs to hit them.
            Object.DestroyImmediate(box.GetComponent<BoxCollider>());
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
        /// One material per surface, created once and reused. The shader is looked up rather than
        /// named outright so the room survives the switch to URP without every box turning magenta.
        /// </summary>
        private sealed class Palette
        {
            /// <summary>
            /// Whichever lit shader the project can actually render. URP is in the manifest but no
            /// pipeline asset is assigned yet, and a URP shader under the built-in pipeline draws
            /// magenta, which is how the first build of this room came out.
            /// </summary>
            private static Shader CurrentLitShader()
            {
                var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                var shader = pipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : null;
                return shader != null ? shader : Shader.Find("Standard");
            }

            public Palette()
            {
                Concrete = Make("RoomConcrete", new Color(0.38f, 0.39f, 0.42f));
                WallCool = Make("RoomWallCool", new Color(0.30f, 0.36f, 0.47f));
                WallWarm = Make("RoomWallWarm", new Color(0.72f, 0.62f, 0.28f));
                Timber = Make("RoomTimber", new Color(0.45f, 0.31f, 0.20f));
                TimberDark = Make("RoomTimberDark", new Color(0.30f, 0.21f, 0.14f));
                Metal = Make("RoomMetal", new Color(0.28f, 0.30f, 0.33f), 0.8f, 0.35f);
                Sofa = Make("RoomSofa", new Color(0.33f, 0.42f, 0.31f));
                Fabric = Make("RoomFabric", new Color(0.60f, 0.57f, 0.51f));
                Screen = Make("RoomScreen", new Color(0.16f, 0.42f, 0.62f), 0.1f, 0.6f);
                Foliage = Make("RoomFoliage", new Color(0.24f, 0.45f, 0.24f));
                Terracotta = Make("RoomTerracotta", new Color(0.55f, 0.32f, 0.22f));
                Cardboard = Make("RoomCardboard", new Color(0.65f, 0.52f, 0.36f));
                Bedding = Make("RoomBedding", new Color(0.40f, 0.48f, 0.62f));
                Linen = Make("RoomLinen", new Color(0.82f, 0.82f, 0.80f));
                Lamp = Make("RoomLamp", new Color(0.95f, 0.82f, 0.55f));
                Accent = Make("RoomAccent", new Color(0.62f, 0.24f, 0.22f));
            }

            public Material Concrete { get; }
            public Material WallCool { get; }
            public Material WallWarm { get; }
            public Material Timber { get; }
            public Material TimberDark { get; }
            public Material Metal { get; }
            public Material Sofa { get; }
            public Material Fabric { get; }
            public Material Screen { get; }
            public Material Foliage { get; }
            public Material Terracotta { get; }
            public Material Cardboard { get; }
            public Material Bedding { get; }
            public Material Linen { get; }
            public Material Lamp { get; }
            public Material Accent { get; }

            private static Material Make(string name, Color colour, float smoothness = 0.15f, float metallic = 0f)
            {
                var path = MaterialFolder + "/" + name + ".mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null)
                {
                    // A material written for the other pipeline renders magenta rather than failing,
                    // so the cached asset is repointed instead of trusted.
                    var wanted = CurrentLitShader();
                    if (existing.shader != wanted)
                    {
                        existing.shader = wanted;
                        existing.SetColor(existing.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", colour);
                        EditorUtility.SetDirty(existing);
                    }

                    return existing;
                }

                var material = new Material(CurrentLitShader()) { name = name };

                material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", colour);
                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", smoothness);
                }
                else if (material.HasProperty("_Glossiness"))
                {
                    material.SetFloat("_Glossiness", smoothness);
                }

                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", metallic);
                }

                AssetDatabase.CreateAsset(material, path);
                return material;
            }
        }
    }
}
