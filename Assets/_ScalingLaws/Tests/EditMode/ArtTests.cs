using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Every picture the code asks for, and whether it is there.
    ///
    /// **A missing file is not an exception in this project, it is a blank plate**, which is the
    /// right behaviour while art arrives in batches and the wrong thing to have no record of. Six
    /// marketing photographs sat in `Art/` for two days without ever being copied into `Resources/`,
    /// so the screen drew flat rectangles and nothing anywhere said so.
    ///
    /// These tests are the record. They fail loudly when a file the code names is absent, and the
    /// ones that are still genuinely undrawn are listed by name in the one test that tolerates them,
    /// so the gap is a line in a file rather than something to notice on a screenshot.
    /// </summary>
    public sealed class ArtTests
    {
        private static bool Exists(string key) => Resources.Load<Texture2D>(key) != null;

        private static void AllPresent(string what, IEnumerable<string> keys)
        {
            var missing = new List<string>();
            foreach (var key in keys)
            {
                if (!Exists(key))
                {
                    missing.Add(key);
                }
            }

            if (missing.Count == 0)
            {
                return;
            }

            var report = new StringBuilder();
            report.Append(what).Append(": ").Append(missing.Count).AppendLine(" missing.");
            foreach (var key in missing)
            {
                report.Append("  Resources/").AppendLine(key);
            }

            report.AppendLine();
            report.Append("The file is probably in Art/ and was never copied across. Art/ is where "
                + "the originals are organised; Resources/ is the only folder the game reads.");

            Assert.Fail(report.ToString());
        }

        [Test]
        public void EveryMarketingChannelHasItsPhotograph()
        {
            var keys = new List<string>();
            foreach (var channel in MarketingCatalog.All)
            {
                keys.Add("Marketing/" + channel.Art);
            }

            AllPresent("Marketing channels", keys);
        }

        /// <summary>
        /// Every rival, including Groq, who is the newest and was the one without a mark.
        /// </summary>
        [Test]
        public void EveryRivalLabHasAMark()
        {
            var missing = new List<string>();

            foreach (CompetitorId lab in System.Enum.GetValues(typeof(CompetitorId)))
            {
                if (lab == CompetitorId.None)
                {
                    continue;
                }

                if (LabLogos.Get(lab) == null)
                {
                    missing.Add(lab + " (" + CompetitorCatalog.NameOf(lab) + ")");
                }
            }

            Assert.IsEmpty(missing,
                "Labs with no mark: " + string.Join(", ", missing)
                + ". The board falls back to an initial, so this is legible rather than broken, but "
                + "a field of nine where one is a letter reads as an oversight.");
        }

        [Test]
        public void EveryResearchNodeWithAnIconResolvesToARealFile()
        {
            var missing = new List<string>();

            foreach (var node in ResearchTree.All)
            {
                // Nodes with no entry in the icon map are not claimed to have art. Nodes that are
                // claimed have to deliver, or the map is lying about what exists.
                if (ResearchIcons.HasArtFor(node.Id) && ResearchIcons.Get(node.Id) == null)
                {
                    missing.Add(node.Id.ToString());
                }
            }

            Assert.IsEmpty(missing,
                "The icon map names a file for these and the file is not there: "
                + string.Join(", ", missing));
        }

        [Test]
        public void EveryFounderSkillHasItsIcon()
        {
            var missing = new List<string>();
            foreach (var skill in PlayerSkillCatalog.All)
            {
                if (SkillIcons.Get(skill.Skill) == null)
                {
                    missing.Add(skill.DisplayName);
                }
            }

            Assert.IsEmpty(missing, "Skills with no icon: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryBottomBarSlotThatNamesAnIconHasOne()
        {
            AllPresent("Bottom bar", new[]
            {
                "Hud/hud_model", "Hud/hud_research", "Hud/hud_architecture", "Hud/hud_upgrade",
                "Hud/hud_fleet", "Hud/hud_business", "Hud/hud_release", "Hud/hud_funding",
                "Hud/hud_ranking", "Hud/hud_intelligence", "Hud/hud_site", "Hud/hud_team",
                "Hud/hud_marketing"
            });
        }

        [Test]
        public void EveryScreenWithABackgroundStripHasOne()
        {
            AllPresent("Page banners", new[]
            {
                "Banners/background_business", "Banners/background_funding",
                "Banners/background_ranking", "Banners/background_release",
                "Banners/background_upgrade", "Banners/background_research",
                "Banners/background_architecture", "Banners/background_compute",
                "Banners/background_team"
            });
        }

        [Test]
        public void EveryCreatorStageHasItsPicture()
        {
            AllPresent("Creator stages", new[]
            {
                "Pages/newmodel_1", "Pages/newmodel_2", "Pages/newmodel_3",
                "Pages/newmodel_4", "Pages/newmodel_5", "Pages/newmodel_6"
            });
        }

        /// <summary>
        /// The art that genuinely has not been drawn, named so the gap is recorded rather than
        /// discovered. Delete a line from here when the file lands; this test failing means
        /// something arrived and nobody updated the list, which is a good failure to have.
        /// </summary>
        [Test]
        public void TheOnlyMissingArtIsTheArtWeKnowIsMissing()
        {
            var known = new[] { "Hosting/hosting_renting", "Hosting/hosting_datacenter" };
            var arrived = new List<string>();

            foreach (var key in known)
            {
                if (Exists(key))
                {
                    arrived.Add(key);
                }
            }

            Assert.IsEmpty(arrived,
                "These are listed as not drawn yet and they now exist: " + string.Join(", ", arrived)
                + ". Wire them up and take them off the list.");
        }
    }
}
