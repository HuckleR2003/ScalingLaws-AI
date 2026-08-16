using System.Collections.Generic;
using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// The opening of a new company: dark, headlights, the car reversing in, then the lights coming
    /// on one at a time.
    ///
    /// **The room is dark before it is lit, and that is the whole trick.** Every light in the office
    /// prefab is switched off on the first frame, the car's headlamps are the only source, and they
    /// sweep the wall as it backs in. Then the room comes up lamp by lamp, four tenths of a second
    /// apart, which is what makes it read as somebody walking in and flicking switches rather than
    /// as a fade.
    ///
    /// Driven by a MonoBehaviour rather than by the shell's schedule because it moves transforms
    /// every frame, and because it has to survive the shell rebuilding a screen underneath it.
    /// </summary>
    public sealed class ArrivalSequence : MonoBehaviour
    {
        /// <summary>Seconds between one lamp coming on and the next.</summary>
        public const float LampDelay = 0.4f;

        /// <summary>How long the car takes to back into the garage.</summary>
        public const float ReverseSeconds = 3.4f;

        /// <summary>Seconds of black before anything moves.</summary>
        public const float HoldBlack = 0.6f;

        /// <summary>How long the founder waits in the car before getting out.</summary>
        public const float DoorPause = 0.7f;

        private readonly List<Light> roomLights = new();
        private readonly List<float> roomIntensities = new();

        private Transform car;
        private Transform founder;
        private Light leftBeam;
        private Light rightBeam;

        private Vector3 carStart;
        private Vector3 carEnd;

        private float clock;
        private int lampsLit;
        private bool running;

        /// <summary>True while the sequence is playing. The shell holds the clock during it.</summary>
        public bool IsPlaying => running;

        /// <summary>
        /// Finds the room and sets it up dark.
        ///
        /// Returns false when there is nothing to animate — no office prefab, no waypoints — and a
        /// caller that gets false simply carries on with a lit room. An opening sequence that
        /// blocks the game when a scene is missing is worse than no opening sequence.
        /// </summary>
        public bool Prepare(Transform officeRoom, Transform founderModel)
        {
            if (officeRoom == null)
            {
                return false;
            }

            founder = founderModel;

            var garage = officeRoom.Find("Waypoints/Garage");
            var carPoint = officeRoom.Find("Waypoints/Car");

            if (garage == null || carPoint == null)
            {
                return false;
            }

            // Every point light in the room, remembered at its authored brightness and then killed.
            foreach (var light in officeRoom.GetComponentsInChildren<Light>(true))
            {
                if (light.type != LightType.Point)
                {
                    continue;
                }

                roomLights.Add(light);
                roomIntensities.Add(light.intensity);
                light.intensity = 0f;
            }

            if (roomLights.Count == 0)
            {
                return false;
            }

            // The car. A box is enough: this is two seconds of silhouette in the dark, and the
            // headlamps are the part anybody looks at.
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ArrivalCar";
            body.transform.SetParent(officeRoom, false);
            body.transform.localScale = new Vector3(1.9f, 1.4f, 4.3f);

            Destroy(body.GetComponent<BoxCollider>());
            car = body.transform;

            carStart = carPoint.position;
            carEnd = garage.position;

            // Facing the way it will travel, so reversing looks like reversing: the lamps point
            // back out of the garage the whole way in.
            var facing = (carStart - carEnd).normalized;
            car.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.z), Vector3.up);
            car.position = carStart;

            leftBeam = Headlamp(car, new Vector3(-0.65f, 0.1f, 2.1f));
            rightBeam = Headlamp(car, new Vector3(0.65f, 0.1f, 2.1f));

            if (founder != null)
            {
                founder.gameObject.SetActive(false);
            }

            clock = 0f;
            lampsLit = 0;
            running = true;
            return true;
        }

        private static Light Headlamp(Transform parent, Vector3 offset)
        {
            var lamp = new GameObject("Headlamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = offset;

            var light = lamp.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 44f;
            light.range = 22f;
            light.intensity = 4.2f;
            light.color = new Color(1f, 0.97f, 0.88f);

            return light;
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }

            clock += Time.unscaledDeltaTime;

            if (clock < HoldBlack)
            {
                return;
            }

            var driving = Mathf.Clamp01((clock - HoldBlack) / ReverseSeconds);

            if (driving < 1f)
            {
                // Eased at both ends, because a car that arrives at constant speed and stops dead
                // is a prop being slid rather than somebody parking.
                car.position = Vector3.Lerp(carStart, carEnd, Mathf.SmoothStep(0f, 1f, driving));
                return;
            }

            car.position = carEnd;

            var since = clock - HoldBlack - ReverseSeconds;

            if (since < DoorPause)
            {
                return;
            }

            if (founder != null && !founder.gameObject.activeSelf)
            {
                founder.gameObject.SetActive(true);
            }

            // The lamps, one every four tenths of a second, in the order the builder made them —
            // which runs from the living room to the racks, so the room fills up rather than
            // lighting from both ends at once.
            var due = Mathf.FloorToInt((since - DoorPause) / LampDelay) + 1;

            while (lampsLit < due && lampsLit < roomLights.Count)
            {
                roomLights[lampsLit].intensity = roomIntensities[lampsLit];
                lampsLit++;
            }

            if (lampsLit < roomLights.Count)
            {
                return;
            }

            // The headlamps go off once the room is up. Leaving them burning inside a lit garage is
            // the detail that would say nobody thought about it.
            if (leftBeam != null)
            {
                leftBeam.intensity = 0f;
            }

            if (rightBeam != null)
            {
                rightBeam.intensity = 0f;
            }

            running = false;
        }

        /// <summary>Puts everything back the way it was, whether or not the sequence finished.</summary>
        public void Finish()
        {
            for (var index = 0; index < roomLights.Count; index++)
            {
                if (roomLights[index] != null)
                {
                    roomLights[index].intensity = roomIntensities[index];
                }
            }

            if (founder != null)
            {
                founder.gameObject.SetActive(true);
            }

            if (car != null)
            {
                Destroy(car.gameObject);
            }

            running = false;
        }
    }
}
