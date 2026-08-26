using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What is being upgraded right now, in the corner, with the days left on it.
    ///
    /// **An upgrade programme was the only long job in the game with nowhere to watch it.** Training
    /// takes the product banner, research has its own strip on its own page, and an upgrade ran for
    /// weeks with the player's only evidence being that the money had gone. A player who clicks
    /// UPGRADE and sees nothing change reasonably concludes the button did not work, and the second
    /// click charges them again for a programme they cannot see.
    ///
    /// One row per programme, because a company can run two at once and a single line saying "2
    /// upgrades" answers none of the questions somebody staring at the corner is asking.
    ///
    /// It is a separate element from <see cref="ModelBanner"/> rather than another mode inside it,
    /// for the reason recorded when research was pulled out: a banner that swaps itself for whatever
    /// is in flight hides the product for as long as the work runs.
    /// </summary>
    public sealed class UpgradeStrip
    {
        private readonly Func<CompanyState> state;
        private readonly List<Row> rows = new();

        public UpgradeStrip(Func<CompanyState> state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));

            Root = new VisualElement();
            Root.AddToClassList("ustrip");
        }

        public VisualElement Root { get; }

        /// <summary>
        /// One programme's row. Kept and reused rather than rebuilt, because the corner refreshes
        /// every simulated day and a rebuilt element restarts its own transitions.
        /// </summary>
        private sealed class Row
        {
            public Row()
            {
                Root = new VisualElement();
                Root.AddToClassList("ustrip__row");

                Fill = new VisualElement();
                Fill.AddToClassList("ustrip__fill");
                Root.Add(Fill);

                var text = new VisualElement();
                text.AddToClassList("ustrip__text");

                Kicker = new Label();
                Kicker.AddToClassList("ustrip__kicker");

                Name = new Label();
                Name.AddToClassList("ustrip__name");

                Days = new Label();
                Days.AddToClassList("ustrip__days");

                var left = new VisualElement();
                left.AddToClassList("ustrip__left");
                left.Add(Kicker);
                left.Add(Name);

                text.Add(left);
                text.Add(Days);
                Root.Add(text);
            }

            public VisualElement Root { get; }
            public VisualElement Fill { get; }
            public Label Kicker { get; }
            public Label Name { get; }
            public Label Days { get; }
        }

        public void Refresh()
        {
            var company = state();
            var projects = company?.UpgradeProjects;

            if (projects == null || projects.Count == 0)
            {
                Root.style.display = DisplayStyle.None;
                return;
            }

            Root.style.display = DisplayStyle.Flex;

            while (rows.Count < projects.Count)
            {
                var row = new Row();
                rows.Add(row);
                Root.Add(row.Root);
            }

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];

                if (index >= projects.Count)
                {
                    row.Root.style.display = DisplayStyle.None;
                    continue;
                }

                row.Root.style.display = DisplayStyle.Flex;
                Fill(row, company, projects[index]);
            }
        }

        private static void Fill(Row row, CompanyState company, ModelUpgradeProject project)
        {
            var definition = ModelTraitCatalog.Get(project.Trait);

            row.Kicker.text = Loc.T("ustrip.kicker");

            // A programme is a basket now. Naming only its headline would say "Reasoning" over a
            // strip that is also doing three other things, and the player would go looking for the
            // rows that used to be there.
            row.Name.text = project.IsBatch
                ? Loc.T("ustrip.batch", project.Steps.Count)
                : $"{definition.DisplayName} {Loc.T("ustrip.to_level", project.TargetLevel + 1)}";

            // **Days, not per cent, is the headline.** It is the number a player plans around, and
            // the same choice the training banner already made. The bar behind the words carries the
            // proportion for whoever wants it.
            var left = Math.Max(0, project.DurationDays - project.DaysCompleted);
            // `Loc.Plural` answers with the noun alone, so the count has to be put in front of it.
            // Without that the corner read "days", which is the one word on the row that carries no
            // information at all. A test caught it; looking at the screen would not have, because a
            // strip saying "days" looks like a strip that is working.
            row.Days.text = $"{left} {Loc.Plural(left, "noun.day")}";

            var progress = Math.Clamp(project.Progress, 0.0, 1.0);
            row.Fill.style.width = Length.Percent((float)(progress * 100.0));

            // A programme that has run its calendar and is waiting on the cluster reads as stuck
            // otherwise: nought days left and nothing happening. Research had exactly this fault and
            // it took a session to work out that nothing was broken.
            var name = ModelNameFor(company, project);

            if (!string.IsNullOrEmpty(name))
            {
                row.Kicker.text = $"{Loc.T("ustrip.kicker")}   {name}";
            }

            if (left == 0 && progress < 1.0)
            {
                row.Days.text = Loc.T("ustrip.waiting");
            }
        }

        private static string ModelNameFor(CompanyState company, ModelUpgradeProject project)
        {
            if (project.OnShelf)
            {
                var shelf = company.Shelf;

                return project.ModelIndex >= 0 && project.ModelIndex < shelf.Count
                    ? shelf[project.ModelIndex].Name
                    : string.Empty;
            }

            var live = company.DeployedModels;

            return project.ModelIndex >= 0 && project.ModelIndex < live.Count
                ? live[project.ModelIndex].Name
                : string.Empty;
        }
    }
}
