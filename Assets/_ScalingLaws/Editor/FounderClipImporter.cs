using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.EditorTools
{
    /// <summary>
    /// Imports the Mixamo clips so they actually retarget onto the founder.
    ///
    /// **A Mixamo FBX is a rig plus one animation, and the rig is not the one the game uses.** Each
    /// one therefore needs its *own* humanoid avatar, and the clip then retargets onto any other
    /// humanoid avatar at runtime. That is what humanoid is for.
    ///
    /// **`CopyFromOther` is the trap here and it fails silently.** It is the right setting when the
    /// animation ships the same skeleton as the character, which is the case if you upload your
    /// model to Mixamo and download animations for it. These were downloaded on Mixamo's own rig
    /// while the character is a Rigify model out of an asset pack, so the bone names do not match,
    /// Unity cannot build the mapping, and it produces **no clip at all**. Not a broken clip, not a
    /// warning: the FBX imports fine, shows 133 sub-assets, and none of them is an animation. The
    /// controller then falls through to whatever placeholder exists and the founder T-poses.
    ///
    /// Three other things get set here and every one of them is a fault somebody hits by hand:
    ///
    /// - **The clip is renamed.** Every Mixamo export calls its clip `mixamo.com`, so six files give
    ///   six clips of the same name and the controller cannot tell them apart.
    /// - **Loop is set per clip, not globally.** Walking has to loop or the founder takes four steps
    ///   and freezes; sitting down must not, or they sit down forever.
    /// - **Materials are not imported.** Mixamo ships its own, and six copies of a grey placeholder
    ///   in the project is six chances to assign the wrong one.
    /// </summary>
    public static class FounderClipImporter
    {
        private const string Folder = "Assets/_ScalingLaws/Art/Character";

        /// <summary>
        /// What each downloaded file becomes, and whether it repeats.
        ///
        /// The key is matched against the file name so the exact Mixamo spelling does not matter.
        /// The value is the state name the controller and <see cref="Simulation.FounderRoutine"/>
        /// both use, which is why they are written once, here.
        /// </summary>
        public static readonly (string File, string Clip, bool Loops)[] Wanted =
        {
            ("walking", "Walk", true),
            ("start walking", "StartWalk", false),
            ("sitting", "SitDown", false),
            ("typing", "Type", true),
            ("lying down", "LieDown", false),
            ("sleeping idle", "Sleep", true)
        };

        [MenuItem("Scaling Laws/Characters/Import downloaded clips")]
        public static void ImportAll()
        {
            var avatarPath = FounderAvatarPath();
            if (avatarPath == null)
            {
                Debug.LogError("[Scaling Laws] No humanoid character to retarget onto. Run "
                    + "Scaling Laws > Characters > Set up humanoid rigs first.");
                return;
            }

            var done = new List<string>();
            var missing = new List<string>();

            foreach (var (fileWord, clipName, loops) in Wanted)
            {
                var path = FindFile(fileWord);
                if (path == null)
                {
                    missing.Add($"{clipName} (no file matching \"{fileWord}\")");
                    continue;
                }

                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                {
                    missing.Add($"{clipName} ({Path.GetFileName(path)} is not a model)");
                    continue;
                }

                importer.animationType = ModelImporterAnimationType.Human;

                // Its own avatar, built from its own skeleton. Humanoid clips are stored in muscle
                // space and retarget onto any humanoid avatar, so the founder does not need to share
                // a rig with them.
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = true;

                // **Two passes, and the order is the whole thing.** `defaultClipAnimations` is
                // derived from the takes Unity found on the last import, so reading it before the
                // rig has been reimported as humanoid returns an empty list. Assigning that empty
                // list back leaves the clip named `mixamo.com`, which is what every one of these
                // files calls it, and the controller then cannot tell six clips apart. It looks
                // like it worked: the settings are right in the inspector and the clip list is
                // simply blank.
                importer.SaveAndReimport();

                // Rename and set looping. Reading the existing clip list keeps whatever the
                // exporter said about the range; only the name and the loop are ours to decide.
                //
                // Already-configured clips first: `defaultClipAnimations` is the *auto-generated*
                // list and it comes back empty once a clip list has been set, so reading only that
                // makes the tool fail on its second run against files it configured correctly the
                // first time.
                var clips = importer.clipAnimations;
                if (clips.Length == 0)
                {
                    clips = importer.defaultClipAnimations;
                }
                if (clips.Length == 0)
                {
                    missing.Add($"{clipName} ({Path.GetFileName(path)} has no animation in it)");
                    continue;
                }

                var first = clips[0];
                first.name = clipName;
                first.loopTime = loops;

                // Mixamo bakes the travel into the clip unless "In Place" was ticked on the download.
                // The actor drives the transform, so the travel has to be thrown away or the founder
                // walks twice as far as the waypoint and slides back.
                first.lockRootHeightY = true;
                first.keepOriginalPositionY = true;

                importer.clipAnimations = new[] { first };
                importer.SaveAndReimport();

                done.Add($"{clipName} <- {Path.GetFileName(path)}");
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[Scaling Laws] Clips imported, retargeting onto "
                + $"{Path.GetFileName(avatarPath)}: "
                + $"{done.Count} ready, {missing.Count} missing."
                + (done.Count > 0 ? "\n  " + string.Join("\n  ", done) : string.Empty)
                + (missing.Count > 0 ? "\nMissing:\n  " + string.Join("\n  ", missing) : string.Empty));
        }

        /// <summary>The clip of a given name, wherever it now lives inside its FBX.</summary>
        public static AnimationClip Find(string clipName)
        {
            // Sub-assets of an FBX are only visible once the import has been flushed. Building the
            // controller in the same pass as an import otherwise silently falls through to the
            // generated placeholder, which is exactly what happened the first time.
            AssetDatabase.Refresh();

            foreach (var path in Directory.GetFiles(Folder, "*.fbx", SearchOption.AllDirectories))
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/')))
                {
                    if (asset is AnimationClip clip && clip.name == clipName)
                    {
                        return clip;
                    }
                }
            }

            // Fall back to the generated placeholders, so a clip that was not downloaded still has
            // something to play rather than leaving a hole in the state machine.
            return AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Folder}/Clips/{clipName}.anim");
        }

        private static string FindFile(string word)
        {
            string best = null;

            foreach (var file in Directory.GetFiles(Folder, "*.fbx", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (!name.Contains(word))
                {
                    continue;
                }

                // "walking" also matches "start walking", so the closest name wins rather than the
                // first one found.
                if (best == null || name.Length < Path.GetFileNameWithoutExtension(best).Length)
                {
                    best = file.Replace('\\', '/');
                }
            }

            return best;
        }

        /// <summary>The avatar the founder prefab uses, which is what everything must retarget onto.</summary>
        private static string FounderAvatarPath()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_ScalingLaws/Resources/Character/Founder.prefab");
            var animator = prefab != null ? prefab.GetComponent<Animator>() : null;

            if (animator != null && animator.avatar != null)
            {
                var path = AssetDatabase.GetAssetPath(animator.avatar);
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }

            foreach (var path in CharacterRigSetup.CharacterFiles())
            {
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                if (avatar != null && avatar.isValid && avatar.isHuman)
                {
                    return path;
                }
            }

            return null;
        }
    }
}
