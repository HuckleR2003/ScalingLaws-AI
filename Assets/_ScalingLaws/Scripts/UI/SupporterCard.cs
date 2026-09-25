using UnityEngine;
using UnityEngine.UIElements;
using ScalingLaws.Data;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The Steam launch fund, said once, in the two places a player already opens.
    ///
    /// **Everything about this offer lived outside the game until now**: on the site, on itch, in a
    /// text file inside the download. So the only people who could see it were the people already
    /// reading about the game rather than playing it, which is exactly backwards. Somebody who has
    /// put an evening into a campaign is the person who might care that the listing fee is the one
    /// thing standing between this build and Steam.
    ///
    /// **It states only what the game can actually deliver today.** The credits carry a SUPPORTERS
    /// section and it is wired, so a name goes in the moment there is a name. The hundred zloty tier
    /// also promises a candidate you can hire on the TEAM screen, and that is **not built**, so it
    /// is not printed here. A tile in the game claiming something the game does not do is worse than
    /// no tile, and `SupporterCardTests` holds that line.
    ///
    /// Not a nag. One flat card in a corner, no animation, no counter ticking at anybody, and it
    /// never opens itself. The player has to look at it.
    /// </summary>
    public static class SupporterCard
    {
        /// <summary>Where the money goes. The same address the site's counter points at.</summary>
        public const string Url = "https://github.com/sponsors/HuckleR2003";

        /// <summary>
        /// The card. <paramref name="corner"/> pins it to a corner for the screens that are already
        /// full, and leaves it in the flow for the ones that have room.
        /// </summary>
        public static VisualElement Build(bool corner = false)
        {
            var card = new VisualElement();
            card.AddToClassList("support-card");
            if (corner)
            {
                card.AddToClassList("support-card--corner");
            }

            var kicker = new Label(Loc.T("support.kicker"));
            kicker.AddToClassList("support-card__kicker");
            card.Add(kicker);

            var title = new Label(Loc.T("support.title"));
            title.AddToClassList("support-card__title");
            card.Add(title);

            var why = new Label(Loc.T("support.why"));
            why.AddToClassList("support-card__why");
            card.Add(why);

            card.Add(Tier(Loc.T("support.tier_name")));
            card.Add(Tier(Loc.T("support.tier_early")));

            var until = new Label(Loc.T("support.until"));
            until.AddToClassList("support-card__until");
            card.Add(until);

            // **Opens a browser and nothing else.** No payment screen, no field to type into, and
            // no state in the save: what the player does on that page is between them and GitHub.
            var open = new Button(() => Application.OpenURL(Url))
            {
                text = Loc.T("support.open")
            };
            open.AddToClassList("support-card__open");
            card.Add(open);

            var free = new Label(Loc.T("support.free"));
            free.AddToClassList("support-card__free");
            card.Add(free);

            return card;
        }

        private static VisualElement Tier(string text)
        {
            var row = new VisualElement();
            row.AddToClassList("support-card__tier");

            var dot = new VisualElement();
            dot.AddToClassList("support-card__dot");
            row.Add(dot);

            var label = new Label(text);
            label.AddToClassList("support-card__tiertext");
            row.Add(label);

            return row;
        }
    }
}
