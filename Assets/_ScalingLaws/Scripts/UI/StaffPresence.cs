using System;
using System.Collections.Generic;
using ScalingLaws.Data;
using ScalingLaws.Simulation;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The people the company has hired, standing in the office.
    ///
    /// **The room has had a `Staff` group since the day it was generated and only ever held the
    /// founder.** A company could hire twelve people, pay them every day, watch them move research
    /// and reliability and brand, and the room they were supposedly in was empty except for the
    /// player. That is the same gap the team page had before names were put on it: the game knew and
    /// never showed it.
    ///
    /// Spawned rather than placed, for the reason the founder is: `Game.unity` holds 107 hand-placed
    /// prefab instances and a guard refuses to regenerate it, so nothing new goes in there by hand
    /// if code can put it in.
    ///
    /// **Rebuilt only when the roster changes.** The office is re-dressed on every tab change and
    /// destroying and re-instantiating a dozen skinned meshes each time is the kind of cost that
    /// does not show up until somebody has a full floor.
    /// </summary>
    public sealed class StaffPresence
    {
        /// <summary>The group the room builder writes, shared with the founder.</summary>
        public const string StaffGroup = FounderPresence.StaffGroup;

        /// <summary>What a spawned employee is called, so the founder is never mistaken for one.</summary>
        public const string NamePrefix = "Employee ";

        /// <summary>
        /// How far apart people stand, in metres.
        ///
        /// They are placed on a grid rather than at desks. The rooms are generated at four different
        /// sizes and only some of them carry desk markers, so anchoring to desks would put a full
        /// floor's worth of people on top of each other in the two rooms that have none. A grid is
        /// honest about being a placeholder and never overlaps.
        /// </summary>
        public const float Spacing = 1.4f;

        /// <summary>Rows before the grid wraps. Wide rather than deep: the camera looks along z.</summary>
        public const int PerRow = 5;

        /// <summary>The desks the room itself was built with, and what a chair is called.</summary>
        public const string DeskGroup = "FixedDesks";

        /// <inheritdoc cref="DeskGroup"/>
        public const string ChairPrefix = "Chair";

        private readonly Func<CompanyState> state;

        /// <summary>
        /// The room that is actually on screen, or null when there is none.
        ///
        /// **Reported as the staff standing outside the scene, on the left.** They were placed on
        /// a grid in the `Staff` group's own local space, which is the house's origin: fine in a
        /// twelve metre garage and, on a sixteen metre floor, a row of people standing two metres
        /// in front of the near wall, off the floor, in the dark, with their name plates the only
        /// thing the player could see of them.
        ///
        /// A room knows where its own desks are. This is how to ask it.
        /// </summary>
        private readonly Func<Transform> room;

        private readonly List<GameObject> spawned = new();

        private int shownCount = -1;
        private string shownSignature = string.Empty;

        public StaffPresence(Func<CompanyState> state, Func<Transform> room = null)
        {
            this.state = state;
            this.room = room;
        }

        /// <summary>How many people are actually standing in the room. Read by the guard.</summary>
        public int Standing
        {
            get
            {
                var count = 0;

                foreach (var person in spawned)
                {
                    if (person != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Puts the right people in the room.
        ///
        /// Cheap to call on every repaint: it builds a short signature of the roster and returns
        /// without touching the scene when nothing has changed.
        /// </summary>
        public void Refresh()
        {
            var company = state?.Invoke();

            if (company == null)
            {
                return;
            }

            var group = GameObject.Find(StaffGroup);

            if (group == null)
            {
                // No office scene loaded. Not a failure: the shell runs in tests and in the menu.
                return;
            }

            var hires = company.Staff.Hires;
            var signature = Signature(hires);

            if (signature == shownSignature && shownCount == hires.Count)
            {
                return;
            }

            shownSignature = signature;
            shownCount = hires.Count;

            Clear();

            var seat = 0;

            for (var index = 0; index < hires.Count; index++)
            {
                // **Remote contractors are not in the building.** They were spawned into the room
                // like everybody else, took a chair each and stood about the office of a company
                // they have never visited, which the author reported as an old bug. They keep their
                // place in the list as an empty slot, so the index a click opens still names the
                // right person, and they take no chair from somebody who does come in.
                if (hires[index].Source == HireSource.Remote)
                {
                    spawned.Add(null);
                    continue;
                }

                Spawn(group.transform, hires[index], index, seat++);
            }

            // A fresh set of people has nobody hidden yet, so the next `SetHour` has to do the work
            // even if the hour has not moved.
            shownHour = -1;
        }

        /// <summary>
        /// What the roster looks like, cheaply.
        ///
        /// Names and roles rather than a hash of everything: a bonus paid or an hour changed does
        /// not move anybody in the room, and rebuilding twelve skinned meshes because somebody
        /// clicked a schedule would be work nobody asked for.
        /// </summary>
        private static string Signature(IReadOnlyList<Hire> hires)
        {
            var text = new System.Text.StringBuilder(hires.Count * 12);

            foreach (var hire in hires)
            {
                text.Append(hire.Name).Append('/').Append((int)hire.Role).Append(';');
            }

            return text.ToString();
        }

        /// <summary>
        /// Puts one person somewhere a person could be.
        ///
        /// **At their own chair when the room has one**, which is the whole point of a lease that
        /// says how many desks it comes with: the tenth hire sits at the tenth desk. A chair found
        /// by name, because the builder writes `FixedDesks/Chair0` upward and the lease charges
        /// for exactly that many.
        ///
        /// The grid is the fallback and stays honest about being one: a company with more people
        /// than desks has people standing, which is also what the desk cap is supposed to feel
        /// like. It is placed inside the room now rather than at the house's origin.
        /// </summary>
        private void Stand(Transform person, int index)
        {
            var floor = room?.Invoke();
            var chair = Seat(floor, index);

            if (chair != null)
            {
                // **On the chair, working.** The author's rule of 2026-09-19: staff never walk and
                // never stand about; they sit at their station and work, and are not there at all
                // outside their hours. Placed exactly where the founder sits down (the chair's own
                // position and facing), and put straight into the typing loop by `Sit`.
                person.position = chair.position;
                person.rotation = chair.rotation;
                return;
            }

            var origin = floor != null ? floor.position : person.parent.position;

            // Along the front of whatever room this is, which is the open side: the camera looks
            // from high x and low z, so this row is the nearest floor to the player.
            person.position = origin + new Vector3(
                1.6f + index % PerRow * Spacing,
                0f,
                1.0f + index / PerRow * Spacing);
        }

        /// <summary>The state a seated employee loops: the founder's own typing clip.</summary>
        public const string WorkingState = "Type";

        /// <summary>
        /// Straight into the typing loop, never through the walk or the sit-down. Each person starts
        /// at a different point in the loop, derived from their index rather than rolled, so a room
        /// of twelve is not twelve people pressing keys in step and the same room looks the same
        /// after a reload.
        /// </summary>
        private static void Sit(GameObject person, int index)
        {
            var animator = person.GetComponentInChildren<Animator>();

            if (animator == null || animator.runtimeAnimatorController == null
                || !animator.HasState(0, Animator.StringToHash(WorkingState)))
            {
                return;
            }

            animator.Play(WorkingState, 0, index * 0.37f % 1f);
        }

        /// <summary>The chair the room built for this hire, or null when it built none.</summary>
        private static Transform Seat(Transform floor, int index)
        {
            var desks = floor == null ? null : floor.Find(DeskGroup);

            return desks == null ? null : desks.Find(ChairPrefix + index);
        }

        private void Spawn(Transform group, Hire hire, int index, int seat)
        {
            var prefab = Resources.Load<GameObject>(FounderPresence.PrefabPath);

            if (prefab == null)
            {
                // Keeps the slot, so the list stays one to one with the roster.
                spawned.Add(null);
                return;
            }

            var person = UnityEngine.Object.Instantiate(prefab, group);
            person.name = NamePrefix + index;

            // **They are not the founder, and the prefab they are made from is.**
            //
            // Reported plainly: the staff walk upstairs in a rented floor and stand inside each
            // other. `Founder.prefab` carries an `OfficeActor`, so every copy of it woke up with
            // the founder's routine, asked for the founder's waypoints and walked the founder's
            // route: eleven people going to one desk, one bed and one car, in step, through each
            // other. The grid below is the placement this class is built on and it is the one that
            // must survive.
            //
            // Destroyed rather than disabled. A disabled `OfficeActor` still runs `Awake`, which
            // is where it indexes the waypoints and sends itself walking.
            if (person.TryGetComponent<OfficeActor>(out var routine))
            {
                UnityEngine.Object.DestroyImmediate(routine);
            }

            Stand(person.transform, seat);
            Sit(person, index);

            person.AddComponent<NamePlate>().Set(
                hire.Name,
                NamePlate.TitleFor(hire.Role),
                NamePlate.ColourFor(hire.Role));

            // **What makes them clickable.** The character packs ship with no collider at all, so a
            // ray through the office camera passed straight through every person in the room. One
            // capsule, sized to a person, on the root the picker walks up to.
            var body = person.AddComponent<CapsuleCollider>();
            body.height = 1.8f;
            body.radius = 0.32f;
            body.center = new Vector3(0f, 0.9f, 0f);

            // Which employee this is, for the panel the click opens. On the root rather than in a
            // dictionary keyed by transform, because the scene is the thing that survives a repaint
            // and a dictionary would have to be kept in step with it.
            person.AddComponent<OfficePerson>().Index = index;

            spawned.Add(person);
        }

        /// <summary>
        /// Which employee is nearest a point in the camera's view, or -1 when nobody is near it.
        ///
        /// **The click path that does not go through physics.** `StagePicking` raycasts, which is
        /// exact when it works and has four ways to come back empty that look identical from the
        /// outside: a capsule on a scaled prefab root, a rig that swallows the ray, the wrong
        /// camera, or a scene with physics not running. A playtest reported the result as "clicking
        /// a person does nothing", which is all any of them look like.
        ///
        /// Projecting is the same arithmetic the build-mode slot markers already use, it needs no
        /// component on the model, and it cannot be defeated by a collider.
        ///
        /// The aim is the body rather than the feet: <see cref="AimHeight"/> up the model, which is
        /// roughly where a person's chest is and where a player points.
        /// </summary>
        public int NearestTo(Camera camera, Vector2 viewport, float radius = 0.055f)
        {
            if (camera == null || spawned.Count == 0)
            {
                return -1;
            }

            // Nulls rather than a shorter list, so an index still means the same person. Filtering
            // the absent out renumbers everybody behind them and opens the wrong card, which from
            // the player's chair looks exactly like the click having missed.
            var aims = new Vector3?[spawned.Count];

            for (var index = 0; index < spawned.Count; index++)
            {
                var person = spawned[index];

                // Somebody off shift is not in the room to be clicked on. Their renderers are off,
                // and a card opened by clicking an empty patch of floor is worse than no card.
                if (person == null || !IsOnShift(index))
                {
                    continue;
                }

                aims[index] = person.transform.position + Vector3.up * AimHeight;
            }

            return NearestIn(camera, viewport, aims, radius);
        }

        /// <summary>
        /// Which of these points is nearest a place in the camera's view, or -1 when none is close.
        ///
        /// **Split out of `NearestTo` so the arithmetic can be measured without a room.** The office
        /// is the one screen where the whole interaction is a click on a person, and testing it used
        /// to need a loaded scene, a character prefab and physics running, which is why none of it
        /// was tested at all. This half is pure geometry and a camera.
        ///
        /// A null entry is somebody who is not there to be clicked, and it keeps its place in the
        /// list so the answer is still an index into the roster.
        /// </summary>
        public static int NearestIn(Camera camera, Vector2 viewport,
            IReadOnlyList<Vector3?> aims, float radius = 0.055f)
        {
            if (camera == null || aims == null)
            {
                return -1;
            }

            var best = -1;
            var bestDistance = radius;

            for (var index = 0; index < aims.Count; index++)
            {
                if (aims[index] == null)
                {
                    continue;
                }

                var view = camera.WorldToViewportPoint(aims[index].Value);

                // Behind the camera. It still projects to a point on screen, which is how an actor
                // standing behind you becomes the nearest thing to the cursor.
                if (view.z <= 0f)
                {
                    continue;
                }

                var distance = Vector2.Distance(new Vector2(view.x, view.y), viewport);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = index;
                }
            }

            return best;
        }

        /// <summary>Where on a person a click is aimed, in metres up from the floor.</summary>
        private const float AimHeight = 1.0f;

        /// <summary>
        /// Whether this employee is currently drawn.
        ///
        /// Read off the renderers rather than recomputed from the clock, so the answer cannot
        /// disagree with what is on screen: they are the same fact.
        /// </summary>
        private bool IsOnShift(int index)
        {
            if (index < 0 || index >= spawned.Count || spawned[index] == null)
            {
                return false;
            }

            foreach (var renderer in spawned[index].GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.enabled)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Shows the people whose shift it is and hides the rest.
        ///
        /// **The first thing in the game that makes the schedule control visible.** Hours were
        /// already saved and already read by loyalty, and the only place they appeared was a panel
        /// nobody opened twice. A control whose effect cannot be seen is a control people set once
        /// and forget about.
        ///
        /// Presentation only, and deliberately: the day still pays a full salary and still does a
        /// full day's work whatever the clock says. Hours are a loyalty and expectation mechanic,
        /// and turning them into a productivity dial is a different design that would have to be
        /// balanced rather than a side effect of drawing somebody.
        ///
        /// Cheap enough for every frame: it compares the hour it last drew and returns.
        /// </summary>
        public void SetHour(double hour)
        {
            var company = state?.Invoke();

            if (company == null || spawned.Count == 0)
            {
                return;
            }

            // Whole hours. The clock sweeps continuously and a person cannot half arrive, so
            // anything finer is work done for a difference nobody can see.
            var now = (int)System.Math.Floor(System.Math.Clamp(hour, 0.0, 23.999));

            if (now == shownHour)
            {
                return;
            }

            shownHour = now;

            var hires = company.Staff.Hires;

            for (var index = 0; index < spawned.Count && index < hires.Count; index++)
            {
                var person = spawned[index];

                if (person == null)
                {
                    continue;
                }

                var hire = hires[index];
                var onDuty = now >= hire.StartHour && now < hire.EndHour;

                // **Gone, name plate and all.** The author's rule of 2026-09-19: somebody outside
                // their hours is not in the office. The plate used to stay up reading "off duty",
                // which left rows of floating labels over empty chairs all night.
                foreach (var renderer in person.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = onDuty;
                }

                // Back into the loop on arrival, or they reappear frozen where they left off.
                if (onDuty)
                {
                    Sit(person, index);
                }

                var plate = person.GetComponent<NamePlate>();

                if (plate != null)
                {
                    plate.SetOnDuty(onDuty, Loc.T("plate.off_duty",
                        NamePlate.TitleFor(hire.Role),
                        hire.StartHour.ToString("00"),
                        hire.EndHour.ToString("00")));
                }
            }
        }

        /// <summary>The hour last drawn, so a frame that changes nothing touches nothing.</summary>
        private int shownHour = -1;

        /// <summary>Takes everybody out, for a rebuild or a move.</summary>
        public void Clear()
        {
            foreach (var person in spawned)
            {
                if (person == null)
                {
                    continue;
                }

                // Destroy is a no-op outside play mode and DestroyImmediate throws inside it. Both
                // matter: the office is dressed from editor tooling as well as from the game.
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(person);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(person);
                }
            }

            spawned.Clear();
        }
    }

    /// <summary>
    /// Which employee a model in the room is.
    ///
    /// A component rather than a lookup keyed by transform, because the scene outlives a repaint and
    /// a dictionary would have to be kept in step with it. The founder does not carry one: index -1
    /// would be a second meaning for the same field, and the picker asks a different question of
    /// them anyway.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficePerson : MonoBehaviour
    {
        /// <summary>Position in <see cref="StaffRoster.Hires"/>, which is what the panel takes.</summary>
        public int Index { get; set; }
    }
}
