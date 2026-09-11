using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What a membership is worth, behind a button, on both screens that sell one.
    ///
    /// **The pitch and the benefits answer different questions.** The pitch says what the outlet
    /// is; this says what it buys you, which is the one a player is actually asking with their hand
    /// on four hundred thousand a month. It was written for the news page and the intelligence
    /// screen never had it, so the same three memberships were sold twice on two screens and only
    /// one of them made the case.
    ///
    /// Moved here rather than copied, the same reasoning `UiParts` was extracted under: two copies
    /// of a sales pitch drift, and nobody reviewing one screen would notice the other had changed.
    /// </summary>
    public static class IntelBenefits
    {
        /// <summary>
        /// What each membership is worth, in the outlet's own terms.
        ///
        /// **A switch rather than a key assembled from the enum name.** A phrase-book key built by
        /// concatenation is invisible to `LocalisationTests`, and this project has already shipped
        /// one screen of raw keys that way.
        /// </summary>
        public static string Copy(IntelTier tier) => tier switch
        {
            IntelTier.NationalPress => Loc.T("intel.benefits.press"),
            IntelTier.KnownWords => Loc.T("intel.benefits.known"),
            _ => Loc.T("intel.benefits.trend")
        };

        /// <summary>
        /// The button, and the paragraph under it when it is open.
        ///
        /// The caller owns which tier is expanded, because that is screen state and the two screens
        /// keep it in different places. This owns what the control looks like and what it says, so
        /// they cannot disagree about either.
        /// </summary>
        public static VisualElement Block(IntelTier tier, bool open, Action toggle)
        {
            var block = new VisualElement();
            block.AddToClassList("desk__benefits");

            var button = new Button(toggle)
            {
                text = Loc.T(open ? "intel.benefits.hide" : "intel.benefits.show")
            };

            button.AddToClassList("desk__seebenefits");
            block.Add(button);

            if (!open)
            {
                return block;
            }

            var body = new Label(Copy(tier));
            body.AddToClassList("desk__benefitstext");
            block.Add(body);

            return block;
        }
    }
}
