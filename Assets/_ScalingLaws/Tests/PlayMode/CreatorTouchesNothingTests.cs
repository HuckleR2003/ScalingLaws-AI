using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// Opening the model creator changes nothing the company is paying for.
    ///
    /// **Reported by Francisco on 2026-10-27:** "When i just enter the create model tab, even if i
    /// dont look at the compute tab, it autochanges my compute." He plays with owned hardware and
    /// nothing rented, which is the case the creator's own opening value is written for.
    ///
    /// This fixture is a measurement before it is a guard. The creator has three places that write
    /// the rent and all three are commented as deliberate, so reading the code produces a confident
    /// answer in either direction and that answer has been wrong twice this week. It opens the
    /// screen the way a player does and reads the pool on the other side.
    ///
    /// Both cases are measured because they take different paths through `OpeningRent`: a company
    /// renting nothing gets the creator's proposal, a company already renting gets its own figure.
    /// </summary>
    public sealed class CreatorTouchesNothingTests
    {
        private static IEnumerator OpenTheGame()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpeningTheCreatorLeavesTheRentAlone()
        {
            yield return OpenTheGame();

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            TabProofCampaign.Furnish(shell.Simulation);

            // What he has: parts bought, nothing rented.
            shell.Simulation.SetRentedPetaflops(0.0);
            shell.OpenScreenByName("Fleet");

            yield return null;

            var before = shell.Simulation.State.Pool.RentedPetaflops;

            Assert.That(shell.OpenScreenByName("Create"), Is.True, "There is no Create screen.");

            yield return null;
            yield return null;

            var after = shell.Simulation.State.Pool.RentedPetaflops;

            Debug.Log("[creator] renting nothing: " + before + " -> " + after);

            Assert.That(after, Is.EqualTo(before).Within(0.0001),
                "Opening the model creator changed the rented fleet from " + before + " to "
                + after + ". The screen is a plan until the player agrees to it, and a company "
                + "running on hardware it owns must not be billed for opening a tab.");
        }

        [UnityTest]
        public IEnumerator OpeningTheCreatorLeavesAnExistingRentAlone()
        {
            yield return OpenTheGame();

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            TabProofCampaign.Furnish(shell.Simulation);

            // A figure the player chose on the fleet page, which no other screen may overwrite.
            shell.Simulation.SetRentedPetaflops(640.0);
            shell.OpenScreenByName("Fleet");

            yield return null;

            var before = shell.Simulation.State.Pool.RentedPetaflops;

            shell.OpenScreenByName("Create");

            yield return null;
            yield return null;

            var after = shell.Simulation.State.Pool.RentedPetaflops;

            Debug.Log("[creator] renting 640: " + before + " -> " + after);

            Assert.That(after, Is.EqualTo(before).Within(0.0001),
                "Opening the model creator moved the rent the player had set on the fleet page, "
                + "from " + before + " to " + after + ".");
        }

        /// <summary>
        /// Once a run has been started, the creator still does not overwrite a rent set elsewhere.
        ///
        /// **The two cases above both passed, which rules out the obvious reading and not the
        /// report.** The creator only writes the rent once the player has agreed to a fleet, and
        /// until that happens its figures are a proposal. Francisco has been playing for weeks and
        /// has certainly started runs, so the path his report lives on is the committed one, and
        /// neither case above reaches it.
        ///
        /// The scenario is his: agree to a fleet in the creator, change the rent on the fleet page
        /// afterwards, then go back into the creator and move between its pages. If the creator
        /// writes its own stale handle back, the figure the player set on the other screen is gone
        /// and nothing said so.
        ///
        /// Reflection, because there is no honest public way to say "the player has already agreed
        /// to a fleet" and adding one for a test would be a second way to set it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCreatorDoesNotWriteBackAStaleRentAfterTheRentChangesElsewhere()
        {
            yield return OpenTheGame();

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            TabProofCampaign.Furnish(shell.Simulation);

            var creator = shell.Creator;
            Assert.That(creator, Is.Not.Null, "The shell has no creator on it.");

            var committed = typeof(ScalingLaws.UI.ModelCreatorPanel)
                .GetField("rentCommitted", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(committed, Is.Not.Null,
                "ModelCreatorPanel has no rentCommitted field any more. If the way the creator "
                + "agrees to a fleet has changed, this fixture has to change with it rather than "
                + "quietly stop testing.");

            // The player sizes a run and starts it: from here the creator owns a fleet.
            shell.Simulation.SetRentedPetaflops(900.0);
            shell.OpenScreenByName("Create");

            yield return null;

            committed.SetValue(creator, true);

            // Then they go to the fleet page and change their mind about the rent.
            shell.OpenScreenByName("Fleet");
            shell.Simulation.SetRentedPetaflops(150.0);

            yield return null;

            var before = shell.Simulation.State.Pool.RentedPetaflops;

            // And come back into the creator, and move around in it.
            shell.OpenScreenByName("Create");

            yield return null;

            creator.Refresh();

            yield return null;
            yield return null;

            var after = shell.Simulation.State.Pool.RentedPetaflops;

            Debug.Log("[creator] committed, rent changed elsewhere: " + before + " -> " + after);

            Assert.That(after, Is.EqualTo(before).Within(0.0001),
                "The creator wrote its own handle back over the rent the player had set on the "
                + "fleet page, moving it from " + before + " to " + after + ". The creator may "
                + "spend what the player agreed to in it, and may not undo a decision made on "
                + "another screen.");
        }
        /// <summary>
        /// Walking the pages of the creator, which is what a player actually does in there.
        ///
        /// **The three paths above open the creator and stop.** Francisco said on 2026-11-01 that
        /// the number that moved was "the compute I rent from the slider thing", and the rent
        /// handle is configured on the COMPUTE page rather than when the screen opens.
        /// `ConfigureSlider` writes `lowValue` and `highValue` every single time that page is
        /// built, and a Slider clamps its value into whatever range it is handed. If that ever
        /// clamps, the change callback fires, which sets `rentCommitted` and reprices, and
        /// repricing writes the handle to the company.
        ///
        /// So this turns every page, twice, with the fleet committed, and fails on any movement at
        /// all. If it stays green then the report is somewhere this has not looked, and the next
        /// step is one more question rather than a fourth guess.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingEveryPageOfTheCreatorLeavesTheRentAlone()
        {
            yield return OpenTheGame();

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            TabProofCampaign.Furnish(shell.Simulation);

            var creator = shell.Creator;
            Assert.That(creator, Is.Not.Null, "The shell has no creator on it.");

            var committed = typeof(ScalingLaws.UI.ModelCreatorPanel)
                .GetField("rentCommitted", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(committed, Is.Not.Null, "rentCommitted is gone; this fixture must change.");

            shell.Simulation.SetRentedPetaflops(640.0);
            shell.OpenScreenByName("Create");

            yield return null;

            committed.SetValue(creator, true);

            var before = shell.Simulation.State.Pool.RentedPetaflops;
            var moved = new System.Collections.Generic.List<string>();

            for (var pass = 0; pass < 2; pass++)
            {
                for (var stage = 0; stage < ScalingLaws.UI.ModelCreatorPanel.StageCount; stage++)
                {
                    creator.Stage = stage;

                    yield return null;

                    var now = shell.Simulation.State.Pool.RentedPetaflops;

                    if (System.Math.Abs(now - before) > 0.0001)
                    {
                        moved.Add($"pass {pass}, stage {stage}: {before:N1} -> {now:N1}");
                        before = now;
                    }
                }
            }

            Debug.Log("[creator] walked every page, rent moved " + moved.Count + " times");

            Assert.That(moved, Is.Empty,
                "Turning a page in the creator changed the company fleet. The creator describes a "
                + "plan; only the rent handle commits one, and only when the player moves it. "
                + "Movements: " + string.Join(" | ", moved));
        }

        /// <summary>
        /// The one that reproduces it. The four above are all below the fault's threshold.
        ///
        /// **Report 10, found 2026-11-01 after he said he could not remember what he was doing but
        /// had been playing for a while.** That answer is what pointed here: every path measured so
        /// far starts on a fresh screen, and this one needs a fleet larger than the interface will
        /// offer, which a company only reaches late and only through the unblock button.
        ///
        /// `SmallestCapacityThatWorks` walks capacity to 400,000 petaflops to find the smallest
        /// fleet that clears a blocked run, and the unblock button commits whatever it finds.
        /// `RentReadout.FullScalePetaflops` is 40,000, and `CeilingPetaflops` used to clamp to
        /// it after taking the company's own fleet as a floor, so the floor was thrown away. The
        /// rent handle is one shared instance that every reprice re-ranges, a Slider clamps into
        /// the range it is handed, and repricing writes the handle to the company. So the fleet was
        /// cut to 40,000 by opening the creator, with the compute page never shown.
        ///
        /// **The fleet has to be a committed one, and the first version of this test forgot that.**
        /// Written without it, the fixture passed against the unfixed ceiling and would have been
        /// quoted as proof that the report was closed: nothing was committed, so repricing wrote
        /// nothing, and the clamp had no way through to the company. The player who meets this has
        /// pressed the unblock button and then started the run, and starting a run is what agrees
        /// to the fleet it was costed against.
        /// </summary>
        [UnityTest]
        public IEnumerator ACommittedFleetLargerThanTheSliderOffersSurvivesTheCreator()
        {
            yield return OpenTheGame();

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            TabProofCampaign.Furnish(shell.Simulation);

            var creator = shell.Creator;
            Assert.That(creator, Is.Not.Null, "The shell has no creator on it.");

            var committed = typeof(ScalingLaws.UI.ModelCreatorPanel)
                .GetField("rentCommitted", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(committed, Is.Not.Null, "rentCommitted is gone; this fixture must change.");

            // Above the cap, which is the whole point, and a figure the unblock button can reach.
            const double rented = 65_536.0;

            shell.Simulation.SetRentedPetaflops(rented);
            shell.OpenScreenByName("Fleet");

            yield return null;

            var before = shell.Simulation.State.Pool.RentedPetaflops;

            Assert.That(before, Is.EqualTo(rented).Within(0.5),
                "The pool would not hold the fleet this test is about, so it measures nothing.");

            Assert.That(before, Is.GreaterThan(ScalingLaws.UI.RentReadout.FullScalePetaflops),
                "FullScalePetaflops has moved past the figure this fixture rents. Raise the rent "
                + "rather than letting the test quietly stop reaching the fault.");

            Assert.That(shell.OpenScreenByName("Create"), Is.True, "There is no Create screen.");

            yield return null;

            // The player pressed the unblock button and started the run: from here the fleet is
            // theirs and the creator is allowed to spend on it.
            committed.SetValue(creator, true);

            creator.Refresh();

            yield return null;
            yield return null;

            var afterOpening = shell.Simulation.State.Pool.RentedPetaflops;

            Debug.Log("[creator] committed past the cap, opened: " + before + " -> " + afterOpening);

            Assert.That(afterOpening, Is.EqualTo(before).Within(0.0001),
                "Opening the model creator cut the rented fleet from " + before + " to "
                + afterOpening + " without the compute page being opened. That is report 10.");

            // And the pages, because the handle is re-ranged every time the compute page is built.
            var moved = new System.Collections.Generic.List<string>();
            var last = afterOpening;

            for (var stage = 0; stage < ScalingLaws.UI.ModelCreatorPanel.StageCount; stage++)
            {
                creator.Stage = stage;

                yield return null;

                var now = shell.Simulation.State.Pool.RentedPetaflops;

                if (System.Math.Abs(now - last) > 0.0001)
                {
                    moved.Add($"stage {stage}: {last:N1} -> {now:N1}");
                    last = now;
                }
            }

            Assert.That(moved, Is.Empty,
                "Turning a page in the creator cut a fleet larger than the slider offers. "
                + "Movements: " + string.Join(" | ", moved));
        }

        /// <summary>
        /// The other half: the interface may not agree to a fleet on the player's behalf.
        ///
        /// **A clamp raises exactly the change event a drag raises, and nothing told them apart.**
        /// The rent handle's change event is consent, so any range that narrowed for any reason
        /// committed whatever the handle happened to be reading and the company started paying.
        ///
        /// The scenario needs no unblock button and no late game. A company parks the handle high
        /// while thinking about it, which is what the handle is for until it is moved, and then the
        /// fleet it actually rents drops. The ceiling follows the smaller of the two down, the
        /// parked handle clamps into it, and without the guard that clamp is read as a yes.
        ///
        /// Driven through the real panel rather than through the flag, because the flag is the fix
        /// and a test that sets it is a test of itself.
        /// </summary>
        [UnityTest]
        public IEnumerator AHandleParkedHighIsNotConsentWhenTheRangeShrinksUnderIt()
        {
            yield return OpenTheGame();

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            TabProofCampaign.Furnish(shell.Simulation);

            var creator = shell.Creator;
            Assert.That(creator, Is.Not.Null, "The shell has no creator on it.");

            var committed = typeof(ScalingLaws.UI.ModelCreatorPanel)
                .GetField("rentCommitted", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(committed, Is.Not.Null, "rentCommitted is gone; this fixture must change.");

            // A large fleet, so the handle opens on it, which is what the creator does on purpose.
            shell.Simulation.SetRentedPetaflops(65_536.0);
            shell.OpenScreenByName("Create");

            yield return null;
            yield return null;

            Assert.That((bool)committed.GetValue(creator), Is.False,
                "Opening the creator agreed to a fleet before this test had done anything.");

            // Then the fleet goes, somewhere that is not this screen.
            shell.OpenScreenByName("Fleet");
            shell.Simulation.SetRentedPetaflops(150.0);

            yield return null;

            var before = shell.Simulation.State.Pool.RentedPetaflops;

            Assert.That(before, Is.EqualTo(150.0).Within(0.5), "The fleet page would not take 150.");

            shell.OpenScreenByName("Create");

            yield return null;

            creator.Refresh();

            yield return null;
            yield return null;

            var after = shell.Simulation.State.Pool.RentedPetaflops;

            Debug.Log("[creator] parked handle, fleet shrank: " + before + " -> " + after);

            Assert.That((bool)committed.GetValue(creator), Is.False,
                "Nobody moved the rent handle and the creator has recorded the player as having "
                + "agreed to a fleet. From here the next reprice spends whatever the handle "
                + "happens to be reading.");

            Assert.That(after, Is.EqualTo(before).Within(0.0001),
                "The company was billed for a fleet it never asked for: " + before + " to "
                + after + ". The range shrank under a handle nobody had touched.");
        }
    }
}