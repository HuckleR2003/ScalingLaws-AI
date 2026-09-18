using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The card that opens when a place on the map is clicked: what it is, what it would cost, and
    /// the way in.
    ///
    /// **It stands next to the building now, not in the corner.** A card pinned to the top right of
    /// the screen makes the player look away from the thing they clicked and then look back to check
    /// they read about the right one. <see cref="PlaceNear"/> puts it beside the building and keeps it
    /// on screen; the map is what moves under it.
    ///
    /// **Bigger, because it is read at a glance and then acted on.** The first version was a 320px
    /// column of 13px prose, which is a leaflet. The numbers are the point of this card and they are
    /// now the size of numbers somebody is about to spend money on.
    ///
    /// **Real figures where a catalog has them.** <see cref="MapSiteDefinition.SystemExists"/> records
    /// whether the rules behind a site are written yet. Offices and the two power plants have
    /// catalogs, so their rows carry the actual rent, desks, capex and build time; the rest still show
    /// a dash, because printing a number the simulation cannot back would be inventing an economy in
    /// the interface.
    /// </summary>
    public sealed class MapSiteCard : VisualElement
    {
        /// <summary>Shown where a number will go once the rules behind a site are written.</summary>
        private const string Pending = "—";

        /// <summary>How far the card sits from the building it belongs to, in pixels.</summary>
        private const float Gap = 28f;

        private readonly Label title = new();
        private readonly Label kindLine = new();
        private readonly Label blurb = new();
        private readonly VisualElement icon = new();
        private readonly VisualElement figures = new();
        private readonly VisualElement preview = new();
        private readonly Label previewNote = new();
        private readonly Button action = new();
        private readonly Label footnote = new();

        private bool previewOpen;

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

            // **A click inside the card, but not on the preview, folds the preview back.** The author
            // asked for exactly this: the big view is a detour, and any other click is a way out of it.
            RegisterCallback<PointerDownEvent>(_ => ShowPreview(false));

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

            // The figures and the little window into the place, side by side: the numbers are what
            // the decision is made on, and the view is what makes it a place rather than a row.
            var middle = new VisualElement();
            middle.AddToClassList("site-card__middle");
            Add(middle);

            figures.AddToClassList("site-card__figures");
            middle.Add(figures);

            preview.AddToClassList("site-card__preview");
            preview.RegisterCallback<PointerDownEvent>(down =>
            {
                ShowPreview(!previewOpen);
                down.StopPropagation();
            });

            previewNote.AddToClassList("site-card__preview-note");
            previewNote.pickingMode = PickingMode.Ignore;
            preview.Add(previewNote);

            middle.Add(preview);

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

            foreach (var (label, value) in FiguresFor(site))
            {
                figures.Add(Figure(label, value));
            }

            action.text = Loc.T(ActionKey(site.Kind));

            // The rules behind most of these are not written yet, and the card says so rather than
            // letting a greyed button look like a bug.
            footnote.text = Loc.T(site.SystemExists ? "map.card.soon" : "map.card.no_system");

            FillPreview(site);

            style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            ShowPreview(false);
            style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Puts the card beside a point on screen, in panel coordinates, and keeps the whole of it
        /// inside the panel. Called every frame while something is selected, because the map moves
        /// under the card and a card that stayed put would drift off its own building.
        /// </summary>
        public void PlaceNear(Vector2 panelPoint)
        {
            var panel = parent;

            if (panel == null || float.IsNaN(panelPoint.x) || float.IsNaN(panelPoint.y))
            {
                return;
            }

            var width = float.IsNaN(resolvedStyle.width) || resolvedStyle.width <= 0f ? 460f : resolvedStyle.width;
            var height = float.IsNaN(resolvedStyle.height) || resolvedStyle.height <= 0f ? 420f : resolvedStyle.height;
            var room = panel.contentRect;

            // To the right of the building where there is room, to its left where there is not: the
            // card should never be the reason the player cannot see what they clicked.
            var left = panelPoint.x + Gap;

            if (left + width > room.width - 12f)
            {
                left = panelPoint.x - Gap - width;
            }

            var top = panelPoint.y - height * 0.5f;

            style.left = Mathf.Clamp(left, 12f, Mathf.Max(12f, room.width - width - 12f));
            style.top = Mathf.Clamp(top, 12f, Mathf.Max(12f, room.height - height - 12f));
            style.right = StyleKeyword.Null;
            style.bottom = StyleKeyword.Null;
        }

        private void ShowPreview(bool open)
        {
            if (previewOpen == open)
            {
                return;
            }

            previewOpen = open;
            preview.EnableInClassList("site-card__preview--open", open);
            EnableInClassList("site-card--preview-open", open);
        }

        /// <summary>
        /// The little window into the place. A still from inside where there is one, and a plain slot
        /// that says what will be there where there is not — the author asked for the frame to exist
        /// now so the views can be dropped in as they are rendered.
        /// </summary>
        private void FillPreview(MapSiteDefinition site)
        {
            ShowPreview(false);

            var inside = site.Kind is MapSiteKind.OfficeLease or MapSiteKind.ServerFacility;
            preview.style.display = inside ? DisplayStyle.Flex : DisplayStyle.None;

            if (!inside)
            {
                return;
            }

            var art = Resources.Load<Texture2D>($"Map/Interiors/{site.Id}");

            preview.style.backgroundImage = art != null ? new StyleBackground(art) : StyleKeyword.Null;
            preview.EnableInClassList("site-card__preview--empty", art == null);
            previewNote.text = Loc.T(art == null ? "map.card.inside.soon" : "map.card.inside");
        }

        /// <summary>One labelled figure.</summary>
        private static VisualElement Figure(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("site-card__figure");

            var name = new Label(label);
            name.AddToClassList("site-card__figure-label");
            row.Add(name);

            var amount = new Label(value);
            amount.AddToClassList("site-card__figure-value");
            row.Add(amount);

            return row;
        }

        /// <summary>
        /// Which figures a kind of site even has, and what they say.
        ///
        /// A tax office has no rent and a car showroom has no lease, so the rows differ by kind. Where
        /// a catalog already computes the numbers they are the real ones; everywhere else the row is
        /// still here, with a dash in it, because the row is what says this place will have a price.
        /// </summary>
        private static IEnumerable<(string Label, string Value)> FiguresFor(MapSiteDefinition site)
        {
            switch (site.Kind)
            {
                case MapSiteKind.OfficeLease when OfficeCatalog.TryGet((OfficeTier)site.Tier, out var office):
                    yield return (Loc.T("map.card.rent"), Loc.T("map.card.per_month", UiFormat.Money(office.MonthlyRentUsd)));
                    yield return (Loc.T("map.card.desks"), office.Desks.ToString());

                    if (office.FitOutCostUsd > 0L)
                    {
                        yield return (Loc.T("map.card.fitout"), UiFormat.Money(office.FitOutCostUsd));
                    }

                    if (office.RequiredCashUsd > 0L)
                    {
                        yield return (Loc.T("map.card.cash_needed"), UiFormat.Money(office.RequiredCashUsd));
                    }

                    if (office.CanBeBought)
                    {
                        yield return (Loc.T("map.card.buy"), UiFormat.Money(office.PurchasePriceUsd));
                    }

                    break;

                case MapSiteKind.OfficeLease:
                    yield return (Loc.T("map.card.rent"), Pending);
                    yield return (Loc.T("map.card.desks"), Pending);
                    break;

                case MapSiteKind.PowerPlantStake when System.Enum.IsDefined(typeof(PowerPlantSite), site.Tier):
                {
                    var plant = PowerPlantCatalog.Get((PowerPlantSite)site.Tier);

                    yield return (Loc.T("map.card.stake"), UiFormat.Money(plant.CapexUsd));
                    yield return (Loc.T("map.card.power"), Loc.T("map.card.megawatts", UiFormat.Count(plant.Megawatts)));
                    yield return (Loc.T("map.card.build_time"), UiFormat.Days(plant.BuildDays));
                    break;
                }

                case MapSiteKind.ServerFacility:
                    yield return (Loc.T("map.card.rent"), Pending);
                    yield return (Loc.T("map.card.buy"), Pending);
                    yield return (Loc.T("map.card.power"), Pending);
                    break;

                case MapSiteKind.PropertyListing:
                    yield return (Loc.T("map.card.buy"), Pending);
                    yield return (Loc.T("map.card.upkeep"), Pending);
                    break;

                case MapSiteKind.PowerPlantStake:
                    yield return (Loc.T("map.card.stake"), Pending);
                    yield return (Loc.T("map.card.power"), Pending);
                    break;

                case MapSiteKind.EventVenue:
                    yield return (Loc.T("map.card.stand"), Pending);
                    yield return (Loc.T("map.card.audience"), Pending);
                    break;

                case MapSiteKind.CarDealership:
                    yield return (Loc.T("map.card.buy"), Pending);
                    break;

                default:
                    yield return (Loc.T("map.card.visit"), Pending);
                    break;
            }
        }

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
            MapSiteKind.RivalHeadquarters => "map.kind.rival_hq",
            _ => "map.kind.cars"
        };
    }
}
