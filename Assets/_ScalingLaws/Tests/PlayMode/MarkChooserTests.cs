using System.Collections;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// One press of the mark arrow steps one mark, however many times the page was rebuilt.
    ///
    /// **The ratchet for a fault a render could never have shown.** `OfferMarks` runs every time
    /// the creator page is rebuilt, and the first version subscribed to the button there, so the
    /// handlers stacked: after ten rebuilds one press stepped the mark ten times. Everything would
    /// have looked right in a screenshot and been wrong in the hand.
    /// </summary>
    public sealed class MarkChooserTests
    {
        [UnityTest]
        public IEnumerator OneClickStepsOneMarkAfterManyRebuilds()
        {
            var texture = new RenderTexture(800, 600, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            var host = new GameObject("MarkChooser");
            var document = host.AddComponent<UIDocument>();

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet =
                Resources.Load<ThemeStyleSheet>("UnityThemes/UnityDefaultRuntimeTheme")
                ?? Resources.Load<ThemeStyleSheet>("unity default runtime theme");
            settings.targetTexture = texture;
            document.panelSettings = settings;

            try
            {
                yield return null;

                var steps = 0;
                var mock = new BrowserPreview();
                document.rootVisualElement.Add(mock.Root);
                mock.Show("Prometheus AI", "Muse 1", "Marcin");

                // What the creator does: the page is rebuilt, and the mock is pointed again.
                for (var rebuild = 0; rebuild < 10; rebuild++)
                {
                    mock.OfferMarks(by => steps += by);
                }

                yield return null;

                var forward = mock.Root.Query<Button>(className: "wb__arrow")
                    .ToList()
                    .LastOrDefault();

                Assert.That(forward, Is.Not.Null, "The mock has no mark arrows on it.");
                Assert.That(forward.enabledSelf, Is.True,
                    "The arrows were offered and are still drawn as a picture of a control.");

                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = forward;
                    forward.SendEvent(submit);
                }

                yield return null;

                Assert.That(steps, Is.EqualTo(1),
                    "One press moved the mark " + steps + " places, so the handler was attached "
                    + "once per rebuild rather than once.");
            }
            finally
            {
                Object.DestroyImmediate(host);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }
    }
}
