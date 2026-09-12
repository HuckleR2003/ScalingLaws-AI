using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// The two rented floors: LVL 1 small office hub and LVL 2 big company hub.
    ///
    /// **One builder for both, parameterised, rather than two files that drift.** They are the same
    /// room at two sizes with a different number of walls in it, and the day somebody changes the
    /// wall thickness in one of them is the day the game has two different offices that were meant
    /// to be one design.
    ///
    /// The plan follows the author's reference: a glass-walled meeting room in one corner, a break
    /// area, and the rest open plan with desks in rows. The desks the builder places are the *fixed*
    /// ones that come with the lease. Anything the player buys goes into `Furniture`, which is left
    /// empty here on purpose and is the decorator's to fill.
    ///
    /// Same camera rule as the house: orthographic, fixed, and the geometry is laid out for it. Only
    /// the two far walls are kept so the camera looks into the room rather than at the back of a box.
    /// </summary>
    public static class HubRoomBuilder
    {
        /// <summary>
        /// Under Resources on purpose. The floors are swapped at runtime when the lease changes,
        /// and Game.unity cannot hold an instance of each: it is a generated scene with a fixed
        /// contents, and three rooms baked into it would all be loaded whichever one is rented.
        /// </summary>
        private const string PrefabFolder = "Assets/_ScalingLaws/Resources/Rooms";
        private const string MaterialFolder = "Assets/_ScalingLaws/Materials";
        private const string ScenesFolder = "Assets/_ScalingLaws/Scenes";

        private const float WallThickness = 0.2f;
        private const float WallHeight = 3.2f;
        private const float SlabThickness = 0.25f;

        /// <summary>Glass stops short of the ceiling, which is what makes it read as a partition.</summary>
        private const float GlassHeight = 2.6f;

        /// <summary>
        /// The band of the floor the fixed desks occupy, as fractions of the room's depth.
        ///
        /// **Deliberately not the whole room.** Whatever is left in front of the back row is the
        /// open ground the furniture shop places into, and it is the part of the floor nearest the
        /// camera. Spread the desks over the whole depth and the shop has nowhere to put a sofa
        /// except on top of a workstation. These two numbers and the decor zone in RoomCatalog are
        /// a pair; a test asserts the shop can still place four things in every room.
        /// </summary>
        private const float DeskBandFront = 0.24f;

        /// <inheritdoc cref="DeskBandFront"/>
        private const float DeskBandBack = 0.56f;

        [MenuItem("Scaling Laws/Build small office hub")]
        public static void BuildSmallHub() => Build(Plan.SmallHub());

        [MenuItem("Scaling Laws/Build big company hub")]
        public static void BuildBigHub() => Build(Plan.BigHub());

        /// <summary>
        /// Prints what every piece in the kit actually measures.
        ///
        /// **Because a pack models its furniture in whatever unit its author liked**, and this
        /// project has twice believed a prefab about its own size and been wrong. The numbers this
        /// prints are what the placements above are written against.
        /// </summary>
        [MenuItem("Scaling Laws/Measure the furniture kit")]
        public static void MeasureKit()
        {
            var sets = new (string Name, string[] Paths)[]
            {
                ("Desk", Kit.Desk),
                ("DeskChair", Kit.DeskChair),
                ("ChairColour", new[] { Kit.ChairColours[0] }),
                ("Monitor", Kit.Monitor),
                ("Kitchen", Kit.Kitchen),
                ("Microwave", Kit.Microwave),
                ("CoffeePot", Kit.CoffeePot),
                ("WaterCooler", Kit.WaterCooler),
                ("Printer", Kit.Printer),
                ("Bin", Kit.Bin),
                ("Shelves", Kit.Shelves),
                ("MeetingTable", Kit.MeetingTable),
                ("MeetingChair", Kit.MeetingChair),
                ("BreakTable", Kit.BreakTable),
                ("Stool", Kit.Stool),
                ("PlantTall", Kit.PlantTall),
                ("PlantWide", Kit.PlantWide),
                ("Laptop", new[] { "Assets/LowPolyOfficeProps_LITE/Prefabs/Laptop_On.prefab" }),
                ("PotLarge", new[] { "Assets/LowPolyOfficeProps_LITE/Prefabs/PlantPotLargeA.prefab" }),
                ("PlantA", new[] { "Assets/LowPolyOfficeProps_LITE/Prefabs/PlantTypeA.prefab" }),
                ("PlantBox", new[] { "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)PlantBox.prefab" })
            };

            foreach (var set in sets)
            {
                var prefab = Load(set.Paths);

                if (prefab == null)
                {
                    Debug.Log($"[kit] {set.Name}: nothing on this machine");
                    continue;
                }

                var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                piece.transform.position = Vector3.zero;
                piece.transform.rotation = Quaternion.identity;
                piece.transform.localScale = Vector3.one;

                var bounds = Measure(piece);

                Debug.Log($"[kit] {set.Name}: {prefab.name} "
                    + $"size {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00}, "
                    + $"centre {bounds.center.x:0.00} {bounds.center.y:0.00} {bounds.center.z:0.00}");

                Object.DestroyImmediate(piece);
            }
        }

        [MenuItem("Scaling Laws/Build both hubs")]
        public static void BuildBoth()
        {
            Build(Plan.SmallHub());
            Build(Plan.BigHub());
        }

        /// <summary>
        /// Everything that differs between the two floors.
        ///
        /// A record rather than two builders, because the difference is genuinely a handful of
        /// numbers and a second meeting room.
        /// </summary>
        private readonly struct Plan
        {
            private Plan(string name, float width, float depth, int desks, int deskRows,
                bool secondMeetingRoom, float cameraSize)
            {
                Name = name;
                Width = width;
                Depth = depth;
                Desks = desks;
                DeskRows = deskRows;
                SecondMeetingRoom = secondMeetingRoom;
                CameraSize = cameraSize;
            }

            public string Name { get; }
            public float Width { get; }
            public float Depth { get; }
            public int Desks { get; }
            public int DeskRows { get; }
            public bool SecondMeetingRoom { get; }
            public float CameraSize { get; }

            /// <summary>Ten desks, which is what the lease says the floor holds.</summary>
            public static Plan SmallHub() => new("SmallHub", 16f, 11f, 10, 2, false, 7.0f);

            /// <summary>Twenty desks, a second meeting room and a storage bay.</summary>
            public static Plan BigHub() => new("BigHub", 22f, 14f, 20, 4, true, 9.0f);

            public string PrefabPath => $"{PrefabFolder}/{Name}.prefab";
            public string ScenePath => $"{ScenesFolder}/{Name}.unity";
        }

        private static void Build(Plan plan)
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(ScenesFolder);

            var palette = new HubPalette();
            var root = new GameObject(plan.Name);

            BuildShell(root.transform, plan, palette);

            // Before anything stands on it, so a zone can never be drawn over a desk leg.
            BuildFloorZones(root.transform, plan, palette);

            BuildGlazing(root.transform, plan, palette);
            BuildMeetingRoom(root.transform, plan, palette);
            BuildBreakArea(root.transform, plan, palette);
            BuildDesks(root.transform, plan, palette);
            BuildGreenery(root.transform, plan, palette);

            if (plan.SecondMeetingRoom)
            {
                BuildSecondRoom(root.transform, plan, palette);
            }

            // The three groups the runtime fills. Furniture is empty on purpose: the decorator owns
            // it, and anything the builder put in it would be deleted the next time it ran.
            Group(root.transform, "Furniture");
            Group(root.transform, "Staff");
            Group(root.transform, "Servers");
            BuildWaypoints(Group(root.transform, "Waypoints"), plan);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, plan.PrefabPath);
            Object.DestroyImmediate(root);

            BuildViewingScene(prefab, plan);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Scaling Laws] {plan.Name} built: {plan.Width} x {plan.Depth}, "
                + $"{plan.Desks} desks. Prefab at {plan.PrefabPath}, scene at {plan.ScenePath}.");
        }

        // ---- the room -------------------------------------------------------------------------

        private static void BuildShell(Transform parent, Plan plan, HubPalette palette)
        {
            var shell = Group(parent, "Shell");

            Box(shell, "Floor",
                new Vector3(plan.Width / 2f, -SlabThickness / 2f, plan.Depth / 2f),
                new Vector3(plan.Width, SlabThickness, plan.Depth),
                palette.Floor);

            // Only the two far walls. The camera sits high on x and low on z, so these are the two
            // it looks at and the other two would fill the frame with their backs.
            Box(shell, "WallBack",
                new Vector3(WallThickness / 2f, WallHeight / 2f, plan.Depth / 2f),
                new Vector3(WallThickness, WallHeight, plan.Depth),
                palette.WallWarm);

            Box(shell, "WallSide",
                new Vector3(plan.Width / 2f, WallHeight / 2f, plan.Depth - WallThickness / 2f),
                new Vector3(plan.Width, WallHeight, WallThickness),
                palette.WallCool);

            // The window band along the back wall. Two boxes rather than a transparent material:
            // glass that has to be see-through is a shader problem and this only has to read as a
            // window from nine metres away.
            Box(shell, "WindowBand",
                new Vector3(WallThickness + 0.02f, 2.0f, plan.Depth / 2f),
                new Vector3(0.06f, 1.5f, plan.Depth - 1.6f),
                palette.Glass);

            AddPointLight(shell, "CeilingA",
                new Vector3(plan.Width * 0.32f, 2.9f, plan.Depth * 0.55f), 1.5f, 12f);

            AddPointLight(shell, "CeilingB",
                new Vector3(plan.Width * 0.72f, 2.9f, plan.Depth * 0.4f), 1.2f, 12f);
        }

        /// <summary>
        /// The floor, in zones.
        ///
        /// **This is most of what separates the reference photographs from a floor plan.** One
        /// flat slab reads as a plan of a room; boards where people walk and eat against stone
        /// where the kitchen is reads as a room, and it does it without a single extra object,
        /// which matters because everything else in here is a box.
        ///
        /// Two centimetres thick and laid on top of the slab rather than replacing parts of it,
        /// so the floor is still one continuous surface and a zone can be moved without leaving
        /// a hole. They are built before anything stands on the floor for the same reason.
        /// </summary>
        private static void BuildFloorZones(Transform parent, Plan plan, HubPalette palette)
        {
            var zones = Group(parent, "FloorZones");

            // **Three heights, a centimetre apart, and none of them shares one.** The first pass
            // laid all three at the same height and the render came back with a band of stripes
            // where two of them met: two surfaces at the same depth is a coin toss per pixel.
            const float stoneTop = 0.020f;
            const float tileTop = 0.021f;
            const float boardTop = 0.022f;

            // The kitchen end, on stone, and only as far as the kitchen goes. The first pass ran
            // it half the room and it read as a hole in the floor rather than as a material.
            var stoneWidth = plan.Width * 0.26f;
            var stoneDepth = plan.Depth * 0.34f;

            Box(zones, "StoneFloor",
                new Vector3(stoneWidth / 2f, stoneTop / 2f, stoneDepth / 2f),
                new Vector3(stoneWidth, stoneTop, stoneDepth), palette.Stone);

            // The working floor, on tile, under the desks and nothing else.
            Box(zones, "TileFloor",
                new Vector3(plan.Width * 0.52f, tileTop / 2f, plan.Depth * 0.40f),
                new Vector3(plan.Width * 0.48f, tileTop, plan.Depth * 0.44f), palette.Tile);

            // And the route between them, on boards: from the kitchen, across the front of the
            // desks, to the meeting room. It is the line the eye follows through the room, which
            // is the reason it is the warm one and the reason it is the only one that is a strip.
            Box(zones, "WalkwayBoards",
                new Vector3(plan.Width * 0.52f, boardTop / 2f, plan.Depth * 0.145f),
                new Vector3(plan.Width * 0.94f, boardTop, plan.Depth * 0.15f), palette.Parquet);
        }

        /// <summary>
        /// Floor to ceiling glazing on the two walls the camera can see, with the frames in it.
        ///
        /// **The mullions are the point, not the glass.** A single pane the length of a wall
        /// reads as a painted blue stripe, which is what the old window band was; the same pane
        /// divided every two and a half metres by a post reads as a window, and it costs four
        /// boxes a wall. Both reference photographs are mostly glass on two sides.
        /// </summary>
        private static void BuildGlazing(Transform parent, Plan plan, HubPalette palette)
        {
            var glazing = Group(parent, "Glazing");

            const float sill = 0.45f;
            const float head = 2.85f;
            const float spacing = 2.5f;

            var height = head - sill;
            var middle = sill + height / 2f;

            // The back wall, on x = 0. Inset a few centimetres so the wall reads behind it.
            Box(glazing, "BackGlass",
                new Vector3(WallThickness + 0.03f, middle, plan.Depth / 2f),
                new Vector3(0.05f, height, plan.Depth - 1.2f), palette.Glass);

            Box(glazing, "BackSill",
                new Vector3(WallThickness + 0.05f, sill, plan.Depth / 2f),
                new Vector3(0.16f, 0.10f, plan.Depth - 1.2f), palette.Metal);

            Box(glazing, "BackHead",
                new Vector3(WallThickness + 0.05f, head, plan.Depth / 2f),
                new Vector3(0.16f, 0.10f, plan.Depth - 1.2f), palette.Metal);

            var posts = Mathf.Max(2, Mathf.RoundToInt((plan.Depth - 1.2f) / spacing));

            for (var index = 1; index < posts; index++)
            {
                var z = 0.6f + (plan.Depth - 1.2f) * (index / (float)posts);

                Box(glazing, $"BackPost{index}",
                    new Vector3(WallThickness + 0.05f, middle, z),
                    new Vector3(0.14f, height, 0.12f), palette.Metal);
            }

            // And the side wall, on z = max, the same way.
            var sideZ = plan.Depth - WallThickness - 0.03f;

            Box(glazing, "SideGlass",
                new Vector3(plan.Width / 2f, middle, sideZ),
                new Vector3(plan.Width - 1.2f, height, 0.05f), palette.Glass);

            Box(glazing, "SideSill",
                new Vector3(plan.Width / 2f, sill, sideZ - 0.02f),
                new Vector3(plan.Width - 1.2f, 0.10f, 0.16f), palette.Metal);

            Box(glazing, "SideHead",
                new Vector3(plan.Width / 2f, head, sideZ - 0.02f),
                new Vector3(plan.Width - 1.2f, 0.10f, 0.16f), palette.Metal);

            var sidePosts = Mathf.Max(2, Mathf.RoundToInt((plan.Width - 1.2f) / spacing));

            for (var index = 1; index < sidePosts; index++)
            {
                var x = 0.6f + (plan.Width - 1.2f) * (index / (float)sidePosts);

                Box(glazing, $"SidePost{index}",
                    new Vector3(x, middle, sideZ - 0.02f),
                    new Vector3(0.12f, height, 0.14f), palette.Metal);
            }
        }

        /// <summary>
        /// Plants, in the corners the furniture shop cannot reach.
        ///
        /// **Placed against the room rather than in the open floor**, which belongs to the
        /// player: `RoomView` names a patch of ground the decorator stands things on, and a
        /// builder that drops a plant in the middle of it would have the shop growing a coffee
        /// bar through it. That is the fault the garage has a note about.
        /// </summary>
        private static void BuildGreenery(Transform parent, Plan plan, HubPalette palette)
        {
            var green = Group(parent, "Greenery");

            // Against the room and out of the way: beside the kitchen, at the end of the
            // walkway, and in the corner nearest the camera where there is nothing else.
            var spots = new[]
            {
                new Vector3(plan.Width * 0.26f, 0f, plan.Depth * 0.30f),
                new Vector3(plan.Width * 0.88f, 0f, plan.Depth * 0.17f),
                new Vector3(plan.Width * 0.30f, 0f, plan.Depth * 0.16f)
            };

            for (var index = 0; index < spots.Length; index++)
            {
                var at = spots[index];
                var tall = index % 2 == 0;

                Piece(green, $"Plant{index}", new Vector3(at.x, 0f, at.z),
                    tall ? new Vector3(0.8f, 1.15f, 0.8f) : new Vector3(1.2f, 0.5f, 1.2f),
                    tall ? Kit.PlantTall : Kit.PlantWide, palette.Foliage,
                    index * 40f);
            }
        }

        /// <summary>The glass-walled room in the corner. Every floor in the reference has one.</summary>
        private static void BuildMeetingRoom(Transform parent, Plan plan, HubPalette palette)
        {
            var room = Group(parent, "MeetingRoom");

            var width = plan.Width * 0.34f;
            var depth = plan.Depth * 0.42f;
            var originX = plan.Width - width;
            var originZ = plan.Depth - depth;

            // Two glass partitions meeting at a corner, open on the other two sides.
            Box(room, "GlassFront",
                new Vector3(originX + width / 2f, GlassHeight / 2f, originZ),
                new Vector3(width, GlassHeight, 0.08f),
                palette.Glass);

            Box(room, "GlassSide",
                new Vector3(originX, GlassHeight / 2f, originZ + depth / 2f),
                new Vector3(0.08f, GlassHeight, depth),
                palette.Glass);

            // The frames, which is what stops the glass reading as a floating pane.
            Box(room, "FrameFront",
                new Vector3(originX + width / 2f, GlassHeight, originZ),
                new Vector3(width, 0.12f, 0.14f), palette.Metal);

            Box(room, "FrameSide",
                new Vector3(originX, GlassHeight, originZ + depth / 2f),
                new Vector3(0.14f, 0.12f, depth), palette.Metal);

            // The table runs along z, which is how the pack models it, so the chairs go down
            // its two long sides rather than at the width of the room.
            var tableX = originX + width / 2f;
            var tableZ = originZ + depth / 2f;

            Piece(room, "Table", new Vector3(tableX, 0f, tableZ),
                new Vector3(1.5f, 0.75f, 3.0f), Kit.MeetingTable, palette.Timber);

            for (var index = 0; index < 6; index++)
            {
                var side = index % 2 == 0 ? -1f : 1f;
                var along = tableZ + (index / 2 - 1) * 0.95f;

                Piece(room, $"Chair{index}",
                    new Vector3(tableX + side * 1.05f, 0f, along),
                    new Vector3(0.58f, 0.88f, 0.58f), Kit.MeetingChair, palette.Fabric,
                    side < 0f ? 90f : 270f);
            }

            Piece(room, "Bin", new Vector3(originX + width * 0.9f, 0f, originZ + depth * 0.12f),
                new Vector3(0.35f, 0.5f, 0.35f), Kit.Bin, palette.Metal);

            // **On the wall that exists.** It used to hang off the room's outer edge, which is the
            // cutaway side: there is no wall there, so a two and a half metre board floated in the
            // air and crossed the glass on its way out of the building. The side wall is the one
            // surface this room actually has.
            Box(room, "Whiteboard",
                new Vector3(originX + width * 0.55f, 1.55f, plan.Depth - WallThickness - 0.06f),
                new Vector3(width * 0.5f, 1.1f, 0.06f), palette.Linen);

            // **Posts down the glass, the same as the windows.** Two bare panes read as two
            // sheets of blue standing on the floor; the same panes divided every couple of
            // metres read as a glazed room, which is what both references have in this corner.
            var frontPosts = Mathf.Max(2, Mathf.RoundToInt(width / 2.2f));

            for (var index = 1; index < frontPosts; index++)
            {
                Box(room, $"FrontPost{index}",
                    new Vector3(originX + width * (index / (float)frontPosts),
                        GlassHeight / 2f, originZ),
                    new Vector3(0.10f, GlassHeight, 0.12f), palette.Metal);
            }

            var sidePosts = Mathf.Max(2, Mathf.RoundToInt(depth / 2.2f));

            for (var index = 1; index < sidePosts; index++)
            {
                Box(room, $"SidePost{index}",
                    new Vector3(originX, GlassHeight / 2f,
                        originZ + depth * (index / (float)sidePosts)),
                    new Vector3(0.12f, GlassHeight, 0.10f), palette.Metal);
            }

            // And the doorway, which is the gap that says the room can be walked into rather
            // than being a glass box somebody is sealed inside.
            Box(room, "DoorPostA",
                new Vector3(originX, GlassHeight / 2f, originZ + depth * 0.80f),
                new Vector3(0.14f, GlassHeight, 0.12f), palette.Metal);

            Box(room, "DoorHead",
                new Vector3(originX, GlassHeight - 0.15f, originZ + depth * 0.90f),
                new Vector3(0.14f, 0.3f, depth * 0.2f), palette.Metal);
        }

        /// <summary>Kitchen and a table. A floor with nowhere to eat reads as a render.</summary>
        private static void BuildBreakArea(Transform parent, Plan plan, HubPalette palette)
        {
            var area = Group(parent, "BreakArea");

            // The run of units along the back wall, with the things that make it a kitchen
            // rather than a counter standing on it.
            // **At the size it was modelled**, which is a five metre run of units two and a half
            // metres tall, stood along the back wall. Asking it to be small is what turned it
            // into a conveyor belt.
            Piece(area, "Kitchen", new Vector3(0.75f, 0f, 3.1f),
                new Vector3(5.6f, 2.5f, 5.6f), Kit.Kitchen, palette.TimberDark);

            Piece(area, "Microwave", new Vector3(1.0f, 0.95f, 1.5f),
                new Vector3(0.6f, 0.42f, 0.6f), Kit.Microwave, palette.Metal, 90f);

            Piece(area, "CoffeePot", new Vector3(1.0f, 0.95f, 4.4f),
                new Vector3(0.35f, 0.26f, 0.35f), Kit.CoffeePot, palette.Metal, 90f);

            Piece(area, "WaterCooler", new Vector3(2.6f, 0f, 0.8f),
                new Vector3(0.5f, 1.55f, 0.5f), Kit.WaterCooler, palette.Metal, 90f);

            // Somewhere to eat. A floor with nowhere to sit down away from a desk is a render.
            Piece(area, "Table", new Vector3(2.9f, 0f, 4.6f),
                new Vector3(1.6f, 0.74f, 1.0f), Kit.BreakTable, palette.Timber, 90f);

            for (var index = 0; index < 4; index++)
            {
                var side = index < 2 ? -0.8f : 0.8f;
                var along = 4.25f + (index % 2 == 0 ? -0.45f : 0.45f);

                Piece(area, $"Stool{index}",
                    new Vector3(2.9f + side, 0f, along),
                    new Vector3(0.55f, 0.85f, 0.55f), Kit.Stool, palette.Fabric,
                    side < 0f ? 90f : 270f);
            }

            Piece(area, "Shelves", new Vector3(0.55f, 0f, 6.4f),
                new Vector3(1.5f, 1.85f, 1.5f), Kit.Shelves, palette.TimberDark, 90f);

            // **The feature wall, which is the first thing the eye finds in the reference.** A
            // dark stone panel behind the kitchen, standing slightly proud of the wall it is
            // fixed to so the edge catches the light. It is the one place in the room allowed to
            // be nearly black, and it is what makes the warm floor in front of it read as warm.
            Box(area, "FeatureWall",
                new Vector3(WallThickness + 0.06f, 1.25f, 2.0f),
                new Vector3(0.10f, 2.5f, 3.4f), palette.Stone);
        }

        /// <summary>
        /// The desks the lease comes with, in rows.
        ///
        /// **These are fixed and the decorator does not own them.** A floor that arrives empty is a
        /// floor the player has to furnish before they can hire anybody, which turns a lease into a
        /// second bill rather than a place to work.
        /// </summary>
        private static void BuildDesks(Transform parent, Plan plan, HubPalette palette)
        {
            var desks = Group(parent, "FixedDesks");

            // **Clusters facing each other, not a grid of separate tables.** Both references put
            // desks in benches of four, two against two, with a gap between the benches; a grid
            // at even spacing is what a floor plan does and it is why ten desks read as a car
            // park. The count is exactly what the lease says, because `RoomView.FixedDesks` seats
            // people at these and a number that disagreed with the geometry would seat somebody
            // at a desk that is not there.
            // **Laid on a grid inside a stated working area rather than marched across the
            // room.** The first pass slid the benches along one axis and the last of them was
            // drawn off the front edge of the floor, half of it hanging in the dark. The area
            // below is the middle of the room: clear of the kitchen at one end, of the meeting
            // room at the other, and of the patch of floor the furniture shop owns.
            var benches = Mathf.Max(1, Mathf.CeilToInt(plan.Desks / 4f));
            var across = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(benches)));
            var rows = Mathf.Max(1, Mathf.CeilToInt(benches / (float)across));

            // **Stops short of the near corner, because the big floor puts a room there.** The
            // second meeting room stands from the front wall to a third of the way back, and the
            // first grid ran the benches straight through it: the render came back with a glass
            // partition cutting a bench of four in half. Two things that each know their own
            // rectangle and nothing about each other is how that happens.
            var areaX = plan.Width * 0.32f;
            var areaWidth = plan.Width * 0.40f;
            var areaZ = plan.Depth * DeskBandFront;
            var areaDepth = plan.Depth * (DeskBandBack - DeskBandFront);

            var placed = 0;

            for (var bench = 0; bench < benches && placed < plan.Desks; bench++)
            {
                var column = bench % across;
                var row = bench / across;

                var benchX = areaX + areaWidth * ((column + 0.5f) / across);
                var benchZ = areaZ + areaDepth * ((row + 0.5f) / rows);

                // Two desks along the bench, and two facing them across it.
                for (var along = 0; along < 2 && placed < plan.Desks; along++)
                {
                    for (var side = 0; side < 2 && placed < plan.Desks; side++)
                    {
                        var x = benchX + (along == 0 ? -0.7f : 0.7f);
                        var z = benchZ + (side == 0 ? -0.40f : 0.40f);
                        var index = placed++;

                        // **Turned a quarter, because the pack models its desk along z.** Facing
                        // each other across the bench then adds the half turn for the far side.
                        var facing = 90f + (side == 0 ? 0f : 180f);

                        Piece(desks, $"Desk{index}", new Vector3(x, 0f, z),
                            new Vector3(1.5f, 0.75f, 0.82f), Kit.Desk, palette.Timber, facing);

                        // The screen backs onto the middle of the bench, which is where two
                        // people sitting opposite each other need something between them.
                        Piece(desks, $"Monitor{index}",
                            new Vector3(x, 0.75f, z + (side == 0 ? 0.24f : -0.24f)),
                            new Vector3(0.7f, 0.5f, 0.3f), Kit.Monitor, palette.Screen, facing);

                        // A different colour per seat, which is what stops a bench of four
                        // reading as one desk copied four times.
                        var colour = new[]
                        {
                            Kit.ChairColours[index % Kit.ChairColours.Length],
                            Kit.DeskChair[0]
                        };

                        Piece(desks, $"Chair{index}",
                            new Vector3(x, 0f, z + (side == 0 ? -0.78f : 0.78f)),
                            new Vector3(0.66f, 1.05f, 0.66f), colour, palette.Fabric,
                            side == 0 ? 0f : 180f);
                    }
                }
            }
        }

        /// <summary>The big floor gets a second room and a storage bay. That is the whole upgrade.</summary>
        private static void BuildSecondRoom(Transform parent, Plan plan, HubPalette palette)
        {
            var room = Group(parent, "SecondRoom");

            var width = plan.Width * 0.24f;
            var depth = plan.Depth * 0.30f;
            var originX = plan.Width - width;

            Box(room, "Partition",
                new Vector3(originX, GlassHeight / 2f, depth / 2f),
                new Vector3(0.08f, GlassHeight, depth), palette.Glass);

            Box(room, "PartitionHead",
                new Vector3(originX, GlassHeight, depth / 2f),
                new Vector3(0.14f, 0.12f, depth), palette.Metal);

            // **And the return, or it is a wall rather than a room.** One partition standing on
            // its own edge reads as a sheet of glass somebody left in the middle of the floor,
            // which is what the render showed. Two meeting at a corner is a room, and it is the
            // same shape the meeting room across the floor already uses.
            Box(room, "Return",
                new Vector3(originX + width / 2f, GlassHeight / 2f, depth),
                new Vector3(width, GlassHeight, 0.08f), palette.Glass);

            Box(room, "ReturnHead",
                new Vector3(originX + width / 2f, GlassHeight, depth),
                new Vector3(width, 0.12f, 0.14f), palette.Metal);

            var returnPosts = Mathf.Max(2, Mathf.RoundToInt(width / 2.2f));

            for (var index = 1; index < returnPosts; index++)
            {
                Box(room, $"ReturnPost{index}",
                    new Vector3(originX + width * (index / (float)returnPosts),
                        GlassHeight / 2f, depth),
                    new Vector3(0.10f, GlassHeight, 0.12f), palette.Metal);
            }

            var posts = Mathf.Max(2, Mathf.RoundToInt(depth / 2.2f));

            for (var index = 1; index < posts; index++)
            {
                Box(room, $"PartitionPost{index}",
                    new Vector3(originX, GlassHeight / 2f, depth * (index / (float)posts)),
                    new Vector3(0.12f, GlassHeight, 0.10f), palette.Metal);
            }

            // The room somebody runs the company from: one desk, one chair, and a screen on it.
            Piece(room, "Desk", new Vector3(originX + width / 2f, 0f, depth * 0.52f),
                new Vector3(1.6f, 0.75f, 0.9f), Kit.Desk, palette.TimberDark, 90f);

            Piece(room, "Monitor", new Vector3(originX + width / 2f, 0.75f, depth * 0.52f + 0.2f),
                new Vector3(0.7f, 0.5f, 0.3f), Kit.Monitor, palette.Screen, 90f);

            Piece(room, "Chair", new Vector3(originX + width / 2f, 0f, depth * 0.52f - 0.75f),
                new Vector3(0.66f, 1.05f, 0.66f), Kit.DeskChair, palette.Fabric);

            Piece(room, "Printer", new Vector3(originX + width * 0.85f, 0f, depth * 0.14f),
                new Vector3(1.0f, 1.6f, 1.0f), Kit.Printer, palette.Metal, 90f);

            for (var index = 0; index < 4; index++)
            {
                var stack = index / 2;
                var height = index % 2;

                Box(room, $"Crate{index}",
                    new Vector3(plan.Width * 0.30f + stack * 0.85f,
                        0.35f + height * 0.7f,
                        plan.Depth * 0.10f),
                    new Vector3(0.7f, 0.65f, 0.7f), palette.Cardboard);
            }
        }

        private static void BuildWaypoints(Transform parent, Plan plan)
        {
            // The same names the house uses, because FounderRoutine walks by name and a floor that
            // called them something else would put the founder at the origin.
            Marker(parent, "Door", new Vector3(plan.Width - 1.2f, 0f, 0.8f));
            Marker(parent, "Desk", new Vector3(plan.Width * 0.14f, 0f, plan.Depth * 0.30f));
            Marker(parent, "Bench", new Vector3(2.2f, 0f, 4.6f));
            Marker(parent, "Sofa", new Vector3(2.2f, 0f, 4.6f));
            Marker(parent, "Racks", new Vector3(plan.Width - 1.4f, 0f, plan.Depth * 0.62f));

            // A rented floor has no stairs and no bed. The routine still asks for them, so they point
            // at the break area: the founder takes their break where the sofa is rather than walking
            // through a wall to a bedroom this lease does not have.
            Marker(parent, "StairFoot", new Vector3(plan.Width * 0.5f, 0f, 1.6f));
            Marker(parent, "StairHead", new Vector3(plan.Width * 0.5f, 0f, 1.6f));
            Marker(parent, "Bed", new Vector3(2.2f, 0f, 4.6f));
            Marker(parent, "UpstairsDesk", new Vector3(plan.Width * 0.14f, 0f, plan.Depth * 0.52f));

            Marker(parent, "Garage", new Vector3(plan.Width + 1.4f, 0f, 0.4f));
            Marker(parent, "Car", new Vector3(plan.Width + 2.8f, 0f, -1.2f));
        }

        // ---- the scene -------------------------------------------------------------------------

        private static void BuildViewingScene(GameObject prefab, Plan plan)
        {
            if (!ScalingLawsSceneBuilder.MayOverwriteScene(plan.ScenePath))
            {
                Debug.LogWarning($"Kept {plan.ScenePath} as it is. The prefab was still rebuilt.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = plan.CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f, 1f);

            // The house's angle, kept: high on x, low on z, so both walls are behind the room.
            cameraObject.transform.rotation = Quaternion.Euler(30f, -45f, 0f);
            cameraObject.transform.position =
                new Vector3(plan.Width * 1.5f, plan.Depth * 1.5f, -plan.Depth * 0.9f);

            var sunObject = new GameObject("Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.05f;
            sunObject.transform.rotation = Quaternion.Euler(46f, -35f, 0f);

            PrefabUtility.InstantiatePrefab(prefab);

            EditorSceneManager.SaveScene(scene, plan.ScenePath);
        }

        // ---- helpers ------------------------------------------------------------------------------

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void Marker(Transform parent, string name, Vector3 position)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
        }

        /// <summary>
        /// Where the furniture comes from.
        ///
        /// **Every entry is a list, and the room survives all of them being absent.** These are
        /// Asset Store packs: they are gitignored because their licences forbid redistributing
        /// the source assets and this repository is public, so a fresh clone has none of them.
        /// A builder that needed them would be a builder nobody else can run, and the room it
        /// wrote would be a prefab full of missing references with nothing standing in for them.
        ///
        /// So `Piece` falls back to the box it replaced. The room is always complete; it is
        /// better on a machine that has the packs, which is the same rule every loader in this
        /// project already follows for art.
        /// </summary>
        private static class Kit
        {
            private const string Nappin = "Assets/nappin/OfficeEssentialsPack/Prefabs/";
            private const string Lite = "Assets/LowPolyOfficeProps_LITE/Prefabs/";

            public static readonly string[] Desk =
            {
                Nappin + "(Prb)Desk1.prefab",
                Lite + "Table_OfficeDesk.prefab"
            };

            public static readonly string[] DeskChair =
            {
                Nappin + "(Prb)OfficeChair.prefab",
                Lite + "Chair_Office.prefab"
            };

            /// <summary>Eight of the same chair in different colours, so a bench is not a clone.</summary>
            public static readonly string[] ChairColours =
            {
                Lite + "Chair_Office_Teal.prefab",
                Lite + "Chair_Office_Olive.prefab",
                Lite + "Chair_Office_Violet.prefab",
                Lite + "Chair_Office_Turquoise.prefab",
                Lite + "Chair_Office_Red.prefab",
                Lite + "Chair_Office_Green.prefab",
                Lite + "Chair_Office_Purple.prefab",
                Lite + "Chair_Office.prefab"
            };

            public static readonly string[] Monitor =
            {
                Nappin + "(Prb)PC.prefab",
                Lite + "Laptop_On.prefab"
            };

            public static readonly string[] Kitchen = { Nappin + "(Prb)KitchenModule.prefab" };
            public static readonly string[] Microwave = { Nappin + "(Prb)Microwave.prefab" };
            public static readonly string[] CoffeePot = { Nappin + "(Prb)CoffePot.prefab" };
            public static readonly string[] WaterCooler = { Nappin + "(Prb)WaterDispenser.prefab" };
            public static readonly string[] Printer = { Nappin + "(Prb)Printer.prefab" };
            public static readonly string[] Bin = { Nappin + "(Prb)TrashCan.prefab" };
            public static readonly string[] Shelves = { Nappin + "(Prb)Shelves1.prefab" };

            public static readonly string[] MeetingTable =
            {
                Nappin + "(Prb)ConferenceTable.prefab",
                Lite + "Table_Conference.prefab"
            };

            public static readonly string[] MeetingChair =
            {
                Lite + "Chair_Conference.prefab",
                Nappin + "(Prb)OfficeChair.prefab"
            };

            public static readonly string[] BreakTable = { Nappin + "(Prb)CoffeTable.prefab" };

            /// <summary>A chair, not the beanbag: Fatboy measures 1.85m across.</summary>
            public static readonly string[] Stool =
            {
                Lite + "Chair_Conference_Olive.prefab",
                Lite + "Chair_Conference.prefab"
            };

            /// <summary>
            /// A pot on the floor. The two in the office pack measure half a metre, which is a
            /// desk plant: blown up to a floor plant it reads as a poinsettia in a bucket.
            /// </summary>
            // Measured: PlantTypeA is 0.79m of actual plant, PlantPotLargeA is an empty pot at
            // 0.37m, and the two in the office pack are half a metre of desk plant.
            public static readonly string[] PlantTall =
            {
                Lite + "PlantTypeA.prefab",
                Nappin + "(Prb)Plant1.prefab"
            };

            public static readonly string[] PlantWide =
            {
                Nappin + "(Prb)PlantBox.prefab",
                Lite + "PlantPotLargeA.prefab",
                Nappin + "(Prb)Plant2.prefab"
            };

            public static readonly string[] DeskPlant = { Nappin + "(Prb)DeskPlant.prefab" };
        }

        /// <summary>
        /// Stands one piece of real furniture, scaled to the space the box used to occupy.
        ///
        /// **Measured, never assumed.** A pack models its desk in whatever unit its author
        /// liked, and this project has already been caught twice believing a prefab about its
        /// own size: the portraits framed nine characters at the chest because one pack builds
        /// people at 2.24m, and the glasses were placed twice on guesses before somebody probed
        /// the mesh and found it centred on the floor. So the renderers are measured and the
        /// piece is scaled uniformly until its footprint fits, then stood with its base on the
        /// floor rather than on its own pivot, which is the other half of that lesson.
        ///
        /// Uniform, because a desk stretched to fit a footprint is a desk that reads as wrong
        /// without anybody being able to say why.
        /// </summary>
        /// <param name="footprint">What the box was: width, height and depth in metres.</param>
        private static void Piece(Transform parent, string name, Vector3 centre,
            Vector3 footprint, string[] candidates, Material fallback, float yaw = 0f)
        {
            var prefab = Load(candidates);

            if (prefab == null)
            {
                Box(parent, name, centre, footprint, fallback);
                return;
            }

            var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            piece.name = name;
            piece.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            piece.transform.localScale = Vector3.one;

            var bounds = Measure(piece);

            if (bounds.size.x <= 0.0001f || bounds.size.z <= 0.0001f)
            {
                // Nothing to measure means nothing to draw. A prefab that renders no geometry
                // would leave an invisible hole where a desk belongs, which is worse than a box.
                Object.DestroyImmediate(piece);
                Box(parent, name, centre, footprint, fallback);
                return;
            }

            // **By height, and the footprint only rescues it.** Fitting the smallest of the
            // three axes sounds safer and is what the first pass did: the kitchen run is five
            // metres long, so asking it to fit inside eighty centimetres of depth shrank the
            // whole unit to fifteen per cent and it drew as a conveyor belt lying against the
            // wall. Height is the one dimension a person reads without thinking. A desk is
            // seventy five centimetres tall and that is what makes it a desk.
            var scale = footprint.y > 0.0001f && bounds.size.y > 0.0001f
                ? footprint.y / bounds.size.y
                : 1f;

            // The rescue: if scaling to height leaves something wildly wider than the space it
            // was given, bring it down. Generously, because a real desk is deeper than the box
            // that stood in for it and that is fine.
            var room = Mathf.Max(footprint.x, footprint.z) * 1.8f;
            var widest = Mathf.Max(bounds.size.x, bounds.size.z) * scale;

            if (room > 0.0001f && widest > room)
            {
                scale *= room / widest;
            }

            piece.transform.localScale = Vector3.one * scale;

            // Re-measured after scaling, because the offset from the pivot scaled with it.
            bounds = Measure(piece);

            var offset = piece.transform.localPosition - parent.InverseTransformPoint(bounds.center);
            var baseLift = bounds.size.y / 2f;

            piece.transform.localPosition = new Vector3(centre.x, centre.y + baseLift, centre.z)
                + offset;

            foreach (var collider in piece.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
        }

        /// <summary>The first candidate that is actually on this machine, or null.</summary>
        private static GameObject Load(string[] candidates)
        {
            foreach (var path in candidates)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
        }

        /// <summary>Every renderer under this object, as one box in world space.</summary>
        private static Bounds Measure(GameObject piece)
        {
            var renderers = piece.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                return new Bounds(piece.transform.position, Vector3.zero);
            }

            var bounds = renderers[0].bounds;

            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static void Box(Transform parent, string name, Vector3 centre, Vector3 size,
            Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = centre;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;

            Object.DestroyImmediate(box.GetComponent<BoxCollider>());
        }

        private static void AddPointLight(Transform parent, string name, Vector3 position,
            float intensity, float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = intensity;
            light.range = range;
            light.color = new Color(1f, 0.95f, 0.86f);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// The floor's own materials.
        ///
        /// Separate from the house's palette because these are different rooms, and shared between
        /// the two hubs because they are the same room at two sizes. The shader is looked up rather
        /// than named, for the reason recorded in the house builder: a URP shader under the built-in
        /// pipeline draws magenta rather than failing.
        /// </summary>
        private sealed class HubPalette
        {
            public HubPalette()
            {
                Floor = Make("HubFloor", new Color(0.19f, 0.21f, 0.25f), 0.35f);
                WallCool = Make("HubWallCool", new Color(0.26f, 0.32f, 0.44f));
                WallWarm = Make("HubWallWarm", new Color(0.58f, 0.44f, 0.24f));
                Glass = Make("HubGlass", new Color(0.42f, 0.62f, 0.78f), 0.85f, 0.1f);
                Metal = Make("HubMetal", new Color(0.30f, 0.32f, 0.36f), 0.7f, 0.4f);
                Timber = Make("HubTimber", new Color(0.44f, 0.32f, 0.22f));
                TimberDark = Make("HubTimberDark", new Color(0.28f, 0.20f, 0.14f));
                Fabric = Make("HubFabric", new Color(0.72f, 0.55f, 0.22f));
                Screen = Make("HubScreen", new Color(0.14f, 0.34f, 0.52f), 0.2f, 0.5f);
                Linen = Make("HubLinen", new Color(0.84f, 0.84f, 0.82f));
                Foliage = Make("HubFoliage", new Color(0.22f, 0.44f, 0.24f));
                Cardboard = Make("HubCardboard", new Color(0.62f, 0.50f, 0.34f));

                // **The four the reference is actually made of.** Both photographs the author
                // sent read as rooms rather than as floor plans because the floor changes
                // material where the room changes purpose: warm boards where people walk and
                // eat, dark stone where the kitchen is, one feature wall behind it. Kept
                // deliberately unbright: this room is the background of the SITE screen and has
                // white text drawn over it.
                Parquet = Make("HubParquet", new Color(0.40f, 0.29f, 0.19f), 0.25f);
                Stone = Make("HubStone", new Color(0.21f, 0.21f, 0.24f), 0.45f, 0.15f);
                Tile = Make("HubTile", new Color(0.27f, 0.28f, 0.32f), 0.30f);
                Pot = Make("HubPot", new Color(0.34f, 0.26f, 0.21f));
            }

            public Material Floor { get; }
            public Material WallCool { get; }
            public Material WallWarm { get; }
            public Material Glass { get; }
            public Material Metal { get; }
            public Material Timber { get; }
            public Material TimberDark { get; }
            public Material Fabric { get; }
            public Material Screen { get; }
            public Material Linen { get; }
            public Material Foliage { get; }
            public Material Cardboard { get; }
            public Material Parquet { get; }
            public Material Stone { get; }
            public Material Tile { get; }
            public Material Pot { get; }

            private static Shader CurrentLitShader()
            {
                var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                var shader = pipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : null;
                return shader != null ? shader : Shader.Find("Standard");
            }

            private static Material Make(string name, Color colour, float smoothness = 0.15f,
                float metallic = 0f)
            {
                var path = MaterialFolder + "/" + name + ".mat";
                var shader = CurrentLitShader();
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (existing != null)
                {
                    // Repointed rather than trusted: a material written for the other pipeline
                    // renders magenta rather than failing.
                    existing.shader = shader;
                    Paint(existing, colour, smoothness, metallic);
                    EditorUtility.SetDirty(existing);
                    return existing;
                }

                var material = new Material(shader) { name = name };
                Paint(material, colour, smoothness, metallic);
                AssetDatabase.CreateAsset(material, path);
                return material;
            }

            private static void Paint(Material material, Color colour, float smoothness, float metallic)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", colour);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", colour);
                }

                if (material.HasProperty("_Glossiness"))
                {
                    material.SetFloat("_Glossiness", smoothness);
                }

                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", smoothness);
                }

                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", metallic);
                }
            }
        }
    }
}
