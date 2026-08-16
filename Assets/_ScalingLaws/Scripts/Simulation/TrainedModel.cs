using System;
using ScalingLaws.Core;
using ScalingLaws.Data;

namespace ScalingLaws.Simulation
{
    /// <summary>
    /// A finished run sitting on the shelf, trained but not shipped.
    ///
    /// This is where release timing lives. The weights are done and the capability is fixed, so
    /// waiting costs nothing directly. What it costs is position: par rises every month, the
    /// frontier ships, and a model held back through a rival's launch week arrives into a worse
    /// market than the one it was built for. Holding is right when a rival is about to launch into
    /// your slot and you would rather not be compared, or when the model needs one more upgrade
    /// before it faces the press. Holding is wrong most of the rest of the time.
    /// </summary>
    public sealed class TrainedModel
    {
        public TrainedModel(
            string name,
            ArchitectureId architecture,
            double capability,
            GameDate completedOn,
            double activeParameterCount,
            double projectedCapability,
            ModelType type = ModelType.General,
            string family = null,
            ModelShape shape = ModelShape.Balanced,
            int assaTier = 0,
            int redTeamTier = 0,
            int dataProtectionTier = -1,
            int safetyEffort = 1)
        {
            // What the run was hardened with. Carried rather than looked up: a company that
            // researches everything next year does not retroactively protect what it shipped today.
            AssaTier = assaTier;
            RedTeamTier = redTeamTier;
            DataProtectionTier = dataProtectionTier;
            SafetyEffort = safetyEffort;
            // The arrangement is a permanent property of what was built, so it travels the whole way
            // from the blueprint to the market rather than being re-derived anywhere.
            Shape = shape;
            // A model that joins no line starts one, named after itself. Without this "start a new
            // line" produced a model belonging to nothing, which nothing could ever supersede, and the
            // family dropdown would have stayed empty forever however many models were released.
            Type = type == ModelType.None ? ModelType.General : type;
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled model" : name.Trim();
            Family = string.IsNullOrWhiteSpace(family) ? Name : family.Trim();
            Architecture = architecture;
            Capability = Math.Clamp(SimUnits.Finite(capability), 0.0, 100.0);
            CompletedOn = completedOn;

            // Par on the day the run finished. The market keeps moving after that, so a model left
            // on the shelf falls behind on its own without anybody doing anything to it.
            Traits = ModelTraitSet.AtMarketPar(completedOn);
            ActiveParameterCount = Math.Max(1e6, SimUnits.Finite(activeParameterCount, 1e6));
            ProjectedCapability = Math.Clamp(SimUnits.Finite(projectedCapability), 0.0, 100.0);
        }

        public string Name { get; }
        public ArchitectureId Architecture { get; }

        /// <summary>Fixed when training started. A finished model cannot be repurposed.</summary>
        public ModelType Type { get; }

        /// <summary>
        /// The line this belongs to, chosen before the run and carried to the shelf. Fixed for the same
        /// reason the type is: a finished model is a finished model, and deciding afterwards which line
        /// it supersedes would let a player rewrite what they had committed to.
        /// </summary>
        public string Family { get; }

        /// <summary>Measured on completion. Waiting does not change it.</summary>
        public double Capability { get; }

        public GameDate CompletedOn { get; }
        public double ActiveParameterCount { get; }

        /// <summary>What the plan said it would be. Kept for the post mortem, never used as a result.</summary>
        public double ProjectedCapability { get; }

        /// <summary>Deep, balanced or wide. Read by the market every day it is on sale.</summary>
        public ModelShape Shape { get; }

        /// <inheritdoc cref="ModelBlueprint.AssaTier"/>
        public int AssaTier { get; }

        /// <inheritdoc cref="ModelBlueprint.RedTeamTier"/>
        public int RedTeamTier { get; }

        /// <inheritdoc cref="ModelBlueprint.DataProtectionTier"/>
        public int DataProtectionTier { get; }

        /// <inheritdoc cref="ModelBlueprint.SafetyEffort"/>
        public int SafetyEffort { get; }

        /// <summary>
        /// What the model is good at, before it has been released.
        ///
        /// **These used to be conjured at the moment of release and never existed before it.** The
        /// comment on Release said the traits were frozen on the day training finished, which was
        /// true and was exactly the problem: something frozen still exists, and a player looking at
        /// a finished model on the shelf could not touch any of it. Materialising the set here is
        /// what makes post-training work possible before the thing ships, which is when a real lab
        /// does most of it.
        /// </summary>
        public ModelTraitSet Traits { get; private set; }

        /// <summary>Puts a loaded or upgraded set back. Ignores null, like the deployed one does.</summary>
        public void RestoreTraits(ModelTraitSet traits)
        {
            if (traits != null)
            {
                Traits = traits;
            }
        }

        public int DaysOnShelf(GameDate date) => Math.Max(0, date.DayIndex - CompletedOn.DayIndex);

        /// <summary>
        /// How far the model has slipped against market par while it sat unreleased. Not a decay of
        /// the model, a decay of the world around it.
        /// </summary>
        public double ParSlippage(GameDate date)
        {
            var atCompletion = ModelTraitSet.AtMarketPar(CompletedOn);
            return -atCompletion.CapabilityBonus(date);
        }

        /// <summary>
        /// What this would score if shipped today. Equal to <see cref="Capability"/> on the day it
        /// finished and lower every day after.
        /// </summary>
        public double CapabilityIfReleasedOn(GameDate date)
        {
            return Math.Clamp(Capability - ParSlippage(date), 0.0, 100.0);
        }

        /// <summary>Turns the shelf item into a live product at a chosen price.</summary>
        public DeployedModel Release(GameDate date, double priceMultiplier)
        {
            var model = new DeployedModel(
                Name,
                Architecture,
                Capability,
                date,
                ActiveParameterCount,
                priceMultiplier,
                Type,
                Family,
                AssaTier,
                RedTeamTier,
                DataProtectionTier,
                SafetyEffort);

            model.SetShape(Shape);

            // The set the model has been carrying, including anything upgraded while it sat on
            // the shelf. It starts at par for the day training finished, so a model nobody touched
            // still ships behind on whatever the market moved on in the meantime.
            model.RestoreTraits(Traits);
            return model;
        }

        public override string ToString() => $"{Name} (shelved {CompletedOn}, cap {Capability:0.0})";
    }
}
