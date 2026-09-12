using System;
using System.Linq;
using ScalingLaws.Data;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// What shape each research board comes out as.
    ///
    /// **Because the frame it is drawn in has a height and the board has another one.** The board is
    /// derived from the prerequisites, so nobody chooses how tall it is; the only honest way to size
    /// the frame around it is to ask. Same reason the furniture kit has a measuring command.
    /// </summary>
    public static class ResearchBoardProbe
    {
        [MenuItem("Scaling Laws/Print the research board")]
        public static void Print()
        {
            foreach (ResearchEra era in Enum.GetValues(typeof(ResearchEra)))
            {
                foreach (ResearchTrack track in Enum.GetValues(typeof(ResearchTrack)))
                {
                    var slots = ResearchLayout.Place(era, track);

                    if (slots.Count == 0)
                    {
                        continue;
                    }

                    var columns = ResearchLayout.Columns(slots);
                    var rows = ResearchLayout.Rows(slots);

                    // Read from the board rather than repeated here, or the probe reports the
                    // size the cards used to be. It already did that once.
                    var wide = UI.ResearchBoard.Margin * 2f
                        + columns * (UI.ResearchBoard.CardWidth + UI.ResearchBoard.ColumnGap)
                        - UI.ResearchBoard.ColumnGap;

                    var tall = UI.ResearchBoard.Margin * 2f
                        + rows * (UI.ResearchBoard.CardHeight + UI.ResearchBoard.RowGap)
                        - UI.ResearchBoard.RowGap;

                    Debug.Log($"[board] {era}/{track}: {slots.Count} nodes, "
                        + $"{columns} cols x {rows} rows, {wide:0}px wide, {tall:0}px tall");
                }
            }
        }
    }
}
