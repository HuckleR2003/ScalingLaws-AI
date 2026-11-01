using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The mark a player chose for their own company, through the save and back.
    ///
    /// **It was chosen, drawn everywhere, and not written down.** The window offering the
    /// seventeen marks shipped before the field that carries one, so a campaign saved and reloaded
    /// came back wearing the first mark whatever the player picked. That is the same shape as the
    /// model type in v15, which turned a coding model back into a general one on reload.
    /// </summary>
    public sealed class CompanyMarkPersistenceTests
    {
        private static CompanyState Company() => CompanyState.FromOpeningChoice(
            "Prometheus AI",
            CompanyArchetype.Custom,
            FounderTrait.SilverTongue,
            FounderTrait.None,
            companyLogo: CompanyLogoCatalog.NameOf(9));

        [Test]
        public void TheMarkSurvivesASaveAndAReload()
        {
            var campaign = Company();
            Assert.That(campaign.CompanyLogo, Is.EqualTo(CompanyLogoCatalog.NameOf(9)),
                "The creator handed a mark over and the company did not take it.");

            var reloaded = SaveStore.Restore(SaveStore.Capture(campaign));

            Assert.That(reloaded.CompanyLogo, Is.EqualTo(CompanyLogoCatalog.NameOf(9)),
                "The mark the player chose is not in the file, so every reload rebrands the "
                + "company.");
        }

        /// <summary>
        /// A name from a build with more marks in it than this one ships. It has to open on the
        /// first rather than on an empty badge, which is the rule every stored-by-name field here
        /// already follows.
        /// </summary>
        [Test]
        public void AMarkThisBuildDoesNotKnowFallsBackRatherThanBreaking()
        {
            var data = SaveStore.Capture(Company());
            data.companyLogo = "custom_logo_9001";

            var reloaded = SaveStore.Restore(data);

            Assert.That(CompanyLogoCatalog.Knows(reloaded.CompanyLogo), Is.True,
                "A mark no file on disk matches was carried into the game.");
        }

        [Test]
        public void AFileFromBeforeTheMarksGetsTheFirstOneRatherThanAGuess()
        {
            var data = SaveStore.Capture(Company());
            data.version = 75;

            // A v75 file has no idea this field exists.
            data.companyLogo = null;

            var upgraded = SaveMigration.UpgradeV75ToV76(data);

            Assert.That(upgraded.version, Is.EqualTo(76),
                "Each step stamps its own number, never whatever happens to be newest today.");

            Assert.That(upgraded.companyLogo, Is.EqualTo(CompanyLogoCatalog.Default),
                "A campaign played before the marks existed never chose one, so it gets the "
                + "first and not something worked out from its name.");
        }
    }
}
