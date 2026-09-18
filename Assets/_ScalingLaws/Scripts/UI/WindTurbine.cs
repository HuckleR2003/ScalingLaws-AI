using UnityEngine;

namespace ScalingLaws.UI
{
    /// <summary>
    /// A turbine on the ridge, turning.
    ///
    /// **The whole component is the turning.** The tower, the nacelle and the blades are built by
    /// <c>CityWindFarm</c> out of primitives and stand still on their own; this spins the rotor so the
    /// hills behind Silicon Valley are not a photograph. The speed is per turbine and gently
    /// different, because a row of turbines all sweeping in step reads as one object, not as
    /// machinery.
    ///
    /// **Unscaled time**, so they keep turning while the game is paused: the map is read while the
    /// simulation is stopped more often than while it runs.
    /// </summary>
    public sealed class WindTurbine : MonoBehaviour
    {
        [SerializeField] private Transform rotor;
        [SerializeField] private float degreesPerSecond = 26f;

        /// <summary>Handed its rotor and its own speed by the builder.</summary>
        public void Drive(Transform spinning, float speed)
        {
            rotor = spinning;
            degreesPerSecond = speed;
        }

        private void Update()
        {
            if (rotor == null)
            {
                return;
            }

            rotor.Rotate(Vector3.forward, degreesPerSecond * Time.unscaledDeltaTime, Space.Self);
        }
    }
}
