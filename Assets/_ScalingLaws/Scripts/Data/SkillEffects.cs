using System.Collections.Generic;

namespace ScalingLaws.Data
{
    /// <summary>
    /// One thing a skill moves, and how hard it moves it at each end of the scale.
    ///
    /// **The same line carries both ends on purpose.** A high setting and a low one are the two
    /// directions of one axis, not two lists to be compared, and writing them as two lists is how
    /// the current cards ended up as paragraphs nobody reads.
    /// </summary>
    public readonly struct SkillEffect
    {
        public SkillEffect(string key, int atHigh, int atLow)
        {
            Key = key ?? string.Empty;
            AtHigh = System.Math.Clamp(atHigh, -3, 3);
            AtLow = System.Math.Clamp(atLow, -3, 3);
        }

        /// <summary>The phrase-book key for the effect's name. A literal, so the guard can see it.</summary>
        public string Key { get; }

        /// <summary>What this line reads as the skill reads today.</summary>
        public string DisplayName => Loc.T(Key);

        /// <summary>
        /// The reading for a strength, written out in both directions rather than negated.
        ///
        /// **Asked for by Francisco on 2026-10-27**, who read ">> Lower cost of running the
        /// company" beside "&lt;&lt; Lower cost of running the company" and said, correctly, that
        /// it comes out as more lower and less lower. The arrows were carrying the sign and the
        /// words were carrying it too, in the same direction, so the two multiplied.
        ///
        /// **Two written phrases rather than a prefix.** Bolting "Higher" or "Lower" onto a noun
        /// works in English and does not work in Polish, where the adjective agrees with the
        /// gender of the noun it is in front of: nizszy koszt, nizsza wydajnosc, nizsze dane. A
        /// mechanical prefix would ship a grammatical error on every second row.
        /// </summary>
        public string NameFor(int strength) =>
            strength < 0 ? Loc.T(Key + ".down") : Loc.T(Key);

        /// <summary>
        /// Strength at a high setting, -3 to 3. Zero is drawn as "no effect" rather than hidden.
        ///
        /// **A zero is information.** A player looking at Development expects it to make models
        /// better, and the honest answer is that it does not: capability comes from the scaling
        /// law and no founder is allowed to bend it. Saying so in a line they were going to read
        /// anyway is worth more than leaving the line out and letting them assume.
        /// </summary>
        public int AtHigh { get; }

        public int AtLow { get; }
    }

    /// <summary>
    /// What each of the seven skills actually does, as points rather than prose.
    ///
    /// **Written against the code rather than against the old copy.** Checked on 2026-10-25: the
    /// description cards had been saying things the multipliers do not do, and the worst of them
    /// was Development, which has the second largest effect in the game and moves something a
    /// player physically cannot see.
    ///
    /// **The "score the model comes out at" line is gone from both cards it was on.** It was
    /// there to say out loud that no founder skill bends the scaling law, and on one card that is
    /// worth reading. On two, both drawn as a tilde, it stopped being information and became the
    /// line a player learns to skip, which is what was reported on 2026-10-25. The honesty flag is
    /// kept where it belongs: in the simulation, and in `SkillSpeedTests.SpeedDoesNotBuyCapability`,
    /// which fails if a founder skill ever moves projected capability.
    ///
    /// Strengths are a statement about how much a skill matters, not a reading of
    /// `PlayerSkillDefinition.FullEffect`. The two are different questions: Software's multiplier
    /// range is small and it is read in ten places, and a player who pushed it to 100 feels that
    /// more than the range suggests.
    /// </summary>
    public static class SkillEffectCatalog
    {
        public const string CatalogVersion = "skilleffects-2026.10.31";

        private static readonly SkillEffect[] Development =
        {
            new("skilleffect.spread", 3, -3),
            new("skilleffect.trainspeed", 2, -2)
        };

        private static readonly SkillEffect[] Management =
        {
            new("skilleffect.dailycost", 2, -2),
            new("skilleffect.runway", 2, -2)
        };

        private static readonly SkillEffect[] Teamwork =
        {
            new("skilleffect.saturation", 3, -3),
            new("skilleffect.loyalty", 2, -2)
        };

        private static readonly SkillEffect[] Concept =
        {
            new("skilleffect.research", 2, -2),
            new("skilleffect.interest", 2, -2)
        };

        private static readonly SkillEffect[] Software =
        {
            new("skilleffect.servingcost", 3, -3)
        };

        private static readonly SkillEffect[] DataEngineering =
        {
            new("skilleffect.corpus", 2, -2),
            new("skilleffect.rentcost", 2, -2)
        };

        private static readonly SkillEffect[] Safety =
        {
            new("skilleffect.incident", 3, -3),

            // Not wired to the regulator yet, and a player assumes it is. The zero says so.
            new("skilleffect.penalty", 0, 0)
        };

        /// <summary>
        /// The points for a skill.
        ///
        /// A written-out switch, not a lookup built from the enum name: a key assembled by
        /// concatenation is invisible to `LocalisationTests`, and that has already shipped one
        /// screen of raw keys in this project.
        /// </summary>
        public static IReadOnlyList<SkillEffect> For(PlayerSkill skill) => skill switch
        {
            PlayerSkill.Development => Development,
            PlayerSkill.Management => Management,
            PlayerSkill.Teamwork => Teamwork,
            PlayerSkill.Concept => Concept,
            PlayerSkill.Software => Software,
            PlayerSkill.DataEngineering => DataEngineering,
            PlayerSkill.Safety => Safety,
            _ => System.Array.Empty<SkillEffect>()
        };

        /// <summary>
        /// The arrows for a strength: green rising, red falling, one amber tilde for nothing.
        ///
        /// Drawn as text rather than as a glyph from a font, for the reason the support face is
        /// drawn rather than typed: a chevron from a fallback face is a different shape and colour
        /// on every machine.
        /// </summary>
        /// <remarks>
        /// **Strength only, since 2026-10-31.** The words say which way it goes and the arrows
        /// say how hard, which is exactly the split the tester asked for. They still carry the
        /// direction as colour, because a red row and a green row are read before either is
        /// read as text.
        /// </remarks>
        public static string Marks(int strength) => System.Math.Abs(strength) switch
        {
            3 => ">>>",
            2 => ">>",
            1 => ">",
            _ => "~"
        };
    }
}
