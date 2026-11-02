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

        /// <summary>Where the Mac bundle lands. Same reasoning, separate folder.</summary>
        public const string MacOutputFolder = "Build~/Scaling Laws Mac";

        /// <summary>Where the Linux player lands. Same reasoning again.</summary>
        public const string LinuxOutputFolder = "Build~/Scaling Laws Linux";

        /// <summary>
        /// What macOS calls the application, and it is not optional there.
        ///
        /// `applicationIdentifier` was empty in ProjectSettings when the first Mac build was
        /// attempted on 2026-11-02. Windows does not care and macOS does: an empty identifier
        /// produces a bundle the system will not treat as an application.
        /// </summary>
        public const string BundleIdentifier = "com.hcklabs.scalinglaws";

        /// <summary>The 1024px source the icon slots are filled from.</summary>
        public const string IconAsset = "Assets/_ScalingLaws/Art/Icon/app_icon.png";

        [MenuItem("Scaling Laws/Build the Windows player")]
        public static void Windows64() => Finish(Run(BuildTarget.StandaloneWindows64));

        /// <summary>
        /// The Mac bundle, built from Windows, which Unity is happy to do.
        ///
        /// **Two things this cannot do, and both matter before the file goes anywhere.** It cannot
        /// be run here, so the only thing a green result proves is that it compiled. And a `.app`
        /// is a folder whose executable needs the Unix permission bit, which a zip written on
        /// Windows does not carry, so whoever downloads it has to repair that before macOS will
        /// open it. Neither is a reason not to build; both are reasons the page needs a sentence.
        /// </summary>
        [MenuItem("Scaling Laws/Build the Mac player")]
        public static void MacUniversal() => Finish(Run(BuildTarget.StandaloneOSX));

        /// <summary>
        /// The Linux player, and it is the cheapest of the three to add.
        ///
        /// Nothing in this game is platform specific, so the only things that differ from Windows
        /// are where the file lands and what it is called. The one thing it shares with the Mac
        /// build is the permission bit: a zip written on Windows carries none, so a Linux player
        /// has to `chmod +x` the binary before it will start, and the page has to say so.
        /// </summary>
        [MenuItem("Scaling Laws/Build the Linux player")]
        public static void Linux64() => Finish(Run(BuildTarget.StandaloneLinux64));

        private static void Finish(string failure)
        {
            if (Application.isBatchMode)
            {
                if (failure != null)
                {
                    Debug.LogError(failure);
                }

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
        public static string Run(BuildTarget target = BuildTarget.StandaloneWindows64)
        {
            var mac = target == BuildTarget.StandaloneOSX;
            var linux = target == BuildTarget.StandaloneLinux64;

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                return $"{target} is not installed in this editor. Add the build support module "
                    + "in Unity Hub; nothing in the project needs to change.";
            }

            if (mac && string.IsNullOrWhiteSpace(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone)))
            {
                return "The application identifier is empty and macOS requires one. Run "
                    + "Scaling Laws > Set the macOS bundle identifier once, then build again.";
            }

            var scenes = ScenesInOrder();

            if (scenes.Length == 0)
            {
                return "There are no enabled scenes in the build settings, so the player would "
                    + "start on a black screen.";
            }

            var folder = mac ? MacOutputFolder : linux ? LinuxOutputFolder : OutputFolder;

            // A `.app` is a folder that macOS presents as a file, so the path handed to Unity ends
            // in the bundle itself rather than in an executable inside it. Linux needs no extension
            // at all, and `.x86_64` is what every Unity game on Linux is called, so somebody who
            // has run one before knows which of the two files in the folder to start.
            var output = Path.Combine(folder,
                PlayerSettings.productName + (mac ? ".app" : linux ? ".x86_64" : ".exe"));

            // The folder is emptied first. A rename of the product leaves the previous exe and its
            // data folder sitting beside the new ones, and the uploaded zip then contains two
            // games, one of which does not run.
            if (Directory.Exists(folder))
            {
                Debug.Log($"Clearing the previous build at {folder}.");
                Directory.Delete(folder, true);
            }

            Directory.CreateDirectory(folder);

            if (mac)
            {
                AimAtBothMacArchitectures();
            }

            Debug.Log($"Building {PlayerSettings.productName} {PlayerSettings.bundleVersion} "
                + $"for {target} from {scenes.Length} scenes into {folder}.");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
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
            // and the check costs one stat call. The Mac bundle is a directory, so asking
            // File.Exists about it would report a good build as missing.
            var there = mac ? Directory.Exists(output) : File.Exists(output);

            if (!there)
            {
                return $"The build reported success and there is no {output} on disk.";
            }

            if (mac)
            {
                // The one file that has to be inside a bundle for macOS to open it at all.
                var binary = Path.Combine(output, "Contents", "MacOS");

                if (!Directory.Exists(binary) || Directory.GetFiles(binary).Length == 0)
                {
                    return $"The bundle at {output} has no executable in Contents/MacOS, so macOS "
                        + "would refuse to open it.";
                }
            }

            Debug.Log($"Built {output}, {Megabytes(folder):N0} MB in "
                + $"{summary.totalTime.TotalMinutes:N1} minutes. Zip the folder, not the "
                + (mac ? "bundle." : linux ? "binary." : "exe."));

            return null;
        }

        /// <summary>
        /// Asks for a bundle that runs on both Intel and Apple Silicon.
        ///
        /// **Through reflection, on purpose.** The setting lives in `UnityEditor.OSXStandalone`,
        /// which only exists when the Mac module is installed, and naming it directly would stop
        /// this file compiling on any machine without it. That includes a fresh clone of a public
        /// repository and any Windows-only build box. If the type is absent the build still runs
        /// and Unity picks its own default, which is a worse bundle rather than no bundle.
        /// </summary>
        private static void AimAtBothMacArchitectures()
        {
            var settings = Type.GetType(
                "UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandalone.Extensions");

            var property = settings?.GetProperty("architecture");

            if (property == null)
            {
                Debug.Log("Could not reach the Mac architecture setting, so Unity's default "
                    + "stands. On 6000.5.8f1 that default was already both architectures.");

                return;
            }

            // **The enum belongs to the property, not to the module.** The first version of this
            // reached for UnityEditor.OSXStandalone.MacOSArchitecture, which exists and is the
            // wrong type: the setter wants UnityEditor.Build.OSArchitecture and threw. The build
            // came out universal anyway because that is Unity's default here, which is exactly
            // the shape of a fix that looks like it worked. Asking the property what it accepts
            // cannot be wrong in that way.
            var wanted = Enum.GetValues(property.PropertyType)
                .Cast<object>()
                .FirstOrDefault(value =>
                {
                    var name = value.ToString();

                    return name.IndexOf("x64", StringComparison.OrdinalIgnoreCase) >= 0
                        && name.IndexOf("arm64", StringComparison.OrdinalIgnoreCase) >= 0;
                });

            if (wanted == null)
            {
                Debug.Log("No architecture on this editor names both Intel and ARM. Available: "
                    + string.Join(", ", Enum.GetNames(property.PropertyType)));

                return;
            }

            try
            {
                property.SetValue(null, wanted);
                Debug.Log($"Mac build aimed at Intel and Apple Silicon ({wanted}).");
            }
            catch (Exception problem)
            {
                Debug.Log($"Left Unity's default Mac architecture: {problem.Message}");
            }
        }

        /// <summary>
        /// Writes the bundle identifier macOS needs, once, into the project settings.
        ///
        /// Separate from the build for the reason the icon command is separate: a build that
        /// edits project settings changes the repository every time it runs.
        /// </summary>
        [MenuItem("Scaling Laws/Set the macOS bundle identifier")]
        public static void SetBundleIdentifier()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleIdentifier);
            AssetDatabase.SaveAssets();

            var written = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);
            var ok = written == BundleIdentifier;

            Debug.Log(ok
                ? $"Bundle identifier set to {written}."
                : $"Bundle identifier NOT set: it reads {written}.");

            // Exits itself for the reason written at the top of this file: `-quit` beside
            // `-executeMethod` has already produced a run here that reported success and did
            // nothing, because Unity left before the method finished.
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
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
