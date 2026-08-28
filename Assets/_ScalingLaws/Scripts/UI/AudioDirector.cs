using System;
using System.Collections;
using System.Collections.Generic;
using ScalingLaws.Core;
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
        Warning
    }

    /// <summary>
    /// Original procedural audio for the interface and main menu.
    ///
    /// The menu theme is intentionally sparse, warm and slow, but it is not a recreation of any
    /// existing game track. Both the music and the short UI sounds are synthesised from simple waves
    /// at runtime, so the project carries no unlicensed samples and a missing imported asset can
    /// never break a screen. This component owns presentation only. It never reads or mutates the
    /// simulation, a save, or the clock.
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

        /// <summary>Available to tests and later explicit screen wiring.</summary>
        public bool HasCue(UiSound sound)
        {
            EnsurePalette();
            return cues.TryGetValue(sound, out var clip) && clip != null;
        }

        /// <summary>The original main-menu loop. Exposed for a lightweight audio contract test.</summary>
        public AudioClip MenuTheme
        {
            get
            {
                EnsurePalette();
                return menuTheme;
            }
        }

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
            if (menuTheme == null || cues.Count == 0)
            {
                BuildPalette();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            boundRoots.Clear();
            SetMenuMusic(scene.name == SceneFlow.MainMenuScene);
            StartCoroutine(BindDocumentsNextFrame());
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

        private void SetMenuMusic(bool shouldPlay)
        {
            if (musicSource == null || menuTheme == null)
            {
                return;
            }

            if (!shouldPlay)
            {
                musicSource.Stop();
                return;
            }

            musicSource.clip = menuTheme;
            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        private void PlayCue(UiSound sound, float volume)
        {
            if (effectsSource == null || !cues.TryGetValue(sound, out var clip) || clip == null)
            {
                return;
            }

            effectsSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void BuildPalette()
        {
            menuTheme = BuildMenuTheme();
            cues[UiSound.Hover] = BuildTone("UI Hover", 0.045f, 660f, 920f, 0.16f, 0.04f, Wave.Sine);
            cues[UiSound.Click] = BuildTone("UI Click", 0.065f, 280f, 190f, 0.26f, 0.025f, Wave.Triangle);
            cues[UiSound.Confirm] = BuildChord("UI Confirm", 0.16f, new[] { 440f, 554.37f, 659.25f }, 0.19f);
            cues[UiSound.Deny] = BuildTone("UI Deny", 0.12f, 210f, 126f, 0.20f, 0.03f, Wave.Sine);
            cues[UiSound.Tab] = BuildTone("UI Tab", 0.055f, 480f, 610f, 0.16f, 0.02f, Wave.Triangle);
            cues[UiSound.Positive] = BuildChord("UI Positive", 0.22f, new[] { 392f, 493.88f, 587.33f }, 0.18f);
            cues[UiSound.Warning] = BuildTone("UI Warning", 0.20f, 330f, 246.94f, 0.18f, 0.04f, Wave.Sine);
        }

        private static AudioClip BuildMenuTheme()
        {
            const float seconds = 28f;
            var samples = new float[Mathf.CeilToInt(seconds * SampleRate)];
            var notes = new[]
            {
                new Note(0.6f, 4.2f, 220f, 0.13f), new Note(5.4f, 3.8f, 329.63f, 0.10f),
                new Note(10.1f, 4.5f, 261.63f, 0.12f), new Note(16.0f, 3.7f, 293.66f, 0.10f),
                new Note(20.8f, 4.8f, 246.94f, 0.12f), new Note(25.3f, 2.1f, 369.99f, 0.09f)
            };

            for (var index = 0; index < samples.Length; index++)
            {
                var time = index / (float)SampleRate;
                var value = 0.0f;

                // A low, slowly moving pad gives the notes space without introducing a recognisable
                // borrowed melody. It resets cleanly at the loop boundary.
                value += 0.035f * Mathf.Sin(2f * Mathf.PI * 110f * time);
                value += 0.018f * Mathf.Sin(2f * Mathf.PI * 164.81f * time + 0.9f);

                foreach (var note in notes)
                {
                    var local = time - note.Start;
                    if (local < 0f || local > note.Duration)
                    {
                        continue;
                    }

                    var envelope = Envelope(local, note.Duration, 0.32f, 1.25f);
                    var phase = 2f * Mathf.PI * note.Frequency * local;
                    value += note.Amplitude * envelope * (Mathf.Sin(phase) + 0.18f * Mathf.Sin(phase * 2f));
                }

                samples[index] = Mathf.Clamp(value, -0.35f, 0.35f);
            }

            var clip = AudioClip.Create("Menu Theme - First Light", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

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
            _ => ClickVolume
        };

        private enum Wave
        {
            Sine,
            Triangle
        }

        private readonly struct Note
        {
            public Note(float start, float duration, float frequency, float amplitude)
            {
                Start = start;
                Duration = duration;
                Frequency = frequency;
                Amplitude = amplitude;
            }

            public float Start { get; }
            public float Duration { get; }
            public float Frequency { get; }
            public float Amplitude { get; }
        }
    }
}
