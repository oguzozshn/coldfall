using Coldfall.Sim;
using UnityEngine;

namespace Coldfall.World
{
    /// <summary>
    /// An authored room, as a box in the scene. Hand-placed per interior, as docs/09, 9.5 says.
    /// </summary>
    public sealed class ThermalVolumeComponent : MonoBehaviour
    {
        [Tooltip("Local-space size of the room in metres. The transform places it.")]
        [SerializeField] private Vector3 size = new Vector3(10f, 2.8f, 8f);

        [Tooltip("How well the envelope holds heat, 0 to 1. Never 1.")]
        [Range(0f, 0.95f)]
        [SerializeField] private float insulation = 0.8f;

        [Tooltip("How exposed to wind the envelope is, 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float windExposure;

        [Tooltip("Heating power inside, in degree-cubic-metres per game minute. A wood stove is about 500.")]
        [SerializeField] private float heatInput;

        [Tooltip("Starting air temperature. Overwritten by the first diffusion tick.")]
        [SerializeField] private float startCelsius = 8f;

        /// <summary>Index into the simulation arrays, assigned by <see cref="ThermalWorld"/>.</summary>
        public int Index { get; internal set; } = -1;

        /// <summary>Current air temperature, written back after each world tick.</summary>
        public float Celsius { get; internal set; }

        /// <summary>Heating power inside this volume. Fires and stoves write to it.</summary>
        public float HeatInput
        {
            get => heatInput;
            set => heatInput = value;
        }

        /// <summary>World-space bounds of the room.</summary>
        public Bounds WorldBounds => new Bounds(transform.position, Vector3.Scale(size, transform.lossyScale));

        /// <summary>
        /// Sets the authored values. Used by the greybox scene generator, which builds the scene
        /// from code rather than by hand.
        /// </summary>
        public void Configure(
            Vector3 roomSize, float envelopeInsulation, float wind, float heat, float startTemperature)
        {
            size = roomSize;
            insulation = Mathf.Clamp(envelopeInsulation, 0f, 0.95f);
            windExposure = Mathf.Clamp01(wind);
            heatInput = heat;
            startCelsius = startTemperature;
            Celsius = startTemperature;
        }

        private void Awake()
        {
            Celsius = startCelsius;
        }

        /// <summary>Builds the simulation struct for this room.</summary>
        public ThermalVolume ToVolume()
        {
            var bounds = WorldBounds;
            var cubic = Mathf.Max(1f, bounds.size.x * bounds.size.y * bounds.size.z);

            return new ThermalVolume
            {
                Celsius = Celsius,
                Insulation = insulation,
                WindExposure = windExposure,
                CubicMetres = cubic,
                HeatInput = heatInput,
                IsOutdoors = false,
            };
        }

        /// <summary>True if the world-space point is inside this room.</summary>
        public bool Contains(Vector3 point) => WorldBounds.Contains(point);

        private void OnDrawGizmosSelected()
        {
            // Coloured by temperature: the first half of the thermal visualiser docs/09, 9.11
            // calls the tool that pays for itself inside a week.
            var warmth = Mathf.InverseLerp(-20f, 25f, Application.isPlaying ? Celsius : 0f);
            Gizmos.color = Color.Lerp(new Color(0.24f, 0.29f, 0.35f), new Color(0.88f, 0.54f, 0.18f), warmth);
            Gizmos.DrawWireCube(transform.position, Vector3.Scale(size, transform.lossyScale));
        }
    }
}
