using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The line that says something happened.
    ///
    /// **SAVE wrote the file and moved not one pixel.** The only way to find out whether a campaign
    /// had been saved was to quit and look, which is the one moment a player cannot afford to be
    /// wrong about it, and the autosave ran on a timer nobody could see.
    /// </summary>
    public sealed class ToastTests
    {
        [TearDown]
        public void Down() => Toast.Reset();

        [Test]
        public void ALineGoesUpAndAnotherReplacesIt()
        {
            Toast.Host = new VisualElement();

            Toast.Show("Saved. 2024-01-01");

            Assert.That(Toast.IsShowing, Is.True);
            Assert.That(Toast.Text, Is.EqualTo("Saved. 2024-01-01"));
            Assert.That(Toast.Host.childCount, Is.EqualTo(1));

            Toast.Show("Autosaved. 2024-02-01");

            Assert.That(Toast.Host.childCount, Is.EqualTo(1),
                "Two of these stacking is a notification centre, which is a different feature.");

            Assert.That(Toast.Text, Is.EqualTo("Autosaved. 2024-02-01"));

            Toast.Hide();
            Assert.That(Toast.IsShowing, Is.False);
            Assert.That(Toast.Host.childCount, Is.Zero);
        }

        [Test]
        public void WithNoHostItIsQuietRatherThanBroken()
        {
            Toast.Host = null;

            Assert.DoesNotThrow(() => Toast.Show("Saved."));
            Assert.That(Toast.IsShowing, Is.False,
                "A message with nowhere to go is not a message, and it must not be an exception "
                + "either: the menu scene has no game panel.");
        }

        /// <summary>
        /// It must never eat a click, because it sits directly over SAVE and MENU.
        ///
        /// This project has shipped two invisible overlays that took every click in a corner of the
        /// screen, one of them mine, and both were found by a player rather than by a test.
        /// </summary>
        [Test]
        public void ItNeverTakesAClickFromTheButtonsUnderneathIt()
        {
            Toast.Host = new VisualElement();
            Toast.Show("Saved.");

            Assert.That(Toast.Host[0].pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        /// <summary>
        /// Both saves say so, and the reachability question is whether anything calls it at all.
        ///
        /// A source check rather than a click, for the reason `ReachabilityTests` gives: an
        /// EditMode element has no panel, so a click sent to the SAVE button is never dispatched.
        /// </summary>
        [Test]
        public void BothTheButtonAndTheAutosaveSaySoAndTheWordsExist()
        {
            var shell = File.ReadAllText(Path.Combine(Application.dataPath,
                "_ScalingLaws", "Scripts", "UI", "GameShell.cs"));

            Assert.That(Regex.Matches(shell, @"Toast\.Show\(").Count, Is.GreaterThanOrEqualTo(2),
                "The button and the autosave both have to report, or half of saving is silent.");

            foreach (Language language in System.Enum.GetValues(typeof(Language)))
            {
                var was = Loc.Current;
                Loc.Current = language;

                try
                {
                    Assert.That(Loc.T("save.done", "2024-01-01"), Does.Not.Contain("save.done"));
                    Assert.That(Loc.T("save.auto", "2024-01-01"), Does.Not.Contain("save.auto"));
                }
                finally
                {
                    Loc.Current = was;
                }
            }
        }
    }
}
