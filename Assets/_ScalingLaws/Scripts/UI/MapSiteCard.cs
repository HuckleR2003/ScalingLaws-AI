using ScalingLaws.Data;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The card that opens when a place on the map is clicked: what it is, what it would cost, and
    /// the way in.
    ///
    /// **Upright and narrow on purpose.** It stands beside the map rather than across it, so the
    /// place being read about stays visible behind it — the opposite of the full-width sheets the
    /// office screens use, where there is nothing behind worth seeing.
    ///
    /// **Prices are deliberately blank.** <see cref="MapSiteDefinition.SystemExists"/> records
    /// whether the rules behind a site are written yet, and most are not; printing a number the
    /// simulation cannot back would be inventing an economy in the interface. The rows are here,
    /// laid out and styled, waiting for the catalogs to fill them.
    /// </summary>
    public sealed class MapSiteCard : VisualElement
    {
        /// <summary>Shown where a number will go once the rules behind a site are written.</summary>
        private const string Pending = "—";

        private readonly Label title = new();
        private readonly Label kindLine = new();
        private readonly Label blurb = new();
        private readonly VisualElement icon = new();
        private readonly VisualElement figures = new();
        private readonly Button action = new();
        private readonly Label footnote = new();

        /// <summary>
        /// True while the cursor is over the card.
        ///
        /// Tracked with the card's own enter and leave events rather than by converting the mouse
        /// into panel coordinates, which needs the panel's scale, the screen's height and a flipped
        /// Y and gets one of the three wrong. The click handler reads this to leave interface clicks
        /// to the interface — without it, closing the card immediately selects whatever was behind
        /// the close button.
        /// </summary>
        public bool PointerIsOver { get; private set; }

        public MapSiteCard(System.Action onClose)
        {
            AddToClassList("site-card");

            RegisterCallback<PointerEnterEvent>(_ => PointerIsOver = true);
            RegisterCallback<PointerLeaveEvent>(_ => PointerIsOver = false);

            var header = new VisualElement();
            header.AddToClassList("site-card__header");
            Add(header);

            // The icon for the kind of place this is, or the category's plain colour for a kind that
            // has none yet, so a card is recognisable either way.
            icon.AddToClassList("site-card__icon");
            header.Add(icon);

            var heading = new VisualElement();
            heading.AddToClassList("site-card__heading");
            header.Add(heading);

            title.AddToClassList("site-card__title");
            heading.Add(title);

            kindLine.AddToClassList("site-card__kind");
            heading.Add(kindLine);

            var close = new Button(() => onClose());
            close.AddToClassList("site-card__close");
            close.text = "×";
            header.Add(close);

            blurb.AddToClassList("site-card__blurb");
            Add(blurb);

            figures.AddToClassList("site-card__figures");
            Add(figures);

            action.AddToClassList("site-card__action");
            action.SetEnabled(false);
            Add(action);

            footnote.AddToClassList("site-card__footnote");
            Add(footnote);
        }

        /// <summary>Fills the card in for one site.</summary>
        public void Show(MapSiteDefinition site)
        {
            title.text = site.DisplayName;
            kindLine.text = Loc.T(KindKey(site.Kind)).ToUpperInvariant();
            blurb.text = site.DecisionBlurb;

            // Reset first: the card is reused, and the last site's icon must not stay on a kind without one.
            icon.style.backgroundImage = StyleKeyword.Null;
            icon.style.backgroundColor = MapCategoryPalette.ColourFor(site.Category);
            icon.EnableInClassList("site-card__icon--image", MapIcons.Apply(icon, MapIcons.For(site.Kind)));

            figures.Clear();

            foreach (var row in RowsFor(site.Kind))
            {
                figures.Add(Figure(Loc.T(row)));
            }

            action.text = Loc.T(ActionKey(site.Kind));

            // Everything here is a door that does not open yet, and the card says so rather than
            // letting a greyed button look like a bug.
            footnote.text = Loc.T(site.SystemExists ? "map.card.soon" : "map.card.no_system");

            style.display = DisplayStyle.Flex;
        }

        public void Hide() => style.display = DisplayStyle.None;

        /// <summary>One labelled figure, with the value still to come.</summary>
        private static VisualElement Figure(string label)
        {
            var row = new VisualElement();
            row.AddToClassList("site-card__figure");

            var name = new Label(label);
            name.AddToClassList("site-card__figure-label");
            row.Add(name);

            var value = new Label(Pending);
            value.AddToClassList("site-card__figure-value");
            row.Add(value);

            return row;
        }

        /// <summary>
        /// Which figures a kind of site even has. A tax office has no rent and a car showroom has
        /// no lease — showing an empty row for each would be noise pretending to be information.
        /// </summary>
        private static string[] RowsFor(MapSiteKind kind) => kind switch
        {
            MapSiteKind.OfficeLease => new[] { "map.card.rent", "map.card.desks" },
            MapSiteKind.ServerFacility => new[] { "map.card.rent", "map.card.buy", "map.card.power" },
            MapSiteKind.PropertyListing => new[] { "map.card.buy", "map.card.upkeep" },
            MapSiteKind.PowerPlantStake => new[] { "map.card.stake", "map.card.power" },
            MapSiteKind.EventVenue => new[] { "map.card.stand", "map.card.audience" },
            MapSiteKind.CarDealership => new[] { "map.card.buy" },
            _ => new[] { "map.card.visit" }
        };

        private static string ActionKey(MapSiteKind kind) => kind switch
        {
            MapSiteKind.EventVenue => "map.card.book",
            MapSiteKind.OfficeLease or MapSiteKind.ServerFacility => "map.card.lease",
            MapSiteKind.PropertyListing or MapSiteKind.CarDealership => "map.card.buy_action",
            _ => "map.card.enter"
        };

        private static string KindKey(MapSiteKind kind) => kind switch
        {
            MapSiteKind.OfficeLease => "map.kind.office",
            MapSiteKind.PropertyListing => "map.kind.property",
            MapSiteKind.ServerFacility => "map.kind.server",
            MapSiteKind.EventVenue => "map.kind.event",
            MapSiteKind.PowerPlantStake => "map.kind.power",
            MapSiteKind.JobAgency => "map.kind.jobs",
            MapSiteKind.TaxOffice => "map.kind.tax",
            _ => "map.kind.cars"
        };
    }
}
