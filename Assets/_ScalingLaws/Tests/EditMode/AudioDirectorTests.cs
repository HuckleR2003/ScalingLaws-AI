using NUnit.Framework;
using ScalingLaws.UI;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>Audio is optional presentation, but its palette must always be complete and harmless.</summary>
    public sealed class AudioDirectorTests
    {
        [Test]
        public void TheOriginalMenuThemeAndEveryInterfaceCueAreAvailable()
        {
            var host = new GameObject("Audio test host");
            try
            {
                var director = host.AddComponent<AudioDirector>();

                Assert.That(director.MenuTheme, Is.Not.Null);
                Assert.That(director.MenuTheme.length, Is.GreaterThan(20f));

                foreach (UiSound sound in System.Enum.GetValues(typeof(UiSound)))
                {
                    Assert.That(director.HasCue(sound), Is.True, $"Missing {sound} cue.");
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
