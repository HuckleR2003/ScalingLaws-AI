using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The opening film's sound has to go through Unity, or no volume slider can reach it.
    ///
    /// **Reported by a player, on build 0.6.2, on day 0**: the music could not be muted and moving
    /// either slider did nothing. It was true, and it was this.
    /// `VideoAudioOutputMode.Direct` hands the audio straight to the platform's output and skips
    /// Unity's audio system on the way, so `AudioListener.volume`, which is what the master slider
    /// sets, never touched it, and neither did anything the music slider touches. The film is the
    /// first sound a new campaign makes, which is exactly why the report says day 0.
    ///
    /// **This reads the source, which is weaker than driving it, and the reason is written down.**
    /// The film is built by `PrepareIntroFilm`, which `BuildIntro` calls when the opening page is
    /// built rather than when the menu loads, so a PlayMode test that loads the scene finds no
    /// `VideoPlayer` at all. The first version of this guard did exactly that and skipped itself,
    /// which is a test that proves nothing. Reading the file is how `FounderRoutineTests` and
    /// `GenderNeutralTests` already hold facts that cannot be reached any other way.
    /// </summary>
    [TestFixture]
    public sealed class IntroFilmAudioTests
    {
        private const string Source =
            "Assets/_ScalingLaws/Scripts/UI/MainMenuController.cs";

        /// <summary>
        /// The file with its comments taken out.
        ///
        /// **A test that reads source has to read code rather than prose.** The first run of
        /// this failed on the explanation above it, which names the enum it is forbidding, and
        /// a guard that cannot be described without tripping is a guard nobody can document.
        /// </summary>
        private static string Code()
        {
            Assert.IsTrue(File.Exists(Source), $"{Source} is not where this test expects it.");

            var lines = File.ReadAllLines(Source);
            var kept = new List<string>(lines.Length);

            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith("//"))
                {
                    kept.Add(line);
                }
            }

            return string.Join(Environment.NewLine, kept);
        }

        [Test]
        public void TheFilmIsNotRoutedPastUnitysAudioSystem()
        {
            Assert.That(Code(), Does.Not.Contain("VideoAudioOutputMode.Direct"),
                "The opening film is on Direct audio output, which bypasses Unity's audio system. "
                + "Neither the master slider nor the music slider can reach it, and a player cannot "
                + "mute the first sound the game makes. This was reported from a real build.");
        }

        [Test]
        public void TheFilmIsGivenSomethingToPlayThrough()
        {
            var source = Code();

            Assert.That(source, Does.Contain("VideoAudioOutputMode.AudioSource"),
                "The film is not routed to an AudioSource, so nothing the interface owns can set "
                + "its level.");

            Assert.That(source, Does.Contain("SetTargetAudioSource"),
                "The film is set to play through an AudioSource and is never given one, so it "
                + "plays silently instead. That is the opposite fault and just as wrong.");

            Assert.That(source, Does.Contain("EnableAudioTrack"),
                "The audio track is not enabled before the player prepares, so there is nothing "
                + "to route and the film comes out silent.");
        }
    }
}
