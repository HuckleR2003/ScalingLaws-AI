using System;
using UnityEngine.UIElements;
using ScalingLaws.Data;

namespace ScalingLaws.UI
{
    /// <summary>
    /// What the company's product looks like on the web, as a live mock rather than a picture.
    ///
    /// **A screenshot would have been an hour's work and the wrong thing.** The name and the mark
    /// change as the player types, the founder's own name sits in the corner, and after a rename the
    /// picture would be a lie. Everything here is elements, so it is always the current company.
    ///
    /// Layout, and every part of it is a thing the player will recognise from software they use:
    /// a rail down the left, the mark and the product name in the middle of the page, the founder in
    /// the bottom corner of the rail, and the mark again, larger, at the top right where a signed-in
    /// product puts its badge. The arrows beside the centre mark are drawn and disabled, because
    /// choosing a mark is a decision the game does not offer yet and hiding that is how a player
    /// never finds out it is coming.
    ///
    /// Reused on the management screen, where it answers the same question the page is about: what a
    /// stranger sees when they look the company up.
    /// </summary>
    public sealed class BrowserPreview
    {
        private readonly BrandMark centreMark = new();
        private readonly BrandMark cornerMark = new();
        private readonly Label productName = new();
        private readonly Label addressText = new();
        private readonly Label founderText = new();
        private readonly Label companyText = new();

        public BrowserPreview()
        {
            Root = new VisualElement();
            Root.AddToClassList("wb");

            Root.Add(BuildChrome());
            Root.Add(BuildBody());
        }

        public VisualElement Root { get; }

        /// <summary>
        /// Points the mock at a company and a product.
        ///
        /// Everything is passed in rather than read from a simulation, because this is shown in the
        /// creator against a model that does not exist yet as well as on the management screen
        /// against one that does.
        /// </summary>
        public void Show(string company, string productLabel, string founder)
        {
            var house = string.IsNullOrWhiteSpace(company) ? "Newco" : company.Trim();
            var product = string.IsNullOrWhiteSpace(productLabel)
                ? Loc.T("wb.untitled")
                : productLabel.Trim();

            centreMark.Company = house;
            cornerMark.Company = house;

            productName.text = product;
            companyText.text = house.ToUpperInvariant();
            founderText.text = string.IsNullOrWhiteSpace(founder)
                ? Loc.T("wb.you")
                : founder.Trim();

            // The address is the company, lowercased, with everything a domain cannot carry taken
            // out. A player typing "Æther Labs 2" should see a plausible address rather than the
            // string they typed with a dot on the end.
            addressText.text = Domain(house) + "/" + Slug(product);
        }

        private static string Domain(string company)
        {
            var host = new System.Text.StringBuilder();

            foreach (var character in company.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    host.Append(character);
                }
            }

            return (host.Length == 0 ? "newco" : host.ToString()) + ".ai";
        }

        private static string Slug(string product)
        {
            var slug = new System.Text.StringBuilder();

            foreach (var character in product.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    slug.Append(character);
                }
                else if (slug.Length > 0 && slug[^1] != '-')
                {
                    slug.Append('-');
                }
            }

            return slug.ToString().Trim('-');
        }

        /// <summary>The window furniture. Three dots, a back arrow and an address.</summary>
        private VisualElement BuildChrome()
        {
            var chrome = new VisualElement();
            chrome.AddToClassList("wb__chrome");

            var lights = new VisualElement();
            lights.AddToClassList("wb__lights");

            for (var index = 0; index < 3; index++)
            {
                var light = new VisualElement();
                light.AddToClassList("wb__light");
                lights.Add(light);
            }

            chrome.Add(lights);

            var bar = new VisualElement();
            bar.AddToClassList("wb__address");

            var padlock = new VisualElement();
            padlock.AddToClassList("wb__lock");
            bar.Add(padlock);

            addressText.AddToClassList("wb__url");
            bar.Add(addressText);

            chrome.Add(bar);
            return chrome;
        }

        private VisualElement BuildBody()
        {
            var body = new VisualElement();
            body.AddToClassList("wb__body");

            body.Add(BuildRail());
            body.Add(BuildPage());

            return body;
        }

        /// <summary>The rail down the left, and the founder sitting at the bottom of it.</summary>
        private VisualElement BuildRail()
        {
            var rail = new VisualElement();
            rail.AddToClassList("wb__rail");

            var top = new VisualElement();
            top.AddToClassList("wb__railtop");

            companyText.AddToClassList("wb__house");
            top.Add(companyText);

            // Four bars standing in for navigation. Deliberately unlabelled: a mock with real menu
            // items in it invites the player to click them, and none of them go anywhere.
            for (var index = 0; index < 4; index++)
            {
                var item = new VisualElement();
                item.AddToClassList("wb__navitem");

                var dot = new VisualElement();
                dot.AddToClassList("wb__navdot");
                item.Add(dot);

                var line = new VisualElement();
                line.AddToClassList("wb__navline");
                line.style.width = Length.Percent(78 - index * 13);
                item.Add(line);

                top.Add(item);
            }

            rail.Add(top);

            // **The founder, by name, in the corner where every product this is imitating puts the
            // signed-in account.** It is the one part of this that is about the player rather than
            // about the company.
            var account = new VisualElement();
            account.AddToClassList("wb__account");

            var avatar = new VisualElement();
            avatar.AddToClassList("wb__avatar");
            account.Add(avatar);

            var who = new VisualElement();
            who.AddToClassList("wb__who");

            founderText.AddToClassList("wb__founder");
            who.Add(founderText);

            var role = new Label(Loc.T("wb.owner"));
            role.AddToClassList("wb__role");
            who.Add(role);

            account.Add(who);
            rail.Add(account);

            return rail;
        }

        private VisualElement BuildPage()
        {
            var page = new VisualElement();
            page.AddToClassList("wb__page");

            // Top right, larger, on its own. Same mark, different job: the one in the middle is the
            // product's face and this one is the company's badge.
            var badge = new VisualElement();
            badge.AddToClassList("wb__badge");
            cornerMark.AddToClassList("mark--badge");
            badge.Add(cornerMark);
            page.Add(badge);

            var centre = new VisualElement();
            centre.AddToClassList("wb__centre");

            var markRow = new VisualElement();
            markRow.AddToClassList("wb__markrow");

            markRow.Add(Arrow("<", Loc.T("wb.locked")));
            centreMark.AddToClassList("mark--hero");
            markRow.Add(centreMark);
            markRow.Add(Arrow(">", Loc.T("wb.locked")));

            centre.Add(markRow);

            productName.AddToClassList("wb__product");
            centre.Add(productName);

            var prompt = new VisualElement();
            prompt.AddToClassList("wb__prompt");

            var ask = new Label(Loc.T("wb.ask"));
            ask.AddToClassList("wb__ask");
            prompt.Add(ask);

            var send = new VisualElement();
            send.AddToClassList("wb__send");
            prompt.Add(send);

            centre.Add(prompt);
            page.Add(centre);

            return page;
        }

        /// <summary>
        /// One of the two arrows beside the mark. Disabled on purpose, and it says why on hover.
        /// </summary>
        private static Button Arrow(string glyph, string why)
        {
            var button = new Button { text = glyph };
            button.AddToClassList("wb__arrow");
            button.SetEnabled(false);
            button.tooltip = why;
            return button;
        }
    }
}
