using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The generated character rig: four clips, a controller and a prefab.
    ///
    /// **A muscle curve bound to a name that does not exist plays perfectly and moves nothing.**
    /// There is no error, no warning at runtime and no visible difference from a clip that works,
    /// which makes it exactly the kind of fault this project keeps shipping. The builder reads the
    /// muscle names out of `HumanTrait` rather than typing them for that reason, and this checks the
    /// result rather than the intention.
    /// </summary>
    public sealed class FounderRigTests
    {
        private const string Folder = "Assets/_ScalingLaws/Art/Character";

        private static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Folder}/Clips/{name}.anim");

        [Test]
        public void TheFourClipsExist()
        {
            foreach (var name in new[] { "Idle", "Walk", "Type", "Sleep" })
            {
                Assert.IsNotNull(Clip(name),
                    $"{name}.anim is missing. Run Scaling Laws > Characters > Build founder rig.");
            }
        }

        [Test]
        public void EveryCurveIsBoundToAMuscleUnityActuallyHas()
        {
            var muscles = new HashSet<string>(HumanTrait.MuscleName);
            var strays = new List<string>();
            var bound = 0;

            foreach (var name in new[] { "Idle", "Walk", "Type", "Sleep" })
            {
                var clip = Clip(name);
                if (clip == null)
                {
                    continue;
                }

                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    bound++;
                    if (!muscles.Contains(binding.propertyName))
                    {
                        strays.Add($"{name}: {binding.propertyName}");
                    }
                }
            }

            Assert.Greater(bound, 20, "Barely any curves were written, so the clips are empty.");
            CollectionAssert.IsEmpty(strays,
                "These curves are bound to nothing and will move nothing: " + string.Join(", ", strays));
        }

        [Test]
        public void TheWalkMovesBothLegsOutOfPhaseWithEachOther()
        {
            var walk = Clip("Walk");
            Assert.IsNotNull(walk);

            AnimationCurve left = null;
            AnimationCurve right = null;

            foreach (var binding in AnimationUtility.GetCurveBindings(walk))
            {
                if (binding.propertyName == "Left Upper Leg Front-Back")
                {
                    left = AnimationUtility.GetEditorCurve(walk, binding);
                }
                else if (binding.propertyName == "Right Upper Leg Front-Back")
                {
                    right = AnimationUtility.GetEditorCurve(walk, binding);
                }
            }

            Assert.IsNotNull(left, "No left leg curve, so this is not a walk.");
            Assert.IsNotNull(right);

            // A quarter of the way in, one leg is forward and the other is back. If both are the
            // same sign the character hops rather than walks, which is the classic mistake.
            var at = walk.length * 0.25f;
            Assert.AreNotEqual(Mathf.Sign(left.Evaluate(at)), Mathf.Sign(right.Evaluate(at)),
                "Both legs swing together, which is a hop.");
        }

        [Test]
        public void EveryClipLoops()
        {
            foreach (var name in new[] { "Idle", "Walk", "Type", "Sleep" })
            {
                var clip = Clip(name);
                if (clip == null)
                {
                    continue;
                }

                Assert.IsTrue(AnimationUtility.GetAnimationClipSettings(clip).loopTime,
                    $"{name} plays once and then the founder freezes.");
            }
        }

        [Test]
        public void ThePrefabIsAHumanoidWithTheActorOnIt()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_ScalingLaws/Resources/Character/Founder.prefab");
            Assert.IsNotNull(prefab, "Run Scaling Laws > Characters > Build founder rig.");

            var animator = prefab.GetComponent<Animator>();
            Assert.IsNotNull(animator, "No Animator, so no clip can ever play.");
            Assert.IsNotNull(animator.avatar, "No avatar, so muscle curves retarget onto nothing.");
            Assert.IsTrue(animator.avatar.isHuman, "The avatar is not humanoid.");
            Assert.IsNotNull(animator.runtimeAnimatorController);

            Assert.IsNotNull(prefab.GetComponent<ScalingLaws.UI.OfficeActor>(),
                "Nothing on the prefab walks it between waypoints.");

            Assert.IsFalse(animator.applyRootMotion,
                "The actor moves the transform. Root motion on top of that fights it.");
        }

        [Test]
        public void EveryStateInTheControllerHasSomethingToPlay()
        {
            // **A state with no motion plays nothing and looks exactly like a state whose clip is
            // simply still.** Three of them shipped empty the first time this was built, because
            // the Mixamo files were imported with CopyFromOther and produced no clip at all: the
            // FBX imported cleanly, showed 133 sub-assets, and not one was an animation.
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                $"{Folder}/Founder.controller");

            Assert.IsNotNull(controller, "Run Scaling Laws > Characters > Build founder rig.");

            var empty = new List<string>();
            foreach (var state in controller.layers[0].stateMachine.states)
            {
                if (state.state.motion == null)
                {
                    empty.Add(state.state.name);
                }
            }

            CollectionAssert.IsEmpty(empty,
                "These states freeze the founder: " + string.Join(", ", empty));

            Assert.GreaterOrEqual(controller.layers[0].stateMachine.states.Length, 7,
                "Seven states: idle, the two halves of walking, sitting down into typing, and lying "
                + "down into sleeping.");
        }

        [Test]
        public void TheDownloadedClipsAreTheOnesActuallyUsed()
        {
            // The generated placeholders are a fallback and must stay one. If a real clip exists for
            // a state and the controller is still on the placeholder, the import silently failed.
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                $"{Folder}/Founder.controller");

            Assert.IsNotNull(controller);

            var placeholders = new List<string>();
            foreach (var state in controller.layers[0].stateMachine.states)
            {
                var name = state.state.name;
                var motion = state.state.motion;
                if (motion == null)
                {
                    continue;
                }

                var onPlaceholder = AssetDatabase.GetAssetPath(motion).EndsWith($"/Clips/{name}.anim");

                // No exceptions any more. The standing idle arrived, so every one of the seven
                // states is on a real clip and a placeholder anywhere means an import failed.
                if (onPlaceholder)
                {
                    placeholders.Add(name);
                }
            }

            CollectionAssert.IsEmpty(placeholders,
                "Still on generated stand-ins, so the Mixamo import did not take: "
                + string.Join(", ", placeholders));
        }

        [Test]
        public void TheControllerHasTheOneParameterTheActorSets()
        {
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                $"{Folder}/Founder.controller");

            Assert.IsNotNull(controller);

            var found = false;
            foreach (var parameter in controller.parameters)
            {
                found |= parameter.name == "Walking"
                         && parameter.type == AnimatorControllerParameterType.Bool;
            }

            Assert.IsTrue(found, "OfficeActor sets a Walking bool and the controller has no such "
                + "parameter, so nothing it does reaches the model.");
        }

        [Test]
        public void TheEntryClipsHandOverToTheOnesThatRepeat()
        {
            // Sitting down is over when it is over, and typing takes it from there. Without the
            // handover the founder sits down and then stands frozen in the last frame of it.
            foreach (var task in new[] { ScalingLaws.Simulation.FounderTask.Working,
                                         ScalingLaws.Simulation.FounderTask.Resting })
            {
                var entry = ScalingLaws.Simulation.FounderRoutine.ClipFor(task);
                var rest = ScalingLaws.Simulation.FounderRoutine.RestingClipFor(task);

                Assert.AreNotEqual(entry, rest,
                    $"{task} plays the same clip on arrival and at rest, so there is no handover.");
            }
        }
    }
}
