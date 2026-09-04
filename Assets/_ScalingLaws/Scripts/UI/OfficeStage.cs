using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The room the office camera is pointed at, and what is standing in it.
    ///
    /// **Why this is a runtime swap rather than three scenes.** The game scene is generated, its
    /// contents are fixed, and the office camera in it is aimed at a room that is already there. A
    /// second and third room baked into the same file would both be loaded whichever one is rented,
    /// and regenerating that scene per tier would change a prefab count the project checks before
    /// every commit. So the garage stays where it is, the two hubs live under Resources, and moving
    /// office means hiding one room and loading another under the same camera.
    ///
    /// The camera itself is a child of the baked room, which is convenient rather than accidental:
    /// it is already at the right height and angle, and the hub prefabs were laid out for that
    /// angle. Only its distance and orthographic size change.
    /// </summary>
    public sealed class OfficeStage
    {
        /// <summary>What the builder called the group a placed piece goes into.</summary>
        public const string FurnitureGroup = "Furniture";

        /// <summary>How far the camera pulls back. Far enough that nothing clips the near plane.</summary>
        public const float CameraPullback = 30f;

        private readonly Transform anchor;
        private readonly Camera camera;
        private readonly Transform bakedRoom;

        private GameObject loadedRoom;
        private OfficeTier? shownTier;

        /// <summary>
        /// Binds to whatever the game scene already has.
        ///
        /// Everything is optional. A shell running in a test, or in a scene that was never
        /// generated, gets a stage that does nothing rather than a null reference on the first
        /// frame the office is shown.
        /// </summary>
        public OfficeStage(GameObject officeRoom)
        {
            if (officeRoom == null)
            {
                return;
            }

            bakedRoom = officeRoom.transform;
            anchor = bakedRoom.parent;
            camera = officeRoom.GetComponentInChildren<Camera>(true);
        }

        public bool IsLive => bakedRoom != null;

        /// <summary>
        /// The camera the room is drawn through, so a click can be turned into a ray.
        ///
        /// Exposed rather than kept private because the picking lives in the screen: the stage owns
        /// the geometry and the screen owns what a click means, which is the same split the basement
        /// already uses.
        /// </summary>
        public Camera View => camera;

        /// <summary>What the camera renders into, needed to undo the crop a click travelled through.</summary>
        public Texture Texture => camera != null ? camera.targetTexture : null;

        /// <summary>Which room is on screen. Null until the first call to <see cref="Show"/>.</summary>
        public OfficeTier? ShownTier => shownTier;

        /// <summary>The room currently being looked at, baked or loaded.</summary>
        public Transform CurrentRoom =>
            loadedRoom != null ? loadedRoom.transform : bakedRoom;

        /// <summary>
        /// Puts the right room under the camera and stands the furniture up in it.
        ///
        /// Cheap to call every time the office screen opens: it returns immediately when the tier
        /// has not changed, and the furniture is rebuilt on its own because a piece can be bought
        /// without the lease changing.
        /// </summary>
        public void Show(OfficeTier tier, DecorPlan decor)
        {
            if (!IsLive)
            {
                return;
            }

            if (shownTier != tier)
            {
                SwapRoom(tier);
                shownTier = tier;
            }

            Dress(decor);
        }

        private void SwapRoom(OfficeTier tier)
        {
            var view = RoomCatalog.For(tier);

            if (loadedRoom != null)
            {
                Object.Destroy(loadedRoom);
                loadedRoom = null;
            }

            if (view.IsLoaded)
            {
                var prefab = Resources.Load<GameObject>(view.ResourcePath);
                if (prefab != null)
                {
                    loadedRoom = Object.Instantiate(prefab, anchor);
                    loadedRoom.name = view.ResourcePath;

                    // Same spot the garage sits in: far below the interface camera, so neither
                    // camera can ever see the other's geometry.
                    loadedRoom.transform.position = bakedRoom.position;
                    loadedRoom.transform.rotation = bakedRoom.rotation;
                }
            }

            // The baked room's own geometry is hidden rather than destroyed, because the camera and
            // the key light are its children and destroying it would take the office view with it.
            SetGeometryVisible(bakedRoom, loadedRoom == null);

            Frame(view);
        }

        /// <summary>
        /// Hides a room's meshes while leaving its cameras and lights alone.
        ///
        /// Renderers rather than the GameObject, for exactly that reason: switching the garage off
        /// wholesale would switch off the camera that is rendering the office.
        /// </summary>
        private static void SetGeometryVisible(Transform room, bool visible)
        {
            if (room == null)
            {
                return;
            }

            foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.enabled = visible;
            }
        }

        private void Frame(RoomView view)
        {
            if (camera == null)
            {
                return;
            }

            camera.orthographicSize = view.CameraSize;

            var floor = CurrentRoom != null ? CurrentRoom.position : Vector3.zero;
            var focus = floor + new Vector3(view.FocusX, 3f, view.FocusZ);

            // The angle is left exactly as the scene builder set it. Every placement in every room
            // was laid out for it and a nudge here moves all of them.
            camera.transform.position = focus - camera.transform.forward * CameraPullback;
        }

        /// <summary>
        /// Rebuilds the placed furniture from scratch.
        ///
        /// Cleared and refilled rather than diffed. The list is a dozen boxes on a screen that is
        /// already rendering a room, and a diff would be code that can disagree with the plan.
        /// </summary>
        private void Dress(DecorPlan decor)
        {
            var room = CurrentRoom;
            if (room == null)
            {
                return;
            }

            var group = room.Find(FurnitureGroup);
            if (group == null)
            {
                var made = new GameObject(FurnitureGroup);
                made.transform.SetParent(room, false);
                group = made.transform;
            }

            for (var index = group.childCount - 1; index >= 0; index--)
            {
                Object.Destroy(group.GetChild(index).gameObject);
            }

            if (decor == null)
            {
                return;
            }

            foreach (var item in decor.Placed)
            {
                Stand(group, item);
            }
        }

        private static void Stand(Transform group, DecorItem item)
        {
            var piece = item.Definition;

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = piece.DisplayName;
            box.transform.SetParent(group, false);
            box.transform.localPosition = new Vector3(item.X, piece.SizeY / 2f, item.Z);
            box.transform.localScale = new Vector3(piece.SizeX, piece.SizeY, piece.SizeZ);

            // Nothing walks into it, and a collider on a decoration is a collider the founder's
            // route has to be told about.
            Object.Destroy(box.GetComponent<BoxCollider>());

            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(piece.Tint);
        }

        private static readonly Dictionary<string, Material> Paints = new();

        /// <summary>
        /// One material per colour, shared.
        ///
        /// The shader is looked up rather than named: a URP shader under the built-in pipeline draws
        /// magenta rather than failing, which is a bug that only shows up on screen.
        /// </summary>
        private static Material MaterialFor(string tint)
        {
            if (Paints.TryGetValue(tint, out var cached) && cached != null)
            {
                return cached;
            }

            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var shader = pipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : null;
            shader = shader != null ? shader : Shader.Find("Standard");

            var material = new Material(shader);

            if (ColorUtility.TryParseHtmlString(tint, out var colour))
            {
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", colour);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", colour);
                }
            }

            Paints[tint] = material;
            return material;
        }
    }
}
