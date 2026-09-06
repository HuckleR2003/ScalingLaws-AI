using System.Linq;
using NUnit.Framework;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The task strip rolls up into its own counter away from home.
    ///
    /// **Every task on it is something you do at the site.** Start the first research, release the
    /// first model, double the budget: there is nowhere else to do any of them. On the fleet screen
    /// or in the basement the strip was still spelling all of that out, at full weight, in the
    /// corner where the product and upgrade banners also live.
    ///
    /// So away from home it becomes the counter and nothing else, at 35% more transparent, and a
    /// click puts it back. The click is forgotten on the way home, because a strip left open by one
    /// click twenty minutes ago has stopped meaning anything by being on screen.
    /// </summary>
    public sealed class TaskBannerTests
    {
        private sealed class Rig
        {
            public Rig()
            {
                Host = new VisualElement();
                Company = new CompanyState("Corner");
                Guide = new GuideProgress { Stage = GuideStage.Touring };

                Banner = new TaskBanner(Host, () => Company, () => Guide, () => Changes++,
                    () => AtHome);
            }

            public VisualElement Host { get; }
            public CompanyState Company { get; }
            public GuideProgress Guide { get; }
            public TaskBanner Banner { get; }
            public int Changes { get; private set; }

            /// <summary>Which screen the player is standing on, as far as the strip can tell.</summary>
            public bool AtHome { get; set; } = true;

            public VisualElement Strip =>
                Host.Query<VisualElement>(className: "taskbar").ToList().FirstOrDefault();

            public int Rows =>
                Host.Query<VisualElement>(className: "taskbar__row").ToList().Count;

            public bool RolledUp =>
                Strip != null && Strip.ClassListContains("taskbar--rolled");
        }

        [Test]
        public void AtHomeTheStripSaysWhatToDo()
        {
            var rig = new Rig();
            rig.Banner.Refresh();

            Assert.That(rig.Strip, Is.Not.Null, "the strip did not draw at all");
            Assert.That(rig.RolledUp, Is.False);
            Assert.That(rig.Rows, Is.GreaterThan(0),
                "A player standing where the tasks are is the one case where the list is worth its "
                + "space, and it has nothing in it.");
        }

        [Test]
        public void AwayFromHomeItIsJustTheCounter()
        {
            var rig = new Rig();
            rig.Banner.Refresh();

            rig.AtHome = false;
            rig.Banner.Refresh();

            Assert.That(rig.RolledUp, Is.True);
            Assert.That(rig.Rows, Is.Zero,
                "The strip is still reading out instructions for a screen the player is not on.");

            Assert.That(
                rig.Host.Query<Label>(className: "taskbar__kicker").ToList(), Is.Not.Empty,
                "Rolling it up is not the same as hiding it. The counter is the part that says "
                + "there is still something waiting.");
        }

        [Test]
        public void ClickingTheCounterOpensIt()
        {
            var rig = new Rig { AtHome = false };
            rig.Banner.Refresh();

            Assert.That(rig.RolledUp, Is.True, "it should have started rolled up");

            rig.Banner.Toggle();

            Assert.That(rig.RolledUp, Is.False);
            Assert.That(rig.Rows, Is.GreaterThan(0), "it opened and there is nothing inside it");
        }

        /// <summary>
        /// Opening it by hand does not follow the player home and back out again.
        ///
        /// The strip only ever rolls up away from home, so an opened one that survived the trip
        /// would be indistinguishable from the old behaviour on the next screen they left for, and
        /// nothing in the corner would tell them why.
        /// </summary>
        [Test]
        public void ComingHomeForgetsThatItWasOpenedByHand()
        {
            var rig = new Rig { AtHome = false };
            rig.Banner.Refresh();

            rig.Banner.Toggle();

            Assert.That(rig.RolledUp, Is.False);

            rig.AtHome = true;
            rig.Banner.Refresh();

            rig.AtHome = false;
            rig.Banner.Refresh();

            Assert.That(rig.RolledUp, Is.True);
        }

        /// <summary>
        /// The rolled-up pill carries no dismiss cross.
        ///
        /// Two controls at that size are one control, and the one the player would hit by accident
        /// is the one that cannot be undone: `BannerDismissed` is set for the rest of the campaign.
        /// Opening it first costs a click and puts the cross back beside the tasks it throws away.
        /// </summary>
        [Test]
        public void TheRolledUpPillCannotBeDismissedByAccident()
        {
            var rig = new Rig { AtHome = false };
            rig.Banner.Refresh();

            Assert.That(rig.Host.Query<Button>(className: "taskbar__close").ToList(), Is.Empty);

            rig.Banner.Toggle();

            Assert.That(rig.Host.Query<Button>(className: "taskbar__close").ToList(), Is.Not.Empty,
                "and once it is open the cross has to come back, or a player away from home can "
                + "never be rid of it");
        }

        /// <summary>
        /// A dismissed strip stays dismissed on every screen.
        ///
        /// Rolling up is a second way for the strip to be smaller, and a second way for the "is it
        /// on screen at all" question to be answered wrongly.
        /// </summary>
        [Test]
        public void DismissingItStillEndsIt()
        {
            var rig = new Rig();
            rig.Guide.BannerDismissed = true;

            rig.Banner.Refresh();
            Assert.That(rig.Strip, Is.Null);

            rig.AtHome = false;
            rig.Banner.Refresh();
            Assert.That(rig.Strip, Is.Null);
        }
    }
}
