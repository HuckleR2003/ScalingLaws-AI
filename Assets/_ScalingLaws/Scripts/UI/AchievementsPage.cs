using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The forty seven, grouped, with what is earned and what is not.
    ///
    /// **The half of the achievement system a player can see.** The catalog, the evaluator and the
    /// store landed complete and unlocked things audibly and invisibly, which is one step away from
    /// the failure this project has now hit twelve times: a mechanism finished underneath with no
    /// control on top. A sound with nothing to look at afterwards is not a feature, it is a rumour.
    ///
    /// Nothing here computes and nothing here writes. It reads `AchievementCatalog` for the list and
    /// `AchievementStore` for what is earned, so the page and the record cannot disagree.
    ///
    /// **Locked entries show their name and their note.** Hiding them would make the screen a list
    /// of what you have already done, which nobody needs; showing them makes it a list of what there
    /// is to do. The three that nothing counts yet are drawn with a quiet marker rather than left
    /// out, because a gap in a numbered set reads as a bug.
    /// </summary>
    public sealed class AchievementsPage
    {
        /// <summary>The order the groups are drawn in. The catalog's own order, made explicit.</summary>
        private static readonly AchievementGroup[] Order =
        {
            AchievementGroup.Cash,
            AchievementGroup.Models,
            AchievementGroup.ModelTypes,
            AchievementGroup.Architecture,
            AchievementGroup.Hardware,
            AchievementGroup.Research,
            AchievementGroup.Regulator,
            AchievementGroup.Survival,
            AchievementGroup.Market,
            AchievementGroup.Time
        };

        /// <summary>
        /// The phrase-book key for a group heading.
        ///
        /// Written out rather than built from the enum name, the same as every catalog in this
        /// project: a key assembled by concatenation is invisible to `LocalisationTests`, which can
        /// only read literals, and this project has already shipped one screen of raw keys that way.
        /// </summary>
        private static string HeadingFor(AchievementGroup group) => group switch
        {
            AchievementGroup.Cash => "ach.group.cash",
            AchievementGroup.Models => "ach.group.models",
            AchievementGroup.ModelTypes => "ach.group.types",
            AchievementGroup.Architecture => "ach.group.architecture",
            AchievementGroup.Hardware => "ach.group.hardware",
            AchievementGroup.Research => "ach.group.research",
            AchievementGroup.Regulator => "ach.group.regulator",
            AchievementGroup.Survival => "ach.group.survival",
            AchievementGroup.Market => "ach.group.market",
            _ => "ach.group.time"
        };

        /// <summary>The whole page, ready to be put inside whatever is showing it.</summary>
        public VisualElement Build()
        {
            var page = new VisualElement();
            page.AddToClassList("achpage");

            page.Add(BuildSummary());

            var scroller = new ScrollView();
            scroller.AddToClassList("achpage__scroll");

            foreach (var group in Order)
            {
                var rows = Rows(group);

                if (rows.Count == 0)
                {
                    continue;
                }

                scroller.Add(BuildGroup(group, rows));
            }

            page.Add(scroller);
            return page;
        }

        /// <summary>
        /// How many of them are earned, and how many companies went under on the way.
        ///
        /// The bankruptcy count is on this page rather than on the statistics page because it is the
        /// one number here that survives a campaign, and three of the achievements are about it.
        /// </summary>
        private static VisualElement BuildSummary()
        {
            var band = new VisualElement();
            band.AddToClassList("achpage__summary");

            var earned = new Label(Loc.T("ach.summary",
                UiFormat.Number(AchievementStore.UnlockedCount(), 0),
                UiFormat.Number(AchievementCatalog.All.Count, 0)));

            earned.AddToClassList("achpage__count");
            band.Add(earned);

            var failures = AchievementStore.LifetimeBankruptcies;

            var note = new Label(failures > 0
                ? Loc.T("ach.summary.busts", UiFormat.Number(failures, 0))
                : Loc.T("ach.summary.clean"));

            note.AddToClassList("achpage__note");
            band.Add(note);

            return band;
        }

        private static List<AchievementDefinition> Rows(AchievementGroup group)
        {
            var rows = new List<AchievementDefinition>();

            foreach (var definition in AchievementCatalog.All)
            {
                if (definition.Group == group)
                {
                    rows.Add(definition);
                }
            }

            return rows;
        }

        private static VisualElement BuildGroup(AchievementGroup group,
            IReadOnlyList<AchievementDefinition> rows)
        {
            var block = new VisualElement();
            block.AddToClassList("achgroup");

            var heading = new Label(Loc.T(HeadingFor(group)));
            heading.AddToClassList("achgroup__heading");
            block.Add(heading);

            foreach (var definition in rows)
            {
                block.Add(BuildRow(definition));
            }

            return block;
        }

        /// <summary>
        /// One achievement: whether it is earned, what it is called, and what it asks for.
        ///
        /// The note is shown whether or not it is earned. A locked achievement whose description is
        /// hidden is a row that says "there is something here and you may not know what", which is
        /// the shape of a puzzle rather than of a goal, and this game has no puzzles.
        /// </summary>
        private static VisualElement BuildRow(AchievementDefinition definition)
        {
            var earned = AchievementStore.IsUnlocked(definition.ApiName);

            var row = new VisualElement();
            row.AddToClassList("achrow");
            row.EnableInClassList("achrow--earned", earned);

            var mark = new Label(earned ? "✓" : string.Empty);
            mark.AddToClassList("achrow__mark");
            row.Add(mark);

            var words = new VisualElement();
            words.AddToClassList("achrow__words");

            var name = new Label(Loc.T(definition.NameKey));
            name.AddToClassList("achrow__name");
            words.Add(name);

            var note = new Label(Loc.T(definition.NoteKey));
            note.AddToClassList("achrow__note");
            words.Add(note);

            row.Add(words);

            // The three the simulation does not count yet say so. A row that can never light up and
            // does not admit it is worse than one that is simply hard.
            if (definition.NeedsCounter)
            {
                var soon = new Label(Loc.T("ach.not_yet"));
                soon.AddToClassList("achrow__soon");
                row.Add(soon);
            }

            return row;
        }
    }
}
