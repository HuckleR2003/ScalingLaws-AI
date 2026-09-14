using System;
using System.Linq;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.UI;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// Every research node is drawn somewhere a player can click it.
    ///
    /// **Francisco, a returning tester, found the fault this holds shut:** "you forgot to include
    /// the safety research nodes, as they are nowhere to be found". Every safety node went into the
    /// era board through a `default:` arm, and the era board lays out capability nodes only, so all
    /// twelve were handed to a board that quietly dropped them.
    ///
    /// **The first run of this fixture found two more than the report did.** Continuous oversight and
    /// redundant inference are statecraft nodes on the safety track that are no module's level, so the
    /// new tiles would have missed them the same way the old board did. They are on a safety band
    /// under their era now.
    /// </summary>
    public sealed class ResearchSurfacesTests
    {
        [Test]
        public void EveryTrackHasSomewhereOnTheScreenToBeDrawn()
        {
            foreach (ResearchTrack track in Enum.GetValues(typeof(ResearchTrack)))
            {
                Assert.DoesNotThrow(() => ResearchSurfaces.Of(track),
                    $"{track} has no surface on the research screen, so its nodes would be dropped.");

                Assert.DoesNotThrow(() => ResearchSurfaces.Of(track, isSafetyLevel: true));
            }
        }

        [Test]
        public void EveryNodeIsLaidOutByTheSurfaceItIsSentTo()
        {
            var ladders = SafetyModuleCatalog.All
                .Select(tier => tier.Requires)
                .Where(id => id != ResearchNodeId.None)
                .ToHashSet();

            foreach (var node in ResearchTree.All)
            {
                switch (ResearchSurfaces.Of(node))
                {
                    case ResearchSurface.SafetyPanel:
                        Assert.That(ladders, Does.Contain(node.Id),
                            $"{node.Id} is sent to the safety tiles and is no module's level.");
                        break;

                    // The safety band is laid out from exactly the nodes sent to it, era by era.
                    case ResearchSurface.EraSafety:
                    {
                        var band = ResearchTree.All
                            .Where(other => other.Era == node.Era
                                            && ResearchSurfaces.Of(other) == ResearchSurface.EraSafety)
                            .ToList();

                        Assert.That(ResearchLayout.Place(band).Select(slot => slot.Node), Does.Contain(node.Id),
                            $"{node.Id} is sent to its era's safety band and the band does not lay it out.");
                        break;
                    }

                    default:
                        Assert.That(ResearchLayout.Place(node.Era, node.Track).Select(slot => slot.Node),
                            Does.Contain(node.Id),
                            $"{node.Id} is sent to a board that does not lay it out.");
                        break;
                }
            }
        }

        [Test]
        public void EverySafetyLevelThatNeedsResearchNamesANodeOnTheSafetyTrack()
        {
            foreach (var tier in SafetyModuleCatalog.All)
            {
                if (tier.Requires == ResearchNodeId.None)
                {
                    continue;
                }

                Assert.AreEqual(ResearchTrack.Safety, ResearchTree.Get(tier.Requires).Track,
                    $"{tier.Module} level {tier.Tier} needs {tier.Requires}, which is drawn on another "
                    + "board, so its tile would point at a card that is not there.");
            }
        }

        /// <summary>
        /// The premises map is built from the first premises node's era, so a second era would draw
        /// only half the offices.
        /// </summary>
        [Test]
        public void ThePremisesNodesShareOneEra()
        {
            var eras = ResearchTree.All
                .Where(node => node.Track == ResearchTrack.Premises)
                .Select(node => node.Era)
                .Distinct()
                .ToList();

            Assert.That(eras.Count, Is.LessThanOrEqualTo(1),
                "The premises map draws one era. Offices in two eras would lose some of them.");
        }
    }
}
