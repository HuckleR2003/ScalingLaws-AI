using ScalingLaws.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Moves every map site's pin to where <see cref="MapSiteCatalog"/> now puts the site, and stands a
    /// pin for any site added to the catalog since the scene was built.
    ///
    /// The pins were stood up by <see cref="MapSiteBuilder"/> when the scene was first built. The
    /// catalog has moved sites since — the State Employment Register and the Tax Office off the
    /// river on 2026-09-15 — and <see cref="CitySiteBuildings"/> put their buildings at the new
    /// positions, which left both pins, and their footprint discs, standing in the river more than a
    /// hundred metres from the buildings they mark.
    ///
    /// Only the position and the footprint's size change; a pin keeps its colour, height and the
    /// site id the buildings pass gave it. Safe to run again: a pin already in place is left alone.
    /// </summary>
    public static class MapSitePins
    {
        [MenuItem("Scaling Laws/Map sites/Move and add pins to match the catalog")]
        public static void MoveToCatalog()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_ScalingLaws/Scenes/City.unity", OpenSceneMode.Single);
            var group = GameObject.Find("City")?.transform.Find("MapSites");

            if (!scene.IsValid() || group == null)
            {
                Debug.LogError("[Pins] No City.unity with a MapSites group.");
                return;
            }

            var moved = 0;
            var added = 0;

            foreach (var site in MapSiteCatalog.All)
            {
                var pin = group.Find(site.DisplayName);
                if (pin == null)
                {
                    // A site added to the catalog after the scene was built has no pin yet.
                    MapSiteBuilder.BuildPin(group, site, CityTerrainBuilder.HeightAt);
                    Debug.Log($"[Pins] {site.DisplayName}: new pin at ({site.Position.X:0}, {site.Position.Z:0})");
                    added++;
                    continue;
                }

                var target = new Vector3(site.Position.X,
                    CityTerrainBuilder.HeightAt(site.Position.X, site.Position.Z), site.Position.Z);

                if (Vector3.Distance(pin.position, target) < 0.5f)
                {
                    continue;
                }

                Debug.Log($"[Pins] {site.DisplayName}: ({pin.position.x:0}, {pin.position.z:0}) -> "
                    + $"({target.x:0}, {target.z:0})");

                // The marker and its disc are children placed in world space under the pin, so they
                // come with it, and keep their height above the ground it now stands on.
                pin.position = target;

                var footprint = pin.Find("Footprint");
                if (footprint != null && site.Radius > 0f)
                {
                    footprint.localScale = new Vector3(site.Radius * 2f, footprint.localScale.y, site.Radius * 2f);
                }

                moved++;
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Pins] {moved} pins moved to their catalog positions, {added} added for new sites.");
        }
    }
}
