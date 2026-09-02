using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ScalingLaws.Core;
using ScalingLaws.Data;
using ScalingLaws.Persistence;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Where the company sits: the room, the furnishing shop and what is on the floor.
    ///
    /// Part of <see cref="GameShell"/>, split out on 2026-08-29. `partial` is a file boundary and
    /// nothing else: the compiler builds the same type either way, so no field changed lifetime and
    /// no call site moved. The shell had reached 5,800 lines because every screen it ever grew was
    /// written into it rather than beside it, and that is the only thing being corrected here.
    /// </summary>
    public sealed partial class GameShell
    {
        /// <summary>
        /// The office. Today it is a still of the room with the company written over it; the scene
        /// itself is built and lives in a prefab, and this is where it gets mounted once the camera
        /// and the render target are wired up.
        ///
        /// It is a screen rather than a background because everything that will eventually be
        /// clickable is in it: the people, the racks, the desk the player sits at.
        /// </summary>
        private VisualElement BuildSiteScreen()
        {
            var page = new VisualElement();
            page.AddToClassList("content");
            page.AddToClassList("site-page");

            // The office fills the screen and everything else is laid over it. The readouts are the
            // guests here, not the room: a tycoon that opens on a table of numbers has already told
            // the player what kind of game it thinks it is.
            var stage = new VisualElement();
            stage.AddToClassList("site-stage");

            // Bubbles lift off the desk while the lab is learning. Attached to the stage rather than
            // the page so they travel over the office rather than over the whole window, and rebuilt
            // with the screen because the screen is rebuilt on every tab change anyway.
            var bubbleHost = new VisualElement();
            stage.Add(bubbleHost);
            // Zero while paused. The points figure is yesterday's and does not change, so the
            // bubbles went on rising out of a desk nobody was sitting at, which reads as the game
            // still running.
            bubbles = new ResearchBubbles(bubbleHost,
                () => clock.Speed == SimSpeed.Paused ? 0.0 : simulation.State.ResearchPointsToday);

            // Cheap when nothing changed, and the only place that has to notice a move: the office
            // is not on screen anywhere else, so re-dressing it on every tab change would be work
            // done for a camera nobody is looking through.
            officeStage?.Show(state.Staff.Office, state.Decor);

            var view = Resources.Load<RenderTexture>("OfficeView");
            if (view != null)
            {
                stage.style.backgroundImage = Background.FromRenderTexture(view);
                stage.AddToClassList("site-stage--live");
            }
            else
            {
                var pending = new Label(Loc.T("panel.the_office"));
                pending.AddToClassList("site-stage__title");
                stage.Add(pending);

                var note = new Label(Loc.T("site.scene_stale"));
                note.AddToClassList("site-stage__note");
                stage.Add(note);
            }

            if (companyInfoOpen)
            {
                var overlay = new VisualElement();
                overlay.AddToClassList("site-overlay");

                var title = new Label(state.CompanyName.ToUpperInvariant());
                title.AddToClassList("page-title");
                overlay.Add(title);

                var subtitle = new Label(
                    Loc.T("site.founder_line", state.FounderName,
                    WorldRegionCatalog.Get(state.HomeCountry).DisplayName));
                subtitle.AddToClassList("page-subtitle");
                overlay.Add(subtitle);

                var strip = new VisualElement();
                strip.AddToClassList("site-strip");
                strip.Add(SiteFigure("STAFF", state.Staff.Headcount.ToString()));
                strip.Add(SiteFigure("MODELS LIVE", state.DeployedModels.Count.ToString()));
                strip.Add(SiteFigure("CASH", UiFormat.Money(state.CashUsd)));
                strip.Add(SiteFigure("DAY", UiFormat.Days(state.Date.DayIndex)));
                overlay.Add(strip);

                stage.Add(overlay);

                // Born off to the left, then released a frame later so the transition has a change
                // to animate from. Same trick the page arrival uses.
                overlay.AddToClassList("site-overlay--entering");
                overlay.schedule.Execute(() => overlay.RemoveFromClassList("site-overlay--entering"))
                    .ExecuteLater(16);
            }

            // The two ways out of the room, one above the other in the corner.
            //
            // The office used to be a 260px plate across the bottom left: a slab of text laid over a
            // photograph of a room the player is already looking at. It is the same decision either
            // way, so it is the same size and shape as the map now, and the numbers that used to be
            // printed on it are in the card that opens under the cursor.
            var rail = new VisualElement();
            rail.AddToClassList("site-rail");

            var place = state.Staff.OfficeDefinition;

            var upgrade = new Button(() => Show(Screen.Offices));
            upgrade.AddToClassList("site-icon");
            SetIcon(upgrade, "Ui/office_upgrade", "OFFICE");

            InsightTip.Attach(upgrade, "THE OFFICE",
                $"Upgrade the office, rent or buy. You are in {place.DisplayName.ToLowerInvariant()}: "
                + $"{state.Staff.Headcount} of {place.Desks} desks at "
                + $"{UiFormat.Money(place.MonthlyRentUsd)} a month, and desks are what caps hiring.",
                InsightTip.Placement.LeftOf);

            rail.Add(upgrade);

            // Clicking the map sends the founder out through the garage to the car, and the screen
            // follows once they are in it. Cutting straight to the map is a scene change; walking
            // out of the room is somebody leaving.
            //
            // It falls straight through when there is nobody to walk, because a player whose office
            // scene has not loaded must not be stranded on a journey that will never finish.
            var map = new Button(() =>
            {
                if (founder == null || !founder.BeginLeaving())
                {
                    Show(Screen.Ranking);
                }
            });

            map.AddToClassList("site-icon");
            SetIcon(map, "Ui/map", "MAP");

            InsightTip.Attach(map, "THE WORLD MAP",
                "Travel. Who is building what, and where. Today it opens the board; the drive out to "
                + "it is being built.",
                InsightTip.Placement.LeftOf);

            rail.Add(map);

            // The basement. Below the map on the same rail, because it is a place in the building
            // rather than a tab: you go downstairs to it. The author's own icon.
            var room = new Button(() => Show(Screen.Room));
            room.AddToClassList("site-icon");
            SetIcon(room, "Ui/tab_compute_center", "ROOM");

            InsightTip.Attach(room, Loc.T("room.title"),
                state.HasServerRoom
                    ? Loc.T("room.strap")
                    : Loc.T("room.locked.body"),
                InsightTip.Placement.LeftOf);

            rail.Add(room);

            // The furniture shop. Third in the rail rather than a tab of its own, because it is a
            // thing you do *to the room you are looking at*, and walking away from the room to
            // furnish it is the wrong way round.
            if (FurnishingShopIsOpen)
            {
                var decorate = new Button(() =>
                {
                    decorOpen = !decorOpen;
                    decorProblem = string.Empty;
                    Show(Screen.Site);
                });

                decorate.AddToClassList("site-icon");
                decorate.EnableInClassList("site-icon--on", decorOpen);
                SetIcon(decorate, "Ui/office_decorate", "DECOR");

                InsightTip.Attach(decorate, "FURNISH THE OFFICE",
                    "Buy desks, sofas and everything else. Desks raise the hiring cap; the rest makes "
                    + "the floor a better place to work. Anything can be sold back at "
                    + $"{FurnitureCatalog.ResaleFraction:P0} of what it cost.",
                    InsightTip.Placement.LeftOf);

                rail.Add(decorate);
            }

            stage.Add(rail);

            if (FurnishingShopIsOpen && decorOpen)
            {
                page.Add(BuildDecorator());
            }

            page.Add(stage);
            return page;
        }

        /// <summary>
        /// Whether the player can place furniture piece by piece.
        ///
        /// **Suspended on 2026-08-22 at the author's call**, in favour of the WITH FURNISHINGS tick
        /// on the office chooser: a standard pack that arrives on the day of the move, cheaper than
        /// the same pieces bought one at a time.
        ///
        /// Nothing under it is deleted. `DecorPlan`, `FurnitureCatalog` and `BuildDecorator` are
        /// intact and still tested, the furnished move buys through the same `DecorPlan.Buy`, and
        /// saves keep carrying whatever is on the floor. Turning this back on is one word, which is
        /// the only reason it is a constant rather than a commit that tore the shop out.
        /// </summary>
        private const bool FurnishingShopIsOpen = false;

        /// <summary>
        /// The furniture shop, laid over the room it changes.
        ///
        /// Two columns: what can be bought on the left, what is already owned on the right. The
        /// room stays visible behind it on purpose. The player is deciding what the office should
        /// look like, and hiding the office to do that would be perverse.
        /// </summary>
        private VisualElement BuildDecorator()
        {
            var panel = new VisualElement();
            panel.AddToClassList("decor");

            var title = new Label(Loc.T("panel.furnish"));
            title.AddToClassList("page-title");
            panel.Add(title);

            var decor = state.Decor ?? new DecorPlan();
            var room = RoomCatalog.For(state.Staff.Office);

            var subtitle = new Label(
                Loc.T("site.desks_and_decor", state.Staff.Headcount, state.Staff.Desks,
                decor.Placed.Count(), UiFormat.Money((long)decor.InvestedUsd)));

            subtitle.AddToClassList("page-subtitle");
            panel.Add(subtitle);

            if (!string.IsNullOrEmpty(decorProblem))
            {
                var problem = new Label(decorProblem);
                problem.AddToClassList("decor__problem");
                panel.Add(problem);
            }

            if (!room.AllowsFurniture)
            {
                var closed = new Label(
                    Loc.T("site.no_floor_spare"));

                closed.AddToClassList("decor__empty");
                panel.Add(closed);
            }
            else
            {
                var columns = new VisualElement();
                columns.AddToClassList("decor__columns");

                columns.Add(BuildShop(room));
                columns.Add(BuildOwned(decor, room));

                panel.Add(columns);
            }

            var close = new Button(() =>
            {
                decorOpen = false;
                Show(Screen.Site);
            })
            { text = Loc.T("common.done") };

            close.AddToClassList("decor__close");
            panel.Add(close);

            return panel;
        }

        private VisualElement BuildShop(RoomView room)
        {
            var column = new VisualElement();
            column.AddToClassList("decor__column");

            var heading = new Label(Loc.T("panel.the_shop"));
            heading.AddToClassList("decor__heading");
            column.Add(heading);

            var list = new ScrollView();
            list.AddToClassList("decor__list");

            foreach (var piece in FurnitureCatalog.All)
            {
                list.Add(BuildShopRow(piece, room));
            }

            column.Add(list);
            return column;
        }

        private VisualElement BuildShopRow(FurniturePiece piece, RoomView room)
        {
            var row = new VisualElement();
            row.AddToClassList("decor-row");

            // The swatch is the only thing tying this list to the boxes in the room. Without it a
            // player cannot tell which of five brown rectangles is the shelf they just bought.
            var swatch = new VisualElement();
            swatch.AddToClassList("decor-row__swatch");

            if (ColorUtility.TryParseHtmlString(piece.Tint, out var tint))
            {
                swatch.style.backgroundColor = tint;
            }

            row.Add(swatch);

            var text = new VisualElement();
            text.AddToClassList("decor-row__text");

            var name = new Label(piece.DisplayName);
            name.AddToClassList("decor-row__name");
            text.Add(name);

            var blurb = new Label(piece.Blurb);
            blurb.AddToClassList("decor-row__blurb");
            text.Add(blurb);

            var effect = new Label(EffectLine(piece));
            effect.AddToClassList("decor-row__effect");
            text.Add(effect);

            row.Add(text);

            var owned = (state.Decor ?? new DecorPlan()).CountOf(piece.Kind);
            var affordable = state.CashUsd >= piece.PriceUsd;

            var buy = new Button(() =>
            {
                decorProblem = simulation.TryBuyFurniture(piece.Kind, ZoneOf(room));

                Show(Screen.Site);
            })
            {
                text = owned > 0
                    ? $"BUY   {UiFormat.Money((long)piece.PriceUsd)}   ({owned} owned)"
                    : $"BUY   {UiFormat.Money((long)piece.PriceUsd)}"
            };

            buy.AddToClassList("decor-row__buy");
            buy.SetEnabled(affordable);

            if (!affordable)
            {
                buy.text = $"NEEDS {UiFormat.Money((long)piece.PriceUsd)}";
            }

            row.Add(buy);
            return row;
        }

        /// <summary>What a piece does, in one line, or that it is only there to look at.</summary>
        private static string EffectLine(FurniturePiece piece)
        {
            var parts = new List<string>();

            if (piece.DeskSeats > 0)
            {
                parts.Add(piece.DeskSeats == 1 ? "+1 desk" : $"+{piece.DeskSeats} desks");
            }

            if (piece.MoraleBonus > 0.0)
            {
                parts.Add($"+{piece.MoraleBonus:P1} how well people work");
            }

            if (piece.ResearchBonus > 0.0)
            {
                parts.Add($"+{piece.ResearchBonus:P1} research");
            }

            parts.Add($"sells back for {UiFormat.Money((long)piece.ResaleValueUsd)}");

            return string.Join("  -  ", parts);
        }

        private VisualElement BuildOwned(DecorPlan decor, RoomView room)
        {
            var column = new VisualElement();
            column.AddToClassList("decor__column");

            var heading = new Label(Loc.T("panel.company_owns"));
            heading.AddToClassList("decor__heading");
            column.Add(heading);

            var list = new ScrollView();
            list.AddToClassList("decor__list");

            if (decor.Items.Count == 0)
            {
                var empty = new Label(Loc.T("offices.floor_as_left"));
                empty.AddToClassList("decor__empty");
                list.Add(empty);
            }

            // Placed first, because those are the ones the player can see in the room behind this
            // panel and the ones they are most likely to want to move or sell.
            foreach (var item in decor.Items
                .OrderByDescending(entry => entry.IsPlaced)
                .ThenBy(entry => entry.Definition.DisplayName))
            {
                list.Add(BuildOwnedRow(item, room));
            }

            column.Add(list);
            return column;
        }

        private VisualElement BuildOwnedRow(DecorItem item, RoomView room)
        {
            var piece = item.Definition;

            var row = new VisualElement();
            row.AddToClassList("decor-row");
            row.EnableInClassList("decor-row--stored", !item.IsPlaced);

            var swatch = new VisualElement();
            swatch.AddToClassList("decor-row__swatch");

            if (ColorUtility.TryParseHtmlString(piece.Tint, out var tint))
            {
                swatch.style.backgroundColor = tint;
            }

            row.Add(swatch);

            var text = new VisualElement();
            text.AddToClassList("decor-row__text");

            var name = new Label(piece.DisplayName);
            name.AddToClassList("decor-row__name");
            text.Add(name);

            var where = new Label(item.IsPlaced
                ? $"On the floor at {item.X:0.#} by {item.Z:0.#}."
                : "In storage. It does nothing until it is standing up.");

            where.AddToClassList("decor-row__blurb");
            text.Add(where);

            row.Add(text);

            var buttons = new VisualElement();
            buttons.AddToClassList("decor-row__buttons");

            var move = new Button(() =>
            {
                decorProblem = item.IsPlaced
                    ? simulation.TryStoreFurniture(item)
                    : simulation.TryPlaceFurniture(item, ZoneOf(room));

                Show(Screen.Site);
            })
            { text = item.IsPlaced ? "STORE" : "PLACE" };

            move.AddToClassList("decor-row__move");
            buttons.Add(move);

            // One click. The refund is small enough that an accidental sale is a real loss but not
            // a campaign-ending one, and a second click on every row would make clearing a floor a
            // chore.
            var sell = new Button(() =>
            {
                var got = simulation.SellFurniture(item);
                decorProblem = got > 0.0
                    ? $"Sold the {piece.DisplayName.ToLowerInvariant()} for {UiFormat.Money((long)got)}."
                    : string.Empty;

                Show(Screen.Site);
            })
            { text = $"SELL   {UiFormat.Money((long)piece.ResaleValueUsd)}" };

            sell.AddToClassList("decor-row__sell");
            buttons.Add(sell);

            row.Add(buttons);
            return row;
        }

        /// <summary>The patch of floor this room leaves clear for furniture.</summary>
        private static DecorZone ZoneOf(RoomView room) =>
            new(room.DecorX, room.DecorZ, room.DecorWidth, room.DecorDepth);

        /// <summary>
        /// Where a furnished move would stand its pack, or nothing at all.
        ///
        /// Null for a place with no open floor, so the garage cannot be charged for six pieces it
        /// has nowhere to put. The chooser already hides the cost in that case; this is the guard
        /// that makes it true rather than merely displayed.
        /// </summary>
        private static DecorZone? FurnishZone(OfficeTier tier, bool furnished)
        {
            if (!furnished)
            {
                return null;
            }

            var room = RoomCatalog.For(tier);
            return room.AllowsFurniture ? ZoneOf(room) : null;
        }

        /// <summary>
        /// Puts art on a control, or the word on it if the art is not there.
        ///
        /// A round button with nothing in it is indistinguishable from a rendering fault, and both
        /// of these open a screen the player needs.
        /// </summary>
        private static void SetIcon(Button button, string resourcePath, string fallback)
        {
            var art = Resources.Load<Texture2D>(resourcePath);
            if (art != null)
            {
                button.style.backgroundImage = new StyleBackground(art);
                return;
            }

            button.text = fallback;
        }

        private static VisualElement SiteFigure(string label, string value)
        {
            var figure = new VisualElement();
            figure.AddToClassList("site-figure");

            var caption = new Label(label);
            caption.AddToClassList("site-figure__label");
            figure.Add(caption);

            var amount = new Label(value);
            amount.AddToClassList("site-figure__value");
            figure.Add(amount);

            return figure;
        }

        private static VisualElement Row(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("readout");
            row.Add(new Label(label));

            var valueLabel = new Label(value);
            valueLabel.AddToClassList("readout__value");
            row.Add(valueLabel);

            return row;
        }

        private static Label Hint(string text)
        {
            var label = new Label(text);
            label.AddToClassList("field__hint");
            return label;
        }

    }
}
