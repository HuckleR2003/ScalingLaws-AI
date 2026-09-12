using System;
using ScalingLaws.Data;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A question with a consequence, and the two answers to it.
    ///
    /// **Deliberately not the notice card, and this is the distinction worth keeping.** The shell
    /// already draws three sheets that stop the game and say something has happened: a run finished,
    /// a grant closed, a regulator reported. Those are one button and a dismiss, because the thing
    /// they describe is already true. This is the other shape: nothing has happened yet and the
    /// player is about to make it happen.
    ///
    /// Merging the two would mean a dismiss that sometimes commits, which is the one behaviour a
    /// modal must never have.
    ///
    /// The veil dismisses as a refusal, for the same reason `TaxDemandDialog` is the exception the
    /// other way: closing a question by clicking beside it means no, and only pressing the button
    /// means yes.
    /// </summary>
    public sealed class ConfirmCard
    {
        private VisualElement mounted;

        public bool IsOpen => mounted != null && mounted.parent != null;

        public void Close()
        {
            mounted?.RemoveFromHierarchy();
            mounted = null;
        }

        /// <summary>
        /// Puts the question up. `onYes` runs only when the player presses the confirming button.
        /// </summary>
        public void Ask(VisualElement host, string title, string body, string confirmLabel,
            Action onYes)
        {
            if (host == null || onYes == null)
            {
                return;
            }

            Close();

            var veil = new VisualElement();
            veil.AddToClassList("notice-veil");
            veil.RegisterCallback<ClickEvent>(_ => Close());

            var card = new VisualElement();
            card.AddToClassList("notice");
            card.AddToClassList("notice--ask");
            card.RegisterCallback<ClickEvent>(click => click.StopPropagation());

            var heading = new Label(title);
            heading.AddToClassList("notice__title");
            card.Add(heading);

            var sentence = new Label(body);
            sentence.AddToClassList("notice__body");
            card.Add(sentence);

            var buttons = new VisualElement();
            buttons.AddToClassList("notice__buttons");

            var back = new Button(Close) { text = Loc.T("common.back") };
            back.AddToClassList("notice__button");
            buttons.Add(back);

            var go = new Button(() =>
            {
                // Closed first, so a handler that rebuilds the page underneath cannot find the card
                // still sitting on a tree it has just replaced.
                Close();
                onYes();
            })
            {
                text = confirmLabel
            };

            go.AddToClassList("notice__button");
            go.AddToClassList("notice__button--go");
            buttons.Add(go);

            card.Add(buttons);
            veil.Add(card);

            mounted = veil;
            host.Add(veil);

            AudioDirector.Page();
        }
    }
}
