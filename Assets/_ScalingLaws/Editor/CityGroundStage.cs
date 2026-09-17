using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Everything the city drives or walks on, covered in real models: suburban lanes, park paths,
    /// the concrete driveway beside every house, and the bridges.
    ///
    /// The arterial roads and district grids were done first, by <see cref="CityRoadTileBuilder"/>,
    /// which re-derives their centrelines. Everything here is covered from the placeholder itself
    /// through <see cref="CityPropTiler"/> — see that file for why the second approach is the one
    /// worth keeping.
    ///
    /// Safe to run twice: each pass destroys the placeholders it covers, so a second run finds
    /// nothing left of what the first one did.
    /// </summary>
    public static class CityGroundStage
    {
        private const string Roads = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitRoads/Models/FBX format/";
        private const string Suburban = "Assets/_ScalingLaws/ThirdParty/Kenney/CityKitSuburban/Models/FBX format/";

        [MenuItem("Scaling Laws/Ground stage (lanes, driveways, paths, bridges)")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity",
                OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError("[Ground] No City.unity.");
                return;
            }

            if (CityRoadNetwork.Supersedes("Ground"))
            {
                return;
            }

            var cityRoot = GameObject.Find("City");
            var existing = GameObject.Find("GroundStage");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var root = new GameObject("GroundStage").transform;
            if (cityRoot != null)
            {
                root.SetParent(cityRoot.transform, false);
            }

            // Measured with KenneyModelAudit: road tiles run along their local X, the suburban
            // driveways and paths along their local Z.
            var roadTile = new CityPropTiler.Piece(Roads + "road-straight.fbx", lengthAlongX: true);
            var drivewayTile = new CityPropTiler.Piece(Suburban + "driveway-long.fbx", lengthAlongX: false);
            var pathTile = new CityPropTiler.Piece(Suburban + "path-long.fbx", lengthAlongX: false);
            var bridgeTile = new CityPropTiler.Piece(Roads + "road-bridge.fbx", lengthAlongX: true);

            var lanes = Group(root, "Lanes");
            var paths = Group(root, "Paths");
            var driveways = Group(root, "Driveways");
            var bridges = Group(root, "Bridges");

            // **The concrete driveway beside every house**, which is what the suburbs were missing:
            // a house on a plot with no way to reach it reads as a model dropped on grass.
            CityPropTiler.Replace(CityPropKind.Driveway, drivewayTile, driveways);

            // Anything six metres or wider that is still a road segment is a street somebody drives
            // down — the subdivision lanes and their cul-de-sac stems.
            CityPropTiler.Replace(CityPropKind.RoadSegment, roadTile, lanes, minimumWidth: 6f);

            // Narrower than that and it is a footpath through a park, which must not have lane
            // markings painted down the middle of it.
            CityPropTiler.Replace(CityPropKind.RoadSegment, pathTile, paths, maximumWidth: 6f);

            // The bridge deck carries its own railings, so it lifts a little higher than tarmac.
            CityPropTiler.Replace(CityPropKind.BridgeDeck, bridgeTile, bridges, lift: 0.04f);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Ground] Ground stage complete.");
        }

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }
    }
}
