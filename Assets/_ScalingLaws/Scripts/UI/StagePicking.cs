using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.UI
{
    /// <summary>
    /// Where a click on a room actually landed.
    ///
    /// Both 3D rooms in this game are a camera rendering into a texture that an element draws as its
    /// background, so a click has to travel through the same crop the picture did before it can
    /// become a ray. **The element and the texture are not the same shape.** The background is drawn
    /// to cover, which crops the long axis, so a pointer position is not a texture position and the
    /// difference is a hundred pixels at the sizes this game uses.
    ///
    /// This lived inside `ServerRoomScreen` and is here because the office needed it too. **One copy
    /// on purpose**: the basement's own note already says what two copies of this arithmetic buy,
    /// which is a click that lands one square from the cursor with the picture right, the selection
    /// wrong, and every test green.
    /// </summary>
    public static class StagePicking
    {
        /// <summary>
        /// Pointer position inside an element to a camera viewport point, or false when it landed on
        /// the cropped-away part of the picture.
        ///
        /// Returns the point in the camera's own convention: x and y from 0 to 1, y measured up from
        /// the bottom, which is the opposite of the way UI Toolkit measures.
        /// </summary>
        public static bool TryViewport(VisualElement view, Vector2 local, Texture texture,
            out Vector2 viewport)
        {
            viewport = default;

            if (view == null || texture == null)
            {
                return false;
            }

            var rect = view.contentRect;

            if (rect.width <= 1f || rect.height <= 1f || float.IsNaN(rect.width))
            {
                return false;
            }

            // Cover, not fit: the larger of the two ratios, which is what a background image drawn
            // to fill an element does.
            var scale = Mathf.Max(rect.width / texture.width, rect.height / texture.height);

            var drawnWidth = texture.width * scale;
            var drawnHeight = texture.height * scale;

            var originX = (rect.width - drawnWidth) / 2f;
            var originY = (rect.height - drawnHeight) / 2f;

            viewport = new Vector2(
                (local.x - originX) / drawnWidth,

                // UI Toolkit measures down from the top and a camera measures up from the bottom.
                1f - (local.y - originY) / drawnHeight);

            return viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }

        /// <summary>
        /// What a click hit in the room, walking up from the collider to the object that carries a
        /// <typeparamref name="T"/>.
        ///
        /// **Up the hierarchy rather than on the collider itself**, because a character's collider
        /// sits on the model and the component that knows who they are sits on the root the game
        /// spawned. Testing only the collider finds nothing and looks exactly like a missing raycast.
        /// </summary>
        public static T Under<T>(Camera camera, Vector2 viewport, float distance = 200f)
            where T : Component
        {
            if (camera == null)
            {
                return null;
            }

            var ray = camera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));

            // Every hit, sorted, rather than the first: a name plate or a piece of furniture in
            // front of somebody must not swallow the click meant for them.
            var hits = Physics.RaycastAll(ray, distance);

            if (hits.Length == 0)
            {
                return null;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var found = hit.collider.GetComponentInParent<T>();

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
