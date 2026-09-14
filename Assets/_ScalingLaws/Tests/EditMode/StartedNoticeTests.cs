using NUnit.Framework;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The three kinds of notice.
    ///
    /// Nothing here can watch one fade in or flash: an EditMode element has no panel, so no
    /// scheduled item ever runs. What it can hold is the part that would break quietly. Which tone
    /// draws which class, that an alert's button goes where it says, that only one notice is ever
    /// up, and that nothing on a notice except that button takes the pointer, because every notice
    /// sits over SAVE and MENU.
    /// </summary>
    public sealed class StartedNoticeTests
    {
        [Test]
        public void AnAlertStaysTwoAndAHalfTimesAsLong()
        {
            Assert.AreEqual(StartedNotice.HoldMilliseconds * 2.5, StartedNotice.AlertHoldMilliseconds, 0.5,
                "Asked for by the author as two and a half times the ordinary hold.");
        }

        [Test]
        public void EachToneDrawsItsOwnClassAndOnlyOneNoticeIsEverUp()
        {
            var host = new VisualElement();
            var notice = new StartedNotice(host);

            notice.Show("ORDER PLACED", "note");
            Assert.IsFalse(notice.Frame.ClassListContains("started--special"));
            Assert.IsFalse(notice.Frame.ClassListContains("started--alert"));

            notice.Show("JOINS THE TEAM", "note", NoticeTone.Special);
            Assert.IsTrue(notice.Frame.ClassListContains("started--special"));

            notice.Show("ALERT", "note", NoticeTone.Alert);
            Assert.IsTrue(notice.Frame.ClassListContains("started--alert"));

            Assert.AreEqual(1, host.childCount,
                "A notice replaces the one before it. Three stacked down the screen is a notification "
                + "centre, which is a different feature.");
        }

        [Test]
        public void TheAlertButtonGoesWhereItSaysAndTakesTheNoticeDown()
        {
            var host = new VisualElement();
            var notice = new StartedNotice(host);
            var went = false;

            notice.Show("ALERT: SCANDAL", "headline", NoticeTone.Alert, "SEE", () => went = true);

            Assert.IsNotNull(notice.Frame.Q<Button>(className: "started__action"),
                "An alert with somewhere to go has to carry the button that goes there.");

            notice.RunAction();

            Assert.IsTrue(went, "SEE did not go anywhere.");
            Assert.IsFalse(notice.IsShowing, "The notice stayed up over the screen it sent the player to.");
        }

        [Test]
        public void ANoticeWithNothingToGoToHasNoButton()
        {
            var notice = new StartedNotice(new VisualElement());

            notice.Show("RESEARCH STARTED", "note");

            Assert.IsNull(notice.Frame.Q<Button>(),
                "A button on a notice that goes nowhere is a click the player spends on nothing.");
        }

        [Test]
        public void NothingButTheButtonTakesThePointer()
        {
            var notice = new StartedNotice(new VisualElement());

            notice.Show("ALERT", "note", NoticeTone.Alert, "SEE", () => { });

            foreach (var element in notice.Frame.Query<VisualElement>().ToList())
            {
                if (element is Button)
                {
                    continue;
                }

                Assert.AreEqual(PickingMode.Ignore, element.pickingMode,
                    "Part of a notice takes the pointer. It sits over SAVE and MENU, and two invisible "
                    + "click eaters have shipped in this project already.");
            }
        }
    }
}
