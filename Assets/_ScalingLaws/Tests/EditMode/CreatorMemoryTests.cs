using System.Linq;
using NUnit.Framework;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// What the creator remembers when the player walks off a page and comes back.
    ///
    /// **Reported by a tester 1,391 days into a campaign**: *"if i set the parameters and tokens,
    /// and switch to, like, e.g. safety, but then decide to return to modify the parameters and
    /// tokens, they are reset to the initial stats, always, also sometimes, out of nowhere, the
    /// capacity just goes to 100% and no matter how much compute i rent or own, it refuses to back
    /// down, until it just suddenly does"*.
    ///
    /// Two symptoms, one cause, and the second one is much worse than the first. The controls are
    /// fields so that they survive a stage change, which is the right design and is written down as
    /// such. The panels that hold them are rebuilt on every visit, which is also right, because the
    /// numbers printed beside them have moved. But the builder configured the control as well as
    /// placing it, so every visit put the handle back where it opened.
    ///
    /// Repricing writes the rent slider to the company. So walking onto the COMPUTE page of the
    /// creator set the company's rented compute to 150 petaflops, whatever it had been, and the
    /// service pegged at a hundred per cent until something happened to resynchronise the handle.
    /// That is the "suddenly does" in the report.
    ///
    /// These drive the real panel, because the fault was in the panel and a test that drove the
    /// simulation would have passed the whole time.
    /// </summary>
    public sealed class CreatorMemoryTests
    {
        /// <summary>The SCALE page, where the parameter and token sliders live.</summary>
        private const int ScaleStage = 2;

        /// <summary>The SAFETY page, which is the one the report walked to.</summary>
        private const int SafetyStage = 5;

        /// <summary>The COMPUTE page, which is the one that costs money.</summary>
        private const int ComputeStage = 4;

        private static CompanySimulation Ready(double petaflops = 4000.0)
        {
            var simulation = new CompanySimulation(new CompanyState("Prometheus AI"));
            simulation.State.CashUsd = 5_000_000_000L;
            simulation.SetRentedPetaflops(petaflops);
            return simulation;
        }

        /// <summary>
        /// **The report, as arithmetic.** Set the scale, go to safety, come back, and it is still
        /// what was set.
        /// </summary>
        [Test]
        public void WalkingOffTheScalePageAndBackKeepsWhatWasSet()
        {
            var simulation = Ready();
            var panel = new ModelCreatorPanel(simulation) { Stage = ScaleStage };
            panel.Refresh();

            var sliders = panel.Root.Query<Slider>().ToList();

            Assume.That(sliders.Count, Is.GreaterThan(1),
                "the scale page has fewer than two sliders on it, so this measures nothing");

            // Away from wherever each one opened, and well inside its own range so nothing is
            // clamped on the way: a clamp would be the panel disagreeing with the company, which is
            // a different fault and has its own fixture.
            foreach (var slider in sliders)
            {
                slider.value = slider.lowValue + (slider.highValue - slider.lowValue) * 0.31f;
            }

            var chosen = sliders.Select(slider => slider.value).ToList();

            panel.Stage = SafetyStage;
            panel.Stage = ScaleStage;

            for (var index = 0; index < sliders.Count; index++)
            {
                Assert.That(sliders[index].value, Is.EqualTo(chosen[index]).Within(0.0001f),
                    "Slider " + index + " came back at " + sliders[index].value + " rather than the "
                    + chosen[index] + " the player set, so every trip to another page throws away "
                    + "the decision they just made.");
            }
        }

        /// <summary>
        /// **And the one that costs money.** Opening the compute page must not cancel the fleet.
        /// </summary>
        [Test]
        public void OpeningTheComputePageDoesNotCancelWhatTheCompanyRents()
        {
            var simulation = Ready();
            var panel = new ModelCreatorPanel(simulation);
            panel.Refresh();

            var before = simulation.State.Pool.RentedPetaflops;

            Assume.That(before, Is.GreaterThan(1000.0),
                "the fixture did not manage to rent anything, so there is nothing to lose");

            panel.Stage = ComputeStage;

            Assert.That(simulation.State.Pool.RentedPetaflops, Is.EqualTo(before).Within(1.0),
                "Walking onto the compute page took the company from " + before + " petaflops to "
                + simulation.State.Pool.RentedPetaflops + ". Nothing was clicked. That is the "
                + "service pegging at a hundred per cent for no reason the player can see.");
        }

        /// <summary>
        /// And walking the whole creator end to end changes nothing the player did not change.
        ///
        /// Held separately from the two above because it is the shape the report actually
        /// describes: somebody moving back and forth while they think, rather than one trip.
        /// </summary>
        [Test]
        public void WalkingEveryPageTwiceChangesNothingByItself()
        {
            var simulation = Ready();
            var panel = new ModelCreatorPanel(simulation) { Stage = ScaleStage };
            panel.Refresh();

            var sliders = panel.Root.Query<Slider>().ToList();
            Assume.That(sliders, Is.Not.Empty);

            foreach (var slider in sliders)
            {
                slider.value = slider.lowValue + (slider.highValue - slider.lowValue) * 0.44f;
            }

            var chosen = sliders.Select(slider => slider.value).ToList();
            var rented = simulation.State.Pool.RentedPetaflops;

            for (var pass = 0; pass < 2; pass++)
            {
                for (var stage = 0; stage <= 6; stage++)
                {
                    panel.Stage = stage;
                }
            }

            panel.Stage = ScaleStage;

            for (var index = 0; index < sliders.Count; index++)
            {
                Assert.That(sliders[index].value, Is.EqualTo(chosen[index]).Within(0.0001f),
                    "Slider " + index + " moved on its own while the player read the other pages.");
            }

            Assert.That(simulation.State.Pool.RentedPetaflops, Is.EqualTo(rented).Within(1.0),
                "The fleet changed size while the player read the pages of the creator.");
        }
    }
}
