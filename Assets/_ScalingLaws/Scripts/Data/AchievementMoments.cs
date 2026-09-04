namespace ScalingLaws.Data
{
    /// <summary>
    /// The achievements that are a moment rather than a total, as plain numbers.
    ///
    /// **One of the forty seven describes something that happens and is then over.** The fix that
    /// brought the catalog listed three, and two of those turned out to be readable after all: a
    /// cabinet where the fan beats the card it displaced is a fact about the floor as it stands, and
    /// a month under water at load is `DaysInDebt` next to yesterday's utilisation. Both are metrics.
    ///
    /// Surviving an inspection is the one that is genuinely gone by the time anything can look:
    /// `PendingAction` is cleared in the same method that decides the verdict, and nothing on the
    /// company records that a file was ever closed in its favour.
    ///
    /// So the rules announce them instead, into `CompanyState.AchievementMomentsToday`, and the
    /// shell hands whatever is in that list to `AchievementStore` on the same tick.
    ///
    /// **Why plain integers rather than `AchievementId`.** The list they go into lives in
    /// `Simulation/`, and a rule that names an achievement is a rule with an opinion about
    /// achievements. This class is the one place the two vocabularies meet, it holds no logic, and
    /// `AchievementTests` asserts each number is a real catalog id, so the indirection cannot rot
    /// quietly.
    ///
    /// The numbers are the `AchievementId` values and must not be renumbered, for the same reason
    /// those must not: they are the identity, not the position.
    /// </summary>
    public static class AchievementMoments
    {
        /// <summary>An inspection ran its five days and closed without a penalty.</summary>
        public const int RegulatorHeld = (int)AchievementId.RegFive;

    }
}
