using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.PlayMode
{
    /// <summary>
    /// The campaign both recorders draw against.
    ///
    /// **Shared rather than copied.** The tab proof and the interface tour photograph the same
    /// screens, and two copies of the setup would drift the moment one of them needed a fourth
    /// model. A contact sheet that disagrees with the video about what the company owns is worse
    /// than either of them alone.
    /// </summary>
    public static class TabProofCampaign
    {
        /// <summary>
        /// Two years of company, assembled rather than played.
        ///
        /// Playing it would be more honest and take a hundred times as long, and the point here is
        /// to fill the screens, not to prove the economy. Everything set is something a real
        /// campaign would have by 2024: money, a fleet, staff, two live models with version
        /// history, research behind it and a corpus.
        /// </summary>
        public static void Furnish(CompanySimulation simulation)
        {
            // The basement, so the room screen has cabinets to photograph rather than a locked page.
            simulation.TryOpenServerRoom(true, out _);
            simulation.State.Hall.Stock(simulation.State.Hall.TotalSlots - 2);
            simulation.TryFitFan(0, 0, out _);

            var state = simulation.State;

            // The tutorial phone lays itself over the middle of the screen and the task strip sits
            // on top of the page. Both are correct on day one and both hide the thing being
            // reviewed, so this campaign is one that has already been through them.
            state.Guide.Restore(GuideStage.Finished, 0, 0L, true);

            state.Date = GameDate.FromCalendar(2024, 6, 18);
            state.CashUsd = 148_000_000;
            state.Reputation = 0.46;
            state.LifetimeRevenueUsd = 402_000_000;
            state.LifetimeOperatingCostUsd = 291_000_000;
            state.LifetimeCapitalSpentUsd = 96_000_000;

            simulation.SetRentedPetaflops(1_250.0);

            state.Pool.AddAsset(new HardwareAsset(
                HardwareGenerationId.AcceleratorH100, ComputeTier.ColocatedServers, 384,
                GameDate.FromCalendar(2023, 4, 11), 33_000, 45));

            state.OwnedDataSources |= DatasetSource.CuratedWeb | DatasetSource.CodeCorpus;
            state.AdoptedArchitectures.Add(ArchitectureId.SparseMixture);

            var flagship = new DeployedModel(
                "Aurora", ArchitectureId.SparseMixture, 54.0,
                GameDate.FromCalendar(2023, 8, 2), 6e10, 1.0);

            state.AddDeployedModel(flagship);
            flagship.SeedLine(20.0, 10_400.0);
            flagship.Line.Publish("Aurora 2", GameDate.FromCalendar(2024, 1, 9), 61.0, 22.0, 10_400.0);

            for (var day = 0; day < 40; day++)
            {
                flagship.Line.Advance();
            }

            // The one nobody liked, which is what the release list exists to show.
            flagship.Line.Publish("Aurora 3", GameDate.FromCalendar(2024, 5, 2), 48.0, 30.0, 8_000.0);

            for (var day = 0; day < 25; day++)
            {
                flagship.Line.Advance();
            }

            state.AddDeployedModel(new DeployedModel(
                "Kestrel", ArchitectureId.DenseTransformer, 41.0,
                GameDate.FromCalendar(2023, 2, 14), 2e10, 0.8));

            // One tick so everything derived from the above actually exists: market standing, the
            // books, awareness, service quality. Without it half the screens read zero, and the top
            // bar keeps printing the boot values it was built with: the chrome is refreshed on a
            // day rolling over, so a campaign assembled after Start never reaches it.
            simulation.AdvanceDay();
        }
    }
}
