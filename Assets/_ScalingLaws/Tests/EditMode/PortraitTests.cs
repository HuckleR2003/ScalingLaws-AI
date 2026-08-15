using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The face the player picks, and whether it survives to the room and back out of a save.
    ///
    /// **A founder who changes face when a campaign is reloaded is a different person**, and the
    /// player chose this one. That is the whole reason a piece of presentation is written into the
    /// save at all.
    /// </summary>
    public sealed class PortraitTests
    {
        private static List<GameObject> Looks()
        {
            var found = new List<GameObject>();
            foreach (var loaded in Resources.LoadAll<GameObject>(PortraitStudio.LookFolder))
            {
                if (loaded.name.StartsWith("look_"))
                {
                    found.Add(loaded);
                }
            }

            return found;
        }

        [Test]
        public void ThereAreEnoughFacesToBeWorthChoosingBetween()
        {
            var looks = Looks();

            Assert.GreaterOrEqual(looks.Count, 8,
                "The author asked for more than seven. Run Scaling Laws > Characters > Build "
                + $"portrait looks. Found {looks.Count}.");

            // Every one has to be renderable in this pipeline. The URP pack is deliberately not
            // here: repainting its materials recovers the clothes and not the faces.
            foreach (var look in looks)
            {
                foreach (var renderer in look.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.IsFalse(
                            material != null && material.shader != null
                            && material.shader.name.Contains("Universal Render Pipeline"),
                            $"{look.name} would render magenta on the built-in pipeline.");
                    }
                }
            }
        }

        [Test]
        public void EveryFaceIsAHumanoidThatCanActuallyBeAnimated()
        {
            // A look with no avatar renders as a T-pose in the portrait and walks nowhere in the
            // office, and neither failure says anything at all.
            var broken = new List<string>();

            foreach (var look in Looks())
            {
                var animator = look.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman
                    || animator.runtimeAnimatorController == null)
                {
                    broken.Add(look.name);
                }
            }

            CollectionAssert.IsEmpty(broken, "These would T-pose: " + string.Join(", ", broken));
        }

        [Test]
        public void TheAnimatorKeepsRunningWhenNothingThinksItIsVisible()
        {
            // The portrait camera renders a face nothing else is looking at. On the default culling
            // mode the animator stops the moment it is off the main camera, which leaves the model
            // frozen in its bind pose in the one place it is being looked at.
            foreach (var look in Looks())
            {
                var animator = look.GetComponent<Animator>();
                Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode, look.name);
            }
        }

        [Test]
        public void ThereIsSomethingToPutOnTheirFace()
        {
            var pairs = 0;
            foreach (var loaded in Resources.LoadAll<GameObject>(PortraitStudio.LookFolder))
            {
                if (loaded.name.StartsWith("glasses_"))
                {
                    pairs++;
                }
            }

            Assert.Greater(pairs, 0, "The GLASSES chooser would have one option, which is not a choice.");
        }

        // ---- it has to survive the round trip ---------------------------------------------------

        [Test]
        public void TheChosenFaceComesBackOutOfASave()
        {
            var looks = Looks();
            Assert.IsNotEmpty(looks);

            var chosen = looks[looks.Count - 1].name;

            var state = new CompanyState("Prometheus AI")
            {
                FounderLook = chosen,
                FounderGlasses = 1
            };

            var restored = SaveStore.Restore(SaveStore.Capture(state));

            Assert.AreEqual(chosen, restored.FounderLook,
                "The founder woke up as somebody else.");
            Assert.AreEqual(1, restored.FounderGlasses);
        }

        [Test]
        public void TheLookIsSavedByNameSoAnotherPackCannotRenameEverybody()
        {
            // An index would be four bytes and a landmine: dropping another character pack in
            // renumbers every look and every existing campaign silently becomes somebody else.
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "_ScalingLaws", "Scripts", "Persistence", "SaveData.cs"));

            StringAssert.Contains("public string founderLook", source,
                "The look has to be saved as a name, not as an index.");
        }

        [Test]
        public void ACampaignStartedBeforeTheresAPortraitKeepsTheDefault()
        {
            var old = new SaveData { version = 25 };
            var moved = SaveMigration.UpgradeV25ToV26(old);

            Assert.AreEqual(26, moved.version);
            Assert.IsEmpty(moved.founderLook,
                "Inventing a face the player never chose and then claiming they chose it is exactly "
                + "what the migration rule forbids.");
        }
    }
}
