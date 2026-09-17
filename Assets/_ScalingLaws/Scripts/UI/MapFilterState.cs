using System;
using ScalingLaws.Data;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Which one map category, if any, is picked out from the rest.
    ///
    /// **On its own, with no UnityEngine in it**, so the toggle rule (clicking the lit category
    /// clears it, clicking any other replaces it) is a fact this file can be wrong about on its own,
    /// checked without a scene, a panel or a pin.
    /// </summary>
    public sealed class MapFilterState
    {
        public MapCategory? Selected { get; private set; }

        public event Action Changed;

        /// <summary>Picks a category, or clears it when it is already the one picked.</summary>
        public void Toggle(MapCategory category)
        {
            Selected = Selected == category ? null : category;
            Changed?.Invoke();
        }

        public void Clear()
        {
            if (Selected == null)
            {
                return;
            }

            Selected = null;
            Changed?.Invoke();
        }

        /// <summary>True when nothing is picked, or this is the category that is.</summary>
        public bool IsEmphasised(MapCategory category) => Selected == null || Selected == category;
    }
}
