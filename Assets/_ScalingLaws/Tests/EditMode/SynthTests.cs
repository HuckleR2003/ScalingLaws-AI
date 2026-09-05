using System;
using NUnit.Framework;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The three loops, measured rather than listened to.
    ///
    /// **A loop that clicks once a minute is a fault nobody reports and everybody hears.** It is also
    /// the one thing about generated music that a test can settle completely, because the whole
    /// waveform is available as an array of floats and the question is arithmetic.
    ///
    /// Two different faults live at the loop point and only one of them is a click:
    ///
    /// - **A step between the last sample and the first** is a click. Measured against the ordinary
    ///   step between neighbouring samples, because a waveform that is simply loud has large steps
    ///   everywhere and comparing against a fixed number would fail on the office loop and pass on
    ///   silence.
    /// - **A step in how much is playing** is heard as the music starting over. This is the one the
    ///   author reported and the one no click test would have caught: the arrangement thinned out
    ///   towards the end of every loop, so the last second sat at a quarter of the peak and the
    ///   opening came back at two thirds.
    ///
    /// Written from the note the fix arrived with, which proposed exactly the first of these.
    /// </summary>
    public sealed class SynthTests
    {
        private static float[][] Loops() => new[]
        {
            Synth.MenuTheme(2), Synth.GameTheme(), Synth.CreatorTheme()
        };

        private static readonly string[] Names = { "menu", "office", "creator" };

        /// <summary>Mean absolute difference between neighbouring samples over a window.</summary>
        private static double TypicalStep(float[] wave, int from, int count)
        {
            var total = 0.0;

            for (var index = from; index < from + count && index + 1 < wave.Length; index++)
            {
                total += Math.Abs(wave[index + 1] - wave[index]);
            }

            return total / Math.Max(1, count);
        }

        /// <summary>Root mean square of one window, which is how loud that stretch actually is.</summary>
        private static double Loudness(float[] wave, int from, int count)
        {
            var total = 0.0;
            var read = 0;

            for (var index = from; index < from + count && index < wave.Length; index++)
            {
                total += wave[index] * (double)wave[index];
                read++;
            }

            return Math.Sqrt(total / Math.Max(1, read));
        }

        [Test]
        public void EveryLoopIsAudibleAndNeverClips()
        {
            var loops = Loops();

            for (var index = 0; index < loops.Length; index++)
            {
                var wave = loops[index];

                Assert.That(wave.Length, Is.GreaterThan(Synth.Rate * 10),
                    $"the {Names[index]} loop is shorter than ten seconds");

                var peak = 0f;
                foreach (var sample in wave)
                {
                    peak = Math.Max(peak, Math.Abs(sample));
                }

                Assert.That(peak, Is.GreaterThan(0.05f), $"the {Names[index]} loop is silence");
                Assert.That(peak, Is.LessThanOrEqualTo(1f), $"the {Names[index]} loop clips");
            }
        }

        /// <summary>
        /// No click where the loop turns over.
        ///
        /// Ten times a normal step is generous and deliberately so: this is here to catch a seam of
        /// the kind that comes from cutting a waveform mid-cycle, which measures in the hundreds.
        /// </summary>
        [Test]
        public void NoLoopClicksWhereItTurnsOver()
        {
            var loops = Loops();

            for (var index = 0; index < loops.Length; index++)
            {
                var wave = loops[index];
                var seam = Math.Abs(wave[0] - wave[^1]);
                var typical = TypicalStep(wave, 0, 2000);

                Assert.That(seam, Is.LessThan(typical * 10.0),
                    $"the {Names[index]} loop steps {seam / typical:F1} times a normal sample step "
                    + "where it turns over, which is a click");
            }
        }

        /// <summary>
        /// And no step in how much is playing, which is the fault a click test cannot see.
        ///
        /// **This is the one the author heard.** Before the crossfade the creator loop ended at 26%
        /// of its peak and restarted at 67%, and it is twenty four seconds long, so that happened
        /// every twenty four seconds. Nothing about it is a click and every sample-level check
        /// passed.
        ///
        /// Half is a loose bar on purpose: music is allowed to breathe, and a loop whose ending
        /// matched its opening to the decibel would be a loop with no shape.
        /// </summary>
        [Test]
        public void NoLoopRestartsMuchLouderThanItEnded()
        {
            var loops = Loops();
            var second = Synth.Rate;

            for (var index = 0; index < loops.Length; index++)
            {
                var wave = loops[index];

                var ending = Loudness(wave, wave.Length - second, second);
                var opening = Loudness(wave, 0, second);

                Assert.That(ending, Is.GreaterThan(opening * 0.5),
                    $"the {Names[index]} loop ends at {ending / opening:P0} of the level it opens "
                    + "at, which is heard as the music starting over rather than continuing");
            }
        }

        /// <summary>
        /// The crossfade does not simply turn the ending up.
        ///
        /// A blend that summed instead of crossfading would pass the test above by being louder,
        /// and would swell every time round. `cos` and `sin` squared sum to one, so the join sits
        /// inside the range the rest of the loop already occupies.
        /// </summary>
        [Test]
        public void TheJoinIsNoLouderThanTheLoopAroundIt()
        {
            var loops = Loops();
            var second = Synth.Rate;

            for (var index = 0; index < loops.Length; index++)
            {
                var wave = loops[index];

                var join = Loudness(wave, wave.Length - second, second);
                var whole = Loudness(wave, 0, wave.Length);

                Assert.That(join, Is.LessThan(whole * 2.0),
                    $"the {Names[index]} loop swells at the join, which is a sum rather than a "
                    + "crossfade");
            }
        }
    }
}
