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

        /// <summary>The entrance doors: a pair of glass leaves in the back wall.</summary>
        private const float DoorWidth = 1.8f;

        /// <inheritdoc cref="DoorWidth"/>
        private const float DoorHeight = 2.35f;

        /// <summary>One desk's share of a bench, along it.</summary>
        private const float DeskPitch = 1.5f;

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

        /// <summary>One bench of desks: where it starts, how many desks down each side, and its middle line.</summary>
        private readonly struct Bench
        {
            public Bench(float startX, int perSide, float z)
            {
                StartX = startX;
                PerSide = perSide;
                Z = z;
            }

            public float StartX { get; }
            public int PerSide { get; }
            public float Z { get; }
        }

        /// <summary>
        /// Everything that differs between the two floors.
        ///
        /// **Two layouts, one set of parts.** Rebuilt on 2026-09-19 against the author's references,
        /// so the numbers below are rooms rather than fractions: where the lobby ends, where the
        /// kitchen runs, which benches stand where. The decor zone in `RoomCatalog` is the open floor
        /// in front of the first bench and has to move if the benches do; a test holds that.
        /// </summary>
        private readonly struct Plan
        {
            private Plan(string name, int level, float width, float depth, int desks, float cameraSize,
                float lobbyWidth, float lobbyDepth, float kitchenFrom, float kitchenTo, float kitchenWidth,
                float loungeX, float loungeWidth, float loungeDepth, Bench[] benches)
            {
                Name = name;
                Level = level;
                Width = width;
                Depth = depth;
                Desks = desks;
                CameraSize = cameraSize;
                LobbyWidth = lobbyWidth;
                LobbyDepth = lobbyDepth;
                KitchenFrom = kitchenFrom;
                KitchenTo = kitchenTo;
                KitchenWidth = kitchenWidth;
                LoungeX = loungeX;
                LoungeWidth = loungeWidth;
                LoungeDepth = loungeDepth;
                Benches = benches;
            }

            public string Name { get; }
            public int Level { get; }
            public float Width { get; }
            public float Depth { get; }
            public int Desks { get; }
            public float CameraSize { get; }
            public float LobbyWidth { get; }
            public float LobbyDepth { get; }
            public float KitchenFrom { get; }
            public float KitchenTo { get; }
            public float KitchenWidth { get; }
            public float LoungeX { get; }
            public float LoungeWidth { get; }
            public float LoungeDepth { get; }
            public Bench[] Benches { get; }

            /// <summary>
            /// Ten desks: a bench of six and a bench of four. Warm wood, a glass meeting room in the
            /// far corner and a lounge by the windows: the author's second reference.
            /// </summary>
            public static Plan SmallHub() => new("SmallHub", 1, 16f, 11f, 10, 7.0f,
                lobbyWidth: 3.4f, lobbyDepth: 4.2f,
                kitchenFrom: 4.2f, kitchenTo: 11f, kitchenWidth: 4.4f,
                loungeX: 4.6f, loungeWidth: 5.0f, loungeDepth: 2.8f,
                benches: new[] { new Bench(5.2f, 3, 3.9f), new Bench(5.95f, 2, 6.5f) });

            /// <summary>
            /// Twenty desks in three benches, dark herringbone, lit dividers, a kitchen, a glass lounge
            /// and a television wall: the author's first reference.
            /// </summary>
            public static Plan BigHub() => new("BigHub", 2, 22f, 14f, 20, 9.0f,
                lobbyWidth: 3.6f, lobbyDepth: 4.6f,
                kitchenFrom: 4.6f, kitchenTo: 10.2f, kitchenWidth: 5.2f,
                loungeX: 0f, loungeWidth: 5.6f, loungeDepth: 3.8f,
                benches: new[] { new Bench(8.8f, 4, 3.9f), new Bench(8.8f, 4, 6.9f), new Bench(17.6f, 2, 7.4f) });

            /// <summary>
            /// The desk the founder works at: in the corner nearest the camera on both floors, and on
            /// the second inside a glass office. The `Desk` waypoint is its chair.
            /// </summary>
            public Vector3 BossDesk => Level == 2
                ? new Vector3(Width - Width * 0.24f / 2f, 0f, Depth * 0.30f * 0.52f)
                : new Vector3(Width * 0.86f, 0f, Depth * 0.16f);

            /// <summary>Where the chair is, which is where the founder is asked to walk.</summary>
            public Vector3 BossChair => BossDesk - new Vector3(0f, 0f, 0.75f);

            /// <summary>The open floor just inside the lobby: every route the routine builds passes it.</summary>
            public Vector3 Aisle => new(LobbyWidth + 0.8f, 0f, LobbyDepth * 0.45f);

            public string PrefabPath => $"{PrefabFolder}/{Name}.prefab";
            public string ScenePath => $"{ScenesFolder}/{Name}.unity";
        }

        private static void Build(Plan plan)
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(ScenesFolder);

            var palette = new HubPalette(plan);
            var root = new GameObject(plan.Name);

            // **Rebuilt on 2026-09-19 against the author's two references**: a warm wood office with a
            // glazed meeting room and a lounge for the first floor, and a dark herringbone floor with
            // lit divider walls, a kitchen, a glass lounge and a television wall for the second. Both
            // start at a lobby with glass doors and the company's name over them, which is the first
            // thing the author asked for.
            BuildShell(root.transform, plan, palette);
            BuildLobby(root.transform, plan, palette);
            BuildKitchen(root.transform, plan, palette);
            BuildLounge(root.transform, plan, palette);
            BuildDesks(root.transform, plan, palette);

            if (plan.Level == 1)
            {
                BuildMeetingRoom(root.transform, plan, palette);
                BuildBossCorner(root.transform, plan, palette);
            }
            else
            {
                BuildFeatureWalls(root.transform, plan, palette);
                BuildBossOffice(root.transform, plan, palette);
            }

            BuildLighting(root.transform, plan, palette);

            // The three groups the runtime fills. Furniture is empty on purpose: the decorator owns
            // it, and anything the builder put in it would be deleted the next time it ran.
            Group(root.transform, "Furniture");
            Group(root.transform, "Staff");
            Group(root.transform, "Servers");
            BuildWaypoints(Group(root.transform, "Waypoints"), plan);

            // The reflections last, from inside the finished room, so the floor shines with the room
            // that is standing on it.
            BuildReflections(root, plan);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, plan.PrefabPath);
            Object.DestroyImmediate(root);

            BuildViewingScene(prefab, plan);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Scaling Laws] {plan.Name} built: {plan.Width} x {plan.Depth}, "
                + $"{plan.Desks} desks. Prefab at {plan.PrefabPath}, scene at {plan.ScenePath}.");
        }

        // ---- the shell ---------------------------------------------------------------------------

        private static void BuildShell(Transform parent, Plan plan, HubPalette palette)
        {
            var shell = Group(parent, "Shell");

            // The slab under everything, and the main floor on it: warm herringbone on the first
            // floor, dark herringbone on the second, which is the single biggest thing either
            // reference is made of.
            Box(shell, "Slab",
                new Vector3(plan.Width / 2f, -SlabThickness / 2f, plan.Depth / 2f),
                new Vector3(plan.Width + 0.4f, SlabThickness, plan.Depth + 0.4f), palette.Plinth);

            Box(shell, "Floor",
                new Vector3(plan.Width / 2f, 0.005f, plan.Depth / 2f),
                new Vector3(plan.Width, 0.01f, plan.Depth), palette.MainFloor);

            // **The back wall has the entrance in it**, so it is built in three pieces round the
            // doorway: below the lobby, above the lobby, and the lintel over the doors.
            var doorFrom = plan.LobbyDepth / 2f - DoorWidth / 2f;
            var doorTo = plan.LobbyDepth / 2f + DoorWidth / 2f;

            Box(shell, "WallBackA",
                new Vector3(WallThickness / 2f, WallHeight / 2f, doorFrom / 2f),
                new Vector3(WallThickness, WallHeight, doorFrom), palette.WallBack);

            Box(shell, "WallBackB",
                new Vector3(WallThickness / 2f, WallHeight / 2f, (doorTo + plan.Depth) / 2f),
                new Vector3(WallThickness, WallHeight, plan.Depth - doorTo), palette.WallBack);

            Box(shell, "WallBackLintel",
                new Vector3(WallThickness / 2f, (DoorHeight + WallHeight) / 2f, (doorFrom + doorTo) / 2f),
                new Vector3(WallThickness, WallHeight - DoorHeight, DoorWidth), palette.WallBack);

            Box(shell, "WallSide",
                new Vector3(plan.Width / 2f, WallHeight / 2f, plan.Depth - WallThickness / 2f),
                new Vector3(plan.Width, WallHeight, WallThickness), palette.WallSide);

            // A skirting and a crown on both walls: the line that says where a wall meets a floor.
            Box(shell, "SkirtBack",
                new Vector3(WallThickness + 0.02f, 0.05f, plan.Depth / 2f + doorTo / 2f),
                new Vector3(0.04f, 0.10f, plan.Depth - doorTo), palette.Trim);

            Box(shell, "SkirtSide",
                new Vector3(plan.Width / 2f, 0.05f, plan.Depth - WallThickness - 0.02f),
                new Vector3(plan.Width, 0.10f, 0.04f), palette.Trim);

            // Windows on the side wall, tall and regular, each with a blind half down: both
            // references read as rooms at night because the windows are dark blue behind pale
            // blinds.
            // Where nothing stands against the wall: over the lounge on both floors. The meeting
            // room's panel wall and the second floor's television and acoustic panels cover the rest.
            var first = plan.Level == 1 ? plan.Width * 0.30f : 0.9f;
            var last = plan.Level == 1 ? plan.Width - 5.4f : plan.LoungeWidth - 0.8f;
            var count = Mathf.Max(2, Mathf.RoundToInt((last - first) / 2.3f));

            for (var index = 0; index <= count; index++)
            {
                var x = Mathf.Lerp(first, last, index / (float)count);
                Window(shell, $"SideWindow{index}", new Vector3(x, 0f, plan.Depth - WallThickness), true, palette);
            }

            // And along the back wall inside the second floor's glass lounge. The first floor's back
            // wall is the lobby and the kitchen, end to end.
            if (plan.Level == 2)
            {
                var from = plan.Depth - plan.LoungeDepth + 0.9f;

                for (var z = from; z < plan.Depth - 0.8f; z += 2.3f)
                {
                    Window(shell, $"BackWindow{z:0}", new Vector3(WallThickness, 0f, z), false, palette);
                }
            }
        }

        /// <summary>
        /// A tall window: dark glass, a frame, and a pale blind drawn a third of the way down.
        /// </summary>
        private static void Window(Transform parent, string name, Vector3 at, bool onSideWall,
            HubPalette palette)
        {
            const float width = 1.5f;
            const float sill = 0.55f;
            const float head = 2.75f;
            var height = head - sill;
            var middle = sill + height / 2f;

            var group = Group(parent, name);

            if (onSideWall)
            {
                var z = at.z - 0.03f;
                Box(group, "Glass", new Vector3(at.x, middle, z), new Vector3(width, height, 0.04f), palette.NightGlass);
                Box(group, "Blind", new Vector3(at.x, head - 0.45f, z - 0.05f), new Vector3(width - 0.1f, 0.8f, 0.02f), palette.Blind);
                Box(group, "FrameTop", new Vector3(at.x, head, z - 0.03f), new Vector3(width + 0.12f, 0.08f, 0.08f), palette.Frame);
                Box(group, "FrameSill", new Vector3(at.x, sill, z - 0.06f), new Vector3(width + 0.2f, 0.06f, 0.16f), palette.Frame);
                Box(group, "FrameL", new Vector3(at.x - width / 2f, middle, z - 0.03f), new Vector3(0.07f, height, 0.08f), palette.Frame);
                Box(group, "FrameR", new Vector3(at.x + width / 2f, middle, z - 0.03f), new Vector3(0.07f, height, 0.08f), palette.Frame);
                Box(group, "Mullion", new Vector3(at.x, middle, z - 0.03f), new Vector3(0.04f, height, 0.05f), palette.Frame);
            }
            else
            {
                var x = at.x + 0.03f;
                Box(group, "Glass", new Vector3(x, middle, at.z), new Vector3(0.04f, height, width), palette.NightGlass);
                Box(group, "Blind", new Vector3(x + 0.05f, head - 0.45f, at.z), new Vector3(0.02f, 0.8f, width - 0.1f), palette.Blind);
                Box(group, "FrameTop", new Vector3(x + 0.03f, head, at.z), new Vector3(0.08f, 0.08f, width + 0.12f), palette.Frame);
                Box(group, "FrameSill", new Vector3(x + 0.06f, sill, at.z), new Vector3(0.16f, 0.06f, width + 0.2f), palette.Frame);
                Box(group, "FrameL", new Vector3(x + 0.03f, middle, at.z - width / 2f), new Vector3(0.08f, height, 0.07f), palette.Frame);
                Box(group, "FrameR", new Vector3(x + 0.03f, middle, at.z + width / 2f), new Vector3(0.08f, height, 0.07f), palette.Frame);
                Box(group, "Mullion", new Vector3(x + 0.03f, middle, at.z), new Vector3(0.05f, height, 0.04f), palette.Frame);
            }
        }

        // ---- the way in --------------------------------------------------------------------------

        /// <summary>
        /// The lobby: glass doors in the back wall with the company's name over them, a reception
        /// counter, and a glass screen between the lobby and the office.
        ///
        /// **The name is not baked.** The prefab carries a <see cref="UI.CompanySignAnchor"/> and
        /// the game writes the player's company name onto it when the room is shown, because a
        /// prefab is built once and a company is named at the creator.
        /// </summary>
        private static void BuildLobby(Transform parent, Plan plan, HubPalette palette)
        {
            var lobby = Group(parent, "Lobby");
            var w = plan.LobbyWidth;
            var d = plan.LobbyDepth;

            Box(lobby, "StoneFloor", new Vector3(w / 2f, 0.012f, d / 2f), new Vector3(w, 0.012f, d),
                palette.LobbyFloor);

            // The doors: a metal frame and two glass leaves, set into the gap in the back wall.
            var doorZ = d / 2f;
            var x = WallThickness * 0.5f;

            Box(lobby, "DoorHead", new Vector3(x, DoorHeight - 0.05f, doorZ), new Vector3(0.16f, 0.10f, DoorWidth + 0.1f), palette.Frame);
            Box(lobby, "DoorJambL", new Vector3(x, DoorHeight / 2f, doorZ - DoorWidth / 2f), new Vector3(0.16f, DoorHeight, 0.08f), palette.Frame);
            Box(lobby, "DoorJambR", new Vector3(x, DoorHeight / 2f, doorZ + DoorWidth / 2f), new Vector3(0.16f, DoorHeight, 0.08f), palette.Frame);
            Box(lobby, "DoorMeet", new Vector3(x, DoorHeight / 2f, doorZ), new Vector3(0.12f, DoorHeight, 0.05f), palette.Frame);
            Box(lobby, "LeafL", new Vector3(x, DoorHeight / 2f, doorZ - DoorWidth / 4f), new Vector3(0.03f, DoorHeight - 0.1f, DoorWidth / 2f - 0.06f), palette.ClearGlass);
            Box(lobby, "LeafR", new Vector3(x, DoorHeight / 2f, doorZ + DoorWidth / 4f), new Vector3(0.03f, DoorHeight - 0.1f, DoorWidth / 2f - 0.06f), palette.ClearGlass);
            Box(lobby, "HandleL", new Vector3(x + 0.08f, 1.05f, doorZ - 0.12f), new Vector3(0.04f, 0.9f, 0.03f), palette.Brass);
            Box(lobby, "HandleR", new Vector3(x + 0.08f, 1.05f, doorZ + 0.12f), new Vector3(0.04f, 0.9f, 0.03f), palette.Brass);

            // Nothing outside the doors: a room draws nothing past its own floor, which is what keeps a
            // move from leaving pieces of the last office hanging in the dark beside the new one.

            // **The sign over the entrance**, on the wall above the lintel: a dark panel, a warm line
            // of light under it, and the name, which is written at runtime.
            var signZ = doorZ;
            var signY = DoorHeight + (WallHeight - DoorHeight) / 2f;
            var signWidth = Mathf.Min(d - 0.6f, 3.4f);

            Box(lobby, "SignPanel", new Vector3(WallThickness + 0.03f, signY, signZ),
                new Vector3(0.05f, 0.62f, signWidth), palette.SignPanel);

            Box(lobby, "SignLight", new Vector3(WallThickness + 0.07f, signY - 0.34f, signZ),
                new Vector3(0.03f, 0.03f, signWidth * 0.9f), palette.Led);

            var anchor = new GameObject("CompanySign");
            anchor.transform.SetParent(lobby, false);
            anchor.transform.localPosition = new Vector3(WallThickness + 0.07f, signY - 0.02f, signZ);

            // Facing +x, into the room and at the camera: text is drawn along its own right vector,
            // which for this turn runs along -z, so the name reads left to right from the camera.
            anchor.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            var sign = anchor.AddComponent<UI.CompanySignAnchor>();
            sign.Width = signWidth * 0.9f;
            sign.Height = 0.46f;

            // Reception: a stone-topped counter across the far corner of the lobby.
            var counterZ = d - 0.75f;
            Box(lobby, "CounterBody", new Vector3(w * 0.55f, 0.5f, counterZ), new Vector3(w * 0.56f, 1.0f, 0.55f), palette.Cabinet);
            Box(lobby, "CounterTop", new Vector3(w * 0.55f, 1.03f, counterZ), new Vector3(w * 0.6f, 0.06f, 0.62f), palette.Counter);
            Box(lobby, "CounterLight", new Vector3(w * 0.55f, 0.08f, counterZ - 0.29f), new Vector3(w * 0.5f, 0.03f, 0.02f), palette.Led);

            Piece(lobby, "LobbyPlant", new Vector3(w - 0.45f, 0f, 0.5f), new Vector3(0.7f, 1.3f, 0.7f),
                Kit.PlantTall, palette.Foliage);

            Piece(lobby, "LobbyBench", new Vector3(w * 0.45f, 0f, 0.45f), new Vector3(1.4f, 0.45f, 0.45f),
                Kit.Bench, palette.Cabinet, 0f);

            // The screen between lobby and office: glass along the lobby's two open sides, with a
            // gap to walk through facing the desks.
            GlassRun(lobby, "LobbyScreenZ", new Vector3(0f, 0f, d), new Vector3(w, 0f, d), palette, 0f, 0f);
            GlassRun(lobby, "LobbyScreenX", new Vector3(w, 0f, 0f), new Vector3(w, 0f, d), palette,
                d * 0.28f, d * 0.62f);
        }

        /// <summary>
        /// A partition of clear glass from one point to another along x or z, with posts, a head
        /// and a base, and an optional gap between two distances along it.
        /// </summary>
        private static void GlassRun(Transform parent, string name, Vector3 from, Vector3 to,
            HubPalette palette, float gapFrom, float gapTo)
        {
            var group = Group(parent, name);
            var along = to - from;
            var length = along.magnitude;
            var alongX = Mathf.Abs(along.x) > Mathf.Abs(along.z);
            var dir = along.normalized;

            void Pane(float a, float b, string label)
            {
                if (b - a < 0.05f)
                {
                    return;
                }

                var middle = from + dir * ((a + b) / 2f);
                var size = alongX
                    ? new Vector3(b - a, GlassHeight - 0.12f, 0.03f)
                    : new Vector3(0.03f, GlassHeight - 0.12f, b - a);

                Box(group, label, middle + Vector3.up * (GlassHeight / 2f + 0.03f), size, palette.ClearGlass);

                var head = alongX ? new Vector3(b - a, 0.06f, 0.07f) : new Vector3(0.07f, 0.06f, b - a);
                Box(group, label + "Head", middle + Vector3.up * GlassHeight, head, palette.Frame);
                Box(group, label + "Base", middle + Vector3.up * 0.03f, head, palette.Frame);
            }

            if (gapTo > gapFrom)
            {
                Pane(0f, gapFrom, "PaneA");
                Pane(gapTo, length, "PaneB");
            }
            else
            {
                Pane(0f, length, "Pane");
            }

            var posts = Mathf.Max(1, Mathf.RoundToInt(length / 1.4f));

            for (var index = 0; index <= posts; index++)
            {
                var t = length * index / posts;

                if (gapTo > gapFrom && t > gapFrom + 0.05f && t < gapTo - 0.05f)
                {
                    continue;
                }

                Box(group, $"Post{index}", from + dir * t + Vector3.up * (GlassHeight / 2f),
                    new Vector3(0.05f, GlassHeight, 0.05f), palette.Frame);
            }
        }

        // ---- somewhere to eat --------------------------------------------------------------------

        private static void BuildKitchen(Transform parent, Plan plan, HubPalette palette)
        {
            var kitchen = Group(parent, "BreakArea");
            var from = plan.KitchenFrom;
            var to = plan.KitchenTo;
            var width = plan.KitchenWidth;

            Box(kitchen, "TileFloor", new Vector3(width / 2f, 0.014f, (from + to) / 2f),
                new Vector3(width, 0.012f, to - from), palette.KitchenFloor);

            // The units against the back wall, with the feature wall behind them: dark stone on the
            // second floor, as in its reference, and the warm wall itself on the first.
            var run = Mathf.Min(to - from - 0.6f, 5.2f);
            var runMiddle = plan.Level == 1 ? to - run / 2f - 0.3f : from + run / 2f + 0.4f;

            Piece(kitchen, "Kitchen", new Vector3(0.6f, 0f, runMiddle),
                new Vector3(run, 2.4f, run), Kit.Kitchen, palette.Cabinet);

            Box(kitchen, "Backsplash", new Vector3(WallThickness + 0.02f, 1.3f, runMiddle),
                new Vector3(0.03f, 0.55f, run), palette.Backsplash);

            Piece(kitchen, "CoffeeMaker", new Vector3(0.55f, 0.92f, runMiddle - run * 0.3f),
                new Vector3(0.3f, 0.35f, 0.3f), Kit.CoffeeMaker, palette.Metal, 90f);

            Piece(kitchen, "Microwave", new Vector3(0.6f, 0.92f, runMiddle + run * 0.25f),
                new Vector3(0.55f, 0.35f, 0.55f), Kit.Microwave, palette.Metal, 90f);

            Piece(kitchen, "WaterCooler", new Vector3(width - 0.4f, 0f, from + 0.4f),
                new Vector3(0.45f, 1.5f, 0.45f), Kit.WaterCooler, palette.Metal, 270f);

            // One table on the small floor, two on the big one, with chairs down both long sides.
            var tables = plan.Level == 1 ? 1 : 2;

            for (var table = 0; table < tables; table++)
            {
                var tableX = width * 0.62f;
                var tableZ = plan.Level == 1
                    ? from + 1.5f
                    : Mathf.Lerp(from + 1.3f, to - 1.3f, table);

                Piece(kitchen, $"Table{table}", new Vector3(tableX, 0f, tableZ),
                    new Vector3(1.8f, 0.75f, 0.9f), Kit.DiningTable, palette.Timber, 0f);

                for (var seat = 0; seat < 4; seat++)
                {
                    var side = seat < 2 ? -1f : 1f;
                    var along = tableX + (seat % 2 == 0 ? -0.45f : 0.45f);

                    Piece(kitchen, $"Chair{table}_{seat}",
                        new Vector3(along, 0f, tableZ + side * 0.72f),
                        new Vector3(0.5f, 0.85f, 0.5f), Kit.DiningChair, palette.Fabric,
                        side < 0f ? 0f : 180f);
                }

                // A warm pendant over each table: the pool of light on the floor under it is most
                // of what the references look like.
                Pendant(kitchen, $"TablePendant{table}", new Vector3(tableX, 0f, tableZ), palette, 1.4f);
            }
        }

        // ---- somewhere to sit ---------------------------------------------------------------------

        private static void BuildLounge(Transform parent, Plan plan, HubPalette palette)
        {
            var lounge = Group(parent, "Lounge");
            var x0 = plan.LoungeX;
            var x1 = plan.LoungeX + plan.LoungeWidth;
            var z0 = plan.Depth - plan.LoungeDepth;
            var z1 = plan.Depth;
            var middle = new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f);

            // The rug: the second floor's is the pink and blue of its reference, the first floor's
            // a quiet grey on the warm wood.
            Box(lounge, "Rug", new Vector3(middle.x, 0.018f, middle.z + 0.1f),
                new Vector3((x1 - x0) * 0.72f, 0.01f, (z1 - z0) * 0.62f), palette.Rug);

            if (plan.Level == 2)
            {
                Box(lounge, "RugAccent", new Vector3(middle.x - (x1 - x0) * 0.18f, 0.019f, middle.z + 0.5f),
                    new Vector3((x1 - x0) * 0.34f, 0.01f, (z1 - z0) * 0.34f), palette.RugAccent);
            }

            // Sofa against the side wall, facing the room, and a low table in front of it.
            Piece(lounge, "Sofa", new Vector3(middle.x, 0f, z1 - 0.75f),
                new Vector3(2.2f, 0.85f, 0.9f), Kit.Sofa, palette.Fabric, 180f);

            Piece(lounge, "CoffeeTable", new Vector3(middle.x, 0f, middle.z - 0.1f),
                new Vector3(1.1f, 0.42f, 0.6f), Kit.CoffeeTable, palette.Timber, 0f);

            Piece(lounge, "ArmchairA", new Vector3(x0 + 0.7f, 0f, middle.z - 0.3f),
                new Vector3(0.8f, 0.85f, 0.8f), Kit.Armchair, palette.Accent, 60f);

            Piece(lounge, "ArmchairB", new Vector3(x1 - 0.7f, 0f, middle.z - 0.3f),
                new Vector3(0.8f, 0.85f, 0.8f), Kit.Armchair, palette.Accent, -60f);

            Piece(lounge, "FloorLamp", new Vector3(x1 - 0.4f, 0f, z1 - 0.5f),
                new Vector3(0.4f, 1.7f, 0.4f), Kit.FloorLamp, palette.Brass);

            AddPointLight(lounge, "LoungeLamp", new Vector3(x1 - 0.5f, 1.6f, z1 - 0.7f), 1.6f, 5f,
                new Color(1f, 0.78f, 0.52f));

            // The second floor's lounge is a glass room, as in the reference.
            if (plan.Level == 2)
            {
                GlassRun(lounge, "LoungeGlassZ", new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), palette,
                    (x1 - x0) * 0.62f, (x1 - x0) * 0.86f);
                GlassRun(lounge, "LoungeGlassX", new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), palette, 0f, 0f);

                Piece(lounge, "Television", new Vector3(x0 + 0.25f, 1.3f, middle.z),
                    new Vector3(0.1f, 0.7f, 1.2f), Kit.Television, palette.Screen, 90f);
            }
        }

        // ---- where the work is ------------------------------------------------------------------

        /// <summary>
        /// The desks the lease comes with, in long benches, people facing each other across a low
        /// screen, as in both references.
        ///
        /// **These are fixed and named `Chair0` upward**, because `StaffPresence` seats the n-th
        /// hire at `FixedDesks/Chair{n}` and the lease charges for exactly this many.
        /// </summary>
        private static void BuildDesks(Transform parent, Plan plan, HubPalette palette)
        {
            var desks = Group(parent, "FixedDesks");
            var placed = 0;

            foreach (var bench in plan.Benches)
            {
                // The bench body: a long shared top reads as one piece of furniture, which is what
                // makes a row of desks look like an office rather than like a classroom.
                var length = bench.PerSide * DeskPitch;
                var centreX = bench.StartX + length / 2f - DeskPitch / 2f;

                Box(desks, $"BenchScreen{placed}", new Vector3(centreX, 0.95f, bench.Z),
                    new Vector3(length - 0.1f, 0.38f, 0.03f), palette.BenchScreen);

                for (var along = 0; along < bench.PerSide; along++)
                {
                    for (var side = 0; side < 2 && placed < plan.Desks; side++)
                    {
                        var x = bench.StartX + along * DeskPitch;
                        var z = bench.Z + (side == 0 ? -0.40f : 0.40f);
                        var index = placed++;

                        // Turned a quarter because the pack models its desk along z; the far side
                        // adds the half turn.
                        var facing = 90f + (side == 0 ? 0f : 180f);

                        Piece(desks, $"Desk{index}", new Vector3(x, 0f, z),
                            new Vector3(1.5f, 0.75f, 0.82f), Kit.Desk, palette.DeskTop, facing);

                        Piece(desks, $"Monitor{index}",
                            new Vector3(x, 0.75f, z + (side == 0 ? 0.24f : -0.24f)),
                            new Vector3(0.7f, 0.5f, 0.3f), Kit.Monitor, palette.Screen, facing);

                        // One chair model for the whole floor, dark, as in both references. A
                        // rainbow of chairs is what made the old floor read as a toy.
                        Piece(desks, $"Chair{index}",
                            new Vector3(x, 0f, z + (side == 0 ? -0.78f : 0.78f)),
                            new Vector3(0.62f, 1.0f, 0.62f), Kit.StaffChair, palette.Fabric,
                            side == 0 ? 0f : 180f);
                    }
                }

                // A low planter at the head of the bench, and a bin. Not on the second floor, where
                // the lit dividers stand at the heads of the benches.
                if (plan.Level == 1)
                {
                    Piece(desks, $"BenchPlant{placed}", new Vector3(bench.StartX - 1.05f, 0f, bench.Z),
                        new Vector3(0.45f, 0.55f, 0.45f), Kit.PlantWide, palette.Foliage);
                }

                Piece(desks, $"BenchBin{placed}", new Vector3(bench.StartX + length - DeskPitch / 2f + 0.35f, 0f, bench.Z),
                    new Vector3(0.28f, 0.4f, 0.28f), Kit.Bin, palette.Metal);

                // Two pendants along every bench.
                Pendant(desks, $"BenchPendantA{placed}", new Vector3(centreX - length * 0.25f, 0f, bench.Z), palette, 1.1f);
                Pendant(desks, $"BenchPendantB{placed}", new Vector3(centreX + length * 0.25f, 0f, bench.Z), palette, 1.1f);
            }
        }

        /// <summary>The first floor's glass meeting room, in the far corner.</summary>
        private static void BuildMeetingRoom(Transform parent, Plan plan, HubPalette palette)
        {
            var room = Group(parent, "MeetingRoom");

            var x0 = plan.Width - 4.8f;
            var z0 = plan.Depth - 4.2f;
            var x1 = plan.Width;
            var z1 = plan.Depth;

            Box(room, "Carpet", new Vector3((x0 + x1) / 2f, 0.016f, (z0 + z1) / 2f),
                new Vector3(x1 - x0, 0.01f, z1 - z0), palette.Carpet);

            GlassRun(room, "GlassFront", new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), palette, 0f, 0f);
            GlassRun(room, "GlassSide", new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), palette,
                (z1 - z0) * 0.62f, (z1 - z0) * 0.88f);

            var tableX = (x0 + x1) / 2f;
            var tableZ = (z0 + z1) / 2f - 0.1f;

            Piece(room, "Table", new Vector3(tableX, 0f, tableZ), new Vector3(2.6f, 0.75f, 1.1f),
                Kit.MeetingTable, palette.Timber, 90f);

            for (var index = 0; index < 6; index++)
            {
                var side = index % 2 == 0 ? -1f : 1f;
                var along = tableX + (index / 2 - 1) * 0.85f;

                Piece(room, $"Chair{index}", new Vector3(along, 0f, tableZ + side * 0.85f),
                    new Vector3(0.55f, 0.88f, 0.55f), Kit.MeetingChair, palette.Fabric,
                    side < 0f ? 0f : 180f);
            }

            // The wall inside the room is the 3D panel wall of the first reference, with a screen.
            Box(room, "PanelWall", new Vector3(tableX, 1.5f, z1 - WallThickness - 0.02f),
                new Vector3(x1 - x0 - 0.4f, 2.6f, 0.03f), palette.PanelWall);

            Piece(room, "Screen", new Vector3(tableX, 1.0f, z1 - WallThickness - 0.06f),
                new Vector3(1.8f, 1.1f, 0.06f), Kit.Television, palette.Screen, 180f);
        }

        /// <summary>
        /// The second floor's lit divider walls, the television wall and the acoustic panels.
        ///
        /// **The light strips are the look.** The reference is a dark room in which the eye goes to
        /// four warm vertical lines on each divider and a row of lit panels along the far wall.
        /// </summary>
        private static void BuildFeatureWalls(Transform parent, Plan plan, HubPalette palette)
        {
            var features = Group(parent, "FeatureWalls");

            Divider(features, "DividerA", new Vector3(plan.Width * 0.33f, 0f, plan.Depth * 0.25f),
                plan.Depth * 0.30f, palette);

            Divider(features, "DividerB", new Vector3(plan.Width * 0.715f, 0f, plan.Depth * 0.40f),
                plan.Depth * 0.26f, palette);

            // The television wall, on the side wall between the lounge and the panels.
            var tvFrom = plan.LoungeX + plan.LoungeWidth + 0.8f;
            var tvTo = plan.Width * 0.58f;
            var tvMiddle = (tvFrom + tvTo) / 2f;

            Box(features, "DomeWall", new Vector3(tvMiddle, 1.55f, plan.Depth - WallThickness - 0.02f),
                new Vector3(tvTo - tvFrom, 3.0f, 0.03f), palette.PanelWall);

            Piece(features, "Television", new Vector3(tvMiddle, 1.1f, plan.Depth - WallThickness - 0.07f),
                new Vector3(2.2f, 1.3f, 0.06f), Kit.Television, palette.Screen, 180f);

            Piece(features, "Credenza", new Vector3(tvMiddle, 0f, plan.Depth - WallThickness - 0.35f),
                new Vector3(2.6f, 0.6f, 0.5f), Kit.MediaTable, palette.Cabinet, 180f);

            Piece(features, "CredenzaPlant", new Vector3(tvTo - 0.4f, 0f, plan.Depth - WallThickness - 0.45f),
                new Vector3(0.6f, 1.2f, 0.6f), Kit.PlantTall, palette.Foliage);

            // The acoustic panels: tall, pale, each with a line of light down one edge.
            var panelsFrom = tvTo + 0.6f;
            var panelsTo = plan.Width - 0.6f;
            var panels = Mathf.Max(3, Mathf.RoundToInt((panelsTo - panelsFrom) / 0.75f));

            for (var index = 0; index < panels; index++)
            {
                var x = Mathf.Lerp(panelsFrom, panelsTo, (index + 0.5f) / panels);
                var z = plan.Depth - WallThickness - 0.06f;

                Box(features, $"Acoustic{index}", new Vector3(x, 1.55f, z),
                    new Vector3(0.52f, 2.7f, 0.08f), palette.Acoustic);

                Box(features, $"AcousticLight{index}", new Vector3(x + 0.29f, 1.55f, z - 0.02f),
                    new Vector3(0.03f, 2.6f, 0.03f), palette.Led);
            }
        }

        /// <summary>A dark partition standing in the room, with four warm light strips and a shelf.</summary>
        private static void Divider(Transform parent, string name, Vector3 at, float length, HubPalette palette)
        {
            var group = Group(parent, name);
            const float height = 2.1f;

            Box(group, "Body", new Vector3(at.x, height / 2f, at.z + length / 2f),
                new Vector3(0.22f, height, length), palette.DividerWall);

            for (var index = 0; index < 4; index++)
            {
                var z = at.z + length * (0.3f + index * 0.12f);
                Box(group, $"Strip{index}", new Vector3(at.x + 0.12f, height * 0.55f, z),
                    new Vector3(0.02f, height * 0.72f, 0.035f), palette.Led);
            }

            Box(group, "Shelf", new Vector3(at.x + 0.35f, 0.4f, at.z + length / 2f),
                new Vector3(0.45f, 0.8f, length * 0.9f), palette.Cabinet);

            Piece(group, "ShelfPlant", new Vector3(at.x + 0.35f, 0.8f, at.z + length * 0.12f),
                new Vector3(0.35f, 0.45f, 0.35f), Kit.PlantWide, palette.Foliage);
        }

        /// <summary>The first floor's boss desk: in the corner nearest the camera, open to the room.</summary>
        private static void BuildBossCorner(Transform parent, Plan plan, HubPalette palette)
        {
            var corner = Group(parent, "BossCorner");
            var desk = plan.BossDesk;

            Box(corner, "BossRug", new Vector3(desk.x, 0.016f, desk.z - 0.3f), new Vector3(3.0f, 0.01f, 2.6f), palette.Carpet);

            Piece(corner, "Desk", desk, new Vector3(1.6f, 0.75f, 0.9f), Kit.Desk, palette.TimberDark, 90f);
            Piece(corner, "Monitor", desk + new Vector3(0f, 0.75f, 0.2f), new Vector3(0.7f, 0.5f, 0.3f), Kit.Monitor, palette.Screen, 90f);
            Piece(corner, "Chair", plan.BossChair, new Vector3(0.66f, 1.05f, 0.66f), Kit.DeskChair, palette.Fabric);
            Piece(corner, "BossPlant", desk + new Vector3(1.2f, 0f, 0.4f), new Vector3(0.7f, 1.3f, 0.7f), Kit.PlantTall, palette.Foliage);
        }

        /// <summary>The second floor's glass office for the boss, in the near corner.</summary>
        private static void BuildBossOffice(Transform parent, Plan plan, HubPalette palette)
        {
            var room = Group(parent, "SecondRoom");
            var x0 = plan.Width - plan.Width * 0.24f;
            var z1 = plan.Depth * 0.30f;

            Box(room, "Carpet", new Vector3((x0 + plan.Width) / 2f, 0.016f, z1 / 2f),
                new Vector3(plan.Width - x0, 0.01f, z1), palette.Carpet);

            GlassRun(room, "Partition", new Vector3(x0, 0f, 0f), new Vector3(x0, 0f, z1), palette, z1 * 0.55f, z1 * 0.85f);
            GlassRun(room, "Return", new Vector3(x0, 0f, z1), new Vector3(plan.Width, 0f, z1), palette, 0f, 0f);

            var desk = plan.BossDesk;
            Piece(room, "Desk", desk, new Vector3(1.6f, 0.75f, 0.9f), Kit.Desk, palette.TimberDark, 90f);
            Piece(room, "Monitor", desk + new Vector3(0f, 0.75f, 0.2f), new Vector3(0.7f, 0.5f, 0.3f), Kit.Monitor, palette.Screen, 90f);
            Piece(room, "Chair", plan.BossChair, new Vector3(0.66f, 1.05f, 0.66f), Kit.DeskChair, palette.Fabric);
            Piece(room, "Printer", new Vector3(plan.Width - 0.6f, 0f, z1 - 0.6f), new Vector3(0.9f, 1.3f, 0.9f), Kit.Printer, palette.Metal, 270f);
            Piece(room, "Plant", new Vector3(x0 + 0.5f, 0f, 0.5f), new Vector3(0.7f, 1.3f, 0.7f), Kit.PlantTall, palette.Foliage);
        }

        // ---- light ----------------------------------------------------------------------------------

        /// <summary>
        /// A warm light from the ceiling over a table or a bench: the pool of light on the glossy
        /// floor under it is most of what the references look like.
        ///
        /// **The light only, no fitting.** The first build hung a shade on a cord, and from this
        /// camera a ceiling fitting in a room with no ceiling is a black square floating in the air.
        /// Neither reference shows one either.
        /// </summary>
        private static void Pendant(Transform parent, string name, Vector3 over, HubPalette palette, float intensity)
        {
            AddPointLight(parent, name + "Light", new Vector3(over.x, 2.3f, over.z), intensity, 4.2f,
                new Color(1f, 0.83f, 0.62f));
        }

        private static void BuildLighting(Transform parent, Plan plan, HubPalette palette)
        {
            var lights = Group(parent, "Lighting");

            // A soft fill over the whole floor, so the corners the pendants do not reach are dim
            // rather than black.
            AddPointLight(lights, "FillA", new Vector3(plan.Width * 0.30f, 3.0f, plan.Depth * 0.45f), 0.55f, plan.Width * 0.8f,
                new Color(0.86f, 0.9f, 1f));
            AddPointLight(lights, "FillB", new Vector3(plan.Width * 0.72f, 3.0f, plan.Depth * 0.45f), 0.55f, plan.Width * 0.8f,
                new Color(0.86f, 0.9f, 1f));

            // The lobby, lit from over the sign.
            AddPointLight(lights, "LobbyLight", new Vector3(plan.LobbyWidth * 0.5f, 2.6f, plan.LobbyDepth * 0.5f), 1.1f, 4.5f,
                new Color(1f, 0.86f, 0.7f));
        }

        /// <summary>
        /// The floor's reflections: a cubemap rendered from the middle of the finished room, given to
        /// a box-projected probe the size of the room.
        ///
        /// **Rendered here rather than baked by the lightmapper**, because the rooms are prefabs
        /// swapped in at runtime and a baked probe belongs to a scene. Box projection is what makes a
        /// reflection on a floor line up with the room instead of sliding across it as the eye moves.
        /// </summary>
        private static void BuildReflections(GameObject root, Plan plan)
        {
            var centre = new Vector3(plan.Width / 2f, 1.3f, plan.Depth / 2f);

            var probeObject = new GameObject("Reflections");
            probeObject.transform.SetParent(root.transform, false);
            probeObject.transform.localPosition = centre;

            var probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
            probe.boxProjection = true;
            probe.size = new Vector3(plan.Width, WallHeight + 0.4f, plan.Depth);
            probe.center = new Vector3(0f, WallHeight / 2f - 1.3f, 0f);
            probe.intensity = 1f;
            probe.importance = 2;

            // Somewhere the lights can reach: a camera in the middle of the room, rendering the six
            // faces into a cubemap written to disk.
            var cameraObject = new GameObject("ReflectionCamera");
            cameraObject.transform.position = root.transform.TransformPoint(centre);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;

            var sunObject = new GameObject("ReflectionSun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.5f;
            sunObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var cubemap = new Cubemap(256, TextureFormat.RGB24, true);
            camera.RenderToCubemap(cubemap);

            var path = $"{TextureForge.Folder}/{plan.Name}_Reflection.cubemap";
            Directory.CreateDirectory(TextureForge.Folder);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(cubemap, path);

            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(sunObject);
        }

        private static void BuildWaypoints(Transform parent, Plan plan)
        {
            // The same names the house uses, because FounderRoutine walks by name and a floor that
            // called them something else would put the founder at the origin.
            Marker(parent, "Door", new Vector3(0.8f, 0f, plan.LobbyDepth / 2f));

            // Both desk markers are the boss chair; see the note on Plan.BossDesk.
            Marker(parent, "Desk", plan.BossChair, 0f);
            Marker(parent, "UpstairsDesk", plan.BossChair, 0f);

            var sofa = new Vector3(plan.LoungeX + plan.LoungeWidth / 2f, 0f, plan.Depth - 1.6f);
            Marker(parent, "Bench", sofa);
            Marker(parent, "Sofa", sofa);
            Marker(parent, "Bed", sofa);
            Marker(parent, "Racks", new Vector3(plan.Width - 1.4f, 0f, plan.Depth * 0.5f));

            // A rented floor has no stairs; the routine routes through these two on its way to
            // everywhere, so they are the front aisle, which is open floor.
            Marker(parent, "StairFoot", plan.Aisle);
            Marker(parent, "StairHead", plan.Aisle);

            // Out through the glass doors.
            Marker(parent, "Garage", new Vector3(-1.2f, 0f, plan.LobbyDepth / 2f));
            Marker(parent, "Car", new Vector3(-2.6f, 0f, plan.LobbyDepth / 2f));
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
            sun.intensity = 0.55f;
            sun.color = new Color(0.8f, 0.86f, 1f);
            sunObject.transform.rotation = Quaternion.Euler(46f, -35f, 0f);

            var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            // The name the game would write, so a snapshot of the room shows a sign rather than a
            // blank panel. The runtime writes the player's own.
            UI.CompanySignAnchor.ApplyAll(room, "Prometheus AI");

            EditorSceneManager.SaveScene(scene, plan.ScenePath);
        }

        // ---- helpers ------------------------------------------------------------------------------

        private static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        /// <summary>
        /// One walking point. A seat also says which way the chair points.
        ///
        /// <paramref name="seatFacing"/> is degrees about y and it is read on arrival. It carries
        /// a <see cref="UI.SeatFacing"/> rather than being inferred from the rotation, because the
        /// chair on both floors faces +z, which is identity: "somebody set this" and "this is
        /// turned" are different questions and the second one answers no exactly here.
        /// </summary>
        private static void Marker(Transform parent, string name, Vector3 position,
            float? seatFacing = null)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;

            if (!seatFacing.HasValue)
            {
                return;
            }

            marker.transform.localRotation = Quaternion.Euler(0f, seatFacing.Value, 0f);
            marker.AddComponent<UI.SeatFacing>();
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

            /// <summary>The staff chair: dark, one model for the whole floor, as in both references.</summary>
            public static readonly string[] StaffChair = { Lite + "Chair_Office.prefab", Nappin + "(Prb)OfficeChair.prefab" };

            private const string Brick = "Assets/Brick Project Studio/Apartment Kit/_Prefabs/";

            public static readonly string[] Bench = { Brick + "Furniture/Living Room/Bench_Apt_01.prefab" };
            public static readonly string[] CoffeeMaker = { Brick + "Props/Kitchen/CoffeeMaker_Apt_01.prefab", Nappin + "(Prb)CoffePot.prefab" };
            public static readonly string[] DiningTable = { Brick + "Furniture/Living Room/Table_Dining_Apt_01.prefab", Nappin + "(Prb)CoffeTable.prefab" };

            /// <summary>Blue chairs round the kitchen tables, as in both references.</summary>
            public static readonly string[] DiningChair = { Lite + "Chair_Conference_Teal.prefab", Lite + "Chair_Conference.prefab" };

            public static readonly string[] Sofa = { Brick + "Furniture/Living Room/Sofa_Apt_01.prefab", Nappin + "(Prb)Sofa1.prefab" };
            public static readonly string[] CoffeeTable = { Brick + "Furniture/Living Room/Table_Coffee_01.prefab", Nappin + "(Prb)CoffeTable.prefab" };
            public static readonly string[] Armchair = { Nappin + "(Prb)LoungeChair.prefab" };
            public static readonly string[] FloorLamp = { Brick + "Props/Lighting/Lamp_Floor_Apt_01.prefab" };
            public static readonly string[] Painting = { Brick + "Props/Art/Canvas_Painting_01.prefab" };
            public static readonly string[] Television = { Brick + "Props/Electronics/TV_Apt_01.prefab" };
            public static readonly string[] MediaTable = { Brick + "Furniture/Living Room/Table_Media_01.prefab" };
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
            float intensity, float range, Color? colour = null)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = intensity;
            light.range = range;
            light.color = colour ?? new Color(1f, 0.95f, 0.86f);

            // Per pixel, or the pools of light on a glossy floor are smeared across whole faces:
            // the look the references are made of is exactly the highlight a vertex light cannot draw.
            light.renderMode = LightRenderMode.ForcePixel;
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
        /// The floor's own materials, per level.
        ///
        /// **Textured, glossy, and named per level**, because a texture's tiling is a property of the
        /// material and the same parquet laid over a sixteen metre floor and a two metre rug needs two
        /// different scales. The textures come from <see cref="TextureForge"/> and are stand-ins for
        /// the asset pass the author has planned; a material here can be repointed at a bought texture
        /// without touching the room.
        ///
        /// The shader is looked up rather than named, for the reason recorded in the house builder: a
        /// URP shader under the built-in pipeline draws magenta rather than failing.
        /// </summary>
        private sealed class HubPalette
        {
            public HubPalette(Plan plan)
            {
                var one = plan.Level == 1;
                var tag = one ? "Hub1" : "Hub2";

                var parquet = one
                    ? TextureForge.Herringbone("ParquetWarm", new Color(0.62f, 0.40f, 0.24f), 0.30f)
                    : TextureForge.Herringbone("ParquetDark", new Color(0.36f, 0.16f, 0.10f), 0.35f);

                var tiles = one
                    ? TextureForge.Tiles("TilesGrey", new Color(0.40f, 0.41f, 0.44f), 4, 0.10f)
                    : TextureForge.Tiles("TilesDark", new Color(0.24f, 0.25f, 0.27f), 4, 0.12f);

                var stone = TextureForge.Stone("StoneLight", new Color(0.80f, 0.79f, 0.77f));
                var stoneDark = TextureForge.Stone("StoneDark", new Color(0.20f, 0.21f, 0.23f));
                var planks = TextureForge.Planks("PlanksWarm", new Color(0.52f, 0.33f, 0.19f), 6);
                var domes = one
                    ? TextureForge.DomePanels("DomesWarm", new Color(0.56f, 0.38f, 0.22f), 6)
                    : TextureForge.DomePanels("DomesGrey", new Color(0.46f, 0.47f, 0.50f), 6);

                MainFloor = Make($"{tag}Floor", Color.white, one ? 0.62f : 0.72f, 0f, parquet,
                    new Vector2(plan.Width / 2.4f, plan.Depth / 2.4f));

                LobbyFloor = Make($"{tag}LobbyFloor", Color.white, 0.85f, 0f, stone,
                    new Vector2(plan.LobbyWidth / 2f, plan.LobbyDepth / 2f));

                KitchenFloor = Make($"{tag}KitchenFloor", Color.white, 0.8f, 0f, tiles,
                    new Vector2(plan.KitchenWidth / 2.4f, (plan.KitchenTo - plan.KitchenFrom) / 2.4f));

                WallBack = one
                    ? Make($"{tag}WallBack", Color.white, 0.25f, 0f, planks, new Vector2(plan.Depth / 1.2f, WallHeight / 1.2f))
                    : Make($"{tag}WallBack", new Color(0.55f, 0.56f, 0.6f), 0.3f, 0f, stoneDark, new Vector2(plan.Depth / 3f, 1f));

                WallSide = one
                    ? Make($"{tag}WallSide", Color.white, 0.25f, 0f, planks, new Vector2(plan.Width / 1.2f, WallHeight / 1.2f))
                    : Make($"{tag}WallSide", new Color(0.55f, 0.56f, 0.6f), 0.3f, 0f, stoneDark, new Vector2(plan.Width / 3f, 1f));

                PanelWall = Make($"{tag}PanelWall", Color.white, 0.35f, 0f, domes, new Vector2(3f, 2f));

                Plinth = Make("HubPlinth", new Color(0.07f, 0.08f, 0.10f));
                Trim = Make("HubTrim", new Color(0.09f, 0.09f, 0.10f), 0.5f);
                Frame = Make("HubFrame", new Color(0.08f, 0.08f, 0.09f), 0.55f, 0.6f);
                NightGlass = Make("HubNightGlass", new Color(0.08f, 0.13f, 0.26f), 0.95f, 0f, null, null,
                    new Color(0.02f, 0.05f, 0.12f));
                Blind = Make("HubBlind", new Color(0.80f, 0.78f, 0.74f), 0.1f);
                ClearGlass = MakeClear("HubClearGlass", new Color(0.62f, 0.78f, 0.88f, 0.16f));
                Brass = Make("HubBrass", new Color(0.72f, 0.56f, 0.30f), 0.65f, 0.9f);
                SignPanel = Make("HubSignPanel", new Color(0.04f, 0.04f, 0.05f), 0.85f);
                Led = Make("HubLed", new Color(1f, 0.86f, 0.64f), 0.5f, 0f, null, null,
                    new Color(1f, 0.82f, 0.58f) * 2.4f);
                Cabinet = one ? Make($"{tag}Cabinet", new Color(0.22f, 0.17f, 0.13f), 0.45f)
                              : Make($"{tag}Cabinet", new Color(0.09f, 0.09f, 0.10f), 0.5f);
                Counter = Make("HubCounter", Color.white, 0.9f, 0f, stone, Vector2.one);
                Backsplash = one ? Make($"{tag}Backsplash", Color.white, 0.8f, 0f, tiles, new Vector2(2f, 0.3f))
                                 : Make($"{tag}Backsplash", Color.white, 0.85f, 0f, stoneDark, new Vector2(2f, 0.4f));
                DeskTop = one ? Make($"{tag}DeskTop", new Color(0.44f, 0.31f, 0.20f), 0.4f)
                              : Make($"{tag}DeskTop", new Color(0.20f, 0.20f, 0.22f), 0.5f);
                BenchScreen = Make("HubBenchScreen", new Color(0.14f, 0.14f, 0.15f), 0.3f);
                Rug = one ? Make($"{tag}Rug", new Color(0.42f, 0.44f, 0.48f), 0.05f)
                          : Make($"{tag}Rug", new Color(0.86f, 0.26f, 0.50f), 0.05f);
                RugAccent = Make("HubRugAccent", new Color(0.18f, 0.48f, 0.80f), 0.05f);
                Accent = one ? Make($"{tag}Accent", new Color(0.20f, 0.44f, 0.50f), 0.2f)
                             : Make($"{tag}Accent", new Color(0.15f, 0.55f, 0.80f), 0.2f);
                Carpet = Make("HubCarpet", new Color(0.20f, 0.21f, 0.24f), 0.05f);
                Acoustic = Make("HubAcoustic", new Color(0.74f, 0.75f, 0.78f), 0.05f);
                DividerWall = Make("HubDivider", new Color(0.07f, 0.07f, 0.08f), 0.45f);

                Metal = Make("HubMetal", new Color(0.30f, 0.32f, 0.36f), 0.7f, 0.4f);
                Timber = Make("HubTimber", new Color(0.44f, 0.32f, 0.22f), 0.35f);
                TimberDark = Make("HubTimberDark", new Color(0.24f, 0.18f, 0.13f), 0.45f);
                Fabric = Make("HubFabric", new Color(0.16f, 0.16f, 0.18f));
                Screen = Make("HubScreen", new Color(0.14f, 0.34f, 0.52f), 0.2f, 0.5f);
                Linen = Make("HubLinen", new Color(0.84f, 0.84f, 0.82f));
                Foliage = Make("HubFoliage", new Color(0.22f, 0.44f, 0.24f));
            }

            public Material MainFloor { get; }
            public Material LobbyFloor { get; }
            public Material KitchenFloor { get; }
            public Material WallBack { get; }
            public Material WallSide { get; }
            public Material PanelWall { get; }
            public Material Plinth { get; }
            public Material Trim { get; }
            public Material Frame { get; }
            public Material NightGlass { get; }
            public Material Blind { get; }
            public Material ClearGlass { get; }
            public Material Brass { get; }
            public Material SignPanel { get; }
            public Material Led { get; }
            public Material Cabinet { get; }
            public Material Counter { get; }
            public Material Backsplash { get; }
            public Material DeskTop { get; }
            public Material BenchScreen { get; }
            public Material Rug { get; }
            public Material RugAccent { get; }
            public Material Accent { get; }
            public Material Carpet { get; }
            public Material Acoustic { get; }
            public Material DividerWall { get; }
            public Material Metal { get; }
            public Material Timber { get; }
            public Material TimberDark { get; }
            public Material Fabric { get; }
            public Material Screen { get; }
            public Material Linen { get; }
            public Material Foliage { get; }

            private static Shader CurrentLitShader()
            {
                var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                var shader = pipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : null;
                return shader != null ? shader : Shader.Find("Standard");
            }

            private static Material Make(string name, Color colour, float smoothness = 0.15f,
                float metallic = 0f, Texture2D texture = null, Vector2? tiling = null, Color? emission = null)
            {
                var path = MaterialFolder + "/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    material = new Material(CurrentLitShader()) { name = name };
                    AssetDatabase.CreateAsset(material, path);
                }

                // Repointed rather than trusted: a material written for the other pipeline renders
                // magenta rather than failing.
                material.shader = CurrentLitShader();
                Paint(material, colour, smoothness, metallic);

                material.mainTexture = texture;
                material.mainTextureScale = tiling ?? Vector2.one;

                if (emission.HasValue)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission.Value);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                }

                EditorUtility.SetDirty(material);
                return material;
            }

            /// <summary>
            /// Glass you can see through: the Standard shader's transparent mode, set by hand because
            /// the inspector normally does it. **The old glass was an opaque blue box**, which is why
            /// a glass room hid everything behind it; the lobby would have been invisible behind its
            /// own screen.
            /// </summary>
            private static Material MakeClear(string name, Color colour)
            {
                var material = Make(name, colour, 0.95f, 0f);

                material.SetFloat("_Mode", 3f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                EditorUtility.SetDirty(material);
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
