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

        /// <summary>Degrees a second the model turns to face where it is going.</summary>
        public const float TurnSpeed = 520f;

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
                transform.position = there;
                leg++;

                if (HasArrived)
                {
                    SetBool("Walking", false);
                    Play(FounderRoutine.ClipFor(task));
                }

                return;
            }

            var step = WalkSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(here, new Vector3(there.x, here.y, there.z), step);

            // The stairs are the one place the height has to be taken as well, and taking it
            // proportionally rather than in a straight line is what stops the model floating up the
            // moment it starts walking towards them.
            transform.position += Vector3.up * Mathf.Clamp(
                (there.y - transform.position.y) * step * 2f, -step, step);

            var facing = Quaternion.LookRotation(flat.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, facing, TurnSpeed * Time.deltaTime);
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
