using System;
using System.Collections.Generic;
using NUnit.Framework;
using ScalingLaws.Data;
using ScalingLaws.Simulation;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The nineteen per cent of the phrase book the existing guard cannot see.
    ///
    /// **`LocalisationTests.EveryKeyTheInterfaceAsksForExists` reads source text**, so it can only
    /// check keys that appear in it as literals. A key built by concatenation never does. An audit
    /// counted the gap: of 1,923 keys, 1,332 appear as literals, 223 are dead copy, and **368 are
    /// asked for by a stem plus a suffix and are invisible to every check in the project**.
    ///
    /// The blind spots are not obscure corners. `TechNotes` is 195 of them - the "(i)" cards on
    /// every control in the game. `SkillNotes` is 35, the seven skills a new player distributes two
    /// hundred points across. `GrantCatalog` is 36 and `WorldEventCatalog` 66.
    ///
    /// **This checks behaviour rather than source text**, which is why it can cover them all at
    /// once and why it keeps working as they grow: it resolves what the catalogs actually return and
    /// fails if any of it came back as its own key. That is what a missing phrase looks like on
    /// screen, and `Loc.T` deliberately does not throw - it renders the key, so `arch.tilte` once
    /// shipped as the visible word.
    ///
    /// Both languages, because a key present in English and absent in Polish is the shape this book
    /// has drifted into before: the two dictionaries were 129 entries apart when they were merged.
    /// </summary>
    public sealed class LocalisationCoverageTests
    {
        /// <summary>
        /// Something that came back as its own key never made it into the book.
        ///
        /// Also catches empty, which is the other way a phrase goes missing without anybody
        /// noticing: a blank label is a layout that looks slightly wrong rather than an error.
        /// </summary>
        private static void Resolved(string what, string value, List<string> missing)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Contains(" ") == false && value.Contains(".")
                && value == value.ToLowerInvariant())
            {
                missing.Add($"{what} = \"{value}\"");
            }
        }

        private static void CheckNote(string what, TechNotes.Note note, List<string> missing)
        {
            Resolved(what + ".title", note.Title, missing);
            Resolved(what + ".what", note.What, missing);
            Resolved(what + ".affects", note.Affects, missing);
            Resolved(what + ".high", note.High, missing);
            Resolved(what + ".low", note.Low, missing);
        }

        private static void InBothLanguages(Action<List<string>> check)
        {
            var before = Loc.Current;

            try
            {
                foreach (var language in new[] { Language.English, Language.Polish })
                {
                    Loc.Current = language;

                    var missing = new List<string>();
                    check(missing);

                    CollectionAssert.IsEmpty(missing,
                        $"These resolved to their own key in {language}, which is what a missing "
                        + "phrase looks like on screen: " + string.Join("; ", missing));
                }
            }
            finally
            {
                Loc.Current = before;
            }
        }

        /// <summary>
        /// Every "(i)" card in the game, all forty of them, five phrases each.
        ///
        /// The largest blind spot by far, and the one that matters most: these cards are the only
        /// place several controls explain what they do at all.
        /// </summary>
        [Test]
        public void EveryTechNoteHasAllFiveOfItsPhrases()
        {
            InBothLanguages(missing =>
            {
                foreach (var property in typeof(TechNotes).GetProperties())
                {
                    if (property.PropertyType != typeof(TechNotes.Note))
                    {
                        continue;
                    }

                    CheckNote("TechNotes." + property.Name,
                        (TechNotes.Note)property.GetValue(null), missing);
                }
            });
        }

        /// <summary>
        /// The ten cards on the SCALE and DATA stages: four widths, three arrangements, three
        /// deduplication passes.
        ///
        /// **They joined this blind spot the day they left the other one.** Until 2026-09-04 they
        /// stored their English in the catalog, so a Polish player read an English DEDUPLICATION
        /// panel with English RAW, STANDARD and AGGRESSIVE cards under it, and no guard could see
        /// that because there was nothing to translate. Now they read `width.*`, `shape.*` and
        /// `dedup.*` by a stem plus a suffix, which the literal-reading guard cannot follow either.
        ///
        /// The warning is checked only where there is one. FP8 is the only width that carries one,
        /// and `Warning` is deliberately empty rather than a key for the other three.
        /// </summary>
        [Test]
        public void EveryTrainingChoiceHasItsWords()
        {
            InBothLanguages(missing =>
            {
                foreach (var width in TrainingChoiceCatalog.AllPrecisions)
                {
                    Resolved($"width.{width.Precision}.name", width.DisplayName, missing);
                    Resolved($"width.{width.Precision}.pitch", width.Pitch, missing);

                    if (width.HasWarning)
                    {
                        Resolved($"width.{width.Precision}.warning", width.Warning, missing);
                    }
                }

                foreach (var shape in TrainingChoiceCatalog.AllShapes)
                {
                    Resolved($"shape.{shape.Shape}.name", shape.DisplayName, missing);
                    Resolved($"shape.{shape.Shape}.pitch", shape.Pitch, missing);
                    Resolved($"shape.{shape.Shape}.note", shape.Note, missing);
                }

                foreach (var pass in TrainingChoiceCatalog.AllPasses)
                {
                    Resolved($"dedup.{pass.Pass}.name", pass.DisplayName, missing);
                    Resolved($"dedup.{pass.Pass}.pitch", pass.Pitch, missing);
                    Resolved($"dedup.{pass.Pass}.note", pass.Note, missing);
                }
            });
        }

        /// <summary>
        /// The three hosting packages, the six marketing channels, the three regions and the
        /// sixteen countries.
        ///
        /// All four lists stored their English until 2026-09-04 and now read the book by a stem, so
        /// they moved from a blind spot no guard could see into the one this fixture exists for.
        ///
        /// The countries are the ones to care about: they are the **first screen of a new
        /// campaign**, before the player has seen anything else, and there are sixteen of them with
        /// a sentence each. A missing key there is sixteen chances to open the game on a raw key.
        ///
        /// `Average` is checked too. It builds a synthetic entry standing for a whole region by
        /// handing the region's own stem to a country, and nothing else in the game exercises that.
        /// </summary>
        [Test]
        public void EveryPackageChannelRegionAndCountryHasItsWords()
        {
            InBothLanguages(missing =>
            {
                foreach (var package in HostingCatalog.All)
                {
                    Resolved($"hosting.{package.Id}.name", package.DisplayName, missing);
                    Resolved($"hosting.{package.Id}.pitch", package.Pitch, missing);
                }

                foreach (var channel in MarketingCatalog.All)
                {
                    Resolved($"channel.{channel.Id}.name", channel.DisplayName, missing);
                    Resolved($"channel.{channel.Id}.pitch", channel.Pitch, missing);
                }

                foreach (var region in WorldRegionCatalog.All)
                {
                    Resolved($"region.{region.Region}.name", region.DisplayName, missing);
                    Resolved($"region.{region.Region}.note", region.Blurb, missing);

                    var average = WorldRegionCatalog.Average(region.Region);

                    Resolved($"average.{region.Region}.name", average.DisplayName, missing);
                    Resolved($"average.{region.Region}.note", average.Note, missing);
                }

                foreach (var country in WorldRegionCatalog.AllCountries)
                {
                    Resolved($"country.{country.Country}.name", country.DisplayName, missing);
                    Resolved($"country.{country.Country}.note", country.Note, missing);
                }
            });
        }

        /// <summary>
        /// The seven jobs and the ten pieces of furniture, the last two catalogs to stop storing
        /// their English.
        ///
        /// After this no catalog in the game holds a player-facing string, and the whole class of
        /// fault is closed: a list built at type load can no longer keep the language it was built
        /// in. Both of these are screens the player is in constantly, and the job title travels
        /// into the offer letter and onto the name plate over somebody's head in the office.
        /// </summary>
        [Test]
        public void EveryJobAndEveryPieceOfFurnitureHasItsWords()
        {
            InBothLanguages(missing =>
            {
                foreach (var position in PositionCatalog.All)
                {
                    Resolved($"job.{position.Skill}.title", position.Title, missing);
                    Resolved($"job.{position.Skill}.blurb", position.Blurb, missing);
                }

                foreach (var piece in FurnitureCatalog.All)
                {
                    Resolved($"piece.{piece.Kind}.name", piece.DisplayName, missing);
                    Resolved($"piece.{piece.Kind}.blurb", piece.Blurb, missing);
                }
            });
        }

        /// <summary>
        /// The four cabinets, the five model types, the six architecture families, the five staff
        /// roles and the five audience segments.
        ///
        /// **The last of them.** With these, no catalog in the game stores a player-facing string,
        /// and the fault this whole fixture keeps finding - a list built at type load keeping the
        /// language it was built in - has nowhere left to happen.
        /// </summary>
        [Test]
        public void EveryCabinetTypeFamilyRoleAndAudienceHasItsWords()
        {
            InBothLanguages(missing =>
            {
                foreach (var rack in ServerRackCatalog.All)
                {
                    Resolved($"rack.{rack.Id}.name", rack.DisplayName, missing);
                    Resolved($"rack.{rack.Id}.pitch", rack.Pitch, missing);
                    Resolved($"rack.{rack.Id}.note", rack.Note, missing);
                }

                foreach (var corpus in DatasetCatalog.All)
                {
                    Resolved($"corpus.{corpus.Flag}.name", corpus.DisplayName, missing);
                }

                foreach (var type in ModelTypeCatalog.All)
                {
                    Resolved($"modeltype.{type.Type}.name", type.DisplayName, missing);
                    Resolved($"modeltype.{type.Type}.desc", type.Description, missing);
                }

                foreach (var family in ArchitectureCatalog.All)
                {
                    Resolved($"family.{family.Id}.name", family.DisplayName, missing);
                }

                foreach (var role in StaffCatalog.All)
                {
                    Resolved($"role.{role.Role}.name", role.DisplayName, missing);
                    Resolved($"role.{role.Role}.desc", role.Description, missing);
                }

                foreach (var segment in AudienceCatalog.All)
                {
                    Resolved($"audience.{segment.Segment}.name", segment.DisplayName, missing);
                    Resolved($"audience.{segment.Segment}.desc", segment.Description, missing);
                }
            });
        }

        /// <summary>
        /// The last seven catalogs that stored English: skills, safety tiers, founder traits,
        /// funding rounds, hiring channels, marketing programmes and compute tiers.
        ///
        /// **The heading on the section above was wrong when it was written.** It said no catalog in
        /// the game stored a player-facing string, and eighteen did: twelve had been moved and seven
        /// had not, which a review from outside counted before anybody here did. That claim is true
        /// now, and this test is the reason it can stay true.
        /// </summary>
        [Test]
        public void EverySkillTierTraitRoundChannelCampaignAndComputeTierHasItsWords()
        {
            InBothLanguages(missing =>
            {
                foreach (var skill in PlayerSkillCatalog.All)
                {
                    Resolved($"skill.{skill.Skill}.name", skill.DisplayName, missing);
                    Resolved($"skill.{skill.Skill}.about", skill.Description, missing);
                    Resolved($"skill.{skill.Skill}.short", skill.ShortEffect, missing);
                    Resolved($"skill.{skill.Skill}.full", skill.EffectAtFull, missing);
                }

                foreach (var tier in SafetyModuleCatalog.All)
                {
                    Resolved($"safety.{tier.Module}{tier.Tier}.name", tier.DisplayName, missing);
                    Resolved($"safety.{tier.Module}{tier.Tier}.about", tier.Description, missing);
                }

                foreach (var trait in FounderTraitCatalog.All)
                {
                    Resolved($"trait.{trait.Trait}.name", trait.DisplayName, missing);
                    Resolved($"trait.{trait.Trait}.flavour", trait.Flavour, missing);
                    Resolved($"trait.{trait.Trait}.effect", trait.EffectSummary, missing);
                }

                foreach (var round in FundingCatalog.All)
                {
                    Resolved($"funding.{round.Stage}.name", round.DisplayName, missing);
                }

                foreach (var channel in HiringChannels.All)
                {
                    Resolved($"hire.{channel.Source}.name", channel.DisplayName, missing);
                    Resolved($"hire.{channel.Source}.tagline", channel.Tagline, missing);
                }

                foreach (var campaign in MonetizationCatalog.All)
                {
                    Resolved($"{campaign.Key}.name", campaign.DisplayName, missing);
                    Resolved($"{campaign.Key}.about", campaign.Description, missing);
                }

                foreach (var tier in ComputeTierCatalog.All)
                {
                    Resolved($"tier.{tier.Tier}.name", tier.DisplayName, missing);
                    Resolved($"tier.{tier.Tier}.about", tier.Description, missing);
                }

                foreach (PricingModel model in Enum.GetValues(typeof(PricingModel)))
                {
                    Resolved($"pricing.{model}", MonetizationCatalog.PricingName(model), missing);
                }
            });
        }

        /// <summary>The seven skills the creator asks a new player to understand.</summary>
        [Test]
        public void EverySkillNoteHasAllFiveOfItsPhrases()
        {
            InBothLanguages(missing =>
            {
                foreach (PlayerSkill skill in Enum.GetValues(typeof(PlayerSkill)))
                {
                    CheckNote("SkillNotes." + skill, SkillNotes.For(skill), missing);
                }
            });
        }

        /// <summary>
        /// Every grant's name, body and terms.
        ///
        /// These keys are built as `"grant.access." + part`, which is the pattern CLAUDE.md forbids
        /// by name. Writing them out whole would be thirty six lines; checking that they resolve is
        /// stronger, because it tests what the player sees rather than what the source says.
        /// </summary>
        [Test]
        public void EveryGrantHasItsNameBodyAndTerms()
        {
            InBothLanguages(missing =>
            {
                foreach (var grant in GrantCatalog.All)
                {
                    Resolved($"{grant.Id}.name", Loc.T(grant.NameKey), missing);
                    Resolved($"{grant.Id}.body", Loc.T(grant.BodyKey), missing);
                    Resolved($"{grant.Id}.terms", Loc.T(grant.TermsKey), missing);
                }
            });
        }

        /// <summary>Every rival trait's badge and the sentence under it.</summary>
        [Test]
        public void EveryLabTraitHasABadgeAndANote()
        {
            InBothLanguages(missing =>
            {
                foreach (LabTrait trait in Enum.GetValues(typeof(LabTrait)))
                {
                    if (trait == LabTrait.None)
                    {
                        continue;
                    }

                    Resolved($"{trait}.name", LabTraits.NameOf(trait), missing);
                    Resolved($"{trait}.note", LabTraits.NoteFor(trait), missing);
                }
            });
        }

        /// <summary>
        /// Every world event's headline and body, on the day it fires.
        ///
        /// Sixty six keys, and the only thing that ever reads them is the wire on one particular
        /// date, which means a missing one would have surfaced years into a campaign as a headline
        /// reading `world.embargo.head`.
        /// </summary>
        [Test]
        public void EveryWorldEventHasAHeadlineAndABody()
        {
            InBothLanguages(missing =>
            {
                foreach (var entry in WorldEventCatalog.All)
                {
                    Resolved($"{entry.Key}.head", Loc.T(entry.Key + ".head"), missing);
                    Resolved($"{entry.Key}.body", Loc.T(entry.Key + ".body"), missing);
                }
            });
        }
    }
}
