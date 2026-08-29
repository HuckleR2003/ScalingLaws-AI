using System;
using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.Persistence
{
    /// <summary>
    /// Player preferences that are not part of a campaign: volume, window mode, motion, language.
    ///
    /// Deliberately separate from <see cref="SaveStore"/>. These survive deleting a campaign and
    /// starting a new one, because they describe the person playing rather than the company being
    /// run. Wiping a save must never reset somebody's accessibility choice.
    /// </summary>
    public static class GameSettings
    {
        private const string Prefix = "ScalingLaws.Settings.";
        private const string MasterVolumeKey = Prefix + "MasterVolume";
        private const string FullscreenKey = Prefix + "Fullscreen";
        private const string ReduceMotionKey = Prefix + "ReduceMotion";
        private const string LanguageKey = Prefix + "Language";
        private const string AutosaveKey = Prefix + "AutosaveMinutes";

        public const float DefaultMasterVolume = 0.8f;

        /// <summary>
        /// The intervals the game offers, in minutes. Zero is off and it is the default.
        ///
        /// **Off by default on purpose.** Writing to disk on its own while somebody is halfway
        /// through pricing a release is the game making a decision for them, and this one is cheap
        /// to turn on once a player knows they want it.
        /// </summary>
        public static readonly int[] AutosaveChoices = { 0, 1, 5, 10 };

        /// <summary>Minutes between automatic saves, or zero for never.</summary>
        public static int AutosaveMinutes { get; private set; }

        public static float MasterVolume { get; private set; } = DefaultMasterVolume;
        public static bool Fullscreen { get; private set; }

        /// <summary>
        /// Shortens or removes the opening sequence and any drift on the office camera. This is an
        /// accessibility setting, not a performance one, so it is honoured everywhere without a
        /// quality tradeoff attached.
        /// </summary>
        public static bool ReduceMotion { get; private set; }

        /// <summary>
        /// Which language the interface is in.
        ///
        /// **A preference, not campaign state.** Somebody who reads Polish reads Polish in every
        /// campaign they start, and wiping a save must never put the game back into English.
        ///
        /// `Loc.Current` is what the screens read, and it is set from here rather than the other
        /// way round: one owner for the value, and it is the one that can persist it.
        /// </summary>
        public static Language Language { get; private set; } = Language.English;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeFirstScene() => Reload();

        public static void Reload()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, 0) == 1;
            ReduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;

            // Defaults to the machine's own language on a first run, so somebody in Poland does not
            // have to find a settings screen written in English in order to say they read Polish.
            var stored = PlayerPrefs.GetInt(LanguageKey, -1);

            Language = stored >= 0 && Enum.IsDefined(typeof(Language), stored)
                ? (Language)stored
                : DetectLanguage();

            Loc.Current = Language;

            AudioListener.volume = MasterVolume;
            ApplyDisplayMode();

            AutosaveMinutes = 0;

            foreach (var choice in AutosaveChoices)
            {
                if (choice == PlayerPrefs.GetInt(AutosaveKey, 0))
                {
                    AutosaveMinutes = choice;
                    break;
                }
            }
        }

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
            PlayerPrefs.Save();
            AudioListener.volume = MasterVolume;
        }

        public static void SetFullscreen(bool value)
        {
            Fullscreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplyDisplayMode();
        }

        public static void SetReduceMotion(bool value)
        {
            ReduceMotion = value;
            PlayerPrefs.SetInt(ReduceMotionKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Sets the autosave interval, clamped to something the game actually offers.
        ///
        /// Clamped rather than trusted, because this reads back out of PlayerPrefs, which a player
        /// can edit and a corrupt install can garble. An interval of minus four would be an
        /// autosave on every frame.
        /// </summary>
        public static void SetAutosaveMinutes(int minutes)
        {
            var wanted = 0;

            foreach (var choice in AutosaveChoices)
            {
                if (choice == minutes)
                {
                    wanted = choice;
                    break;
                }
            }

            AutosaveMinutes = wanted;
            PlayerPrefs.SetInt(AutosaveKey, wanted);
            PlayerPrefs.Save();
        }

        public static void SetLanguage(Language value)
        {
            Language = Enum.IsDefined(typeof(Language), value) ? value : Language.English;
            PlayerPrefs.SetInt(LanguageKey, (int)Language);
            PlayerPrefs.Save();
            Loc.Current = Language;
        }

        /// <summary>What the machine says it is set to, mapped onto what the game has.</summary>
        private static Language DetectLanguage() =>
            Application.systemLanguage == SystemLanguage.Polish ? Language.Polish : Language.English;

        private static void ApplyDisplayMode()
        {
            // Changing the window while the editor or a batch run owns it does nothing useful and
            // can hang a headless test, so it is skipped in both.
            if (Application.isEditor || Application.isBatchMode)
            {
                return;
            }

            Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }
    }
}
