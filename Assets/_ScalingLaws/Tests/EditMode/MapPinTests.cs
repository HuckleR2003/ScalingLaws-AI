using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.Tests.EditMode
{
    /// <summary>
    /// The city and the site catalog agree about which places exist.
    ///
    /// **Francisco reported three things on 2026-10-27 that look like three faults and share one
    /// cause**: map filters that appear to do nothing, buildings with no circle on them, and a
    /// marker he could barely find. A pin is a component placed on a building in `City.unity`, and
    /// `MapSiteBeacons` draws a ring and a column for each one. No pin means no ring, no card and
    /// nothing for a filter to light up, and that is indistinguishable from a filter that is
    /// broken.
    ///
    /// The scene is hand-built, so this cannot be fixed from code and is not trying to be. It
    /// reads the scene file as text, the same way `FounderRoutineTests` reads the room builder,
    /// and says exactly which places are wrong so the editor work is five minutes rather than a
    /// hunt.
    /// </summary>
    public sealed class MapPinTests
    {
        private static string CityScene =>
            Path.Combine(Application.dataPath, "_ScalingLaws", "Scenes", "City.unity");

        /// <summary>The `MapSitePin` script, by the guid its meta file carries.</summary>
        private const string PinGuid = "925b8a351f0d8ff4e9b0c86a6fe9e83d";

        private static readonly Regex SiteId =
            new(@"siteId:\s*(?<id>\S*)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>Every siteId written on a pin in the city, blanks included.</summary>
        private static List<string> PinnedIds()
        {
            var text = File.ReadAllText(CityScene);
            var found = new List<string>();

            foreach (var block in text.Split(PinGuid).Skip(1))
            {
                // The component's own fields follow the script reference, so the first siteId
                // after it is this pin's.
                var match = SiteId.Match(block);
                found.Add(match.Success ? match.Groups["id"].Value.Trim() : string.Empty);
            }

            return found;
        }

        [Test]
        public void EveryPinInTheCityNamesAPlace()
        {
            var ids = PinnedIds();

            Assert.That(ids, Is.Not.Empty, "No pins were found in City.unity at all.");

            var blanks = ids.Count(string.IsNullOrWhiteSpace);

            Assert.That(blanks, Is.Zero,
                blanks + " of the " + ids.Count + " pins in City.unity have no siteId on them. A "
                + "pin with no place is a marker on a building that can never be selected, can "
                + "never draw a ring and can never be lit by a filter. Set the siteId on each, or "
                + "take the component off the building.");
        }

        [Test]
        public void EveryPinNamesAPlaceTheCatalogKnows()
        {
            var known = new HashSet<string>(MapSiteCatalog.All.Select(site => site.Id));

            var strangers = PinnedIds()
                .Where(id => !string.IsNullOrWhiteSpace(id) && !known.Contains(id))
                .Distinct()
                .ToList();

            Assert.That(strangers, Is.Empty,
                "These pins name places the catalog does not have, so the card has nothing to "
                + "show when they are clicked:\n  " + string.Join("\n  ", strangers));
        }

        /// <summary>
        /// Every place the game says is open has somewhere to stand.
        ///
        /// **The places still marked `systemExists: false` are exempt**, and deliberately: the
        /// catalog carries thirty three of them as surveyed ground for mechanics that are not
        /// written, and putting a marker on each would promise thirty three doors that do not
        /// open. A place whose rules exist and whose building does not is the real fault.
        /// </summary>
        [Test]
        public void EveryWorkingPlaceHasAPinSomewhereInTheCity()
        {
            var pinned = new HashSet<string>(PinnedIds().Where(id => !string.IsNullOrWhiteSpace(id)));

            var homeless = MapSiteCatalog.All
                .Where(site => site.SystemExists && !pinned.Contains(site.Id))
                .Select(site => site.Id + " (" + site.Kind + ")")
                .ToList();

            Assert.That(homeless, Is.Empty,
                "These places work in the simulation and have no building in the city, so no "
                + "player can reach them:\n  " + string.Join("\n  ", homeless));
        }
    }
}
