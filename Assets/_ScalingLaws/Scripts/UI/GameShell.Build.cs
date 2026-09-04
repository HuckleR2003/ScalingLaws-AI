using System;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Furnishing the office: the shop, the store room, and a piece on the cursor.
    ///
    /// **The same shape as the basement, deliberately.** That room already teaches right-click to
    /// pick up, left-click to put down, and a store room that money has already been spent on, and a
    /// second grammar for the same verb in the same game is how a player learns neither. What differs
    /// is only what a square is: the basement reads a marker per square out of its own prefab, and
    /// the office computes its slots from the plan, because a floor here is a rectangle of open
    /// ground described in metres rather than a grid somebody built.
    ///
    /// **The piece on the cursor is not a third state**, which is the rule the basement arrived at
    /// after getting it wrong. It is either a line in the store room the screen has singled out, or a
    /// piece still standing exactly where it was while the player decides. Nothing is in limbo,
    /// nothing new is saved, and quitting mid-carry loses nobody anything.
    /// </summary>
    public sealed partial class GameShell
    {
        /// <summary>How wide a slot marker is drawn, as a share of the stage.</summary>
        public const float SlotMarkerSize = 0.030f;

        /// <summary>
        /// What is on the cursor, or null.
        ///
        /// A piece the plan already owns in both cases. Buying puts it in the store room first and
        /// then picks it up, so there is exactly one way a piece comes to be carried and exactly one
        /// place it can be while it is.
        /// </summary>
        private DecorItem carryingPiece;

        /// <summary>
        /// Where the carried piece was standing, or null when it came out of the store room.
        ///
        /// **Lifting is marked, never performed.** The piece stays on the floor while it is being
        /// carried, so putting it back down is a move rather than a place, and cancelling costs
        /// nothing. The basement learned this the hard way: a lift that actually removed the piece
        /// destroyed the fans bought for it.
        /// </summary>
        private (float X, float Z)? liftedFrom;

        // ---- what a click on the room means ---------------------------------------------------------

        /// <summary>
        /// The floor slot under a point on the stage, or null.
        ///
        /// Two steps, and both belong to somebody else: the stage turns a pointer into a place on
        /// the floor, and the plan says which slot that place is. This only picks the nearest one and
        /// refuses anything further away than half a slot, so a click in the gap between two squares
        /// is a miss rather than a coin toss.
        /// </summary>
        private (float X, float Z)? SlotAt(VisualElement view, Vector2 local)
        {
            if (officeStage == null || !officeStage.IsLive)
            {
                return null;
            }

            if (!StagePicking.TryViewport(view, local, officeStage.Texture, out var viewport))
            {
                return null;
            }

            if (!officeStage.FloorPointAt(viewport, out var x, out var z))
            {
                return null;
            }

            var zone = FurnishZone(state.Staff.Office, true);

            if (!zone.HasValue)
            {
                return null;
            }

            var best = (X: 0f, Z: 0f);
            var bestDistance = float.MaxValue;

            foreach (var slot in state.Decor.AllSlots(zone.Value))
            {
                var distance = (slot.x - x) * (slot.x - x) + (slot.z - z) * (slot.z - z);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (slot.x, slot.z);
                }
            }

            var reach = DecorPlan.SlotSpacing * 0.5f;

            return bestDistance <= reach * reach ? best : null;
        }

        /// <summary>
        /// A click on the room while the build rail is open.
        ///
        /// Left puts down or picks nothing; right picks up or puts back. Returns true when it did
        /// something, so the caller knows whether to leave the click for whoever else wanted it -
        /// which is how clicking a person still opens that person while the rail is open.
        /// </summary>
        private bool OnBuildClick(VisualElement view, MouseDownEvent down)
        {
            if (!decorOpen)
            {
                return false;
            }

            var slot = SlotAt(view, down.localMousePosition);

            if (!slot.HasValue)
            {
                return false;
            }

            var standing = state.Decor.At(slot.Value.X, slot.Value.Z);

            // Right button: pick up what is there, or put back what is on the cursor.
            if (down.button == 1)
            {
                if (carryingPiece != null)
                {
                    StoreCarried();
                    return true;
                }

                if (standing == null)
                {
                    return false;
                }

                carryingPiece = standing;
                liftedFrom = slot;

                Show(Screen.Site);
                return true;
            }

            if (down.button != 0 || carryingPiece == null)
            {
                return false;
            }

            PutDown(slot.Value.X, slot.Value.Z);
            return true;
        }

        /// <summary>
        /// Stands the carried piece on a slot, by whichever route it arrived.
        ///
        /// A piece that was lifted is still on its old square, so it is moved off it first; one that
        /// came out of the store room is simply placed. Either way a refusal leaves everything
        /// exactly as it was, which is what makes a full floor a message rather than a lost sofa.
        /// </summary>
        private void PutDown(float x, float z)
        {
            var piece = carryingPiece;

            if (piece == null)
            {
                return;
            }

            if (liftedFrom.HasValue)
            {
                if (liftedFrom.Value.X == x && liftedFrom.Value.Z == z)
                {
                    // Put back where it came from. Nothing to do but stop carrying it.
                    carryingPiece = null;
                    liftedFrom = null;

                    Show(Screen.Site);
                    return;
                }

                state.Decor.Store(piece);
            }

            if (!state.Decor.PlaceOn(piece, x, z))
            {
                // Occupied. Put a lifted piece back where it was rather than leaving it in the store
                // room, because the player asked to move it and the move did not happen.
                if (liftedFrom.HasValue)
                {
                    state.Decor.PlaceOn(piece, liftedFrom.Value.X, liftedFrom.Value.Z);
                }

                return;
            }

            carryingPiece = null;
            liftedFrom = null;

            RefreshChrome();
            Show(Screen.Site);
        }

        /// <summary>Puts the carried piece in the store room. The money stays spent.</summary>
        private void StoreCarried()
        {
            if (carryingPiece == null)
            {
                return;
            }

            state.Decor.Store(carryingPiece);

            carryingPiece = null;
            liftedFrom = null;

            RefreshChrome();
            Show(Screen.Site);
        }

        // ---- the rail beside the room -----------------------------------------------------------------

        /// <summary>
        /// The shop, the store room and what is on the cursor.
        ///
        /// Beside the office rather than under it, so the room stays visible while a piece is being
        /// placed. A shop that covers the floor is a shop the player buys from blind.
        /// </summary>
        private VisualElement BuildFurnishRail()
        {
            var rail = new VisualElement();
            rail.AddToClassList("obuild");

            var head = new VisualElement();
            head.AddToClassList("obuild__head");

            var title = new Label(Loc.T("build.title"));
            title.AddToClassList("obuild__title");
            head.Add(title);

            var close = new Button(() =>
            {
                // Anything on the cursor goes back where it came from rather than vanishing with
                // the rail. A player closing a panel has not asked to lose a sofa.
                if (carryingPiece != null)
                {
                    StoreCarried();
                }

                decorOpen = false;
                Show(Screen.Site);
            })
            { text = Loc.T("build.done") };

            close.AddToClassList("obuild__close");
            head.Add(close);

            rail.Add(head);

            if (carryingPiece != null)
            {
                rail.Add(CarryNote());
            }

            var scroller = new ScrollView();
            scroller.AddToClassList("obuild__scroll");

            var shopHeading = new Label(Loc.T("build.shop"));
            shopHeading.AddToClassList("obuild__section");
            scroller.Add(shopHeading);

            foreach (var piece in FurnitureCatalog.All)
            {
                scroller.Add(ShopRow(piece));
            }

            var storeHeading = new Label(Loc.T("build.store"));
            storeHeading.AddToClassList("obuild__section");
            scroller.Add(storeHeading);

            var stored = 0;

            foreach (var item in state.Decor.Stored)
            {
                if (item == carryingPiece)
                {
                    continue;
                }

                scroller.Add(StoredRow(item));
                stored++;
            }

            if (stored == 0)
            {
                var empty = new Label(Loc.T("build.store_empty"));
                empty.AddToClassList("obuild__hint");
                scroller.Add(empty);
            }

            rail.Add(scroller);
            return rail;
        }

        /// <summary>What is on the cursor, and the two ways to put it down.</summary>
        private VisualElement CarryNote()
        {
            var note = new VisualElement();
            note.AddToClassList("obuild__carry");

            var what = new Label(carryingPiece.Definition.DisplayName.ToUpperInvariant());
            what.AddToClassList("obuild__carryname");
            note.Add(what);

            var how = new Label(Loc.T("build.carrying"));
            how.AddToClassList("obuild__hint");
            note.Add(how);

            // **The storage button lights up while something is carried**, which is the second way
            // out the author asked for: put it on a square, or put it back in the store room.
            var stash = new Button(StoreCarried) { text = Loc.T("build.stash") };
            stash.AddToClassList("obuild__stash");
            note.Add(stash);

            return note;
        }

        private VisualElement ShopRow(FurniturePiece piece)
        {
            var affordable = state.CashUsd >= (long)piece.PriceUsd;

            var card = new Button(() =>
            {
                var zone = FurnishZone(state.Staff.Office, true);

                if (!zone.HasValue)
                {
                    decorProblem = Loc.T("build.no_floor");
                    Show(Screen.Site);
                    return;
                }

                var refused = simulation.TryBuyFurniture(piece.Kind, zone.Value);

                if (refused.Length > 0)
                {
                    decorProblem = refused;
                }

                // **Bought, stood up, and then picked up.** The purchase already puts it in the
                // first free slot, so a player who does nothing gets what the old shop gave them;
                // picking it up straight afterwards is what turns that into a placement. If the
                // floor was full it is in the store room and there is nothing on the cursor.
                var bought = state.Decor.Newest;

                if (bought != null && bought.IsPlaced)
                {
                    carryingPiece = bought;
                    liftedFrom = (bought.X, bought.Z);
                }

                RefreshChrome();
                Show(Screen.Site);
            });

            card.AddToClassList("obuild__card");
            card.SetEnabled(affordable && carryingPiece == null);

            var name = new Label(piece.DisplayName.ToUpperInvariant());
            name.AddToClassList("obuild__name");
            card.Add(name);

            var price = new Label(UiFormat.Money((long)piece.PriceUsd));
            price.AddToClassList("obuild__price");
            card.Add(price);

            var blurb = new Label(piece.Blurb);
            blurb.AddToClassList("obuild__blurb");
            card.Add(blurb);

            if (piece.DeskSeats > 0)
            {
                var seats = new Label(Loc.Counted(piece.DeskSeats, "noun.desk"));
                seats.AddToClassList("obuild__seats");
                card.Add(seats);
            }

            return card;
        }

        private VisualElement StoredRow(DecorItem item)
        {
            var row = new VisualElement();
            row.AddToClassList("obuild__stored");

            var take = new Button(() =>
            {
                carryingPiece = item;
                liftedFrom = null;

                Show(Screen.Site);
            })
            { text = item.Definition.DisplayName.ToUpperInvariant() };

            take.AddToClassList("obuild__take");
            take.SetEnabled(carryingPiece == null);
            row.Add(take);

            var sell = new Button(() =>
            {
                simulation.SellFurniture(item);

                RefreshChrome();
                Show(Screen.Site);
            })
            { text = Loc.T("build.sell", UiFormat.Money((long)item.Definition.ResaleValueUsd)) };

            sell.AddToClassList("obuild__sell");
            row.Add(sell);

            return row;
        }

        // ---- the markers over the room ---------------------------------------------------------------

        /// <summary>
        /// A small square on every slot a carried piece could go on.
        ///
        /// **Only while something is on the cursor.** A floor permanently covered in markers is a
        /// grid the player has to look past to see the room, and the room is the thing they came for.
        /// Drawn as elements over the render texture rather than into the scene, because they belong
        /// to the interface and the office camera is shared with the tab render.
        /// </summary>
        private void AddSlotMarkers(VisualElement stage)
        {
            if (!decorOpen || carryingPiece == null || officeStage == null || !officeStage.IsLive)
            {
                return;
            }

            var zone = FurnishZone(state.Staff.Office, true);

            if (!zone.HasValue)
            {
                return;
            }

            foreach (var slot in state.Decor.AllSlots(zone.Value))
            {
                var taken = state.Decor.At(slot.x, slot.z);

                // The square it came off counts as free: putting it back is allowed and is the way
                // out of a carry the player changed their mind about.
                var mine = liftedFrom.HasValue
                    && liftedFrom.Value.X == slot.x && liftedFrom.Value.Z == slot.z;

                if (taken != null && !mine)
                {
                    continue;
                }

                if (!officeStage.ViewportOfSlot(slot.x, slot.z, out var point))
                {
                    continue;
                }

                var marker = new VisualElement();
                marker.AddToClassList("obuild__slot");
                marker.pickingMode = PickingMode.Ignore;

                marker.style.left = Length.Percent((point.x - SlotMarkerSize * 0.5f) * 100f);

                // The camera measures up from the bottom and UI Toolkit measures down from the top.
                marker.style.top = Length.Percent((1f - point.y - SlotMarkerSize * 0.5f) * 100f);

                marker.style.width = Length.Percent(SlotMarkerSize * 100f);
                marker.style.height = Length.Percent(SlotMarkerSize * 100f);

                stage.Add(marker);
            }
        }
    }
}
