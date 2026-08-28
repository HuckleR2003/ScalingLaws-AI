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
    /// Pictures for a store page, staged around one claim each.
    ///
    /// **This is not the contact sheet.** `TabProofTests` photographs every screen against one
    /// ordinary campaign, which is the right thing for reviewing a design and the wrong thing for a
    /// listing: a screenshot only earns its place if it shows the thing the caption says.
    ///
    /// So each shot here builds the exact state its claim needs. The cabinet that is throttling is
    /// genuinely over its cooling. The regulator's banner is a real open inspection with real days
    /// left on it. Nothing is mocked up for the camera, because a listing built on staged numbers is
    /// a listing that stops being true the first time somebody plays.
    ///
    /// Frames land in <c>StoreShots~/</c>, named after the claim rather than the screen.
    /// </summary>
    public sealed class StoreShotTests
    {
        private const int Width = 1920;
        private const int Height = 1080;

        private static string Folder =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "StoreShots~");

        private RenderTexture texture;
        private PanelSettings settings;
        private UIDocument document;
        private GameShell shell;

        private IEnumerator Shoot(string fileName)
        {
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

            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(Path.Combine(Folder, fileName), readable.EncodeToPNG());
            Object.DestroyImmediate(readable);

            Debug.Log($"StoreShots~/{fileName}");
        }

        [UnityTest]
        public IEnumerator EveryClaimHasAPictureBehindIt()
        {
            SceneFlow.ResumeSavedCampaign = false;
            SceneManager.LoadScene(SceneFlow.GameScene);

            yield return null;
            yield return null;

            shell = Object.FindFirstObjectByType<GameShell>();
            Assert.That(shell, Is.Not.Null, "The game scene has no shell on it.");

            document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);

            TabProofCampaign.Furnish(shell.Simulation);
            Loc.Current = Language.English;

            settings = Object.Instantiate(document.panelSettings);
            texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            texture.Create();

            settings.targetTexture = texture;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(Width, Height);
            document.panelSettings = settings;

            // The tutorial phone mounts on the panel root and stays up until somebody answers it.
            foreach (var ringing in document.rootVisualElement.Query(className: "phone").ToList())
            {
                ringing.RemoveFromHierarchy();
            }

            yield return null;

            var simulation = shell.Simulation;

            // ---- 1. Seven cards beat eight -------------------------------------------------------
            //
            // The claim needs a floor where one cabinet is visibly over its cooling and another is
            // not, so the grid shows the difference as colour rather than as a number nobody reads.
            simulation.TryOpenServerRoom(true, out _);

            // **The date has to move for this claim to be true at all.** 2022 silicon does not
            // throttle in any cabinet, which is the design working: the squeeze arrives with the
            // generations, not on day one. The first render of this shot showed four green racks
            // and "Nothing throttling" under a caption about heat, which would have been a lie on
            // a store page.
            var whenChipsRunHot = GameDate.FromCalendar(2030, 1, 1);
            var before = simulation.State.Date;
            simulation.State.Date = whenChipsRunHot;

            var hall = simulation.State.Hall;

            // Seven cards in every cabinet, then a fan in two of them. Identical silicon either
            // side, and the only difference is the slot given to air.
            hall.Stock(hall.TotalSlots - 4);

            simulation.State.CashUsd = 400_000_000;
            simulation.TryFitFan(0, 0, out _);
            simulation.TryFitFan(1, 0, out _);

            shell.OpenScreenByName("Room");
            yield return Shoot("gem_cooling_costs_a_slot.png");

            simulation.State.Date = before;

            // ---- 2. Money is not what gates scale ------------------------------------------------
            //
            // The architecture screen with its locked sliders, each naming the research that opens
            // it. This is the clearest single picture of the rule the whole economy rests on.
            shell.OpenScreenByName("Family");
            yield return Shoot("gem_research_gates_the_sliders.png");

            // ---- 3. The market is people, not a score --------------------------------------------
            shell.OpenScreenByName("Fleet");
            yield return Shoot("gem_load_decides_who_stays.png");

            // ---- 4. Fourteen labs, and three of them come apart -----------------------------------
            shell.OpenScreenByName("Ranking");
            yield return Shoot("gem_rivals_with_histories.png");

            // ---- 5. The model creator, one trade at a time ---------------------------------------
            shell.OpenScreenByName("Create");
            yield return Shoot("gem_creator_scale_belt.png");

            // ---- 6. Shipping an update does not move everybody onto it ---------------------------
            shell.OpenScreenByName("Upgrade");
            yield return Shoot("gem_versions_keep_their_users.png");

            // ---- 7. The regulator gives you five days --------------------------------------------
            //
            // A real open inspection rather than a drawn banner: the countdown on it is the same
            // field the verdict is rolled against.
            var incident = new SafetyIncident(
                IncidentSeverity.Severe,
                simulation.State.Date,
                "User data from the assistant reached a third party",
                reputationLoss: 0.18,
                fineUsd: 90_000_000,
                forcedWithdrawal: false);

            simulation.State.PendingAction = new RegulatoryAction(
                incident, simulation.State.Date, simulation.State.CompanyName);

            shell.OpenScreenByName("Site");
            yield return Shoot("gem_regulator_gives_you_five_days.png");

            // ---- 8. The office, which is where a campaign opens ----------------------------------
            simulation.State.PendingAction = null;
            shell.OpenScreenByName("Site");
            yield return Shoot("shot_the_office.png");

            // ---- 9. And the same room in Polish, because the game ships in two ------------------
            Loc.Current = Language.Polish;
            shell.OpenScreenByName("Room");
            yield return Shoot("shot_polish.png");

            Loc.Current = Language.English;

            Assert.That(Directory.GetFiles(Folder, "*.png").Length, Is.GreaterThanOrEqualTo(9),
                "A claim without a picture behind it should not go on a store page.");
        }
    }
}
