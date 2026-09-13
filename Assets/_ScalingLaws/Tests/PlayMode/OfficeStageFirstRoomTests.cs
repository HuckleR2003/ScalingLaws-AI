using System.Collections;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// The first room a campaign shows must be visible, even when it is not the house.
    ///
    /// **Reported as critical: loading a save put the player in an office that was not there.**
    /// A wall, four boxes of furniture and a bench, no floor, no desks. Nothing in the player log,
    /// no exception, because nothing failed: the room hid itself.
    ///
    /// `OfficeStage.Extras` sweeps every renderer in the scene that is not part of the house and
    /// stands inside the house's own box, remembers them, and dims them whenever a room is loaded
    /// over the top. A loaded room is instantiated at exactly the house's position, so every piece
    /// of it is inside that box and none of it is a child of the house. If the sweep runs for the
    /// first time while a room is already loaded, it collects that room as somebody else's
    /// furniture.
    ///
    /// **A new campaign could never hit it.** It opens in the garage, which is not a loaded room,
    /// so the sweep ran against an empty stage and cached the answer before the company ever moved.
    /// Loading a save into an office hit it every time, which is why `TabProofTests` and
    /// `MovingOfficeLeavesNoneOfTheOldRoomOnScreen` were both green the whole time: both of them
    /// start in the garage and move.
    ///
    /// This fixture drives `OfficeStage` directly rather than through a saved campaign, because a
    /// test that wrote a save would overwrite the player's own: `SaveStore` is `PlayerPrefs`, and
    /// the editor and the built game share it.
    /// </summary>
    public sealed class OfficeStageFirstRoomTests
    {
        [UnityTest]
        public IEnumerator ARoomShownFirstDoesNotHideItself()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var root = GameObject.Find("OfficeRoom");
            Assume.That(root, Is.Not.Null, "the office room is not in the scene");

            // **A stage of its own, which has never shown anything.** The shell's stage has already
            // opened the garage by now, so its sweep is cached and the fault is out of reach
            // through it. This is the state a loaded save arrives in: a brand new stage whose very
            // first instruction is a tier with a room prefab behind it.
            var stage = new OfficeStage(root);

            Assume.That(stage.IsLive, Is.True);

            stage.Show(OfficeTier.Loft, null);

            for (var frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            var pieces = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer != null
                    && renderer.GetComponentsInParent<Transform>(true)
                        .Any(step => step.name.Contains("SmallHub")))
                .ToList();

            Assume.That(pieces.Count, Is.GreaterThan(10),
                "the small hub never loaded, so this fixture is measuring nothing");

            var lit = pieces.Count(renderer => renderer.enabled);

            // Every piece of it, not most of it. There is no rule that dims part of a room the
            // company is standing in, so anything dark here is the fault coming back.
            Assert.That(lit, Is.EqualTo(pieces.Count),
                $"{pieces.Count - lit} of {pieces.Count} pieces of the office the company is "
                + "standing in are switched off, which is the room having hidden itself.");
        }

        /// <summary>
        /// And the sweep still does its real job: the hand-placed things in the house go dark.
        ///
        /// Without this the fix could be "never hide anything", which would leave the sofa and the
        /// vases from the garage lit and floating through the floor of a rented office. That was
        /// the fault the sweep was written for.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHouseStillGoesDarkUnderIt()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var root = GameObject.Find("OfficeRoom");
            Assume.That(root, Is.Not.Null);

            var stage = new OfficeStage(root);
            stage.Show(OfficeTier.Loft, null);

            for (var frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            var house = root.GetComponentsInChildren<MeshRenderer>(true);

            Assume.That(house.Length, Is.GreaterThan(0), "the house has no geometry to hide");

            Assert.That(house.Count(renderer => renderer.enabled), Is.Zero,
                "The house is still drawn underneath the office the company moved into, which is "
                + "two rooms in one frame.");
        }
    }
}
