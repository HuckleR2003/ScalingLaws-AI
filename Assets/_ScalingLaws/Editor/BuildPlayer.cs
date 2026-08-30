using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Makes the thing a player downloads.
    ///
    /// **A build made by clicking is a build nobody can repeat.** Which scenes were in it, which
    /// order they were in, whether it was a development build and whether the last one's files were
    /// still in the folder are all decided in a dialog that keeps no record. This is one command,
    /// it fails loudly, and the answers to those questions are in this file where they can be read
    /// and argued with.
    ///
    /// ```bash
    /// "/c/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe" -batchmode \
    ///   -projectPath . -executeMethod ScalingLaws.Editor.BuildPlayer.Windows64 -logFile build.log
    /// ```
    ///
    /// No `-quit`: it exits itself with a status, and `-quit` alongside `-executeMethod` has
    /// already cost this project a run that reported success while doing nothing.
    /// </summary>
    public static class BuildPlayer
    {
        /// <summary>
        /// Where the player lands. The `~` keeps it out of Unity's asset database and it is
        /// gitignored: this is a hundreds-of-megabytes artefact that is rebuilt from the source
        /// beside it, and the repository is public.
        /// </summary>
        public const string OutputFolder = "Build~/Scaling Laws Windows";

        /// <summary>The 1024px source the icon slots are filled from.</summary>
        public const string IconAsset = "Assets/_ScalingLaws/Art/Icon/app_icon.png";

        [MenuItem("Scaling Laws/Build the Windows player")]
        public static void Windows64()
        {
            var failure = Run();

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(failure == null ? 0 : 1);

                return;
            }

            if (failure != null)
            {
                Debug.LogError(failure);
            }
        }

        /// <summary>
        /// The build itself. Returns null on success and the reason on failure, rather than
        /// throwing, so the batch path can turn it into an exit code and the menu path into a
        /// console line without the two disagreeing about what went wrong.
        /// </summary>
        public static string Run()
        {
            var scenes = ScenesInOrder();

            if (scenes.Length == 0)
            {
                return "There are no enabled scenes in the build settings, so the player would "
                    + "start on a black screen.";
            }

            var exe = Path.Combine(OutputFolder, PlayerSettings.productName + ".exe");

            // The folder is emptied first. A rename of the product leaves the previous exe and its
            // data folder sitting beside the new ones, and the uploaded zip then contains two
            // games, one of which does not run.
            if (Directory.Exists(OutputFolder))
            {
                Debug.Log($"Clearing the previous build at {OutputFolder}.");
                Directory.Delete(OutputFolder, true);
            }

            Directory.CreateDirectory(OutputFolder);

            Debug.Log($"Building {PlayerSettings.productName} {PlayerSettings.bundleVersion} "
                + $"from {scenes.Length} scenes into {OutputFolder}.");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,

                // Not a development build. A development build carries the profiler, reports
                // stack traces to strangers and runs measurably slower, and this one goes to
                // people who are playing rather than debugging.
                options = BuildOptions.None
            });

            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                return $"The build finished as {summary.result} with {summary.totalErrors} errors.";
            }

            // Read the result off disk rather than trusting the report. A succeeded build that
            // wrote nothing has happened in this project's tooling before, in a different form,
            // and the check costs one stat call.
            if (!File.Exists(exe))
            {
                return $"The build reported success and there is no {exe} on disk.";
            }

            Debug.Log($"Built {exe}, {Megabytes(OutputFolder):N0} MB in "
                + $"{summary.totalTime.TotalMinutes:N1} minutes. Zip the folder, not the exe.");

            return null;
        }

        /// <summary>
        /// Fills every Windows icon slot from one drawing.
        ///
        /// Run once; the result is written into `ProjectSettings.asset` and committed. It is a
        /// separate command rather than a step of the build because a build that quietly edits
        /// project settings is a build that changes the repository every time it runs.
        /// </summary>
        [MenuItem("Scaling Laws/Set the application icon")]
        public static void SetIcon()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAsset);

            if (texture == null)
            {
                Debug.LogError($"No icon at {IconAsset}.");

                return;
            }

            var target = NamedBuildTarget.Standalone;
            var slots = PlayerSettings.GetIconSizes(target, IconKind.Application).Length;

            PlayerSettings.SetIcons(target, Enumerable.Repeat(texture, slots).ToArray(),
                IconKind.Application);

            AssetDatabase.SaveAssets();

            // Read it back. Unity accepts an icon array of the wrong length by silently keeping
            // the old one, and the difference between that and success is invisible until an exe
            // comes out wearing the default Unity logo.
            var written = PlayerSettings.GetIcons(target, IconKind.Application);
            var filled = written.Count(icon => icon != null);

            Debug.Log(filled == slots && slots > 0
                ? $"Icon set on all {slots} slots."
                : $"Icon NOT set: {filled} of {slots} slots hold a texture.");
        }

        /// <summary>The enabled scenes, in the order the build settings list them.</summary>
        public static string[] ScenesInOrder() => EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        private static double Megabytes(string folder)
        {
            var total = new DirectoryInfo(folder)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);

            return total / (1024.0 * 1024.0);
        }
    }
}
