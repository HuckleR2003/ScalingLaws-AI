using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ScalingLaws.EditorTools
{
    /// <summary>
    /// Makes the imported characters move, without Mixamo and without hand animation.
    ///
    /// **The packs ship a model and no animation.** That is the normal state of a character pack and
    /// it is why they look like a dead end. They are not: once the rig is Humanoid, a clip is just a
    /// set of curves over Unity's muscle space, and muscle space is the same on every humanoid avatar
    /// that has ever existed. A walk is the legs out of phase, the arms out of phase with the legs,
    /// and a little counter-rotation in the spine. That is four sine waves.
    ///
    /// **These are not good animations and they are not meant to be.** They are the difference
    /// between a scene where nothing moves and a scene where the founder walks to bed, and they exist
    /// so the room can be built, watched and judged before anybody downloads anything. Every clip
    /// this writes is a normal `.anim` asset: dropping a Mixamo clip of the same name over the top
    /// replaces it and nothing else has to change.
    ///
    /// **Muscle names are read from `HumanTrait` rather than typed.** They differ across Unity
    /// versions and a typo produces a clip that plays perfectly and moves nothing.
    /// </summary>
    public static class FounderRigBuilder
    {
        private const string Folder = "Assets/_ScalingLaws/Art/Character";
        private const string ClipFolder = Folder + "/Clips";

        /// <summary>Samples a second. Twelve is enough for a walk and keeps the curves readable.</summary>
        private const int Fps = 12;

        [MenuItem("Scaling Laws/Characters/Build founder rig and clips")]
        public static void Build()
        {
            Directory.CreateDirectory(ClipFolder);

            var clips = new Dictionary<string, AnimationClip>
            {
                ["Idle"] = BuildIdle(),
                ["Walk"] = BuildWalk(),
                ["Type"] = BuildType(),
                ["Sleep"] = BuildSleep()
            };

            foreach (var (name, clip) in clips)
            {
                Save(clip, $"{ClipFolder}/{name}.anim");
            }

            var controller = BuildController(clips);
            var prefab = BuildPrefab(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Scaling Laws] Founder rig built. {clips.Count} clips in {ClipFolder}, "
                + $"controller and prefab at {Folder}."
                + (prefab != null
                    ? "\nDrop the prefab into the office scene under Staff."
                    : "\nNo humanoid model found, so the prefab was not built. Run "
                      + "Scaling Laws > Characters > Set up humanoid rigs first."));
        }

        // ---- muscle space ---------------------------------------------------------------------

        /// <summary>
        /// The real name of a muscle, matched on keywords.
        ///
        /// Typing "Left Upper Leg Front-Back" and getting the spacing wrong produces a curve bound to
        /// nothing: the clip plays, the model does not move, and there is no error anywhere. Asking
        /// Unity what its muscles are called cannot go wrong that way.
        /// </summary>
        private static string Muscle(params string[] words)
        {
            foreach (var name in HumanTrait.MuscleName)
            {
                var lower = name.ToLowerInvariant();
                var all = true;

                foreach (var word in words)
                {
                    all &= lower.Contains(word.ToLowerInvariant());
                }

                if (all)
                {
                    return name;
                }
            }

            Debug.LogWarning($"[Scaling Laws] No muscle matches {string.Join(" + ", words)}.");
            return null;
        }

        private static void Curve(AnimationClip clip, string muscle, params (float Time, float Value)[] keys)
        {
            if (string.IsNullOrEmpty(muscle))
            {
                return;
            }

            var curve = new AnimationCurve();
            foreach (var (time, value) in keys)
            {
                curve.AddKey(time, value);
            }

            for (var index = 0; index < curve.length; index++)
            {
                curve.SmoothTangents(index, 0f);
            }

            clip.SetCurve(string.Empty, typeof(Animator), muscle, curve);
        }

        /// <summary>A sine over one loop, which is what every cyclic muscle in a walk is.</summary>
        private static (float, float)[] Wave(float length, float amplitude, float phase)
        {
            var keys = new List<(float, float)>();

            for (var step = 0; step <= Fps; step++)
            {
                var time = length * step / Fps;
                var angle = (step / (float)Fps + phase) * Mathf.PI * 2f;
                keys.Add((time, Mathf.Sin(angle) * amplitude));
            }

            return keys.ToArray();
        }

        // ---- the clips ---------------------------------------------------------------------------

        private static AnimationClip BuildWalk()
        {
            // One second a cycle, two steps in it. Legs half a cycle apart, arms opposite their own
            // side's leg, which is the whole of what makes a walk read as a walk.
            const float length = 1.0f;
            var clip = new AnimationClip { name = "Walk" };

            Curve(clip, Muscle("left", "upper leg", "front-back"), Wave(length, 0.55f, 0f));
            Curve(clip, Muscle("right", "upper leg", "front-back"), Wave(length, 0.55f, 0.5f));

            // Knees only bend one way, so the stretch curve is offset rather than centred.
            Curve(clip, Muscle("left", "lower leg", "stretch"), Wave(length, 0.35f, 0.25f));
            Curve(clip, Muscle("right", "lower leg", "stretch"), Wave(length, 0.35f, 0.75f));

            Curve(clip, Muscle("left", "arm", "front-back"), Wave(length, 0.40f, 0.5f));
            Curve(clip, Muscle("right", "arm", "front-back"), Wave(length, 0.40f, 0f));

            Curve(clip, Muscle("left", "forearm", "stretch"), Wave(length, 0.18f, 0.5f));
            Curve(clip, Muscle("right", "forearm", "stretch"), Wave(length, 0.18f, 0f));

            // Twice a cycle: the body rises on each step, not once per pair of steps.
            Curve(clip, Muscle("spine", "left-right"), Wave(length, 0.10f, 0.25f));

            Loop(clip, true);
            return clip;
        }

        private static AnimationClip BuildIdle()
        {
            // Four seconds of breathing. Long and shallow, because a fast idle reads as a twitch.
            const float length = 4.0f;
            var clip = new AnimationClip { name = "Idle" };

            Curve(clip, Muscle("spine", "front-back"), Wave(length, 0.05f, 0f));
            Curve(clip, Muscle("chest", "front-back"), Wave(length, 0.04f, 0.1f));
            Curve(clip, Muscle("head", "nod"), Wave(length, 0.03f, 0.3f));

            Loop(clip, true);
            return clip;
        }

        private static AnimationClip BuildType()
        {
            // Sitting is a pose; typing is the pose plus small forearm movement. Held rather than
            // swung, because the hands are on a desk.
            const float length = 1.4f;
            var clip = new AnimationClip { name = "Type" };

            Curve(clip, Muscle("left", "upper leg", "front-back"), (0f, 0.95f), (length, 0.95f));
            Curve(clip, Muscle("right", "upper leg", "front-back"), (0f, 0.95f), (length, 0.95f));
            Curve(clip, Muscle("left", "lower leg", "stretch"), (0f, -0.85f), (length, -0.85f));
            Curve(clip, Muscle("right", "lower leg", "stretch"), (0f, -0.85f), (length, -0.85f));

            Curve(clip, Muscle("left", "arm", "front-back"), (0f, 0.42f), (length, 0.42f));
            Curve(clip, Muscle("right", "arm", "front-back"), (0f, 0.42f), (length, 0.42f));
            Curve(clip, Muscle("left", "forearm", "stretch"), Wave(length, 0.09f, 0f));
            Curve(clip, Muscle("right", "forearm", "stretch"), Wave(length, 0.09f, 0.4f));

            Curve(clip, Muscle("spine", "front-back"), (0f, 0.18f), (length, 0.18f));
            Curve(clip, Muscle("head", "nod"), (0f, 0.12f), (length, 0.12f));

            Loop(clip, true);
            return clip;
        }

        private static AnimationClip BuildSleep()
        {
            // Lying down is mostly root rotation, which a muscle clip cannot do, so this is the
            // shape of somebody asleep and the actor lays them on the bed by rotating the transform.
            const float length = 5.0f;
            var clip = new AnimationClip { name = "Sleep" };

            Curve(clip, Muscle("left", "upper leg", "front-back"), (0f, 0.22f), (length, 0.22f));
            Curve(clip, Muscle("right", "upper leg", "front-back"), (0f, 0.18f), (length, 0.18f));
            Curve(clip, Muscle("left", "arm", "front-back"), (0f, 0.25f), (length, 0.25f));
            Curve(clip, Muscle("right", "arm", "front-back"), (0f, 0.20f), (length, 0.20f));
            Curve(clip, Muscle("spine", "front-back"), Wave(length, 0.06f, 0f));
            Curve(clip, Muscle("head", "nod"), (0f, -0.25f), (length, -0.25f));

            Loop(clip, true);
            return clip;
        }

        private static void Loop(AnimationClip clip, bool looping)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = looping;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void Save(AnimationClip clip, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                // Overwrite in place so anything already pointing at this clip keeps pointing at it.
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
                return;
            }

            AssetDatabase.CreateAsset(clip, path);
        }

        // ---- the controller ----------------------------------------------------------------------

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            var path = $"{Folder}/Founder.controller";
            AssetDatabase.DeleteAsset(path);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();

            // Seven states. The downloaded clips are preferred and the generated ones are the
            // fallback, which is why Idle is still a placeholder: nobody downloaded a standing idle
            // and the room needs one for the moment between arriving and sitting down.
            foreach (var name in new[] { "Idle", "StartWalk", "Walk", "SitDown", "Type", "LieDown", "Sleep" })
            {
                var state = machine.AddState(name);

                var downloaded = FounderClipImporter.Find(name);
                state.motion = downloaded
                               ?? (clips.TryGetValue(name, out var generated) ? generated : null);

                // Said out loud, because a state with a null motion plays nothing and looks
                // identical to a state whose clip is simply still. Silence here is how three empty
                // states shipped the first time this ran.
                if (state.motion == null)
                {
                    Debug.LogWarning($"[Scaling Laws] {name} has no clip. The founder will freeze "
                        + "in that state.");
                }
                else
                {
                    Debug.Log($"[Scaling Laws]   {name}: {AssetDatabase.GetAssetPath(state.motion)}");
                }

                states[name] = state;
            }

            machine.defaultState = states["Idle"];

            // Walking is the one parameter. Every resting state can be interrupted by it, and each
            // transition is written out rather than using AnyState: an AnyState transition on a bool
            // re-fires the moment the walk cycle starts, so the founder restarts the first step
            // forever and never actually walks.
            foreach (var from in new[] { "Idle", "Type", "Sleep", "SitDown", "LieDown" })
            {
                var out_ = states[from].AddTransition(states["StartWalk"]);
                out_.AddCondition(AnimatorConditionMode.If, 0f, "Walking");
                out_.hasExitTime = false;
                out_.duration = 0.18f;
            }

            // The three that run themselves out and hand over. Pushing off into the walk cycle,
            // settling into typing, settling into sleep.
            Handover(states["StartWalk"], states["Walk"]);
            Handover(states["SitDown"], states["Type"]);
            Handover(states["LieDown"], states["Sleep"]);

            var stop = states["Walk"].AddTransition(states["Idle"]);
            stop.AddCondition(AnimatorConditionMode.IfNot, 0f, "Walking");
            stop.hasExitTime = false;
            stop.duration = 0.2f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// One state playing itself out and handing over to the one that repeats.
        ///
        /// Exit time rather than a condition, because these are the entries: sitting down is over
        /// when the sitting down is over, and nothing else decides that.
        /// </summary>
        private static void Handover(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 0.85f;
            transition.duration = 0.15f;
        }

        // ---- the prefab ---------------------------------------------------------------------------

        private static GameObject BuildPrefab(AnimatorController controller)
        {
            var modelPath = FirstHumanoidModel();
            if (modelPath == null)
            {
                return null;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelPath);
            if (model == null || avatar == null)
            {
                return null;
            }

            var root = new GameObject("Founder");
            var body = Object.Instantiate(model, root.transform);
            body.name = "Body";
            body.transform.localPosition = Vector3.zero;

            // The model may carry its own Animator from the importer. One is enough, and the one
            // that matters is the one on the root the actor moves.
            foreach (var stray in body.GetComponentsInChildren<Animator>())
            {
                Object.DestroyImmediate(stray);
            }

            var animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            root.AddComponent<ScalingLaws.UI.OfficeActor>();

            // Resources, because the shell spawns it rather than referencing it from a scene:
            // Game.unity holds 107 hand-placed prefab instances and a guard refuses to regenerate
            // it, so nothing new goes in there by hand if it can be put in by code.
            const string prefabFolder = "Assets/_ScalingLaws/Resources/Character";
            Directory.CreateDirectory(prefabFolder);

            var path = $"{prefabFolder}/Founder.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return saved;
        }

        /// <summary>
        /// The first model that actually has a humanoid avatar.
        ///
        /// Preferring the Dudes pack because the author asked for it: it is the varied one, and a
        /// varied pack is what an office full of people needs.
        /// </summary>
        private static string FirstHumanoidModel()
        {
            var files = CharacterRigSetup.CharacterFiles();

            files.Sort((left, right) =>
            {
                var leftDudes = left.Contains("/Dudes/") ? 0 : 1;
                var rightDudes = right.Contains("/Dudes/") ? 0 : 1;
                return leftDudes != rightDudes
                    ? leftDudes.CompareTo(rightDudes)
                    : string.CompareOrdinal(left, right);
            });

            foreach (var path in files)
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
