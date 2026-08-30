using System.Collections;
using System.IO;
using NUnit.Framework;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using ScalingLaws.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// One frame of the investing screen with a real holding in a rival on it.
    ///
    /// **Rendered rather than drawn, because the number in the picture has to be one the game can
    /// actually produce.** The stake this was asked for was 47 per cent, and
    /// <see cref="ShareMarket.TradableShare"/> is 0.35: the market will not sell more than a third
    /// of any lab's float, so the game refuses 47 with "not for sale". A picture showing it would
    /// be contradicted by the game itself the first time anybody tried to repeat it, which is the
    /// worst thing a screenshot used for publicity can be.
    ///
    /// So the shot buys everything the market will let go of, and the caption is whatever that
    /// turns out to be. Written to <c>StakeShot~/</c> beside the other proof folders.
    /// </summary>
    public sealed class StakeShotTests
    {
        private const int Width = 1920;
        private const int Height = 1080;

        /// <summary>The lab to take a position in. Antropic, the parody of Anthropic.</summary>
        private const CompetitorId Target = CompetitorId.Anthropic;

        /// <summary>The clickable row carrying a lab's name, wherever the screen keeps it.</summary>
        private static VisualElement FindRow(VisualElement root, string labName)
        {
            foreach (var button in root.Query<Button>().ToList())
            {
                if (button.text != null && button.text.Contains(labName))
                {
                    return button;
                }

                foreach (var label in button.Query<Label>().ToList())
                {
                    if (label.text != null && label.text.Contains(labName))
                    {
                        return button;
                    }
                }
            }

            return null;
        }

        private static string ShotFolder =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "StakeShot~");

        [UnityTest]
        public IEnumerator TheInvestingScreenWithARealStakeInARival()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            var shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            var document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);

            var simulation = shell.Simulation;
            TabProofCampaign.Furnish(simulation);

            // Enough to pay for the position. A stake this size is a late-campaign decision and the
            // proof campaign is only two years in, so the cash is granted rather than earned. The
            // holding itself still goes through TryBuyShares, which is the part that has to be real.
            simulation.State.CashUsd = 40_000_000_000L;

            var available = simulation.SharesAvailableIn(Target);

            Assert.That(available, Is.GreaterThan(0L),
                "The market is offering nothing in that lab, so there is no position to photograph.");

            Assert.That(simulation.TryBuyShares(Target, available, out var cost, out var why),
                Is.True, why);

            var outstanding = ShareMarket.SharesOutstanding(Target);
            var percent = 100.0 * simulation.SharesHeldIn(Target) / outstanding;

            Debug.Log($"STAKE: {percent:N1}% of {Target} for ${cost:N0}. "
                + $"The market's ceiling is {ShareMarket.TradableShare * 100:N0}% of the float.");

            Loc.Current = Language.English;

            var settings = Object.Instantiate(document.panelSettings);
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            settings.targetTexture = texture;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(Width, Height);
            document.panelSettings = settings;

            foreach (var ringing in document.rootVisualElement.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            yield return null;

            Assert.That(shell.OpenScreenByName("Investing"), Is.True,
                "The shell has no screen called Investing.");

            yield return null;

            // The screen opens on the first lab in the list, so the panel showed OpenSI and every
            // figure in it read zero while the holding sat two rows below as a badge. Select the
            // lab the shot is about. A submit rather than a synthetic pointer press, because a
            // press is three events that have to arrive in the right order to do the same thing.
            var row = FindRow(document.rootVisualElement, CompetitorCatalog.NameOf(Target));

            Assert.That(row, Is.Not.Null,
                $"No row for {CompetitorCatalog.NameOf(Target)} on the investing screen.");

            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = row;
                row.SendEvent(submit);
            }

            for (var pass = 0; pass < 10; pass++)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.7f);

            var readable = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            readable.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(ShotFolder);
            File.WriteAllBytes(Path.Combine(ShotFolder, "stake_in_antropic.png"),
                readable.EncodeToPNG());

            // A render texture nobody drew into is a rectangle of the clear colour, which on a dark
            // page is indistinguishable from a screen that worked.
            var pixels = readable.GetPixels32();
            var lowest = 255;
            var highest = 0;

            for (var index = 0; index < pixels.Length; index += 41)
            {
                var value = (pixels[index].r + pixels[index].g + pixels[index].b) / 3;
                lowest = Mathf.Min(lowest, value);
                highest = Mathf.Max(highest, value);
            }

            Object.DestroyImmediate(readable);

            Assert.That((highest - lowest) / 255f, Is.GreaterThan(0.02f),
                "The frame came back flat, so nothing was drawn into the panel.");
        }
    }
}
