using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScalingLaws.Core;
using ScalingLaws.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>The small vocabulary of interface sounds. The names describe intent, not a screen.</summary>
    public enum UiSound
    {
        Hover,
        Click,
        Confirm,
        Deny,
        Tab,
        Positive,
        Warning,
        Page,
        Message,
        PhoneOpen,
        PhoneClose
    }

    /// <summary>Which loop is wanted. What is playing is a separate question until it is built.</summary>
    public enum MusicTrack
    {
        None,
        Menu,
        Office,
        Creator
    }

    /// <summary>
    /// Original procedural audio for the interface, the menu and the game.
    ///
    /// Nothing here is a sample. The seven short interface cues are synthesised in this file exactly
    /// as they always were; the three music loops and the four longer cues come from
    /// <see cref="Synth"/>. The project therefore carries no unlicensed audio and a missing imported
    /// asset can never break a screen. This component owns presentation only: it never reads or
    /// mutates the simulation, a save, or the clock.
    ///
    /// **The music is built on a worker thread and that is not an optimisation.** Measured on this
    /// machine the three loops cost about 4.4 seconds of one core between them, and the office loop
    /// alone is 2.1. Built where the seven cues are built, that is a game which freezes on the
    /// splash screen for four seconds on a fast machine and longer on a slow one. Built behind the
    /// scene, the menu appears at once and its music arrives a second or two later, which nobody
    /// notices and nobody can complain about. The short cues stay synchronous because all eleven of
    /// them together cost under forty milliseconds.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        private const int SampleRate = 24000;
        private const float MusicVolume = 0.18f;
        private const float HoverVolume = 0.16f;
        private const float ClickVolume = 0.24f;

        private static AudioDirector instance;

        private readonly Dictionary<UiSound, AudioClip> cues = new();
        private readonly HashSet<VisualElement> boundRoots = new();

        private AudioSource musicSource;
        private AudioSource effectsSource;
        private AudioListener fallbackListener;

        private AudioClip menuTheme;
        private AudioClip officeTheme;
        private AudioClip creatorTheme;

        private MusicTrack wanted = MusicTrack.None;
        private MusicTrack playing = MusicTrack.None;
        private float appliedMusicVolume = -1f;
        private float nextHoverTime;

        /// <summary>Creates the single director before either UI document builds.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            if (instance != null)
            {
                return;
            }

            var host = new GameObject("Scaling Laws Audio");
            instance = host.AddComponent<AudioDirector>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = MusicVolume;

            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.volume = 1f;

            // Existing scenes predate audio and do not all carry a listener. This fallback makes
            // the new sound work immediately, then gets out of the way if a later scene owns one.
            fallbackListener = gameObject.AddComponent<AudioListener>();

            BuildPalette();
            StartCoroutine(BuildMusicOffThread());
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                instance = null;
            }
        }

        /// <summary>
        /// Keeps the music at whatever the player last asked for.
        ///
        /// Polled rather than pushed, and cheap because it only writes when the number has actually
        /// moved. The alternative is `GameSettings` raising an event, which would make persistence
        /// know about presentation for the sake of one float.
        /// </summary>
        private void Update()
        {
            // **Divided back out of the listener.** The master slider is on `AudioListener`, and
            // the player asked for two independent controls rather than one inside the other, so the
            // music undoes the listener's share and applies its own. Guarded against a master of
            // zero, which would otherwise be a division by nothing and a burst of full-volume music.
            var master = Mathf.Max(0.0001f, GameSettings.MasterVolume);
            var target = MusicVolume * GameSettings.MusicVolume / master;

            if (musicSource != null && !Mathf.Approximately(target, appliedMusicVolume))
            {
                appliedMusicVolume = target;
                musicSource.volume = target;
            }
        }

        // =====================================================================================
        // WHAT THE REST OF THE INTERFACE CALLS
        // =====================================================================================

        /// <summary>Plays a named cue. Safe before the audio service has loaded.</summary>
        public static void Play(UiSound sound)
        {
            instance?.PlayCue(sound, DefaultVolume(sound));
        }

        /// <summary>Lets a screen use the affirmative cue without knowing about AudioSource.</summary>
        public static void Confirm() => Play(UiSound.Confirm);

        /// <summary>Lets a refused action give feedback without becoming a simulation dependency.</summary>
        public static void Deny() => Play(UiSound.Deny);

        /// <summary>Lets an important completion or event mark itself with a restrained cue.</summary>
        public static void Positive() => Play(UiSound.Positive);

        /// <summary>Lets an important caution mark itself with a restrained cue.</summary>
        public static void Warning() => Play(UiSound.Warning);

        /// <summary>A notice arriving. One page, turning, next to the player's hands.</summary>
        public static void Page() => Play(UiSound.Page);

        /// <summary>Something has come in on the phone.</summary>
        public static void Message() => Play(UiSound.Message);

        /// <summary>The phone coming up, and any other pick that is not a press.</summary>
        public static void PhoneOpen() => Play(UiSound.PhoneOpen);

        /// <summary>The phone going away.</summary>
        public static void PhoneClose() => Play(UiSound.PhoneClose);

        /// <summary>
        /// Asks for a loop. Takes effect when that loop has finished building, and not before.
        ///
        /// Idempotent, so a screen may call it on every rebuild without restarting the music. That
        /// matters: `GameShell.Show` runs a full rebuild on almost every click.
        /// </summary>
        public static void SetTrack(MusicTrack track)
        {
            if (instance == null)
            {
                return;
            }

            instance.wanted = track;
            instance.ApplyTrack();
        }

        /// <summary>Available to tests and later explicit screen wiring.</summary>
        public bool HasCue(UiSound sound)
        {
            EnsurePalette();
            return cues.TryGetValue(sound, out var clip) && clip != null;
        }

        /// <summary>
        /// The main-menu loop. Exposed for a lightweight audio contract test.
        ///
        /// **Builds on the spot if the worker has not finished.** In edit mode there is no worker at
        /// all, because `Awake` does not run, and a test that adds this component and asks for the
        /// theme has to get one. It costs about a second and a half in that one test and nothing at
        /// all in the game, where the coroutine has almost always won the race.
        /// </summary>
        public AudioClip MenuTheme
        {
            get
            {
                if (menuTheme == null)
                {
                    menuTheme = ToClip("Menu Theme - First Light", Synth.MenuTheme(2));
                }

                return menuTheme;
            }
        }

        // =====================================================================================
        // BUILDING
        // =====================================================================================

        /// <summary>
        /// Builds the palette on first use if the lifecycle has not already done it.
        ///
        /// **`Awake` does not run in edit mode**, so an EditMode test that adds this component gets
        /// an object with no clips on it and the audio contract test failed on a null theme. Waiting
        /// for a lifecycle callback to produce data that is pure arithmetic is the wrong dependency
        /// anyway: the palette is synthesised from constants and needs nothing from the scene.
        ///
        /// Idempotent, so `Awake` calling it and an accessor calling it cost one build between them.
        /// </summary>
        private void EnsurePalette()
        {
            if (cues.Count == 0)
            {
                BuildPalette();
            }
        }

        /// <summary>
        /// The three loops, one after another, on a worker thread.
        ///
        /// Sequential rather than three tasks at once, deliberately: a player's machine is running a
        /// game, and taking three cores for four seconds to save two of them is a bad trade. The
        /// menu is first because it is the only one anybody hears in the first minute.
        ///
        /// `AudioClip.Create` and `SetData` are main thread only, so the arithmetic happens on the
        /// worker and the clip is made here, which is what the yield is for.
        /// </summary>
        private IEnumerator BuildMusicOffThread()
        {
            if (menuTheme == null)
            {
                var work = Task.Run(() => Synth.MenuTheme(2));
                while (!work.IsCompleted)
                {
                    yield return null;
                }

                menuTheme = ToClip("Menu Theme - First Light", work.Result);
                ApplyTrack();
            }

            var office = Task.Run(Synth.GameTheme);
            while (!office.IsCompleted)
            {
                yield return null;
            }

            officeTheme = ToClip("Office Theme - The Long Run", office.Result);
            ApplyTrack();

            var creator = Task.Run(Synth.CreatorTheme);
            while (!creator.IsCompleted)
            {
                yield return null;
            }

            creatorTheme = ToClip("Creator Theme - Eight Stages", creator.Result);
            ApplyTrack();
        }

        private static AudioClip ToClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void BuildPalette()
        {
            // The seven that shipped, unchanged. They are the sound of the interface and nobody
            // asked for a different one.
            cues[UiSound.Hover] = BuildTone("UI Hover", 0.045f, 660f, 920f, 0.16f, 0.04f, Wave.Sine);
            cues[UiSound.Click] = BuildTone("UI Click", 0.065f, 280f, 190f, 0.26f, 0.025f, Wave.Triangle);
            cues[UiSound.Confirm] = BuildChord("UI Confirm", 0.16f, new[] { 440f, 554.37f, 659.25f }, 0.19f);
            cues[UiSound.Deny] = BuildTone("UI Deny", 0.12f, 210f, 126f, 0.20f, 0.03f, Wave.Sine);
            cues[UiSound.Tab] = BuildTone("UI Tab", 0.055f, 480f, 610f, 0.16f, 0.02f, Wave.Triangle);
            cues[UiSound.Positive] = BuildChord("UI Positive", 0.22f, new[] { 392f, 493.88f, 587.33f }, 0.18f);
            cues[UiSound.Warning] = BuildTone("UI Warning", 0.20f, 330f, 246.94f, 0.18f, 0.04f, Wave.Sine);

            // The four longer ones. Under forty milliseconds for all of them, so there is no reason
            // to make anybody wait for these.
            cues[UiSound.Page] = ToClip("UI Page", Synth.PageTurn());
            cues[UiSound.Message] = ToClip("UI Message", Synth.Message());
            cues[UiSound.PhoneOpen] = ToClip("UI Phone Open", Synth.PhoneOpen());
            cues[UiSound.PhoneClose] = ToClip("UI Phone Close", Synth.PhoneClose());
        }

        // =====================================================================================
        // SCENES, TRACKS AND INPUT
        // =====================================================================================

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            boundRoots.Clear();
            SetTrack(scene.name == SceneFlow.MainMenuScene ? MusicTrack.Menu : MusicTrack.Office);
            StartCoroutine(BindDocumentsNextFrame());
        }

        /// <summary>
        /// Puts the wanted loop on, if it exists yet.
        ///
        /// Silence is the right answer while a loop is still being built. The alternative is playing
        /// the wrong one for a second and cutting it off, which is worse than waiting.
        /// </summary>
        private void ApplyTrack()
        {
            if (musicSource == null)
            {
                return;
            }

            var clip = wanted switch
            {
                MusicTrack.Menu => menuTheme,
                MusicTrack.Office => officeTheme,
                MusicTrack.Creator => creatorTheme,
                _ => null
            };

            if (clip == null)
            {
                if (wanted == MusicTrack.None)
                {
                    musicSource.Stop();
                    playing = MusicTrack.None;
                }

                return;
            }

            if (playing == wanted && musicSource.isPlaying)
            {
                return;
            }

            playing = wanted;
            musicSource.clip = clip;
            musicSource.time = 0f;
            musicSource.Play();
        }

        private IEnumerator BindDocumentsNextFrame()
        {
            // UI documents create their tree in OnEnable. Waiting one frame means the root exists
            // before callbacks are attached, without imposing an execution-order contract on UI.
            yield return null;

            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                Bind(document?.rootVisualElement);
            }

            RefreshListener();
        }

        private void RefreshListener()
        {
            if (fallbackListener == null)
            {
                return;
            }

            var sceneOwnsListener = false;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (listener != null && listener != fallbackListener && listener.enabled
                    && listener.gameObject.scene == SceneManager.GetActiveScene())
                {
                    sceneOwnsListener = true;
                    break;
                }
            }

            fallbackListener.enabled = !sceneOwnsListener;
        }

        private void Bind(VisualElement root)
        {
            if (root == null || !boundRoots.Add(root))
            {
                return;
            }

            root.RegisterCallback<PointerOverEvent>(OnPointerOver);
            root.RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        private void OnPointerOver(PointerOverEvent pointer)
        {
            if (FindButton(pointer.target) == null || Time.unscaledTime < nextHoverTime)
            {
                return;
            }

            nextHoverTime = Time.unscaledTime + 0.045f;
            PlayCue(UiSound.Hover, HoverVolume);
        }

        private void OnPointerDown(PointerDownEvent pointer)
        {
            if (pointer.button == 0 && FindButton(pointer.target) != null)
            {
                PlayCue(UiSound.Click, ClickVolume);
            }
        }

        private void PlayCue(UiSound sound, float volume)
        {
            EnsurePalette();

            if (effectsSource == null || !cues.TryGetValue(sound, out var clip) || clip == null)
            {
                return;
            }

            effectsSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        // =====================================================================================
        // THE ORIGINAL CUE SYNTHESIS, UNCHANGED
        // =====================================================================================

        private static AudioClip BuildTone(
            string name,
            float seconds,
            float startFrequency,
            float endFrequency,
            float amplitude,
            float attack,
            Wave wave)
        {
            var samples = new float[Mathf.Max(1, Mathf.CeilToInt(seconds * SampleRate))];
            var phase = 0f;
            for (var index = 0; index < samples.Length; index++)
            {
                var progress = index / (float)Mathf.Max(1, samples.Length - 1);
                var frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
                phase += 2f * Mathf.PI * frequency / SampleRate;
                var envelope = Envelope(progress * seconds, seconds, attack, seconds * 0.55f);
                samples[index] = amplitude * envelope * Sample(wave, phase);
            }

            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip BuildChord(string name, float seconds, float[] frequencies, float amplitude)
        {
            var samples = new float[Mathf.Max(1, Mathf.CeilToInt(seconds * SampleRate))];
            for (var index = 0; index < samples.Length; index++)
            {
                var time = index / (float)SampleRate;
                var value = 0f;
                foreach (var frequency in frequencies)
                {
                    value += Mathf.Sin(2f * Mathf.PI * frequency * time);
                }

                samples[index] = amplitude * Envelope(time, seconds, 0.018f, seconds * 0.5f)
                    * value / Mathf.Max(1, frequencies.Length);
            }

            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Envelope(float time, float duration, float attack, float release)
        {
            var safeAttack = Mathf.Max(0.001f, attack);
            var safeRelease = Mathf.Max(0.001f, release);
            var inLevel = Mathf.Clamp01(time / safeAttack);
            var outLevel = Mathf.Clamp01((duration - time) / safeRelease);
            return Mathf.Min(inLevel, outLevel);
        }

        private static float Sample(Wave wave, float phase) => wave == Wave.Triangle
            ? 2f / Mathf.PI * Mathf.Asin(Mathf.Sin(phase))
            : Mathf.Sin(phase);

        private static Button FindButton(IEventHandler target)
        {
            if (target is Button button)
            {
                return button;
            }

            return target is VisualElement element ? element.GetFirstAncestorOfType<Button>() : null;
        }

        private static float DefaultVolume(UiSound sound) => sound switch
        {
            UiSound.Hover => HoverVolume,
            UiSound.Click => ClickVolume,
            UiSound.Confirm => 0.30f,
            UiSound.Deny => 0.24f,
            UiSound.Tab => 0.20f,
            UiSound.Positive => 0.32f,
            UiSound.Warning => 0.28f,

            // The four from Synth are levelled in the waveform rather than here, so what the author
            // hears in the preview file is what a player hears in the game. A second multiplier in
            // this table would be a second place the answer lives.
            UiSound.Page => 1.0f,
            UiSound.Message => 1.0f,
            UiSound.PhoneOpen => 1.0f,
            UiSound.PhoneClose => 1.0f,

            _ => ClickVolume
        };

        private enum Wave
        {
            Sine,
            Triangle
        }
    }
}
