using Coldfall.Sim;
using UnityEngine;

namespace Coldfall.World
{
    /// <summary>
    /// The scene-side owner of the one <see cref="WeatherSystem"/>, driven by the world tick.
    /// <para>
    /// Lives in the bootstrap scene beside <see cref="SimClockHost"/> and is never unloaded, so
    /// the weather keeps running while chunks and interiors stream in and out.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class WeatherHost : MonoBehaviour
    {
        [Tooltip("Identifies the world. The same seed always produces the same weather sequence.")]
        [SerializeField] private uint worldSeed = 1u;

        [Tooltip("Day the run begins on, 1-based.")]
        [SerializeField] private int startDay = 1;

        /// <summary>The active weather system, or null outside play mode.</summary>
        public static WeatherSystem Current { get; private set; }

        /// <summary>The system this host owns.</summary>
        public WeatherSystem Weather { get; private set; }

        private SimClock _clock;

        private void Awake()
        {
            Weather = new WeatherSystem(worldSeed, startDay);
            Current = Weather;
        }

        private void OnEnable()
        {
            // Subscribed here rather than in Awake because SimClockHost builds the clock in its
            // own Awake, and execution order only guarantees that the clock exists by now.
            _clock = SimClockHost.Current;
            if (_clock != null)
            {
                _clock.WorldTick += OnWorldTick;
            }
        }

        private void OnDisable()
        {
            if (_clock != null)
            {
                _clock.WorldTick -= OnWorldTick;
                _clock = null;
            }
        }

        private void OnDestroy()
        {
            if (Current == Weather)
            {
                Current = null;
            }
        }

        private void Update()
        {
            // A late subscription, for the case where this host is enabled before the clock is
            // built, such as when a scene is loaded additively.
            if (_clock != null || SimClockHost.Current == null)
            {
                return;
            }

            _clock = SimClockHost.Current;
            _clock.WorldTick += OnWorldTick;
        }

        private void OnWorldTick(double gameSeconds)
        {
            Weather?.Step((float)(gameSeconds / 60d));
        }
    }
}
