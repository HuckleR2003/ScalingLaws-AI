using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What <see cref="MapSiteCatalog"/> entry a pin in the city scene stands for, kept on the pin
    /// itself so a runtime filter never has to search the catalog by name to find out.
    ///
    /// Separate from <see cref="CityProp"/> rather than added to it: `CityProp.Describe` is called
    /// from every builder in this project and widening its signature for one caller's extra field
    /// would touch code that has nothing to do with the map. A second small component costs one
    /// `AddComponent` and touches nobody else.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapSitePin : MonoBehaviour
    {
        [SerializeField] private MapCategory category;
        [SerializeField] private MapSiteKind kind;
        [SerializeField] private string siteId;

        public MapCategory Category => category;
        public MapSiteKind Kind => kind;

        /// <summary>
        /// Which <see cref="MapSiteCatalog"/> entry this is, so a click can show what the catalog
        /// already says about it — the name, the decision it poses, whether its rules are written
        /// yet. The category and kind alone cannot answer any of that: five sites share a kind.
        /// </summary>
        public string SiteId => siteId;

        /// <summary>The catalog entry itself, or null if the id no longer matches one.</summary>
        public MapSiteDefinition Definition
        {
            get
            {
                if (string.IsNullOrEmpty(siteId))
                {
                    return null;
                }

                foreach (var site in MapSiteCatalog.All)
                {
                    if (site.Id == siteId)
                    {
                        return site;
                    }
                }

                return null;
            }
        }

        public void Describe(MapCategory forCategory, MapSiteKind forKind, string forSiteId = null)
        {
            category = forCategory;
            kind = forKind;

            if (!string.IsNullOrEmpty(forSiteId))
            {
                siteId = forSiteId;
            }
        }
    }
}
