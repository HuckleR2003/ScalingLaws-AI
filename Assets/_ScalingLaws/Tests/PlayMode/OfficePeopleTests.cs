using System.Collections;
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
    /// People are not furniture, and the room may not switch them off.
    ///
    /// **Reported as the founder losing their name plate after moving into a rented floor.** The
    /// plate is two `TextMesh` objects and a quad, a `TextMesh` carries a `MeshRenderer`, and
    /// `Staff` is a child of the house, so hiding the house's geometry under a loaded room took
    /// the name with it. The body survived because a character is a `SkinnedMeshRenderer`, which
    /// that sweep never touched: what the player saw was a person with no name rather than no
    /// person, which reads as a bug in the plate.
    ///
    /// The staff kept theirs by accident of timing. They are respawned whenever the roster
    /// changes, which is after the move, so their plates were built into an already-hidden room
    /// and enabled by default. One fault, two symptoms, and only one of them visible.
    /// </summary>
    public sealed class OfficePeopleTests
    {
        [UnityTest]
        public IEnumerator APersonKeepsTheirNameWhenTheCompanyMoves()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var root = GameObject.Find("OfficeRoom");
            Assume.That(root, Is.Not.Null, "the office room is not in the scene");

            var people = root.transform.Find(OfficeStage.PeopleGroup);
            Assume.That(people, Is.Not.Null,
                "the room has no Staff group, so this fixture is measuring nothing");

            // A stand-in rather than the founder prefab, because what is being tested is the
            // sweep's opinion of anything standing in that group: the plate is a plain mesh and
            // so is this.
            var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plate.name = "NamePlateStandIn";
            plate.transform.SetParent(people, false);
            plate.transform.localPosition = new Vector3(2f, 2f, 2f);

            var stage = new OfficeStage(root);
            Assume.That(stage.IsLive, Is.True);

            stage.Show(OfficeTier.Garage, null);

            for (var frame = 0; frame < 2; frame++)
            {
                yield return null;
            }

            Assume.That(plate.GetComponent<MeshRenderer>().enabled, Is.True,
                "the stand-in was already switched off in the room it was made in");

            stage.Show(OfficeTier.Loft, null);

            for (var frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            Assert.That(plate.GetComponent<MeshRenderer>().enabled, Is.True,
                "Moving into a rented floor switched off something standing in the Staff group, "
                + "which is what took the founder's name away.");
        }
    }
}
