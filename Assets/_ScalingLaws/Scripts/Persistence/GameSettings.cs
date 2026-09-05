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
        private const string MusicVolumeKey = Prefix + "MusicVolume";
        private const string FullscreenKey = Prefix + "Fullscreen";
        private const string ReduceMotionKey = Prefix + "ReduceMotion";
        private const string LanguageKey = Prefix + "Language";
        private const string AutosaveKey = Prefix + "AutosaveMinutes";

        public const float DefaultMasterVolume = 0.8f;

        /// <summary>
        /// How loud the music is under everything else, before the master volume touches it.
        ///
        /// **Full by default, and separate from master on purpose.** Interface sound is feedback:
        /// it answers a click and a player who turns it off loses information. Music is a room, and
        /// wanting to run a tycoon with a podcast on is a normal thing to want. One slider cannot
        /// serve both, and a player who only has one turns everything off to get rid of the loop.
        /// </summary>
        public const float DefaultMusicVolume = 1.0f;

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

        /// <summary>
        /// The window the game opens in when it is not fullscreen, expressed against the reference.
        ///
        /// The panel scales to 1920x1080, so a window of that shape is the one the interface was
        /// laid out in. Anything larger is scaled up rather than given more room.
        /// </summary>
        public const int DesignWidth = 1920;

        private const float DesignAspect = 16f / 9f;

        /// <summary>
        /// Pixels down the display that a window may not use: the title bar above it and the
        /// taskbar below. Generous, because a bottom bar half under the taskbar is unusable and a
        /// window forty pixels shorter than it could be is not.
        /// </summary>
        private const int VerticalChrome = 96;

        /// <summary>How much of the display's width a window may take.</summary>
        private const float SideFraction = 0.94f;

        /// <summary>Below this the bottom bar's fifteen slots stop being clickable targets.</summary>
        private const int SmallestUsableWidth = 960;

        public static float MasterVolume { get; private set; } = DefaultMasterVolume;

        /// <summary>
        /// Music only, multiplied into the music source rather than into the listener.
        ///
        /// Read every frame by <c>AudioDirector</c> rather than pushed from here, so this file
        /// stays a store of preferences and knows nothing about who plays what.
        /// </summary>
        public static float MusicVolume { get; private set; } = DefaultMusicVolume;

        /// <summary>
        /// Whether the game fills the screen. **Defaults to true, and that is a fix.**
        ///
        /// It defaulted to false, so `ApplyDisplayMode` put every first launch into a window at
        /// `defaultScreenWidth` x `defaultScreenHeight`, which is 1920x1080. On the commonest
        /// monitor there is that means a window exactly the size of the display: the title bar
        /// above the top of the screen and the game's own bottom bar behind the taskbar. The
        /// player's clock, speed controls and every category slot were under Windows' own
        /// furniture on the first screen they ever saw, and the window could not be resized.
        /// </summary>
        public static bool Fullscreen { get; private set; } = true;

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
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
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

        /// <summary>
        /// Sets the music level. Nothing is applied here on purpose.
        ///
        /// Master volume goes to `AudioListener`, which is a global and therefore this file's to
        /// set. Music belongs to one `AudioSource` owned by the presentation layer, and reaching
        /// across to touch it would make persistence depend on the interface. `AudioDirector` reads
        /// this instead.
        /// </summary>
        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            PlayerPrefs.Save();
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

        /// <summary>
        /// The size of the windowed game on a display of the given size.
        ///
        /// **Setting the mode is not enough and that was the bug.** `Screen.fullScreenMode` changes
        /// how the window is presented and leaves its resolution alone, so leaving fullscreen at
        /// 1920x1080 produced a 1920x1080 window on a 1920x1080 desktop. A size has to be chosen.
        ///
        /// The largest 16:9 box that fits with the desktop's own furniture left clear. Sixteen by
        /// nine because that is what the panel scales against, and a window of another shape gets
        /// letterboxed by the scaler rather than showing more of anything.
        ///
        /// Pure and separate from <see cref="ApplyDisplayMode"/> so it can be tested: `Screen`
        /// cannot be driven from a test, and the half worth testing is the arithmetic. Same split
        /// as `KeyboardShortcuts.Resolve`.
        /// </summary>
        public static Vector2Int WindowedSize(int displayWidth, int displayHeight)
        {
            // Clamped before the arithmetic rather than after it. A display driver reporting
            // something absurd would otherwise overflow the multiply below, and a float that does
            // not fit an int converts to a value nothing here should be reasoning about.
            var wide = Mathf.Clamp(displayWidth, 1, 32_000);
            var tall = Mathf.Clamp(displayHeight, 1, 32_000);

            var roomAcross = Mathf.FloorToInt(wide * SideFraction);
            var roomDown = tall - VerticalChrome;

            var width = Mathf.Min(roomAcross, Mathf.FloorToInt(roomDown * DesignAspect));

            // The floor wins over the fit on a display too small for either. A window of six
            // hundred pixels is not a smaller game, it is an unreadable one, and every display the
            // game will meet is large enough for this to be moot.
            width = Mathf.Clamp(width, SmallestUsableWidth, DesignWidth);

            return new Vector2Int(width, Mathf.RoundToInt(width / DesignAspect));
        }

        private static void ApplyDisplayMode()
        {
            // Changing the window while the editor or a batch run owns it does nothing useful and
            // can hang a headless test, so it is skipped in both.
            if (Application.isEditor || Application.isBatchMode)
            {
                return;
            }

            var display = Screen.currentResolution;

            if (Fullscreen)
            {
                Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);

                return;
            }

            var window = WindowedSize(display.width, display.height);

            Screen.SetResolution(window.x, window.y, FullScreenMode.Windowed);
        }
    }
}
