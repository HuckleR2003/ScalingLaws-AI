using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.EditorTools
{
    /// <summary>
    /// Turns the imported character packs into rigs the game can actually animate.
    ///
    /// **Every character pack in this project imports as Generic and none of them has to.** The
    /// Dudes pack is a complete Rigify biped, seventy two bones, skinned. CharCrafter is a forty
    /// bone biped, skinned. Both carry every bone a humanoid avatar needs and both arrive with
    /// `animationType: 2` because that is what the FBX exporter wrote and nobody changed it.
    ///
    /// Generic is the whole difference between "these models are useless for Mixamo" and "these
    /// models take any humanoid clip ever made". It is one enum on the importer, and doing it by
    /// hand across ten files is ten chances to miss one and then wonder why a clip does not retarget.
    ///
    /// The menu item is idempotent: a model already set to Humanoid with a working avatar is left
    /// alone, so this can be run after every pack import without touching what already works.
    /// </summary>
    public static class CharacterRigSetup
    {
        /// <summary>Folders that hold character FBXs. Add a pack by adding a line.</summary>
        private static readonly string[] Folders =
        {
            "Assets/Dudes/Models",
            "Assets/CharCrafter – Free Preset Characters Pack (Vol. 1)/BaseModel",
            "Assets/_ScalingLaws/Art/Character"
        };

        /// <summary>
        /// Meshes that are in a character folder and are not characters.
        ///
        /// The Dudes pack ships a sofa and two pairs of glasses next to its people. Running the
        /// humanoid mapper over a sofa fails loudly and leaves a broken avatar on the asset.
        /// </summary>
        private static readonly string[] NotPeople = { "sofa", "glasses", "prop", "hair" };

        [MenuItem("Scaling Laws/Characters/Set up humanoid rigs")]
        public static void SetUpAll()
        {
            var changed = new List<string>();
            var failed = new List<string>();
            var already = 0;

            foreach (var path in CharacterFiles())
            {
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                {
                    continue;
                }

                if (importer.animationType == ModelImporterAnimationType.Human)
                {
                    already++;
                    continue;
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

                // Optional bones left in means a hand with fingers keeps them. Mixamo clips animate
                // fingers and a rig that dropped them plays those channels into nothing.
                importer.optimizeGameObjects = false;

                try
                {
                    importer.SaveAndReimport();
                }
                catch (System.Exception exception)
                {
                    failed.Add($"{Path.GetFileName(path)}: {exception.Message}");
                    continue;
                }

                // Reimporting does not guarantee a usable avatar. Unity will happily produce an
                // invalid one when it cannot find the bones it needs, and every clip retargeted onto
                // it then plays as a T-pose. Read it back rather than trusting the setter, which is
                // the same lesson the panel settings taught this project twice.
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                if (avatar != null && avatar.isValid && avatar.isHuman)
                {
                    changed.Add(Path.GetFileName(path));
                }
                else
                {
                    failed.Add($"{Path.GetFileName(path)}: humanoid mapping did not take. Open the "
                        + "model, press Configure, and map the bones the mapper could not find.");
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[Scaling Laws] Humanoid rigs: {changed.Count} converted, {already} already "
                + $"human, {failed.Count} need a look."
                + (changed.Count > 0 ? $"\nConverted: {string.Join(", ", changed)}" : string.Empty)
                + (failed.Count > 0 ? $"\nNeed a look:\n  {string.Join("\n  ", failed)}" : string.Empty));
        }

        /// <summary>Every character FBX across the known packs.</summary>
        public static List<string> CharacterFiles()
        {
            var found = new List<string>();

            foreach (var folder in Folders)
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(folder, "*.fbx", SearchOption.AllDirectories))
                {
                    var path = file.Replace('\\', '/');
                    var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                    var isProp = false;
                    foreach (var word in NotPeople)
                    {
                        isProp |= name.Contains(word);
                    }

                    if (!isProp)
                    {
                        found.Add(path);
                    }
                }
            }

            return found;
        }
    }
}
