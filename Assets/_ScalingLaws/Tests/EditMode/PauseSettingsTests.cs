using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Sound and music are reachable from inside a campaign.
    ///
    /// **Both sliders existed and neither was in the game.** They are on the main menu, so a player
    /// who found the office loop loud had to leave the campaign to turn it down, and the Escape
    /// menu is the one place a player looks for exactly this.
    ///
    /// Two independent controls rather than one inside the other. That was settled when the sliders
    /// were built and the reason still holds: a control that needs a footnote explaining that the
    /// control above it also moves it is two controls doing one job.
    /// </summary>
    public sealed class PauseSettingsTests
    {
        private float master;
        private float music;

        [SetUp]
        public void Remember()
        {
            master = GameSettings.MasterVolume;
            music = GameSettings.MusicVolume;
        }

        /// <summary>These are real preferences on the machine running the tests, not campaign state.</summary>
        [TearDown]
        public void PutItBack()
        {
            GameSettings.SetMasterVolume(master);
            GameSettings.SetMusicVolume(music);
        }

        private static VisualElement SettingsPage()
        {
            var simulation = new CompanySimulation(new CompanyState("Pause", 0x9Cu));
            var pause = new PauseMenu(() => simulation, () => { });

            pause.Open();
            pause.OpenTab(PauseTab.Settings);

            return pause.Build();
        }

        [Test]
        public void TheEscapeMenuHasBothVolumeSliders()
        {
            var sliders = SettingsPage().Query<Slider>(className: "pause__slider").ToList();

            Assert.That(sliders, Has.Count.EqualTo(2),
                "A player who wants the music down has to quit to the main menu to do it, which is "
                + "what was reported.");
        }

        [Test]
        public void EachSliderIsLabelledWithWhatItMoves()
        {
            var text = SettingsPage().Query<Label>().ToList().Select(label => label.text).ToList();

            Assert.That(text, Has.Member(Loc.T("settings.volume")));
            Assert.That(text, Has.Member(Loc.T("settings.music")));

            Assert.That(text, Has.Member(Loc.T("settings.volume.note")),
                "Two sliders with no note between them is a player guessing which one is which.");

            Assert.That(text, Has.Member(Loc.T("settings.music.note")));
        }

        /// <summary>
        /// Each slider is sitting on its own setting, and they are not the same one.
        ///
        /// **The read direction, because the write direction cannot be reached from here.**
        /// `BaseField.value` announces itself by sending a change event and an element with no
        /// panel dispatches nothing, so a test that moved a handle would be measuring its own
        /// assignment. The same wall the management screen and the basement cursor are behind.
        ///
        /// This still catches the fault worth catching. Two sliders built from one setting, or
        /// built in the other order, is a music control that moves the clicks, and that is a wiring
        /// mistake rather than an event one.
        /// </summary>
        [Test]
        public void EachSliderSitsOnItsOwnSetting()
        {
            GameSettings.SetMasterVolume(0.30f);
            GameSettings.SetMusicVolume(0.90f);

            var sliders = SettingsPage().Query<Slider>(className: "pause__slider").ToList();
            Assume.That(sliders, Has.Count.EqualTo(2));

            Assert.That(sliders[0].value, Is.EqualTo(30f).Within(0.5f),
                "the first slider is not showing the effects volume");

            Assert.That(sliders[1].value, Is.EqualTo(90f).Within(0.5f),
                "The second slider is not showing the music volume. Two handles reading one setting "
                + "is the shape this page had before, where master scaled the music.");
        }

        /// <summary>
        /// And the reading beside each one agrees with the handle.
        ///
        /// A percentage that does not match the slider it is printed next to is worse than no
        /// percentage, because the player believes the number.
        /// </summary>
        [Test]
        public void TheReadingAgreesWithTheHandle()
        {
            GameSettings.SetMasterVolume(0.30f);
            GameSettings.SetMusicVolume(0.90f);

            var page = SettingsPage();
            var readings = page.Query<Label>(className: "pause__reading").ToList();

            Assert.That(readings, Has.Count.EqualTo(2));
            Assert.That(readings[0].text, Is.EqualTo("30%"));
            Assert.That(readings[1].text, Is.EqualTo("90%"));
        }

    }
}
