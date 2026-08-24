using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The model as a piece of silicon, with the company's mark on the die.
    ///
    /// **One element, three places.** It was drawn inline inside the upgrade panel, which meant the
    /// model hub and the creator had nothing to show and there was no way to give all three the same
    /// picture without copying forty lines twice. A chip drawn one way on one screen and another way
    /// on the next reads as two different things.
    ///
    /// Sizes come from the class the caller adds, not from a parameter, so the same element is a
    /// 44px row icon and a 210px stage piece without knowing which it is.
    /// </summary>
    public static class ChipPreview
    {
        /// <summary>
        /// A chip carrying a company mark and, optionally, a name stamped under it.
        /// </summary>
        /// <param name="company">Whose mark goes on the die.</param>
        /// <param name="stamp">The model name, or null for the small sizes where it will not fit.</param>
        public static VisualElement Build(string company, string stamp = null)
        {
            var stage = new VisualElement();
            stage.AddToClassList("die");

            var die = new VisualElement();
            die.AddToClassList("die__body");

            // Contacts down both sides, which is the whole reason a rounded rectangle reads as a
            // processor rather than as a card.
            for (var side = 0; side < 2; side++)
            {
                var rail = new VisualElement();
                rail.AddToClassList("die__rail");
                rail.AddToClassList(side == 0 ? "die__rail--left" : "die__rail--right");

                for (var pin = 0; pin < 9; pin++)
                {
                    var contact = new VisualElement();
                    contact.AddToClassList("die__pin");
                    rail.Add(contact);
                }

                die.Add(rail);
            }

            var face = new VisualElement();
            face.AddToClassList("die__face");

            var mark = new BrandMark { Company = company };
            mark.AddToClassList("mark--die");
            face.Add(mark);

            if (!string.IsNullOrWhiteSpace(stamp))
            {
                var label = new Label(stamp.Length > 16 ? stamp[..16] : stamp);
                label.AddToClassList("die__stamp");
                face.Add(label);
            }

            die.Add(face);
            stage.Add(die);

            return stage;
        }
    }
}
