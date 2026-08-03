using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Puts a photograph behind a card, or leaves the card as a colour block when there is no
    /// photograph for it yet.
    ///
    /// Art arrives piecemeal, so nothing here requires a complete set. A card with no image is not a
    /// broken card, it is the same card the game shipped with before any art existed, and the two
    /// sit next to each other without looking like a mistake because the scrim over the photograph
    /// lands close to the flat card colour anyway.
    ///
    /// Images live under Resources so they load by name at runtime. That is the same decision as the
    /// stylesheet: a missing serialized reference must never be able to silently blank the screen.
    /// </summary>
    public static class CardArt
    {
        private const string ResourceFolder = "Cards/";

        private static readonly Dictionary<string, Texture2D> Cache = new();

        /// <summary>Which image belongs to which accelerator, host, memory or fabric part.</summary>
        public static string ForHardware(HardwareClass hardwareClass) => hardwareClass switch
        {
            HardwareClass.Accelerator => "card_gpu",
            HardwareClass.Cpu => "card_cpu",
            HardwareClass.Memory => "card_ram",
            HardwareClass.Network => "card_network",
            _ => null
        };

        /// <summary>Which image belongs to which compute tier.</summary>
        public static string ForTier(ComputeTier tier) => tier switch
        {
            ComputeTier.RentedCloud => "card_cloud",
            ComputeTier.ColocatedServers => "card_rack",
            ComputeTier.OwnDatacenter => "card_datacenter",
            _ => null
        };

        /// <summary>Which image belongs to which upgradeable trait.</summary>
        public static string ForTrait(ModelTrait trait) => trait switch
        {
            ModelTrait.Reasoning => "trait_reasoning",
            ModelTrait.Knowledge => "trait_knowledge",
            ModelTrait.Coding => "trait_coding",
            ModelTrait.Multilingual => "trait_multilingual",
            ModelTrait.Multimodal => "trait_multimodal",
            ModelTrait.ContextLength => "trait_context",
            ModelTrait.Safety => "trait_safety",
            ModelTrait.Latency => "trait_speed",
            ModelTrait.Efficiency => "trait_efficiency",
            ModelTrait.ToolUse => "trait_tools",
            ModelTrait.Ecosystem => "trait_ecosystem",
            _ => null
        };

        /// <summary>
        /// Which image sits behind a research node. All the nodes of an era share one, which turns
        /// four images into cover for seventeen cards and makes the eras read as eras.
        /// </summary>
        public static string ForEra(ResearchEra era) => era switch
        {
            ResearchEra.Foundations => "era_foundations",
            ResearchEra.Scaling => "era_scaling",
            ResearchEra.Autonomy => "era_autonomy",
            _ => "era_superintelligence"
        };

        /// <summary>
        /// Applies an image to a card if it exists. Adds the scrim first so the title and the price
        /// always land on top of it rather than on the photograph.
        /// </summary>
        public static void Apply(VisualElement card, string imageName)
        {
            if (card == null || string.IsNullOrEmpty(imageName))
            {
                return;
            }

            var texture = Load(imageName);
            if (texture == null)
            {
                return;
            }

            card.style.backgroundImage = new StyleBackground(texture);
            card.AddToClassList("card--art");

            var scrim = new VisualElement();
            scrim.AddToClassList("card__scrim");
            scrim.pickingMode = PickingMode.Ignore;
            card.Insert(0, scrim);
        }

        private static Texture2D Load(string imageName)
        {
            if (Cache.TryGetValue(imageName, out var cached))
            {
                return cached;
            }

            var texture = Resources.Load<Texture2D>(ResourceFolder + imageName);
            Cache[imageName] = texture;
            return texture;
        }

        /// <summary>Drops the cache so re-imported art shows up without restarting play mode.</summary>
        public static void ClearCache() => Cache.Clear();
    }
}
