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

            // After the room, because the people are parented into it. Cheap when the roster has
            // not changed, which is almost every repaint.
            staff?.Refresh();

            // **The room and the rail are one row**, so the floor stays visible while a piece is
            // being placed. A shop that covers the office is a shop somebody buys from blind.
            var floor = new VisualElement();
            floor.AddToClassList("site-floor");

            var view = Resources.Load<RenderTexture>("OfficeView");
            if (view != null)
            {
                stage.style.backgroundImage = Background.FromRenderTexture(view);
                stage.AddToClassList("site-stage--live");

                // **Clicking somebody in the room opens who they are.** `PersonPanel` has existed
                // since the person page was built and the only way to reach it was a row on the
                // team screen, so the office was a picture of people the player could not talk to.
                stage.RegisterCallback<MouseDownEvent>(down =>
                {
                    // The build rail gets first refusal, because right-click means nothing else on
                    // this screen and a left click on a floor slot while carrying is a placement
                    // rather than an attempt to talk to somebody standing on it.
                    if (OnBuildClick(stage, down))
                    {
                        down.StopPropagation();
                        return;
                    }

                    OnOfficeClick(stage, down);
                });

                AddSlotMarkers(stage);
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
                    Loc.T("site.founder_line", UiFormat.PersonName(state.FounderName),
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

            // **A flash on the way back, once, just after the walkthrough.** The basement is
            // reached from this icon and from nowhere else, and a player who has just been walked
            // around it has no reason to know that. Two and a half seconds and it is gone: this
            // screen already carries two permanent labels and does not need a third.
            if (showRoomWayBackUntil > 0f && Time.realtimeSinceStartup < showRoomWayBackUntil)
            {
                room.AddToClassList("site-icon--lit");

                var wayBack = new Label(Loc.T("room.way_back"));
                wayBack.AddToClassList("site-icon__hint");
                wayBack.pickingMode = PickingMode.Ignore;
                room.Add(wayBack);
            }

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

            // **The room and the build rail are one row.** The old shop was a panel under the
            // office, so furnishing meant scrolling away from the thing being furnished.
            floor.Add(stage);

            if (FurnishingShopIsOpen && decorOpen)
            {
                floor.Add(BuildFurnishRail());
            }

            page.Add(floor);
            return page;
        }

        /// <summary>
        /// The furniture shop is open again.
        ///
        /// It was switched off in favour of the furnished move, which buys six pieces in one click
        /// and cannot put any of them anywhere in particular. Both exist now: the pack is the fast
        /// way in, and the rail is the one where the room is actually arranged.
        /// </summary>
        private const bool FurnishingShopIsOpen = true;

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

        /// <summary>
        /// Somebody in the room was clicked.
        ///
        /// **Employees and the founder are asked for differently and that is deliberate.** An
        /// employee carries an `OfficePerson` with their index on it, which is what `PersonPanel`
        /// takes. The founder is not on the roster at all, so a founder mode in that panel would be
        /// a parallel data path through every one of its tabs; they get the company card instead,
        /// which is already the page about who the player is.
        /// </summary>
        private void OnOfficeClick(VisualElement stage, MouseDownEvent down)
        {
            if (down.button != 0 || officeStage == null || !officeStage.IsLive)
            {
                return;
            }

            if (!StagePicking.TryViewport(stage, down.localMousePosition, officeStage.Texture,
                    out var viewport))
            {
                return;
            }

            var person = StagePicking.Under<OfficePerson>(officeStage.View, viewport);

            // **The ray is the first attempt, not the only one.** See `StaffPresence.NearestTo`
            // for the four ways a raycast into this room comes back empty while somebody is plainly
            // standing there. Projection answers the same question and cannot fail for any of them.
            var index = person != null ? person.Index : staff?.NearestTo(officeStage.View, viewport) ?? -1;

            if (index >= 0 && index < state.Staff.Hires.Count)
            {
                personPanel ??= new PersonPanel(() => simulation, () => Show(current));
                personPanel.Show(index);
                Show(current);

                down.StopPropagation();
                return;
            }

            // Nobody hired under the cursor. The founder is the other thing in the room worth
            // clicking, and they are the one person in it with no index.
            var plate = StagePicking.Under<NamePlate>(officeStage.View, viewport);

            if (plate != null && founder?.Model != null && plate.transform.IsChildOf(founder.Model))
            {
                companyInfoOpen = !companyInfoOpen;
                Show(current);

                down.StopPropagation();
            }
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
