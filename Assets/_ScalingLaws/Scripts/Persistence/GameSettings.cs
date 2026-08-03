using UnityEngine;

namespace ScalingLaws.Persistence
{
    /// <summary>
    /// Player preferences that are not part of a campaign: volume, window mode, motion.
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

        public const float DefaultMasterVolume = 0.8f;

        public static float MasterVolume { get; private set; } = DefaultMasterVolume;
        public static bool Fullscreen { get; private set; }

        /// <summary>
        /// Shortens or removes the opening sequence and any drift on the office camera. This is an
        /// accessibility setting, not a performance one, so it is honoured everywhere without a
        /// quality tradeoff attached.
        /// </summary>
        public static bool ReduceMotion { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeFirstScene() => Reload();

        public static void Reload()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, 0) == 1;
            ReduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;

            AudioListener.volume = MasterVolume;
            ApplyDisplayMode();
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
