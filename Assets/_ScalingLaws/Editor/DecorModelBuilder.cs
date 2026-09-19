using System.IO;
using ScalingLaws.Data;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// A model for every piece the furniture shop sells, saved where the game can load it.
    ///
    /// **Reported on 2026-09-19 as "strange blocks at the front of the office".** Every piece a
    /// player bought was drawn as a coloured box, because the game cannot reach the Store packs the
    /// rooms are furnished from: those are editor assets, and a build only has what is under
    /// `Resources`. So each piece is assembled here once, from the packs where they have something
    /// that fits and from parts where they do not, and saved as `Resources/Decor/{Kind}.prefab` with
    /// its pivot in the middle of its base and its footprint the catalog's own size.
    ///
    /// A piece with no prefab still draws as its box, so a clone without the packs is complete.
    /// </summary>
    public static class DecorModelBuilder
    {
        public const string Folder = "Assets/_ScalingLaws/Resources/Decor";
        private const string Materials = "Assets/_ScalingLaws/Materials";

        private const string Brick = "Assets/Brick Project Studio/Apartment Kit/_Prefabs/";
        private const string Nappin = "Assets/nappin/OfficeEssentialsPack/Prefabs/";

        [MenuItem("Scaling Laws/Build furniture shop models")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);

            foreach (var piece in FurnitureCatalog.All)
            {
                Build(piece);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Decor] {FurnitureCatalog.All.Count} shop models written to {Folder}.");
        }

        private static void Build(FurniturePiece piece)
        {
            var root = new GameObject(piece.Kind.ToString());
            var t = root.transform;
            var x = piece.SizeX;
            var y = piece.SizeY;
            var z = piece.SizeZ;

            var frame = Mat("HubFrame");
            var linen = Mat("HubLinen");
            var cabinet = Mat("Hub2Cabinet");
            var stone = Mat("HubCounter");
            var led = Mat("HubLed");
            var glass = Mat("HubClearGlass");

            switch (piece.Kind)
            {
                case FurnitureKind.Plant:
                    HubRoomBuilder.Piece(t, "Plant", Vector3.zero, new Vector3(x, y, z),
                        HubRoomBuilder.Kit.PlantTall, Mat("HubFoliage"));
                    break;

                case FurnitureKind.Whiteboard:
                    HubRoomBuilder.Box(t, "LegL", new Vector3(-x / 2f + 0.05f, 0.9f, 0f), new Vector3(0.04f, 1.8f, 0.04f), frame);
                    HubRoomBuilder.Box(t, "LegR", new Vector3(x / 2f - 0.05f, 0.9f, 0f), new Vector3(0.04f, 1.8f, 0.04f), frame);
                    HubRoomBuilder.Box(t, "FootL", new Vector3(-x / 2f + 0.05f, 0.02f, 0f), new Vector3(0.06f, 0.04f, 0.5f), frame);
                    HubRoomBuilder.Box(t, "FootR", new Vector3(x / 2f - 0.05f, 0.02f, 0f), new Vector3(0.06f, 0.04f, 0.5f), frame);
                    HubRoomBuilder.Box(t, "Board", new Vector3(0f, 1.25f, 0f), new Vector3(x - 0.12f, 0.95f, 0.03f), linen);
                    HubRoomBuilder.Box(t, "Rim", new Vector3(0f, 1.25f, 0.005f), new Vector3(x - 0.08f, 0.99f, 0.02f), frame);
                    HubRoomBuilder.Box(t, "Tray", new Vector3(0f, 0.76f, -0.05f), new Vector3(x - 0.3f, 0.03f, 0.08f), frame);
                    break;

                case FurnitureKind.Desk:
                    HubRoomBuilder.Piece(t, "Desk", new Vector3(0f, 0f, 0.1f), new Vector3(x, y, z * 0.8f),
                        HubRoomBuilder.Kit.Desk, Mat("Hub1DeskTop"), 90f);
                    HubRoomBuilder.Piece(t, "Monitor", new Vector3(0f, y, 0.3f), new Vector3(0.6f, 0.45f, 0.25f),
                        HubRoomBuilder.Kit.Monitor, Mat("HubScreen"), 90f);
                    HubRoomBuilder.Piece(t, "Chair", new Vector3(0f, 0f, -0.45f), new Vector3(0.55f, 0.95f, 0.55f),
                        HubRoomBuilder.Kit.StaffChair, Mat("HubFabric"));
                    break;

                case FurnitureKind.Bookshelf:
                    HubRoomBuilder.Piece(t, "Shelf", Vector3.zero, new Vector3(x, y, z),
                        new[] { Brick + "Furniture/Living Room/Shelf_Apt_01.prefab", Nappin + "(Prb)Shelves2.prefab" },
                        Mat("HubTimberDark"), 180f);
                    break;

                case FurnitureKind.Sofa:
                    HubRoomBuilder.Piece(t, "Sofa", Vector3.zero, new Vector3(x, y, z),
                        new[] { Brick + "Furniture/Living Room/Sofa_Apt_02.prefab", Brick + "Furniture/Living Room/Sofa_Apt_01.prefab" },
                        Mat("HubFabric"), 180f);
                    break;

                case FurnitureKind.StandingDesk:
                    HubRoomBuilder.Box(t, "Top", new Vector3(0f, 1.05f, 0f), new Vector3(x, 0.04f, z), Mat("Hub1DeskTop"));
                    HubRoomBuilder.Box(t, "ColumnL", new Vector3(-x / 2f + 0.12f, 0.52f, 0f), new Vector3(0.07f, 1.04f, 0.07f), frame);
                    HubRoomBuilder.Box(t, "ColumnR", new Vector3(x / 2f - 0.12f, 0.52f, 0f), new Vector3(0.07f, 1.04f, 0.07f), frame);
                    HubRoomBuilder.Box(t, "FootL", new Vector3(-x / 2f + 0.12f, 0.02f, 0f), new Vector3(0.08f, 0.04f, z * 0.9f), frame);
                    HubRoomBuilder.Box(t, "FootR", new Vector3(x / 2f - 0.12f, 0.02f, 0f), new Vector3(0.08f, 0.04f, z * 0.9f), frame);
                    HubRoomBuilder.Piece(t, "Monitor", new Vector3(0f, 1.07f, 0.15f), new Vector3(0.6f, 0.45f, 0.25f),
                        HubRoomBuilder.Kit.Monitor, Mat("HubScreen"), 90f);
                    break;

                case FurnitureKind.CoffeeBar:
                    HubRoomBuilder.Box(t, "Counter", new Vector3(0f, 0.5f, 0f), new Vector3(x, 1.0f, z), cabinet);
                    HubRoomBuilder.Box(t, "Top", new Vector3(0f, 1.02f, 0f), new Vector3(x + 0.04f, 0.05f, z + 0.04f), stone);
                    HubRoomBuilder.Box(t, "Kick", new Vector3(0f, 0.06f, -z / 2f - 0.01f), new Vector3(x * 0.9f, 0.02f, 0.02f), led);
                    HubRoomBuilder.Piece(t, "Machine", new Vector3(-x * 0.2f, 1.045f, 0.05f), new Vector3(0.35f, 0.4f, 0.35f),
                        HubRoomBuilder.Kit.CoffeeMaker, Mat("HubMetal"), 180f);
                    HubRoomBuilder.Piece(t, "Microwave", new Vector3(x * 0.25f, 1.045f, 0.05f), new Vector3(0.5f, 0.3f, 0.4f),
                        new[] { Nappin + "(Prb)Microwave.prefab" }, Mat("HubMetal"), 180f);
                    break;

                case FurnitureKind.ArtPiece:
                    HubRoomBuilder.Box(t, "Plinth", new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 0.5f), stone);
                    HubRoomBuilder.Piece(t, "Sculpture", new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 1.1f, 0.4f),
                        new[] { Brick + "Props/Sculptures/Sculpture_apt_02_01.prefab" }, Mat("HubBrass"));
                    HubRoomBuilder.Box(t, "Glow", new Vector3(0f, 0.505f, -0.26f), new Vector3(0.8f, 0.02f, 0.02f), led);
                    break;

                case FurnitureKind.Aquarium:
                    HubRoomBuilder.Box(t, "Stand", new Vector3(0f, 0.35f, 0f), new Vector3(x, 0.7f, z), cabinet);
                    HubRoomBuilder.Box(t, "Water", new Vector3(0f, 1.05f, 0f), new Vector3(x - 0.1f, 0.62f, z - 0.1f), Water());
                    HubRoomBuilder.Box(t, "Tank", new Vector3(0f, 1.08f, 0f), new Vector3(x, 0.76f, z), glass);
                    HubRoomBuilder.Box(t, "Lid", new Vector3(0f, 1.48f, 0f), new Vector3(x + 0.02f, 0.05f, z + 0.02f), frame);
                    HubRoomBuilder.Box(t, "Sand", new Vector3(0f, 0.76f, 0f), new Vector3(x - 0.12f, 0.08f, z - 0.12f), Mat("HubCounter"));
                    HubRoomBuilder.Piece(t, "Weed", new Vector3(x * 0.3f, 0.8f, 0f), new Vector3(0.3f, 0.5f, 0.3f),
                        HubRoomBuilder.Kit.PlantWide, Mat("HubFoliage"));
                    break;

                case FurnitureKind.SleepPod:
                    HubRoomBuilder.Box(t, "Base", new Vector3(0f, 0.2f, 0f), new Vector3(x, 0.4f, z), cabinet);
                    HubRoomBuilder.Box(t, "Mattress", new Vector3(0f, 0.46f, 0.05f), new Vector3(x - 0.2f, 0.14f, z - 0.25f), linen);
                    HubRoomBuilder.Box(t, "ShellBack", new Vector3(0f, 0.85f, z / 2f - 0.06f), new Vector3(x, 0.9f, 0.12f), Mat("HubDivider"));
                    HubRoomBuilder.Box(t, "ShellTop", new Vector3(0f, 1.27f, 0.05f), new Vector3(x, 0.06f, z - 0.1f), Mat("HubDivider"));
                    HubRoomBuilder.Box(t, "ShellEnd", new Vector3(x / 2f - 0.05f, 0.85f, 0.05f), new Vector3(0.1f, 0.9f, z - 0.1f), Mat("HubDivider"));
                    HubRoomBuilder.Box(t, "Reading", new Vector3(0f, 1.22f, 0.05f), new Vector3(x * 0.7f, 0.02f, 0.04f), led);
                    break;

                default:
                    HubRoomBuilder.Box(t, "Body", new Vector3(0f, y / 2f, 0f), new Vector3(x, y, z), cabinet);
                    break;
            }

            // Nothing in the room is walked into, and a collider on a decoration is one more thing
            // the founder's route would have to be told about.
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{piece.Kind}.prefab");
            Object.DestroyImmediate(root);
        }

        private static Material Mat(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/{name}.mat");

            if (material == null)
            {
                Debug.LogWarning($"[Decor] No material {name}; build the hubs first.");
            }

            return material;
        }

        /// <summary>Aquarium water: a lit blue, so the tank reads as water rather than as a blue box.</summary>
        private static Material Water()
        {
            var path = $"{Materials}/HubWater.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = "HubWater" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_Color", new Color(0.10f, 0.45f, 0.62f));
            material.SetFloat("_Glossiness", 0.95f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.05f, 0.30f, 0.45f));
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
