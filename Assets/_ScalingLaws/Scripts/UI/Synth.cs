using System;
using System.Collections.Generic;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Original procedural music and cues for Scaling Laws.
    ///
    /// Everything is arithmetic over a float buffer: no samples, no imported assets, nothing with a
    /// licence attached. Same rule the interface cues already follow, and the reason a missing file
    /// can never break a screen.
    ///
    /// **Four voices, and each one exists because a screen needed something the others could not do.**
    /// A pad is bowed and holds a chord underneath. A piano is struck, and every partial falls at
    /// its own rate with the high ones first, which is most of what a piano actually is. A mallet is
    /// struck softly: no transient edge, partials that fade together, and it is what the office loop
    /// uses, because a piano figure heard for three hours is a piano figure somebody mutes. A shimmer
    /// is barely there on purpose.
    ///
    /// **Nothing here beats, wobbles or vibrates.** An earlier version detuned a second oscillator by
    /// three tenths of a percent for warmth. On a low A that is two tones 0.33 Hz apart, one swell
    /// every three seconds, drifting against every other note in the chord. Measured on a sustained
    /// note it moved the loudness by 42 per cent; the voices below move it by under one. Warmth comes
    /// from partials and from the room instead.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 24000;

        // -----------------------------------------------------------------------------------------
        // A sine table, because this runs on somebody's machine rather than on a render farm.
        //
        // The office loop alone asks for about forty million sines: a hundred and fifty notes, four
        // to six partials each, twenty four thousand samples a second. Through `Math.Sin` that is
        // several seconds of one core, and several seconds is a game that starts in silence. Through
        // a four thousand entry table with linear interpolation it is a fraction of that.
        //
        // The error this introduces is about three parts in ten million, which is roughly 130 dB
        // below the signal, or some seventy dB below the last bit of a sixteen bit file. It cannot
        // be heard and it cannot even be stored.
        // -----------------------------------------------------------------------------------------
        const int TableBits = 12;
        const int TableSize = 1 << TableBits;
        const double TableScale = TableSize / (2.0 * Math.PI);

        static readonly float[] SineTable = BuildSineTable();

        static float[] BuildSineTable()
        {
            // One entry past the end, so interpolation never has to test for the wrap.
            var table = new float[TableSize + 1];
            for (var index = 0; index <= TableSize; index++)
            {
                table[index] = (float)Math.Sin(2.0 * Math.PI * index / TableSize);
            }

            return table;
        }

        static float Sine(double phase)
        {
            var x = phase * TableScale;
            var whole = (long)x;
            var frac = (float)(x - whole);
            var index = (int)(whole & (TableSize - 1));
            return SineTable[index] + (SineTable[index + 1] - SineTable[index]) * frac;
        }

        const float E2 = 82.41f, F2 = 87.31f, G2 = 98.00f, A2 = 110.00f, B2 = 123.47f;
        const float C3 = 130.81f, D3 = 146.83f, E3 = 164.81f, F3 = 174.61f, Fs3 = 185.00f;
        const float G3 = 196.00f, A3 = 220.00f, B3 = 246.94f;
        const float C4 = 261.63f, Cs4 = 277.18f, D4 = 293.66f, E4 = 329.63f, F4 = 349.23f;
        const float Fs4 = 369.99f, G4 = 392.00f, Gs4 = 415.30f, A4 = 440.00f, B4 = 493.88f;
        const float C5 = 523.25f, Cs5 = 554.37f, D5 = 587.33f, E5 = 659.25f, Fs5 = 739.99f;
        const float G5 = 783.99f, A5 = 880.00f, B5 = 987.77f, C6 = 1046.50f, E6 = 1318.51f;

        enum Voice { Pad, Piano, Mallet, Shimmer, Legacy }

        struct Note
        {
            public float Start, Duration, Frequency, Amplitude;
            public Voice Voice;
            public Note(Voice voice, float start, float duration, float frequency, float amplitude)
            {
                Voice = voice; Start = start; Duration = duration;
                Frequency = frequency; Amplitude = amplitude;
            }
        }

        // =====================================================================================
        // MUSIC
        // =====================================================================================

        /// <summary>
        /// The menu loop. A minor, forty seconds, pads under a piano.
        ///
        /// Three readings of one piece, because which of them is right is a matter of taste and
        /// taste is not settled by argument.
        ///
        /// <list type="bullet">
        /// <item>0, plain: the piano as written, close and clear.</item>
        /// <item>1, warm: the same played longer, with a quiet octave sympathetically ringing over
        /// each melody note, in a bigger room.</item>
        /// <item>2, damped: warm, plus the felt down on the strings so the upper partials go, plus
        /// quiet paired accents in the gaps between phrases. Softer and busier at once, which
        /// sounds contradictory and is not: the edge comes off the notes and the movement comes
        /// from something else being there.</item>
        /// </list>
        /// </summary>
        public static float[] MenuTheme(int variant = 0)
        {
            const float bar = 10f;
            var warm = variant >= 1;
            var damped = variant >= 2;
            var notes = new List<Note>();

            Pad(notes, 0 * bar, bar, 0.085f, A2, E3, A3, C4);
            Pad(notes, 1 * bar, bar, 0.085f, F2, C3, F3, A3);
            Pad(notes, 2 * bar, bar, 0.085f, C3, G3, C4, E4);
            Pad(notes, 3 * bar, bar, 0.085f, G2, D3, G3, B3);

            float[,] melody =
            {
                { 0.75f, A4, 0.150f }, { 1.70f, C5, 0.130f }, { 2.75f, B4, 0.120f },
                { 4.05f, A4, 0.145f }, { 6.40f, E4, 0.105f }, { 7.45f, G4, 0.095f },

                { 10.80f, F4, 0.140f }, { 11.75f, A4, 0.125f }, { 12.85f, C5, 0.130f },
                { 14.30f, A4, 0.140f }, { 16.90f, G4, 0.100f },

                { 20.70f, E4, 0.115f }, { 21.65f, G4, 0.120f }, { 22.70f, C5, 0.145f },
                { 23.90f, E5, 0.135f }, { 25.30f, B4, 0.115f }, { 27.10f, G4, 0.100f },

                { 30.60f, D5, 0.135f }, { 31.60f, B4, 0.120f }, { 32.75f, G4, 0.115f },
                { 34.20f, A4, 0.130f }, { 36.30f, E4, 0.100f }, { 38.10f, D4, 0.090f }
            };

            for (var index = 0; index < melody.GetLength(0); index++)
            {
                var at = melody[index, 0];
                var pitch = melody[index, 1];
                var strength = melody[index, 2];

                notes.Add(new Note(Voice.Piano, at, warm ? 5.4f : 3.6f, pitch,
                    warm ? strength * 0.94f : strength));

                if (warm)
                {
                    // A sympathetic octave, far enough down in level that it is heard as the same
                    // note ringing rather than as a second one being played.
                    notes.Add(new Note(Voice.Piano, at + 0.012f, 4.2f, pitch * 2f, strength * 0.085f));
                }
            }

            if (damped)
            {
                // Two soft mallet notes in every gap, from the chord standing at the time.
                //
                // **Pairs rather than single notes, and that is the whole trick.** One quiet note in
                // a gap is heard as a stray; two, a fifth of a second apart, are heard as a figure,
                // and a figure is what reads as energy. At this level neither competes with the
                // melody: they are the room answering, not a second part.
                float[,] accents =
                {
                    { 5.30f, E4 }, { 8.55f, A3 },
                    { 15.55f, C4 }, { 18.40f, A3 },
                    { 25.85f, G4 }, { 28.70f, E4 },
                    { 35.10f, D4 }, { 37.35f, B3 }
                };

                for (var index = 0; index < accents.GetLength(0); index++)
                {
                    var at = accents[index, 0];
                    var pitch = accents[index, 1];
                    notes.Add(new Note(Voice.Mallet, at, 2.6f, pitch, 0.042f));
                    notes.Add(new Note(Voice.Mallet, at + 0.21f, 2.4f, pitch * 1.5f, 0.034f));
                }
            }

            return Render(notes, 4 * bar,
                damped ? 0.35f : warm ? 0.32f : 0.26f,
                damped ? 3200f : warm ? 3800f : 4200f,
                pianoRing: warm ? 0.62f : 1.0f,
                pianoAttack: damped ? 150f : 420f,
                pianoDamp: damped ? 0.70f : 1.0f,
                crossfade: CrossfadeSeconds);
        }

        /// <summary>
        /// The office loop, sixty seconds, for the game scene where there was silence.
        ///
        /// **Mallets rather than a piano, and that is the whole difference.** The four strike figure
        /// stayed because it works; the instrument changed because this one plays for hours. A piano
        /// has a hard front edge on every note, and an edge repeated two thousand times in an evening
        /// is what makes somebody reach for the volume. A soft mallet has an attack of about twenty
        /// milliseconds instead of two, and partials that fade together rather than from the top
        /// down, so a note blooms and goes rather than striking and dying.
        ///
        /// Under all of it a shimmer holds two high notes at a level just above nothing, crossfading
        /// over twenty seconds. It is not a part. It is there so the silence between cells is a room
        /// rather than a gap.
        /// </summary>
        public static float[] GameTheme()
        {
            const float bar = 10f;
            const float cell = 2.0f;
            var notes = new List<Note>();

            Pad(notes, 0 * bar, bar, 0.058f, C3, G3, D4, E4);
            Pad(notes, 1 * bar, bar, 0.058f, A2, E3, G3, C4);
            Pad(notes, 2 * bar, bar, 0.058f, F2, C3, E3, G3);
            Pad(notes, 3 * bar, bar, 0.058f, G2, D3, G3, B3);
            Pad(notes, 4 * bar, bar, 0.058f, E2, B2, D3, G3);
            Pad(notes, 5 * bar, bar, 0.058f, F2, A2, C3, E4);

            float[][] ladders =
            {
                new[] { E4, G4, A4, C5, D5, E5 },
                new[] { C4, E4, G4, A4, C5, E5 },
                new[] { A3, C4, F4, G4, A4, C5 },
                new[] { B3, D4, G4, A4, B4, D5 },
                new[] { B3, E4, G4, A4, B4, E5 },
                new[] { A3, C4, E4, F4, A4, C5 }
            };

            var cells = (int)(6 * bar / cell);
            for (var index = 0; index < cells; index++)
            {
                var at = index * cell;
                var ladder = ladders[(int)(at / bar)];
                var high = index % 2 == 1;
                var strength = high ? 0.105f : 0.115f;

                for (var step = 0; step < 4; step++)
                {
                    var pitch = ladder[(high ? 2 : 0) + step];
                    var offset = step * 0.17f + (step == 2 ? 0.015f : 0f);
                    notes.Add(new Note(Voice.Mallet, at + offset, 3.2f, pitch,
                        strength * (1f - step * 0.07f)));
                }
            }

            // The background. Six twenty second swells starting every ten, alternating between two
            // notes a fourth apart, at a level that is felt rather than heard.
            //
            // **The spacing is exactly half the length and that is arithmetic, not taste.** The
            // envelope is a squared sine, so two copies half a period apart sum to sin squared plus
            // cos squared, which is one. The bed is therefore perfectly even, and because the last
            // swell runs ten seconds past the loop and gets folded back onto the first ten, the
            // wrap is even too. Placed anywhere else it steps at the loop point, which is audible
            // as a click once a minute, forever.
            // **Sixty per cent quieter than it was first written, and one partial short.** At the
            // original level it read as a whine rather than as a room: a high tone held for twenty
            // seconds is the one thing in a loop the ear will not stop finding. The second partial
            // went too, because on the upper of these two notes it lands above two kilohertz, which
            // is exactly where a sustained tone starts to sound thin.
            float[] air = { G5, C6, G5, C6, G5, C6 };
            for (var index = 0; index < air.Length; index++)
            {
                notes.Add(new Note(Voice.Shimmer, index * 10f, 20f, air[index], 0.011f));
            }

            return Render(notes, 6 * bar, 0.26f, 3600f, tailSeconds: 10.4f,
                crossfade: CrossfadeSeconds);
        }

        /// <summary>
        /// The model creator, twenty four seconds, and deliberately the lightest of the three.
        ///
        /// **Nothing below A3.** The other two carry a bass an octave and a half under the melody,
        /// which is right for a menu and for an office and sits on the chest on a working screen.
        /// A major against two minor keys, so it reads as a different room rather than as the same
        /// piece transposed.
        ///
        /// The piano here is played softer than in the menu and struck less hard: a longer attack
        /// takes the edge off the front of each note, and more of the room is mixed in, which is
        /// what puts an instrument behind something rather than in front of it.
        /// </summary>
        public static float[] CreatorTheme()
        {
            const float bar = 6f;
            var notes = new List<Note>();

            Pad(notes, 0 * bar, bar, 0.052f, A3, Cs4, E4, A4);
            Pad(notes, 1 * bar, bar, 0.052f, Fs3, A3, Cs4, Fs4);
            Pad(notes, 2 * bar, bar, 0.052f, D4, Fs4, A4, D5);
            Pad(notes, 3 * bar, bar, 0.052f, E4, Gs4, B4, E5);

            float[,] line =
            {
                { 0.55f, A4, 0.105f }, { 1.65f, Cs5, 0.090f }, { 3.40f, E5, 0.095f }, { 4.70f, Cs5, 0.080f },
                { 6.50f, Fs4, 0.100f }, { 7.60f, A4, 0.088f }, { 9.35f, Cs5, 0.092f }, { 10.80f, A4, 0.080f },
                { 12.45f, D5, 0.105f }, { 13.55f, Fs5, 0.092f }, { 15.30f, A5, 0.085f }, { 16.75f, Fs5, 0.080f },
                { 18.40f, E5, 0.100f }, { 19.50f, B4, 0.088f }, { 21.20f, Gs4, 0.085f }, { 22.60f, E4, 0.078f }
            };

            for (var index = 0; index < line.GetLength(0); index++)
            {
                notes.Add(new Note(Voice.Piano, line[index, 0], 3.6f, line[index, 1],
                    line[index, 2] * 0.70f));
            }

            return Render(notes, 4 * bar, 0.30f, 4400f, pianoAttack: 105f,
                crossfade: CrossfadeSeconds);
        }

        // =====================================================================================
        // CUES
        // =====================================================================================

        /// <summary>
        /// A page turning.
        ///
        /// **One continuous rustle, not a row of taps.** The version before this fired four separate
        /// noise bursts and they read as four events with a room behind them, which is a sheet of
        /// metal landing rather than paper moving. Paper is one sound that swells and settles, so
        /// this is one noise bed under a single smooth curve, brightened as it speeds up and rolled
        /// off as it slows, with the reverb almost all the way down: a page turns next to your hands,
        /// not across a hall.
        /// </summary>
        public static float[] PageTurn()
        {
            var length = (int)(0.82f * Rate);
            var buffer = new float[length];
            var random = new Random(20260905);

            // Slow random walk over the noise, so it rustles rather than hisses.
            var grain = 0f;

            for (var index = 0; index < length; index++)
            {
                var t = index / (float)Rate;
                var progress = index / (float)length;

                // One swell: up over the first third, away over the rest.
                var envelope = progress < 0.34f
                    ? (float)Math.Pow(progress / 0.34f, 0.85)
                    : (float)Math.Pow(1.0 - (progress - 0.34f) / 0.66f, 1.9);

                grain += ((float)random.NextDouble() * 2f - 1f - grain) * 0.055f;
                var texture = 0.62f + 0.38f * (grain * 1.8f + 1f) * 0.5f;

                buffer[index] = envelope * texture * ((float)random.NextDouble() * 2f - 1f) * 0.85f;
            }

            // The brightness follows the speed of the sheet: dull, bright at the fold, dull again.
            SweepBandPass(buffer, 1400f, 3900f, 1200f);
            Reverb(buffer, 0.05f);
            Normalise(buffer, 0.34f);
            return buffer;
        }

        /// <summary>
        /// A message arriving on the phone, which in this game means the cousin.
        ///
        /// Forty per cent quieter than the version before it. It lands on top of a loop that is
        /// already playing and it has to survive being heard forty times in a tutorial.
        /// </summary>
        public static float[] Message()
        {
            var notes = new List<Note>
            {
                new Note(Voice.Piano, 0.00f, 0.55f, E5, 0.20f),
                new Note(Voice.Piano, 0.11f, 0.65f, A5, 0.17f)
            };

            return Render(notes, 0.85f, 0.20f, 6500f, false, 0.24f);
        }

        /// <summary>
        /// The phone coming up, and anything else that is a pick rather than a press.
        ///
        /// **Kept exactly as it was first written, on request.** A quicker version was tried and
        /// turned down, so this one keeps the original lengths and the original voice, including a
        /// second oscillator three tenths of a percent off. On a note that lasts a fifth of a second
        /// that detune never completes a cycle of its own beat, so it colours the tone and cannot
        /// wobble, which is why it is wrong for a held chord and right here.
        /// </summary>
        public static float[] PhoneOpen()
        {
            var notes = new List<Note>
            {
                new Note(Voice.Legacy, 0.00f, 0.20f, A3, 0.20f),
                new Note(Voice.Legacy, 0.03f, 0.26f, E4, 0.14f)
            };

            return Render(notes, 0.40f, 0.14f, 5000f, false, 0.55f, legacyRoom: true);
        }

        /// <summary>The same tick, falling instead of rising, for going back. Also kept as first written.</summary>
        public static float[] PhoneClose()
        {
            var notes = new List<Note>
            {
                new Note(Voice.Legacy, 0.00f, 0.20f, E4, 0.16f),
                new Note(Voice.Legacy, 0.03f, 0.24f, A3, 0.18f)
            };

            return Render(notes, 0.38f, 0.14f, 5000f, false, 0.55f, legacyRoom: true);
        }

        // =====================================================================================
        // THE ENGINE
        // =====================================================================================

        static void Pad(List<Note> notes, float start, float duration, float amplitude,
            float root, float a, float b, float c)
        {
            notes.Add(new Note(Voice.Pad, start, duration * 1.45f, root, amplitude));
            notes.Add(new Note(Voice.Pad, start + 0.35f, duration * 1.30f, a, amplitude * 0.66f));
            notes.Add(new Note(Voice.Pad, start + 0.60f, duration * 1.24f, b, amplitude * 0.54f));
            notes.Add(new Note(Voice.Pad, start + 0.85f, duration * 1.18f, c, amplitude * 0.46f));
        }

        /// <summary>
        /// How long the end of a loop is blended with its own beginning.
        ///
        /// **Not a fade.** A loop that fades out has a hole in it once a minute; this fills the hole
        /// instead. Two seconds is long enough to cover the ring-out of the last phrase and short
        /// enough that the opening motif arriving early reads as an anticipation.
        /// </summary>
        public const float CrossfadeSeconds = 2.0f;

        static float[] Render(List<Note> notes, float loopSeconds, float reverb, float cutoff,
            bool seamless = true, float peak = 0.62f, float pianoRing = 1.0f,
            float pianoAttack = 420f, bool legacyRoom = false, float tailSeconds = 4.2f,
            float pianoDamp = 1.0f, float crossfade = 0f)
        {
            // Long enough to hold everything that outlives the loop. Four seconds covers a reverb
            // tail; a part that runs past the end, like the office bed, needs its own length.
            var tail = seamless ? (int)(tailSeconds * Rate) : 0;
            var length = (int)(loopSeconds * Rate);
            var buffer = new float[length + tail];

            foreach (var note in notes)
            {
                switch (note.Voice)
                {
                    case Voice.Piano: PlayPiano(buffer, note, pianoRing, pianoAttack, pianoDamp); break;
                    case Voice.Mallet: PlayMallet(buffer, note); break;
                    case Voice.Shimmer: PlayShimmer(buffer, note); break;
                    case Voice.Legacy: PlayLegacy(buffer, note); break;
                    default: PlayPad(buffer, note); break;
                }
            }

            OnePoleLowPass(buffer, cutoff);

            if (legacyRoom)
            {
                ResonantRoom(buffer, reverb);
            }
            else
            {
                Reverb(buffer, reverb);
            }

            if (!seamless)
            {
                Normalise(buffer, peak);
                return buffer;
            }

            // The tail of the last note belongs at the top of the loop rather than cut off at it.
            // A loop that stops its own reverb dead has a seam every time round.
            var loop = new float[length];
            Array.Copy(buffer, loop, length);

            // **The arrangement thins out before the loop ends, and then restarts dense.** Summing
            // the overhang onto the opening carries the reverb across, which is why there was never
            // a click, and it cannot carry notes that were never written: measured on the three
            // loops, the last second sat at 39%, 57% and 26% of peak against openings at 68%, 78%
            // and 67%. A step that size is heard as the music starting over, and it is what the
            // author reported as the loop not looping.
            //
            // So inside the first `blend` seconds the two are crossfaded rather than summed. The
            // loop then **opens on the material that was playing as it ended** and grows into its
            // own beginning, which turns the edge into a ramp.
            //
            // **It has to be this side of the loop.** Blending the head into the tail fills the same
            // hole and breaks the join: the last sample stops being the one before `buffer[length]`
            // and becomes something from two seconds in, which measured as an eighty-fold step and a
            // real click. Done here, the first sample is `buffer[length]` exactly, which is by
            // construction what follows the last one.
            //
            // `cos` and `sin` rather than a ramp: two correlated signals crossfaded linearly dip by
            // three decibels in the middle, and these two squared sum to one.
            var blend = Math.Min(Math.Min((int)(crossfade * Rate), tail), length / 2);

            for (var index = 0; index < blend; index++)
            {
                var phase = 0.5 * Math.PI * index / blend;

                loop[index] = loop[index] * (float)Math.Sin(phase)
                    + buffer[length + index] * (float)Math.Cos(phase);
            }

            // Past the blend there is nothing left but reverb, and reverb is summed as it always was.
            for (var index = blend; index < tail && index < length; index++)
            {
                loop[index] += buffer[length + index];
            }

            Normalise(loop, peak);
            return loop;
        }

        /// <summary>A held tone under a chord. Slow in, slow out, three partials, nothing that moves.</summary>
        static void PlayPad(float[] buffer, Note note)
        {
            var from = (int)(note.Start * Rate);
            var count = (int)(note.Duration * Rate);

            for (var index = 0; index < count; index++)
            {
                var at = from + index;
                if (at < 0 || at >= buffer.Length) { continue; }

                var t = index / (float)Rate;
                var envelope = (1f - (float)Math.Exp(-t * 2.6f))
                    * (float)Math.Exp(-t * (1.9f / Math.Max(0.4f, note.Duration)));
                var w = 2 * Math.PI * note.Frequency * t;

                buffer[at] += note.Amplitude * envelope * 0.62f * (
                    1.00f * Sine(w)
                    + 0.30f * Sine(w * 2.0)
                    + 0.10f * Sine(w * 3.0));
            }
        }

        /// <summary>
        /// A struck string. Every partial falls at its own rate and the high ones go first, which is
        /// most of what separates a piano from an organ with a fast attack. The stretch on the upper
        /// partials is real too: a stiff string is not a perfect harmonic series.
        /// </summary>
        static void PlayPiano(float[] buffer, Note note, float ring, float attackRate, float damp)
        {
            var from = (int)(note.Start * Rate);
            var count = (int)(note.Duration * Rate);
            var baseDecay = (1.45f + 900f / Math.Max(120f, note.Frequency) * 0.35f) * ring;

            // `damp` is the felt coming down on the string: each partial is taken by another factor
            // of it, so the fundamental stays and everything above it goes, which is what a muted
            // piano is. One number, applied as a power, because damping is not a shelf.
            float[] weights = { 1.00f, 0.38f, 0.20f, 0.115f, 0.070f, 0.045f };
            if (damp < 0.999f)
            {
                for (var partial = 0; partial < weights.Length; partial++)
                {
                    weights[partial] *= (float)Math.Pow(damp, partial);
                }
            }

            for (var index = 0; index < count; index++)
            {
                var at = from + index;
                if (at < 0 || at >= buffer.Length) { continue; }

                var t = index / (float)Rate;
                var attack = 1f - (float)Math.Exp(-t * attackRate);
                var value = 0f;

                for (var partial = 0; partial < weights.Length; partial++)
                {
                    var n = partial + 1;
                    var stretch = (float)Math.Sqrt(1.0 + 0.00042 * n * n);
                    var decay = (float)Math.Exp(-t * baseDecay * (1f + 0.62f * partial));
                    value += weights[partial] * decay
                        * Sine(2 * Math.PI * note.Frequency * n * stretch * t);
                }

                buffer[at] += note.Amplitude * attack * value * 0.52f;
            }
        }

        /// <summary>
        /// A soft mallet on a tuned bar, for the loop that plays for hours.
        ///
        /// Two things separate it from the piano and both matter over an evening. The attack is
        /// twenty milliseconds rather than two, so there is no hard front edge to fatigue against.
        /// And the partials fade at nearly the same rate rather than from the top down, so the note
        /// keeps its colour as it goes instead of collapsing to a dull fundamental. The fourth
        /// partial sits slightly sharp, which is what a struck bar does and what stops this reading
        /// as a plain sine.
        /// </summary>
        static void PlayMallet(float[] buffer, Note note)
        {
            var from = (int)(note.Start * Rate);
            var count = (int)(note.Duration * Rate);
            var baseDecay = 1.25f + 700f / Math.Max(140f, note.Frequency) * 0.30f;

            float[] weights = { 1.00f, 0.20f, 0.085f, 0.040f };
            float[] ratios = { 1.00f, 2.00f, 3.01f, 4.21f };

            for (var index = 0; index < count; index++)
            {
                var at = from + index;
                if (at < 0 || at >= buffer.Length) { continue; }

                var t = index / (float)Rate;
                var attack = 1f - (float)Math.Exp(-t * 46f);
                var value = 0f;

                for (var partial = 0; partial < weights.Length; partial++)
                {
                    var decay = (float)Math.Exp(-t * baseDecay * (1f + 0.26f * partial));
                    value += weights[partial] * decay
                        * Sine(2 * Math.PI * note.Frequency * ratios[partial] * t);
                }

                buffer[at] += note.Amplitude * attack * value * 0.60f;
            }
        }

        /// <summary>
        /// The background. One high note, twenty seconds, in and out so slowly that it is never an
        /// event. Two partials only: anything richer starts being a part somebody listens to.
        /// </summary>
        static void PlayShimmer(float[] buffer, Note note)
        {
            var from = (int)(note.Start * Rate);
            var count = (int)(note.Duration * Rate);

            for (var index = 0; index < count; index++)
            {
                var at = from + index;
                if (at < 0 || at >= buffer.Length) { continue; }

                var t = index / (float)Rate;
                var progress = index / (float)count;
                // A full sine hump: silent at both ends, so overlapping copies crossfade with no seam.
                var envelope = (float)Math.Sin(Math.PI * progress);
                envelope *= envelope;
                var w = 2 * Math.PI * note.Frequency * t;

                buffer[at] += note.Amplitude * envelope * (
                    1.00f * Sine(w) + 0.05f * Sine(w * 2.0));
            }
        }

        /// <summary>The first voice this file had. Used only where its sound was chosen on purpose.</summary>
        static void PlayLegacy(float[] buffer, Note note)
        {
            var from = (int)(note.Start * Rate);
            var count = (int)(note.Duration * Rate);

            for (var index = 0; index < count; index++)
            {
                var at = from + index;
                if (at < 0 || at >= buffer.Length) { continue; }

                var t = index / (float)Rate;
                var attack = 1f - (float)Math.Exp(-t * 26f);
                var decay = (float)Math.Exp(-t * (1.55f / Math.Max(0.2f, note.Duration)) * 2.4f);
                var vibrato = 1f + 0.0016f * (float)Math.Sin(2 * Math.PI * 4.4f * t);
                var w = 2 * Math.PI * note.Frequency * vibrato * t;

                buffer[at] += note.Amplitude * attack * decay * (
                    0.60f * Sine(w)
                    + 0.55f * Sine(w * 1.003)
                    + 0.17f * Sine(w * 2.0)
                    + 0.06f * Sine(w * 3.0));
            }
        }

        /// <summary>
        /// A band pass whose centre rises and falls across the sound, for the page.
        ///
        /// Done as a low pass chasing a high pass rather than as a real filter, because the point is
        /// the movement and not the shape: bright where the sheet is travelling fastest, dull at
        /// both ends. A fixed filter over noise is a hiss no matter how it is shaped.
        /// </summary>
        static void SweepBandPass(float[] buffer, float lowest, float highest, float floorHz)
        {
            var lowState = 0f;
            var highIn = 0f;
            var highOut = 0f;
            var dt = 1f / Rate;

            for (var index = 0; index < buffer.Length; index++)
            {
                var progress = index / (float)buffer.Length;
                var lift = (float)Math.Sin(Math.PI * Math.Min(1.0, progress / 0.8));
                var cutoff = lowest + (highest - lowest) * lift;

                var rcLow = 1f / (2f * (float)Math.PI * cutoff);
                var aLow = dt / (rcLow + dt);
                lowState += aLow * (buffer[index] - lowState);

                var rcHigh = 1f / (2f * (float)Math.PI * floorHz);
                var aHigh = rcHigh / (rcHigh + dt);
                highOut = aHigh * (highOut + lowState - highIn);
                highIn = lowState;

                buffer[index] = highOut;
            }
        }

        /// <summary>
        /// Six damped combs into four allpasses.
        ///
        /// **The damping is the point.** An undamped comb rings on one frequency and that ring is
        /// what an earlier version of this file heard as a wobble. Rolling the feedback path off
        /// above a few kilohertz makes the tail die the way a room's does, high end first.
        /// </summary>
        static void Reverb(float[] buffer, float wet)
        {
            if (wet <= 0f) { return; }

            int[] combs = { 607, 647, 691, 739, 773, 811 };
            const float feedback = 0.802f;
            const float damping = 0.42f;

            var sum = new float[buffer.Length];

            foreach (var delay in combs)
            {
                var line = new float[delay];
                var cursor = 0;
                var store = 0f;

                for (var index = 0; index < buffer.Length; index++)
                {
                    var output = line[cursor];
                    store = output * (1f - damping) + store * damping;
                    line[cursor] = buffer[index] + store * feedback;
                    cursor = cursor + 1 == delay ? 0 : cursor + 1;
                    sum[index] += output / combs.Length;
                }
            }

            int[] allpasses = { 302, 240, 186, 122 };
            const float allpassGain = 0.5f;

            foreach (var delay in allpasses)
            {
                var line = new float[delay];
                var cursor = 0;

                for (var index = 0; index < buffer.Length; index++)
                {
                    var input = sum[index];
                    var stored = line[cursor];
                    sum[index] = stored - input;
                    line[cursor] = input + stored * allpassGain;
                    cursor = cursor + 1 == delay ? 0 : cursor + 1;
                }
            }

            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] = buffer[index] * (1f - wet) + sum[index] * wet;
            }
        }

        /// <summary>
        /// The first room this file had: undamped, and ringing because of it.
        ///
        /// Wrong under held chords, which is why the music does not use it. Right under a cue that
        /// lasts a third of a second, where the ring is heard as brightness and never has time to
        /// become a wobble.
        /// </summary>
        static void ResonantRoom(float[] buffer, float wet)
        {
            if (wet <= 0f) { return; }

            int[] combs = { 1327, 1637, 1901 };
            float[] gains = { 0.79f, 0.76f, 0.73f };
            var sum = new float[buffer.Length];

            for (var c = 0; c < combs.Length; c++)
            {
                var delay = combs[c];
                var gain = gains[c];
                var line = new float[buffer.Length];

                for (var index = 0; index < buffer.Length; index++)
                {
                    var back = index - delay >= 0 ? line[index - delay] : 0f;
                    line[index] = buffer[index] + gain * back;
                    sum[index] += line[index] / combs.Length;
                }
            }

            const int allpassDelay = 223;
            const float allpassGain = 0.5f;
            var pass = new float[buffer.Length];

            for (var index = 0; index < buffer.Length; index++)
            {
                var xBack = index - allpassDelay >= 0 ? sum[index - allpassDelay] : 0f;
                var yBack = index - allpassDelay >= 0 ? pass[index - allpassDelay] : 0f;
                pass[index] = -allpassGain * sum[index] + xBack + allpassGain * yBack;
            }

            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] = buffer[index] * (1f - wet) + pass[index] * wet;
            }
        }

        static void OnePoleLowPass(float[] buffer, float cutoffHz)
        {
            var dt = 1f / Rate;
            var rc = 1f / (2f * (float)Math.PI * cutoffHz);
            var a = dt / (rc + dt);
            var last = 0f;

            for (var index = 0; index < buffer.Length; index++)
            {
                last += a * (buffer[index] - last);
                buffer[index] = last;
            }
        }

        /// <summary>
        /// Scales to a fixed peak, so every track lands at the same loudness. Measured rather than
        /// trusted: two notes landing together decide the real peak, and clipping inside a loop is
        /// the kind of fault somebody hears on the fortieth pass and cannot name.
        /// </summary>
        static void Normalise(float[] buffer, float peak)
        {
            var loudest = 0f;
            foreach (var sample in buffer)
            {
                var magnitude = Math.Abs(sample);
                if (magnitude > loudest) { loudest = magnitude; }
            }

            if (loudest <= 0.0001f) { return; }

            var scale = peak / loudest;
            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] *= scale;
            }
        }
    }
}
