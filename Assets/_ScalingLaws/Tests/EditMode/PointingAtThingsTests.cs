using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Two things asked for on 2026-09-30, both about a control the player could not find.
    ///
    /// **The fan had been reported three times and moved once.** The first repair made the cabinet
    /// window three columns so the button stopped falling off the bottom of the screen, which fixed
    /// the symptom and left it where it was: in the right hand column, among the readings. The
    /// author asked for it at the head of the inventory on the left, and the reason it is the head
    /// rather than the foot is that the inventory grows with the company.
    ///
    /// **The banner had been reported as impossible to see.** A ring around something in a corner
    /// is not a signpost, so `AttentionPull` moves it to the middle and lets it walk home.
    /// </summary>
    public sealed class PointingAtThingsTests
    {
        /// <summary>
        /// The fan is in the inventory column, directly under its heading.
        ///
        /// **Asserted as position, not as presence.** A test that only asked whether the button
        /// exists passed through every build in which it was in the wrong place, including the one
        /// where it was drawn half off the bottom of the window.
        /// </summary>
        [Test]
        public void TheFanSitsAtTheHeadOfTheInventoryAndNotAmongTheReadings()
        {
            var before = Loc.Current;

            try
            {
                Loc.Current = Language.English;

                var simulation = CompanyWithARoom();
                var panel = new RackEditorPanel(() => simulation, () => { });
                var tree = panel.Build(0, 0);

                var store = tree.Query<VisualElement>(className: "rackstore").First();
                Assert.IsNotNull(store, "The cabinet window has no inventory column.");

                var actions = store.Query<VisualElement>(className: "rackmodal__actions").ToList();

                Assert.AreEqual(1, actions.Count,
                    "The fan controls are not inside the inventory column. They were asked for "
                    + "there by name, and the right hand column is where they were reported from.");

                Assert.AreEqual(1, PositionOf(store, actions[0]),
                    "The fan is in the inventory column but not directly under its heading. Under "
                    + "the parts instead puts it below a list that grows with the company, which "
                    + "is the position it was moved out of.");

                var desk = tree.Query<VisualElement>(className: "rackmodal__desk").First();

                Assert.IsEmpty(desk.Query<VisualElement>(className: "rackmodal__actions").ToList(),
                    "The fan is still in the right hand column as well, so there are two of it.");
            }
            finally
            {
                Loc.Current = before;
            }
        }

        /// <summary>
        /// The rule behind the pull, held without a panel and without a clock.
        ///
        /// It is a rule rather than a list of steps on purpose: anything the tour rings in a corner
        /// gets pointed at, and anything already under the player's eye is left alone. A list would
        /// be one more thing to remember the next time a walkthrough is written.
        /// </summary>
        [Test]
        public void AThingInTheCornerIsWorthPointingAtAndAThingInTheMiddleIsNot()
        {
            var window = new Rect(0f, 0f, 1920f, 1080f);

            var corner = new Rect(1660f, 40f, 240f, 120f);
            var middle = new Rect(860f, 500f, 200f, 80f);

            Assert.IsTrue(AttentionPull.IsOffToOneSide(corner, window),
                "The banner in the top right is exactly the case this was written for.");

            Assert.IsFalse(AttentionPull.IsOffToOneSide(middle, window),
                "Something already in the middle of the window would be dragged out and put back "
                + "for no reason, which is decoration rather than a signpost.");

            Assert.IsFalse(AttentionPull.IsOffToOneSide(corner, new Rect(0f, 0f, 0f, 0f)),
                "A window with no size has no middle to be away from, and a pull measured against "
                + "it would divide by nothing.");
        }

        private static int PositionOf(VisualElement parent, VisualElement child)
        {
            for (var index = 0; index < parent.childCount; index++)
            {
                if (parent[index] == child)
                {
                    return index;
                }
            }

            return -1;
        }

        private static CompanySimulation CompanyWithARoom()
        {
            var simulation = new CompanySimulation(new CompanyState("Roomco", 88));
            simulation.State.CashUsd = 5_000_000L;

            Assert.IsTrue(simulation.TryOpenServerRoom(true, out var why), why);
            Assert.IsFalse(simulation.State.Hall.At(0, 0).IsEmpty,
                "Opening the room left the first square empty, so there is no cabinet to open.");

            simulation.State.Hall.Fill(6);
            return simulation;
        }
    }
}
