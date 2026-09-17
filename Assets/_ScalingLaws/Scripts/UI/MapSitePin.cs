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

        public MapCategory Category => category;
        public MapSiteKind Kind => kind;

        public void Describe(MapCategory forCategory, MapSiteKind forKind)
        {
            category = forCategory;
            kind = forKind;
        }
    }
}
