using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// OFFICES FOR RENT, the banner under the map filters: three size chips and a button that flies
    /// to the next office of the sizes ticked.
    ///
    /// The walk and the sizes live in <see cref="OfficeListing"/>; this is only the controls, so the
    /// order the offices come in can be tested without a panel.
    /// </summary>
    public sealed class MapOfficePanel : VisualElement
    {
        private readonly OfficeListing listing;
        private readonly Action<MapSiteDefinition> show;
        private readonly Dictionary<OfficeSize, Button> chips = new();
        private readonly Button next;
        private readonly Label current;

        /// <param name="officeListing">The offices and which sizes are ticked.</param>
        /// <param name="showOffice">Flies the map to an office and opens its card.</param>
        public MapOfficePanel(OfficeListing officeListing, Action<MapSiteDefinition> showOffice)
        {
            listing = officeListing;
            show = showOffice;

            AddToClassList("map-offices");

            var title = new Label(Loc.T("map.offices.title"));
            title.AddToClassList("map-legend__title");
            Add(title);

            var sizes = new VisualElement();
            sizes.AddToClassList("map-offices__sizes");
            Add(sizes);

            foreach (OfficeSize size in Enum.GetValues(typeof(OfficeSize)))
            {
                var captured = size;
                var chip = new Button(() =>
                {
                    listing.Toggle(captured);
                    Refresh();
                })
                {
                    text = Loc.T(KeyFor(size))
                };

                chip.AddToClassList("map-offices__size");
                chips[size] = chip;
                sizes.Add(chip);
            }

            current = new Label();
            current.AddToClassList("map-offices__current");
            Add(current);

            next = new Button(ShowNext);
            next.AddToClassList("map-legend__tour");
            Add(next);

            Refresh();
        }

        /// <summary>The phrase-book key for a size, written out so the localisation guard can see it.</summary>
        public static string KeyFor(OfficeSize size) => size switch
        {
            OfficeSize.Small => "map.offices.small",
            OfficeSize.Medium => "map.offices.medium",
            OfficeSize.Large => "map.offices.large",
            _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown office size.")
        };

        private void ShowNext()
        {
            var office = listing.Next();

            if (office != null)
            {
                current.text = Loc.T("map.offices.showing", office.DisplayName,
                    OfficeListing.DesksOf(office));
                show?.Invoke(office);
            }

            Refresh();
        }

        private void Refresh()
        {
            foreach (var (size, chip) in chips)
            {
                chip.EnableInClassList("map-offices__size--on", listing.IsPicked(size));
            }

            next.text = Loc.T("map.offices.next", listing.NextOrdinal, listing.Count);
            next.SetEnabled(listing.Count > 0);
        }
    }
}
