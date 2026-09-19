using System.Collections.Generic;
using ScalingLaws.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The testers, one row each: a small picture if they have one, the name, and what they did
    /// underneath in small type.
    ///
    /// One element used in two places, the SETTINGS sheet and the CREDITS sheet, so the two can never
    /// list different people or describe the same person twice in two ways.
    /// </summary>
    public static class TesterList
    {
        private const int IconSize = 34;

        private static readonly Dictionary<string, Texture2D> Icons = new();

        /// <summary>The whole panel: heading, a line under it, then every tester.</summary>
        public static VisualElement Build()
        {
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.style.marginTop = 14;

            var heading = new Label(Loc.T("credits.testers"));
            heading.AddToClassList("signature-studio");
            panel.Add(heading);

            var note = new Label(Loc.T("credits.testers.note"));
            note.AddToClassList("console-caption");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginBottom = 6;
            panel.Add(note);

            foreach (var tester in Credits.Testers)
            {
                panel.Add(Row(tester));
            }

            return panel;
        }

        /// <summary>One tester. The picture sits left of the name; without one the text starts flush.</summary>
        public static VisualElement Row(Tester tester)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;

            var icon = IconFor(tester.Icon);
            if (icon != null)
            {
                var picture = new VisualElement();
                picture.style.width = IconSize;
                picture.style.height = IconSize;
                picture.style.flexShrink = 0;
                picture.style.marginRight = 10;
                picture.style.backgroundImage = new StyleBackground(icon);
                picture.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                row.Add(picture);
            }

            var text = new VisualElement();
            text.style.flexShrink = 1;
            text.style.flexGrow = 1;

            var name = new Label(tester.Name);
            name.AddToClassList("signature-author");
            text.Add(name);

            var role = new Label(Loc.T(tester.RoleKey));
            role.AddToClassList("console-caption");
            role.style.whiteSpace = WhiteSpace.Normal;
            role.style.fontSize = 11;
            role.style.marginTop = 1;
            text.Add(role);

            row.Add(text);
            return row;
        }

        /// <summary>Cached, and a missing file is no picture rather than a hole or an exception.</summary>
        private static Texture2D IconFor(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (!Icons.TryGetValue(path, out var texture))
            {
                texture = Resources.Load<Texture2D>(path);
                Icons[path] = texture;
            }

            return texture;
        }
    }
}
