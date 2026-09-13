using System.Collections.Generic;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The founder, walking around the room they work in.
    ///
    /// **The room has had a `Waypoints` group and an empty `Staff` group since it was generated and
    /// nothing has ever moved between them.** This is the half that moves a model; `FounderRoutine`
    /// is the half that decides where it should go, and it knows nothing about Unity so it can be
    /// tested without opening a scene.
    ///
    /// Waypoints rather than a navmesh, which is the decision already recorded for this scene: at an
    /// orthographic camera nine metres out the difference is invisible and the complexity is not.
    ///
    /// **Every animation state is optional.** The controller is looked up by name and a missing
    /// clip leaves the model in whatever it was already playing rather than throwing, so the walk
    /// works today with the one Idle clip the pack shipped and gets better the moment real clips
    /// land in the folder. A scene component that requires art to not crash is a scene component
    /// that blocks the art.
    /// </summary>
    public sealed class OfficeActor : MonoBehaviour
    {
        /// <summary>Metres a second. Slow: this is somebody crossing their own living room.</summary>
        public const float WalkSpeed = 1.35f;

        /// <summary>How close counts as arrived. Larger than it looks, because the model has width.</summary>
        public const float ArriveDistance = 0.18f;

        /// <summary>
        /// Degrees a second the model turns to face where it is going.
        ///
        /// **220, down from 520, which was a snap.** Half a turn in a tenth of a second is a
        /// direction change between two frames, so a walk across the floor read as a series of
        /// instant decisions rather than as somebody walking. A person turns about this fast.
        /// </summary>
        public const float TurnSpeed = 220f;

        /// <summary>
        /// How much of the walking speed is left while the model is still turning.
        ///
        /// **Because it used to leave at full speed facing the wrong way**, which is a person
        /// sliding sideways for a fifth of a second at every corner. Slowing into a turn and
        /// gathering speed out of it is most of what makes a walk look deliberate.
        /// </summary>
        public const float TurningSpeedFloor = 0.3f;

        [SerializeField] private Transform waypointRoot;
        [SerializeField] private Animator animator;

        private readonly Dictionary<string, Transform> waypoints = new();
        private readonly List<Transform> route = new();

        private int leg;
        private FounderTask task = FounderTask.Working;

        /// <summary>True once the last waypoint of the current route has been reached.</summary>
        public bool HasArrived => leg >= route.Count;

        /// <summary>What the founder is currently doing. Set by the shell, never decided here.</summary>
        public FounderTask Task => task;

        private void Awake()
        {
            if (waypointRoot == null)
            {
                // The builder writes the group; finding it by name means the prefab does not have to
                // be re-wired every time it is regenerated.
                var found = GameObject.Find("Waypoints");
                waypointRoot = found != null ? found.transform : null;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            IndexWaypoints();
            Send(FounderTask.Working);
        }

        /// <summary>
        /// Points the actor at a different room's walking points and restarts the current walk.
        ///
        /// **Called whenever the lease changes.** Without it the founder keeps the points they
        /// were indexed with at spawn, which is the house, and a rented floor has no mezzanine to
        /// put a bed on: the founder sat down three metres above the floor.
        ///
        /// Idempotent. Handing it the group it already has does nothing, so the shell can offer it
        /// every frame without restarting a walk halfway through.
        /// </summary>
        public void UseWaypoints(Transform root)
        {
            if (root == null || ReferenceEquals(root, waypointRoot))
            {
                return;
            }

            waypointRoot = root;
            IndexWaypoints();

            // The route is a list of transforms from the old room, every one of them now in the
            // wrong place or gone. Rebuilt against the new room by asking for the same task again.
            var current = task;
            route.Clear();
            leg = 0;
            task = FounderTask.Away;
            Send(current);
        }

        private void IndexWaypoints()
        {
            waypoints.Clear();
            if (waypointRoot == null)
            {
                return;
            }

            foreach (Transform child in waypointRoot)
            {
                waypoints[child.name] = child;
            }
        }

        /// <summary>
        /// Starts the walk for a task.
        ///
        /// Public and idempotent: sending the task that is already running does nothing, so the
        /// shell can call this every day without restarting a walk halfway through.
        /// </summary>
        public void Send(FounderTask next)
        {
            if (next == task && route.Count > 0)
            {
                return;
            }

            task = next;
            route.Clear();
            leg = 0;

            foreach (var name in FounderRoutine.RouteFor(next))
            {
                if (waypoints.TryGetValue(name, out var point))
                {
                    route.Add(point);
                }
            }

            // A route with nothing in it would leave the founder standing wherever they were, which
            // is the correct answer for Away and a silent failure for everything else.
            if (route.Count == 0 && next != FounderTask.Away)
            {
                Debug.LogWarning($"[Scaling Laws] No waypoints for {next}. The room was built before "
                    + "these existed; run Scaling Laws > Build office room.");
            }

            SetBool("Walking", route.Count > 0);
        }

        private void Update()
        {
            if (HasArrived)
            {
                return;
            }

            var target = route[leg];
            if (target == null)
            {
                leg++;
                return;
            }

            // Flat distance. The mezzanine is a metre and a half up and the walker is on the stairs
            // for part of the trip; measuring in three dimensions would make it stop short.
            var here = transform.position;
            var there = target.position;
            var flat = new Vector3(there.x - here.x, 0f, there.z - here.z);

            if (flat.sqrMagnitude <= ArriveDistance * ArriveDistance)
            {
                leg++;

                if (!HasArrived)
                {
                    // **A corner is not an arrival.** Snapping onto every waypoint on the way
                    // jumped the model up to eighteen centimetres each time it rounded one, which
                    // at this camera is a visible twitch. Only the end of a route is a place the
                    // model has to be exactly.
                    return;
                }

                transform.position = there;

                // A seat says which way it faces. Without this the founder sat down facing
                // whichever way they last walked in from, which from the front aisle is sideways
                // to their own monitor.
                if (target.GetComponent<SeatFacing>() != null)
                {
                    transform.rotation = target.rotation;
                }

                SetBool("Walking", false);
                Play(FounderRoutine.ClipFor(task));

                return;
            }

            var facing = Quaternion.LookRotation(flat.normalized, Vector3.up);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, facing, TurnSpeed * Time.deltaTime);

            // How much of the turn is done. The dot product of two normalised directions is the
            // cosine of the angle between them, so this is 1 facing the target and 0 at a right
            // angle to it.
            var aligned = Mathf.Clamp01(Vector3.Dot(transform.forward, flat.normalized));

            var step = WalkSpeed * Time.deltaTime * Mathf.Lerp(TurningSpeedFloor, 1f, aligned);

            transform.position = Vector3.MoveTowards(here, new Vector3(there.x, here.y, there.z), step);

            // The stairs are the one place the height has to be taken as well, and taking it
            // proportionally rather than in a straight line is what stops the model floating up the
            // moment it starts walking towards them.
            transform.position += Vector3.up * Mathf.Clamp(
                (there.y - transform.position.y) * step * 2f, -step, step);
        }

        /// <summary>A parameter, if the controller has one. Silent when it does not.</summary>
        private void SetBool(string name, bool value)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == name && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(name, value);
                    return;
                }
            }
        }

        /// <summary>A state, if the controller has one. Silent when it does not.</summary>
        private void Play(string state)
        {
            if (animator == null || animator.runtimeAnimatorController == null
                || string.IsNullOrEmpty(state))
            {
                return;
            }

            if (animator.HasState(0, Animator.StringToHash(state)))
            {
                animator.CrossFade(state, 0.2f);
            }
        }
    }
}
