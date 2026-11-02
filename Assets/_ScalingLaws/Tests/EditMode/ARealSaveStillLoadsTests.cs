using System;
using System.IO;
using NUnit.Framework;
using ScalingLaws.Persistence;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Points the loader at a campaign somebody actually played, rather than at one a test wrote.
    ///
    /// **Every other save test in this project round-trips through the code it is testing.** It
    /// writes a file with today's writer and reads it with today's reader, which proves the two
    /// agree and proves nothing about a file written by a build that shipped weeks ago. That is
    /// the gap this fills, and it was opened wide on 2026-11-01 when a field was removed from the
    /// save shape and from the run that carried it.
    ///
    /// The file never enters this repository, because a real campaign carries the player's own
    /// company name and founder name and this repository is public. Give it a path instead:
    ///
    /// <code>
    /// SCALINGLAWS_REAL_SAVE=/path/to/save.json Unity.exe -batchmode -runTests ...
    /// </code>
    ///
    /// A campaign is kept in PlayerPrefs, which on Windows is the registry, so getting one out is
    /// a read of <c>HKCU:\Software\&lt;company&gt;\&lt;product&gt;</c> and a UTF-8 decode with the
    /// trailing null trimmed.
    ///
    /// **Inconclusive rather than failing when no path is given.** A guard that goes red on every
    /// machine that has not been handed a save is a guard somebody deletes, and this one is a tool
    /// for a question that comes up when the save shape moves rather than a thing to run hourly.
    /// </summary>
    [Explicit("Point SCALINGLAWS_REAL_SAVE at a campaign out of PlayerPrefs and name this fixture "
        + "in a test filter. It is a tool for the days the save shape moves, not an hourly guard.")]
    public sealed class ARealSaveStillLoadsTests
    {
        private const string PathVariable = "SCALINGLAWS_REAL_SAVE";

        private static string ReadTheFile()
        {
            var path = Environment.GetEnvironmentVariable(PathVariable);

            if (string.IsNullOrWhiteSpace(path))
            {
                Assert.Inconclusive(
                    $"No {PathVariable} given, so no real campaign was checked. Point it at a save "
                    + "pulled out of PlayerPrefs whenever the save shape changes.");
            }

            if (!File.Exists(path))
            {
                Assert.Fail($"{PathVariable} points at {path} and there is nothing there.");
            }

            return File.ReadAllText(path);
        }

        /// <summary>
        /// It parses, it migrates, and what comes out is a company rather than a blank one.
        ///
        /// The third assertion is the one that matters. A loader that swallows a file it cannot
        /// read and hands back a default campaign looks exactly like a loader that worked, and
        /// the player finds out by seeing somebody else's company on their own screen.
        /// </summary>
        [Test]
        public void ACampaignSomebodyActuallyPlayedStillLoads()
        {
            var json = ReadTheFile();

            // **Read before parsing, because Parse migrates.** The first version of this line
            // took the version off the parsed object and printed it as the version on disk, which
            // reported a v75 file as v77 and quietly claimed the migration chain had not run when
            // it had. A figure labelled as one thing and holding another is the fault this whole
            // evening has been about.
            var versionOnDisk = "unknown";
            const string marker = "\"version\":";
            var at = json.IndexOf(marker, StringComparison.Ordinal);

            if (at >= 0)
            {
                var rest = json.Substring(at + marker.Length).TrimStart();
                var end = 0;

                while (end < rest.Length && char.IsDigit(rest[end]))
                {
                    end++;
                }

                if (end > 0)
                {
                    versionOnDisk = rest.Substring(0, end);
                }
            }

            var data = SaveStore.Parse(json);

            Assert.That(data, Is.Not.Null,
                "The file did not parse at all. A player opening this build would lose a campaign.");

            var state = SaveStore.Restore(data);

            Assert.That(state, Is.Not.Null, "Parsed and then restored to nothing.");

            Assert.That(state.CompanyName, Is.Not.Empty,
                "The campaign came back with no company name, which is what a default state looks "
                + "like. The file was read and then quietly discarded.");

            Assert.That(state.Date.DayIndex, Is.GreaterThan(0),
                "The campaign came back on day zero, so it was replaced by a new one rather than "
                + "loaded.");

            TestContext.WriteLine(
                $"[real save] version {versionOnDisk} on disk, migrated to {data.version}, "
                + $"current is {SaveData.CurrentVersion}, "
                + $"day {state.Date.DayIndex}, cash {state.CashUsd:N0}, "
                + $"{state.DeployedModels.Count} live model(s), "
                + $"run in flight: {state.ActiveRun != null}");
        }

        /// <summary>
        /// And a key the game no longer reads does not stop it reading the rest.
        ///
        /// **This is the specific risk taken on 2026-11-01.** `actualTokensBillions` was removed
        /// from the save shape with no version bump, on the reasoning that a key nothing reads is
        /// a key nothing misses. That reasoning is sound for a serialiser that ignores unknown
        /// fields and is worth nothing as an opinion, so it is measured against a file that has
        /// the key in it.
        /// </summary>
        [Test]
        public void AKeyTheGameNoLongerReadsDoesNotBreakTheFileThatStillHasIt()
        {
            var json = ReadTheFile();

            Assume.That(json, Does.Contain("actualTokensBillions"),
                "This particular file predates the removal, so point the variable at one that "
                + "does not, or leave this case to the one above.");

            var state = SaveStore.Restore(SaveStore.Parse(json));

            Assert.That(state, Is.Not.Null,
                "A file carrying the removed key failed to load, so the removal needed a version "
                + "bump and a migration after all.");

            Assert.That(state.CompanyName, Is.Not.Empty,
                "It loaded into a blank campaign, which is the same fault wearing a quieter face.");
        }
    }
}
