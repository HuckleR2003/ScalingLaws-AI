using System;
using ScalingLaws.Core;
using ScalingLaws.Data;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The card that asks for a name and then opens the form.
    ///
    /// **The name is taken here rather than on the page.** A player who has already typed it does
    /// not type it again, and a form that opens with a field already filled reads as a form that
    /// was expecting them. It travels as a query parameter, which is the only thing a static page
    /// can read without a server.
    ///
    /// It is a card rather than a screen because it interrupts something. A player who found a bug
    /// found it on a screen they want to go back to.
    /// </summary>
    public sealed class FeedbackDialog
    {
        private readonly Action changed;
        private readonly Func<GameDate> today;

        private string who = string.Empty;

        public FeedbackDialog(Func<GameDate> today, Action changed)
        {
            this.today = today;
            this.changed = changed;
        }

        public bool IsOpen { get; private set; }

        /// <summary>Closes the card. Set by the shell.</summary>
        public Action Closed { get; set; }

        public void Open()
        {
            IsOpen = true;
            changed?.Invoke();
        }

        public void Close()
        {
            IsOpen = false;
            changed?.Invoke();
        }

        public VisualElement Build(string version)
        {
            var sheet = new VisualElement();
            sheet.AddToClassList("report");

            var card = new VisualElement();
            card.AddToClassList("report__card");

            var head = new VisualElement();
            head.AddToClassList("report__head");

            var title = new Label(Loc.T("feedback.title"));
            title.AddToClassList("report__title");
            head.Add(title);

            var close = new Button(() =>
            {
                Close();
                Closed?.Invoke();
            })
            { text = Loc.T("common.close") };

            close.AddToClassList("chip");
            head.Add(close);

            card.Add(head);

            var body = new Label(Loc.T("feedback.body"));
            body.AddToClassList("report__body");
            card.Add(body);

            // The one promise on this card, and it is deliberately smaller than the ask above it.
            // A reward printed larger than the request reads as the point of the exercise.
            var promise = new Label(Loc.T("feedback.promise"));
            promise.AddToClassList("report__promise");
            card.Add(promise);

            var row = new VisualElement();
            row.AddToClassList("report__row");

            var label = new Label(Loc.T("feedback.name"));
            label.AddToClassList("report__label");
            row.Add(label);

            var field = new TextField { value = who };
            field.AddToClassList("report__field");
            field.RegisterValueChangedCallback(change => who = change.newValue);
            row.Add(field);

            var go = new Button(() =>
            {
                FeedbackLink.Open(today?.Invoke() ?? default, version, who);

                Close();
                Closed?.Invoke();
            })
            { text = Loc.T("feedback.go") };

            go.AddToClassList("button");
            go.AddToClassList("button--primary");
            row.Add(go);

            card.Add(row);

            // The address in full, small and grey. Somebody whose browser will not open from a game
            // can still read it and type it, and somebody suspicious of a game opening a browser can
            // see exactly where it goes before pressing anything.
            var link = new Label(FeedbackLink.BaseUrl);
            link.AddToClassList("report__link");
            card.Add(link);

            sheet.Add(card);
            return sheet;
        }
    }
}
