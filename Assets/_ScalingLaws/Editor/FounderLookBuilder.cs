using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.EditorTools
{
    /// <summary>
    /// One prefab per face the player can pick, put where the game can load it.
    ///
    /// **The character packs are gitignored and live outside `Resources/`**, so nothing in them can
    /// be loaded at runtime. The portrait needs a live model rather than a photograph, which means
    /// each one has to exist as a prefab somewhere `Resources.Load` can reach. That is the whole job
    /// of this file.
    ///
    /// Prefabs are named `look_00` upward in a stable sorted order, and the **name is what the save
    /// records**, never the index: dropping another pack into the project would otherwise renumber
    /// everybody and every existing campaign would wake up as a different person.
    /// </summary>
    public static class FounderLookBuilder
    {
        public const string LookFolder = "Assets/_ScalingLaws/Resources/Character/Looks";

        /// <summary>
        /// Files that are animation, not people.
        ///
        /// The six Mixamo downloads sit in the character folder and import as valid humanoids, so
        /// they pass every check a character passes and would otherwise appear in the portrait as
        /// six more identical grey mannequins.
        /// </summary>
        private static readonly string[] NotFaces =
        {
            "walking", "sitting", "typing", "lying", "sleeping", "standing", "idle", "founder"
        };

        [MenuItem("Scaling Laws/Characters/Build portrait looks")]
        public static void Build()
        {
            Directory.CreateDirectory(LookFolder);

            // Prefabs and the materials repainted beside them. Clearing only the prefabs left
            // orphaned .mat files behind every time the roster changed, and they are named after
            // looks that no longer exist.
            foreach (var pattern in new[] { "*.prefab", "*.mat" })
            {
                foreach (var stale in Directory.GetFiles(LookFolder, pattern))
                {
                    AssetDatabase.DeleteAsset(stale.Replace("\\", "/"));
                }
            }

            var models = new List<string>();
            foreach (var path in CharacterRigSetup.CharacterFiles())
            {
                var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                var isAnimation = false;
                foreach (var word in NotFaces)
                {
                    isAnimation |= name.Contains(word);
                }

                if (isAnimation)
                {
                    continue;
                }

                // **CharCrafter is a URP pack and this project is on the built-in pipeline.**
                // Repainting its materials gets the clothes back but not the faces: the heads come
                // out blank and some accessories stay magenta, because a URP material carries more
                // than a colour and a texture and there is nothing honest to map the rest onto.
                //
                // Five blank faces in a chooser is worse than nine good ones, so it is left out
                // until URP is turned on, which is a decision the working notes already record as
                // pending. Nothing else about the pack is wasted: its models still animate and are
                // still available for the office crowd, where they are eight pixels tall.
                if (path.Contains("CharCrafter"))
                {
                    continue;
                }

                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
                if (avatar != null && avatar.isValid && avatar.isHuman)
                {
                    models.Add(path);
                }
            }

            // Sorted so the numbering is the same on every machine and every rebuild.
            models.Sort(string.CompareOrdinal);

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_ScalingLaws/Art/Character/Founder.controller");

            var built = new List<string>();

            for (var index = 0; index < models.Count; index++)
            {
                var path = models[index];
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);

                var root = new GameObject($"look_{index:00}");
                var body = Object.Instantiate(model, root.transform);
                body.name = "Body";
                body.transform.localPosition = Vector3.zero;

                foreach (var stray in body.GetComponentsInChildren<Animator>())
                {
                    Object.DestroyImmediate(stray);
                }

                var animator = root.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                // Culling off. The portrait camera renders a layer the main camera never sees, and
                // a culled animator stops updating the moment nothing thinks it is visible, which
                // leaves the model frozen in its bind pose in the one place it is being looked at.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                Repaint(body);

                // The name is read before the object goes. Reading it afterwards throws a
                // MissingReferenceException, which is how the first run built exactly one prefab.
                var lookName = root.name;

                PrefabUtility.SaveAsPrefabAsset(root, $"{LookFolder}/{lookName}.prefab");
                Object.DestroyImmediate(root);

                built.Add($"{lookName} <- {Path.GetFileNameWithoutExtension(path)}");
            }

            BuildGlasses();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Scaling Laws] {built.Count} portrait looks built.\n  "
                + string.Join("\n  ", built));
        }

        /// <summary>
        /// Drags materials back onto a shader this project can actually render.
        ///
        /// **CharCrafter ships URP materials and this project is on the built-in pipeline**, where a
        /// URP shader renders magenta rather than failing. Five of the fourteen portraits came out as
        /// bright pink silhouettes and nothing in the console said a word: it is the same fault that
        /// made the first office room build magenta, recorded in the working notes at the time.
        ///
        /// The colour and the main texture are carried across. Everything else in a URP material has
        /// no built-in equivalent worth guessing at, and these are flat-shaded low poly people whose
        /// materials are a colour and nothing else.
        /// </summary>
        private static void Repaint(GameObject person)
        {
            var standard = Shader.Find("Standard");
            if (standard == null)
            {
                return;
            }

            foreach (var renderer in person.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;

                for (var index = 0; index < materials.Length; index++)
                {
                    var material = materials[index];
                    if (material == null)
                    {
                        continue;
                    }

                    var shader = material.shader;
                    var broken = shader == null
                                 || shader.name.Contains("Universal Render Pipeline")
                                 || shader.name == "Hidden/InternalErrorShader";

                    if (!broken)
                    {
                        continue;
                    }

                    var colour = material.HasProperty("_BaseColor")
                        ? material.GetColor("_BaseColor")
                        : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

                    var texture = material.HasProperty("_BaseMap")
                        ? material.GetTexture("_BaseMap")
                        : material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;

                    var rebuilt = new Material(standard) { name = material.name + " (built-in)" };
                    rebuilt.SetColor("_Color", colour);
                    if (texture != null)
                    {
                        rebuilt.SetTexture("_MainTex", texture);
                    }

                    // Flat and matte. These are low poly people and a default half-metallic
                    // Standard material makes them look like they are made of wet plastic.
                    rebuilt.SetFloat("_Glossiness", 0.08f);
                    rebuilt.SetFloat("_Metallic", 0f);

                    AssetDatabase.CreateAsset(rebuilt,
                        $"{LookFolder}/{person.transform.parent.name}_{index}_{material.name}.mat");

                    materials[index] = rebuilt;
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        /// <summary>
        /// The glasses, as prefabs beside the faces.
        ///
        /// They are props rather than characters: generic rigs, no avatar, parented to the head bone
        /// at runtime. Built here so the portrait can load them by name like everything else.
        /// </summary>
        private static void BuildGlasses()
        {
            var index = 0;

            foreach (var path in Directory.GetFiles("Assets/Dudes/Models", "glasses*.fbx"))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace("\\", "/"));
                if (model == null)
                {
                    continue;
                }

                var root = new GameObject($"glasses_{index:00}");
                var body = Object.Instantiate(model, root.transform);
                body.name = "Body";
                body.transform.localPosition = Vector3.zero;

                var glassesName = root.name;
                PrefabUtility.SaveAsPrefabAsset(root, $"{LookFolder}/{glassesName}.prefab");
                Object.DestroyImmediate(root);

                index++;
            }
        }
    }
}
