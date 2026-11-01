using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// How the stand went, shown on the day the event closes.
    ///
    /// **It arrives during the day rather than on the next tick**, because it is an occasion and
    /// this project already rations that kind of card to occasions. The author's layout: a
    /// rectangular cutscene tile across the top, then what was gained, then one line said from the
    /// stand.
    ///
    /// **Nothing here computes.** The outcome is handed in, the way every figure on the venue
    /// window is handed in, so the screen and the rules cannot disagree about what a stand was
    /// worth. That is the fault this project has recorded four copies of about one heat threshold.
    /// </summary>
    public sealed class EventSummaryPanel
    {
        private readonly CityEventDefinition happening;
        private readonly EventVenueDefinition venue;
        private readonly EventOutcome outcome;
        private readonly StandTier tier;

        public EventSummaryPanel(CityEventDefinition happening, EventVenueDefinition venue,
            StandTier tier, EventOutcome outcome)
        {
            this.happening = happening;
            this.venue = venue;
            this.tier = tier;
            this.outcome = outcome;

            Root = new VisualElement();
            Root.AddToClassList("evsum");

            Build();
        }

        public VisualElement Root { get; }

        private void Build()
        {
            // ---- the cutscene, a rectangle across the top ---------------------------------------
            //
            // A short animation of people at the stand goes here. Until it exists the tile draws a
            // plate and says so, like every other loader in this project: an absent file has to be
            // visible as unfinished rather than as a hole.
            var film = new VisualElement();
            film.AddToClassList("evsum__film");

            var clip = Resources.Load<Texture2D>("Venues/scene_"
                + venue.Id.ToString().ToLowerInvariant());

            if (clip != null)
            {
                film.style.backgroundImage = new StyleBackground(clip);
            }
            else
            {
                var waiting = new Label(Loc.T("evsum.film_soon"));
                waiting.AddToClassList("evsum__film-waiting");
                film.Add(waiting);
            }

            Root.Add(film);

            // ---- what it was ---------------------------------------------------------------------
            var kicker = new Label(Loc.T("evsum.kicker"));
            kicker.AddToClassList("evsum__kicker");
            Root.Add(kicker);

            var title = new Label(happening.DisplayName.ToUpperInvariant());
            title.AddToClassList("evsum__title");
            Root.Add(title);

            var where = new Label(venue.DisplayName + "  ·  " + EventVenueCatalog.TierName(tier));
            where.AddToClassList("evsum__where");
            Root.Add(where);

            // ---- the result, which is the point of the card ---------------------------------------
            if (outcome.WentViral)
            {
                var viral = new VisualElement();
                viral.AddToClassList("evsum__viral");

                var word = new Label(Loc.T("evsum.viral"));
                word.AddToClassList("evsum__viral-word");
                viral.Add(word);

                // **The odds, beside the result.** Going viral at 6% and at 38% are two different
                // stories and the word alone tells them identically.
                var odds = new Label(Loc.T("evsum.viral_odds",
                    UiFormat.Percent(outcome.ViralShare, 0)));

                odds.AddToClassList("evsum__viral-odds");
                viral.Add(odds);

                Root.Add(viral);
            }

            var gains = new VisualElement();
            gains.AddToClassList("evsum__gains");

            gains.Add(Gain(Loc.T("evsum.fans"), "+" + UiFormat.Count(outcome.Fans)));
            gains.Add(Gain(Loc.T("evsum.reputation"),
                "+" + UiFormat.Number(outcome.Reputation, 3)));

            Root.Add(gains);

            // ---- one line from the stand -----------------------------------------------------------
            var said = new Label(venue.ClosingComment);
            said.AddToClassList("evsum__said");
            Root.Add(said);
        }

        private static VisualElement Gain(string caption, string figure)
        {
            var block = new VisualElement();
            block.AddToClassList("evsum__gain");

            var value = new Label(figure);
            value.AddToClassList("evsum__gain-value");
            block.Add(value);

            var head = new Label(caption);
            head.AddToClassList("evsum__gain-caption");
            block.Add(head);

            return block;
        }
    }
}
