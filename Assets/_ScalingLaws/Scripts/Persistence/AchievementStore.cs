using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.Persistence
{
    /// <summary>
    /// What the player has earned, across every campaign they have ever run.
    ///
    /// **Deliberately not part of a save**, for the same reason <see cref="GameSettings"/> is not:
    /// this describes the person playing rather than the company being run. Three things follow
    /// from that and all three are wanted.
    ///
    /// One, the save format does not change, so this needs no version bump and no migration step.
    /// Two, deleting a campaign does not take back what was earned, which is how Steam behaves and
    /// what a player expects. Three, and this is the one that matters in this game: a company that
    /// goes under is a real outcome here rather than a failure state, and counting how many times
    /// it has happened is only possible somewhere a bankruptcy cannot erase.
    ///
    /// Keyed on <see cref="AchievementDefinition.ApiName"/> rather than on the enum, because the
    /// API name is the identifier Steam will use and is fixed for the life of the achievement,
    /// while an enum value is only as stable as the next person editing the enum.
    /// </summary>
    public static class AchievementStore
    {
        private const string Prefix = "ScalingLaws.Achievements.";
        private const string BankruptcyKey = Prefix + "Bankruptcies";
        private const string CatalogKey = Prefix + "CatalogVersion";

        /// <summary>How many companies this player has run into the ground, ever.</summary>
        public static int LifetimeBankruptcies => PlayerPrefs.GetInt(BankruptcyKey, 0);

        /// <summary>True once this achievement has been earned and written down.</summary>
        public static bool IsUnlocked(string apiName) =>
            !string.IsNullOrEmpty(apiName) && PlayerPrefs.GetInt(Prefix + apiName, 0) == 1;

        /// <summary>How many of the catalog are earned. For a summary line on a screen.</summary>
        public static int UnlockedCount()
        {
            var count = 0;
            foreach (var definition in AchievementCatalog.All)
            {
                if (IsUnlocked(definition.ApiName))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Brings the record up to date with a campaign and returns **only what is new**.
        ///
        /// The caller shows what comes back and nothing else, so an achievement announces itself
        /// once. Calling this every day is the intended use and costs one pass over 47 entries.
        ///
        /// Writes once at the end rather than once per achievement, because a campaign that clears
        /// six of them in a day should not mean six flushes to disk.
        /// </summary>
        public static List<AchievementDefinition> Sync(CompanyState state)
        {
            var fresh = new List<AchievementDefinition>();

            if (state == null)
            {
                return fresh;
            }

            foreach (var definition in AchievementEvaluator.Satisfied(state, LifetimeBankruptcies))
            {
                if (IsUnlocked(definition.ApiName))
                {
                    continue;
                }

                PlayerPrefs.SetInt(Prefix + definition.ApiName, 1);
                fresh.Add(definition);
            }

            if (fresh.Count > 0)
            {
                PlayerPrefs.SetString(CatalogKey, AchievementCatalog.CatalogVersion);
                PlayerPrefs.Save();
            }

            return fresh;
        }

        /// <summary>
        /// Counts one company going under, then re-checks the achievements that read that count.
        ///
        /// Call this **once**, on the day the company becomes insolvent, not every day it stays
        /// insolvent. `CompanyState.IsBankrupt` stays true after the fact, so a caller that polls
        /// it would count the same failure every morning until the player started again.
        /// </summary>
        public static List<AchievementDefinition> RecordBankruptcy(CompanyState state)
        {
            PlayerPrefs.SetInt(BankruptcyKey, LifetimeBankruptcies + 1);
            PlayerPrefs.Save();
            return Sync(state);
        }

        /// <summary>
        /// Awards one achievement outright, for the ones that are a moment rather than a number.
        ///
        /// **This is the answer for the three marked <c>NotWiredYet</c>**, and it is a better answer
        /// than adding a counter. Surviving an inspection, a cabinet of seven beating a cabinet of
        /// eight, a month under water at full load: each of those is something the simulation
        /// already knows at the instant it happens and forgets afterwards. Recording it needs no new
        /// field on <c>CompanyState</c>, which means no change to the save format, no version bump
        /// and no migration step. One call at the moment, from wherever the moment is decided.
        ///
        /// Returns the definition when this is the first time, and null when it was already earned,
        /// so a caller can announce it without checking first.
        /// </summary>
        public static AchievementDefinition Unlock(AchievementId id)
        {
            var definition = AchievementCatalog.Get(id);

            if (definition == null || IsUnlocked(definition.ApiName))
            {
                return null;
            }

            PlayerPrefs.SetInt(Prefix + definition.ApiName, 1);
            PlayerPrefs.SetString(CatalogKey, AchievementCatalog.CatalogVersion);
            PlayerPrefs.Save();
            return definition;
        }

        /// <summary>
        /// Records whatever the rules announced today, and returns only what was new.
        ///
        /// **The other half of `Unlock`, for the caller that has a list rather than an id.** The
        /// simulation cannot call this: `Persistence/` imports UnityEngine and `Simulation/` may
        /// not know it exists. So the rules write plain numbers into a transient list on the state
        /// and the shell brings them here on the same tick, which keeps the layer rule intact and
        /// still costs one call site.
        ///
        /// An unknown number is ignored rather than throwing. The list is written by rules that
        /// have no reason to know the catalog, and a moment for an achievement somebody deleted
        /// should not take a campaign down with it.
        /// </summary>
        public static List<AchievementDefinition> Claim(IReadOnlyList<int> moments)
        {
            var fresh = new List<AchievementDefinition>();

            if (moments == null)
            {
                return fresh;
            }

            foreach (var moment in moments)
            {
                var earned = Unlock((AchievementId)moment);

                if (earned != null)
                {
                    fresh.Add(earned);
                }
            }

            return fresh;
        }

        /// <summary>
        /// Forgets everything, including the bankruptcy count.
        ///
        /// Here for tests and for a settings screen that offers it. Not called from anywhere in the
        /// game today: an achievement the player did not ask to lose should not be losable by
        /// accident.
        /// </summary>
        public static void Reset()
        {
            foreach (var definition in AchievementCatalog.All)
            {
                PlayerPrefs.DeleteKey(Prefix + definition.ApiName);
            }

            PlayerPrefs.DeleteKey(BankruptcyKey);
            PlayerPrefs.DeleteKey(CatalogKey);
            PlayerPrefs.Save();
        }
    }
}
