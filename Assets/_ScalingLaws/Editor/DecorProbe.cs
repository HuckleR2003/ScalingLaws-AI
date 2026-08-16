using System.IO;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Buys a floor's worth of furniture and renders the room it lands in.
    ///
    /// **The assertions cannot see this.** A test can prove the plan placed ten things inside the
    /// zone; only a picture proves the zone is open floor rather than the middle of a desk, and that
    /// the boxes are the right size for the room they are standing in. Six mechanisms in this
    /// project have passed their tests and delivered nothing visible, so the room gets photographed.
    /// </summary>
    public static class DecorProbe
    {
        private const string OutputFolder = "HubProof~";

        [MenuItem("Scaling Laws/Photograph a furnished floor")]
        public static void Shoot()
        {
            Directory.CreateDirectory(OutputFolder);

            Furnish(OfficeTier.Loft, "Assets/_ScalingLaws/Scenes/SmallHub.unity",
                "small_hub_furnished.png");

            Furnish(OfficeTier.Floor, "Assets/_ScalingLaws/Scenes/BigHub.unity",
                "big_hub_furnished.png");

            // The garage too. Its room was built long before the shop existed and its free floor is
            // whatever the sofa, the workbench and the stairs left over, which is not something a
            // number in a table can be trusted about.
            Furnish(OfficeTier.Garage, "Assets/_ScalingLaws/Scenes/Office.unity",
                "garage_furnished.png");
        }

        private static void Furnish(OfficeTier tier, string scenePath, string fileName)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"No scene at {scenePath}.");
                return;
            }

            var room = RoomCatalog.For(tier);
            var zone = new DecorZone(room.DecorX, room.DecorZ, room.DecorWidth, room.DecorDepth);

            // A realistic shopping trip rather than one of everything: this is what a player who
            // has just moved in and has money would actually buy.
            var plan = new DecorPlan();
            var order = new[]
            {
                FurnitureKind.Sofa, FurnitureKind.CoffeeBar, FurnitureKind.Plant,
                FurnitureKind.Desk, FurnitureKind.Desk, FurnitureKind.Bookshelf,
                FurnitureKind.Aquarium, FurnitureKind.Plant, FurnitureKind.ArtPiece,
                FurnitureKind.SleepPod
            };

            var placed = 0;
            foreach (var kind in order)
            {
                if (plan.Buy(kind, zone).IsPlaced)
                {
                    placed++;
                }
            }

            var group = new GameObject("ProbeFurniture");

            foreach (var item in plan.Placed)
            {
                var piece = item.Definition;

                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = piece.DisplayName;
                box.transform.SetParent(group.transform, false);
                box.transform.localPosition = new Vector3(item.X, piece.SizeY / 2f, item.Z);
                box.transform.localScale = new Vector3(piece.SizeX, piece.SizeY, piece.SizeZ);

                var material = new Material(Shader.Find("Standard"));
                if (ColorUtility.TryParseHtmlString(piece.Tint, out var tint))
                {
                    material.color = tint;
                }

                box.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            var camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogWarning($"{scenePath} has no camera.");
                return;
            }

            var target = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var wasActive = RenderTexture.active;

            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            shot.Apply();

            RenderTexture.active = wasActive;
            camera.targetTexture = null;

            var path = Path.Combine(OutputFolder, fileName);
            File.WriteAllBytes(path, shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(group);

            Debug.Log($"[Scaling Laws] {tier}: {placed} of {order.Length} pieces stood up. "
                + $"Wrote {path}.");
        }
    }
}
