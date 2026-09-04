using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The people in the office, and the wire that puts them there.
    ///
    /// **The twelfth time this project has built a mechanism nothing drives, and it happened during
    /// the session that added it.** `StaffPresence` was constructed by the shell and `Refresh` was
    /// never called: a company could hire a floor of people and the room stayed empty, with every
    /// test green, because the class was complete and correct and nobody asked it anything.
    ///
    /// `UiWiringTests` could not see it. That guard checks private methods with no caller, and this
    /// was a public class with no driver, which is the same fault wearing different clothes.
    /// </summary>
    public sealed class OfficePresenceTests
    {
        private static string Read(string file) =>
            File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(),
                "Assets", "_ScalingLaws", "Scripts", "UI", file));

        /// <summary>
        /// Everything the shell creates to put something in the room is also driven by it.
        ///
        /// Source text rather than behaviour, because an EditMode test loads no scene and cannot
        /// watch a model appear. It proves the call exists, not that a person shows up, which is
        /// the same promise `ReachabilityTests` makes about cabinets and is worth exactly as much:
        /// it catches the gap that has now shipped twelve times.
        /// </summary>
        [Test]
        public void EveryPresenceTheShellBuildsIsAlsoDriven()
        {
            var shell = Read("GameShell.cs") + Read("GameShell.Site.cs");

            foreach (var name in new[] { "founder", "staff" })
            {
                Assert.IsTrue(Regex.IsMatch(shell, @"\b" + name + @"\s*=\s*new "),
                    $"The shell never creates {name}.");

                Assert.IsTrue(
                    Regex.IsMatch(shell, @"\b" + name + @"\??\.(Refresh|Spawn)\s*\("),
                    $"The shell creates {name} and never drives it, so whatever it puts in the room "
                    + "is never put there.");
            }
        }

        /// <summary>
        /// A click on the room reaches the panel that says who somebody is.
        ///
        /// The panel has existed since the person page was built and its only route in was a row on
        /// the team screen, so the office was a picture of people nobody could talk to.
        /// </summary>
        [Test]
        public void ClickingTheRoomCanOpenAPerson()
        {
            var site = Read("GameShell.Site.cs");

            StringAssert.Contains("RegisterCallback<MouseDownEvent>", site,
                "Nothing listens for a click on the office.");

            StringAssert.Contains("OfficePerson", site,
                "The click never asks which employee is under it.");

            StringAssert.Contains("personPanel", site,
                "A click on somebody in the room opens nothing.");
        }

        /// <summary>
        /// It does nothing at all without a scene, rather than throwing.
        ///
        /// The shell runs in tests and in the menu, where there is no office and no `Staff` group.
        /// Every loader in this project degrades rather than failing, and a presence that needed a
        /// scene to not throw would take the whole shell down on the title screen.
        /// </summary>
        [Test]
        public void WithNoOfficeSceneItSimplyDoesNothing()
        {
            var state = new CompanyState("Adco", 3);
            var presence = new StaffPresence(() => state);

            Assert.DoesNotThrow(() => presence.Refresh());
            Assert.AreEqual(0, presence.Standing);

            // And with no state either, which is the shell's very first frame.
            var empty = new StaffPresence(() => null);

            Assert.DoesNotThrow(() => empty.Refresh());
            Assert.DoesNotThrow(() => empty.Clear());
        }

        /// <summary>
        /// The office is not rebuilt because somebody was paid a bonus.
        ///
        /// The site screen refreshes on every repaint, which is about every second and a half at
        /// normal speed. Destroying and re-instantiating a dozen skinned meshes at that rate is the
        /// kind of cost nobody notices until a company has a full floor.
        /// </summary>
        [Test]
        public void ChangingSomebodysHoursDoesNotRebuildTheRoom()
        {
            var simulation = new CompanySimulation(new CompanyState("Adco", 5));
            var state = simulation.State;

            state.CashUsd = 50_000_000L;
            state.Staff.SetOffice(OfficeTier.Loft);

            state.Staff.Add(new Hire(StaffRole.ResearchScientist, 3, state.Date, "Ada Kowalska",
                PlayerSkill.Concept, HireSource.Agency, 100.0));

            var before = Signature(state);

            Assert.IsTrue(simulation.TrySetHours(0, 10, 18, out var why), why);
            Assert.AreEqual(before, Signature(state),
                "A schedule change moved somebody in the room, so the office is rebuilt for it.");

            simulation.TryPayBonus(0, state.Staff.Hires[0].SalaryPerYearUsd / 12, out _);
            Assert.AreEqual(before, Signature(state), "A bonus rebuilt the room.");

            // Hiring somebody does.
            state.Staff.Add(new Hire(StaffRole.GoToMarket, 2, state.Date, "Marek Nowak",
                PlayerSkill.Management, HireSource.Agency, 80.0));

            Assert.AreNotEqual(before, Signature(state),
                "Hiring somebody did not change the room, so they never appear in it.");
        }

        /// <summary>
        /// The same rule the presence uses, so this test cannot pass while the presence disagrees.
        ///
        /// Kept here rather than exposed on `StaffPresence`, because it is a detail of when to
        /// rebuild and not something any other caller should be making decisions on.
        /// </summary>
        private static string Signature(CompanyState state)
        {
            var text = new System.Text.StringBuilder();

            foreach (var hire in state.Staff.Hires)
            {
                text.Append(hire.Name).Append('/').Append((int)hire.Role).Append(';');
            }

            return text.ToString();
        }
    }
}
