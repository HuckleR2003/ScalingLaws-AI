using System;
using System.Collections.Generic;
using System.Linq;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>How far the player got with the phone.</summary>
    public enum GuideStage
    {
        /// <summary>Never opened. The phone rings on the first frame of a new company.</summary>
        Unseen = 0,

        /// <summary>The conversation is open and the player has not answered yet.</summary>
        Talking = 1,

        /// <summary>They said yes. Emil is walking them round.</summary>
        Touring = 2,

        /// <summary>Done, either way. The corner keeps the task list and nothing else.</summary>
        Finished = 3
    }

    /// <summary>
    /// The tutorial's state, and whether its three tasks are done.
    ///
    /// **The tasks are checked against the company, not ticked by the tutorial.** A task list that
    /// marks itself complete when a panel says so is a list that can congratulate somebody for
    /// something they did not do — and worse, one that goes out of step the moment a save is
    /// reloaded halfway through. Everything here is derived from what the company actually has.
    ///
    /// Only the stage and the dismissal are stored, because those are choices the player made and
    /// nothing else can reconstruct them.
    /// </summary>
    public sealed class GuideProgress
    {
        public GuideStage Stage { get; set; } = GuideStage.Unseen;

        /// <summary>How far through Emil's tour they are. Index into GuideScript.Steps.</summary>
        public int Step { get; set; }

        /// <summary>
        /// What the company was worth when the tutorial started.
        ///
        /// Recorded rather than assumed, because "double the budget" has to mean double what you
        /// began with. A company that raises a round and spends it should not be told it has
        /// doubled anything.
        /// </summary>
        public long StartingCashUsd { get; set; }

        /// <summary>True once the player has closed the task banner for good.</summary>
        public bool BannerDismissed { get; set; }

        /// <summary>
        /// A favour owed: the next research programme costs nothing.
        ///
        /// **Emil has a mate at a lab who owes him one**, and the first node is on the house. It is
        /// a real grant rather than a line of dialogue: the points and the cash are both waived when
        /// the programme starts, which is the difference between a tutorial that teaches the
        /// research screen and one that describes it.
        ///
        /// Saved, because it is a promise the player has been made and has not spent yet. Dropping
        /// it on reload would take back something they were given, which is worse than never
        /// offering it.
        /// </summary>
        public bool FreeResearchOwed { get; set; }

        /// <summary>
        /// Whether a task is done, read from the company.
        ///
        /// Unknown ids return false rather than throwing: a save written by a build with one more
        /// task in the list must still open.
        /// </summary>
        public bool IsDone(string taskId, CompanyState state)
        {
            if (state == null)
            {
                return false;
            }

            return taskId switch
            {
                // Started or finished, and **not counting the node every company begins with**.
                // A new company already holds ResearchTree.StartingNode, so a bare count ticked this
                // task on day zero and the strip opened with its first line already crossed out.
                //
                // In flight counts, because a node running is the thing the task asked for and
                // waiting four months to tick it would leave the strip stuck on step one.
                "first_research" => state.ActiveResearch != null
                    || state.UnlockedResearch.Any(node => node != ResearchTree.StartingNode),

                // Anything trained counts, whether it is still on the shelf or already out.
                "first_model" => state.Shelf.Count > 0 || state.DeployedModels.Count > 0,

                "first_release" => state.DeployedModels.Count > 0,

                // Same reasoning: an upgrade running is an upgrade the player commissioned. Level
                // above par on anything live counts as one already finished.
                "first_upgrade" => state.UpgradeProjects.Count > 0
                    || state.DeployedModels.Any(model => model.Traits.TotalLevels > 0),

                "double_cash" => StartingCashUsd > 0L && state.CashUsd >= StartingCashUsd * 2L,

                _ => false
            };
        }

        /// <summary>The first task not yet done, or null when they are all finished.</summary>
        public string CurrentTask(CompanyState state)
        {
            foreach (var (id, _) in GuideScript.Tasks)
            {
                if (!IsDone(id, state))
                {
                    return id;
                }
            }

            return null;
        }

        /// <summary>Every task, with whether it is done. What the corner banner draws.</summary>
        public IEnumerable<(string Id, string Text, bool Done)> Tasks(CompanyState state)
        {
            foreach (var (id, key) in GuideScript.Tasks)
            {
                yield return (id, Loc.T(key), IsDone(id, state));
            }
        }

        /// <summary>True when there is nothing left to show in the corner.</summary>
        public bool AllTasksDone(CompanyState state) => CurrentTask(state) == null;

        public void Restore(GuideStage stage, int step, long startingCash, bool dismissed,
            bool freeResearchOwed = false)
        {
            Stage = Enum.IsDefined(typeof(GuideStage), stage) ? stage : GuideStage.Unseen;
            Step = Math.Max(0, step);
            StartingCashUsd = Math.Max(0L, startingCash);
            BannerDismissed = dismissed;
            FreeResearchOwed = freeResearchOwed;
        }
    }
}
