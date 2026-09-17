using ScalingLaws.Core;
using ScalingLaws.Data;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Drives the overview camera in the city map scene: WASD and the arrows pan the ground, "="
    /// and "-" dolly the camera along its own forward vector to zoom, and ESC hands the scene back
    /// to <see cref="SceneFlow.ReturnFromCityMap"/>.
    ///
    /// **Panning is relative to the camera's own flattened basis, not to world X/Z.** The camera
    /// sits at a fixed 36-degree pitch — <see cref="Editor.CityDressingBuilder.BuildCamera"/> never
    /// rotates it, there is no orbit — so "forward" only ever means one thing: away from the
    /// camera, projected onto the ground. Reading world axes directly would still work today, by
    /// coincidence of what that one fixed angle happens to point along, and break the moment
    /// anybody changes it.
    ///
    /// **The key-reading and the world-math are split on purpose**, the same way
    /// <see cref="KeyboardShortcuts"/> splits `Resolve`/`ResolveScroll` from the `Input` calls that
    /// feed them: the panel of eight keys collapsing to one direction, and the clamping, are the
    /// part worth getting right in a test that does not need a live scene. Reading the keyboard is
    /// not.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CityMapController : MonoBehaviour
    {
        /// <summary>Ground units a second at <see cref="ReferenceHeight"/>. Faster once zoomed out, see <see cref="HeightFactor"/>.</summary>
        public const float PanUnitsPerSecond = 500f;

        /// <summary>World units a second the camera dollies along its own forward vector while zooming.</summary>
        public const float ZoomUnitsPerSecond = 900f;

        /// <summary>The height <see cref="PanUnitsPerSecond"/> is measured at. Panning scales from here.</summary>
        public const float ReferenceHeight = 900f;

        /// <summary>Never below this fraction of the base pan speed, even pressed right up against the terrain.</summary>
        public const float MinHeightFactor = 0.35f;

        /// <summary>Never above this multiple of the base pan speed, however far out the player zooms.</summary>
        public const float MaxHeightFactor = 3.5f;

        /// <summary>Closest the camera may dolly in. Below this a wide shot starts clipping through real geometry.</summary>
        public const float MinHeight = 100f;

        /// <summary>
        /// Furthest out. A little past the builder's own opening height (2050) so a player who
        /// zooms out to get their bearings is not stopped just short of the view that greeted them.
        /// </summary>
        public const float MaxHeight = 3200f;

        /// <summary>
        /// How far past the coastline panning still goes. Bayview is not a rectangle — a hard stop
        /// at exactly <see cref="CityLayout.Size"/> would clip the view at an angle wherever the
        /// shoreline curves inside that square, mid-district.
        /// </summary>
        public const float PanMargin = 400f;

        /// <summary>How quickly a district jump closes the distance each second. Higher eases out faster.</summary>
        public const float FlyToRate = 2.4f;

        /// <summary>Close enough that snapping the rest of the way is not a visible jump.</summary>
        public const float FlyToArrivalDistance = 2f;

        private Camera cam;
        private Vector3? flyTarget;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        /// <summary>
        /// Starts an eased flight to a spot on the ground, keeping the current height and the
        /// fixed look angle — the same "district" jump the map's quick-travel panel offers.
        ///
        /// Takes the target's own ground height rather than assuming one, because
        /// <see cref="Data.CityLayout.Districts"/> are levelled to different heights and aiming
        /// from a fixed guess would centre every district except the one whose height happened to
        /// match it.
        /// </summary>
        public void FlyTo(Vector2 groundTarget, float targetGroundHeight)
        {
            var ground = CameraGroundFor(groundTarget, targetGroundHeight,
                transform.position.y, transform.forward);

            flyTarget = new Vector3(ground.x, transform.position.y, ground.y);
        }

        /// <summary>
        /// Where the camera's own ground point has to be so that, looking along `forward` from
        /// `cameraHeight`, the fixed angle lands exactly on `target` — the maths behind
        /// <see cref="FlyTo"/>, pulled out because it is worth checking without a live camera.
        ///
        /// A straight teleport to the target's own XZ would put the district under the camera
        /// rather than under where the camera is looking, which at this pitch is well short of
        /// directly below — the district would land in the corner of the frame, not the centre.
        /// </summary>
        public static Vector2 CameraGroundFor(Vector2 target, float targetHeight,
            float cameraHeight, Vector3 forward)
        {
            var drop = -forward.y;
            var reach = drop > 0.001f ? (cameraHeight - targetHeight) / drop : 0f;

            return new Vector2(target.x - forward.x * reach, target.y - forward.z * reach);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SceneFlow.ReturnFromCityMap();
                return;
            }

            var deltaSeconds = Time.unscaledDeltaTime;

            var panAxis = ResolvePan(
                Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow),
                Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow),
                Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow),
                Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow));

            var zoom = ResolveZoom(
                Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus),
                Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus));

            // Either key is the player taking the camera back, mid-flight or not.
            if (panAxis != Vector2.zero || zoom != 0f)
            {
                flyTarget = null;
            }

            if (flyTarget.HasValue)
            {
                var target = flyTarget.Value;

                if (Vector3.Distance(transform.position, target) <= FlyToArrivalDistance)
                {
                    transform.position = target;
                    flyTarget = null;
                }
                else
                {
                    var t = 1f - Mathf.Exp(-FlyToRate * deltaSeconds);
                    transform.position = Vector3.Lerp(transform.position, target, t);
                }

                return;
            }

            if (panAxis != Vector2.zero)
            {
                var forward = FlattenToGround(transform.forward);
                var right = FlattenToGround(transform.right);
                var world = forward * panAxis.y + right * panAxis.x;

                var factor = HeightFactor(transform.position.y);
                var moved = transform.position + world * (PanUnitsPerSecond * factor * deltaSeconds);

                var bounded = ClampToMap(new Vector2(moved.x, moved.z));
                transform.position = new Vector3(bounded.x, moved.y, bounded.y);
            }

            if (zoom != 0f)
            {
                var candidate = transform.position + transform.forward * (zoom * ZoomUnitsPerSecond * deltaSeconds);

                if (candidate.y >= MinHeight && candidate.y <= MaxHeight)
                {
                    transform.position = candidate;
                }
            }
        }

        /// <summary>
        /// Eight keys collapsed to one ground-plane direction, in the camera's own forward/right —
        /// x is right, y is forward. Zero when nothing is pressed or opposite keys cancel out.
        /// </summary>
        public static Vector2 ResolvePan(bool forward, bool back, bool left, bool right)
        {
            var axis = new Vector2(
                (right ? 1f : 0f) - (left ? 1f : 0f),
                (forward ? 1f : 0f) - (back ? 1f : 0f));

            return axis == Vector2.zero ? Vector2.zero : axis.normalized;
        }

        /// <summary>+1 zooming in, -1 zooming out, 0 for neither or both held at once.</summary>
        public static float ResolveZoom(bool zoomIn, bool zoomOut)
        {
            if (zoomIn == zoomOut)
            {
                return 0f;
            }

            return zoomIn ? 1f : -1f;
        }

        /// <summary>
        /// How much faster than <see cref="PanUnitsPerSecond"/> to pan at a given camera height.
        /// Crossing the whole map should take roughly the same few seconds whether the player is
        /// zoomed in on one district or looking at all five at once, which a fixed speed cannot do.
        /// </summary>
        public static float HeightFactor(float cameraHeight) =>
            Mathf.Clamp(cameraHeight / ReferenceHeight, MinHeightFactor, MaxHeightFactor);

        /// <summary>Keeps the camera's ground point within <see cref="PanMargin"/> of the map square.</summary>
        public static Vector2 ClampToMap(Vector2 groundPosition) => new(
            Mathf.Clamp(groundPosition.x, -PanMargin, CityLayout.Size + PanMargin),
            Mathf.Clamp(groundPosition.y, -PanMargin, CityLayout.Size + PanMargin));

        private static Vector3 FlattenToGround(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }
    }
}
