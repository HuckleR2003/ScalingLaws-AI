using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The city map's sixteen icons: one per <see cref="MapCategory"/>, for the legend and the
    /// district tiles, and one per <see cref="MapSiteKind"/>, for the card a click on a site opens.
    ///
    /// Each is a white glyph on a rounded tile already in its own colour, 256 px square with a
    /// transparent margin, in `Resources/Map/Icons/`. They are shown as drawn and never tinted:
    /// tinting would dye the white glyph along with the tile.
    ///
    /// **A missing file is a coloured square, not an error.** Every caller keeps the plain category
    /// colour it had before the set arrived when this returns null, so an icon renamed or not yet
    /// drawn shows as that colour rather than as a hole. <c>MapIconTests</c> is what notices.
    /// </summary>
    public static class MapIcons
    {
        public const string Folder = "Map/Icons/";

        private static readonly Dictionary<string, Texture2D> Loaded = new();

        public static Texture2D For(MapCategory category) => Load(FileFor(category));

        public static Texture2D For(MapSiteKind kind) => Load(FileFor(kind));

        public static string FileFor(MapCategory category) => category switch
        {
            MapCategory.Compute => "cat_compute",
            MapCategory.Business => "cat_business",
            MapCategory.Research => "cat_research",
            MapCategory.Events => "cat_events",
            MapCategory.Media => "cat_media",
            MapCategory.Energy => "cat_energy",
            MapCategory.Finance => "cat_finance",
            MapCategory.Regulation => "cat_regulation",
            _ => null
        };

        public static string FileFor(MapSiteKind kind) => kind switch
        {
            MapSiteKind.OfficeLease => "site_office",
            MapSiteKind.PropertyListing => "site_property",
            MapSiteKind.ServerFacility => "site_server",
            MapSiteKind.EventVenue => "site_event",
            MapSiteKind.PowerPlantStake => "site_power",
            MapSiteKind.JobAgency => "site_jobs",
            MapSiteKind.TaxOffice => "site_tax",
            MapSiteKind.CarDealership => "site_cars",
            // No icon of its own yet: a headquarters is an office building, and the office icon says so.
            MapSiteKind.RivalHeadquarters => "site_office",
            _ => null
        };

        /// <summary>
        /// Shows an icon on an element in place of its plain colour. Returns false, leaving the
        /// element exactly as it was, when there is no icon to show.
        /// </summary>
        public static bool Apply(VisualElement element, Texture2D icon)
        {
            if (icon == null)
            {
                return false;
            }

            element.style.backgroundImage = new StyleBackground(icon);
            element.style.backgroundColor = Color.clear;
            return true;
        }

        private static Texture2D Load(string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return null;
            }

            if (!Loaded.TryGetValue(file, out var texture) || texture == null)
            {
                texture = Resources.Load<Texture2D>(Folder + file);
                Loaded[file] = texture;
            }

            return texture;
        }
    }
}
