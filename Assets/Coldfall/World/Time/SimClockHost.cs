using Coldfall.Sim;
using UnityEngine;

// See the note in SimClock.cs: a namespace called Time would shadow UnityEngine.Time for the
// whole assembly, so the folder keeps the name and the namespace does not.
namespace Coldfall.World
{
    /// <summary>
    /// The scene-side owner of the one <see cref="SimClock"/>. Lives in the bootstrap scene
    /// (docs/09-technical-architecture.md, 9.4), which is never unloaded, so the clock survives
    /// every chunk and interior load.
    /// <para>
    /// This component's only job is to hand the clock a real-time delta once per frame. All
    /// simulation work happens in the clock's own tick events, which is what keeps simulation
    /// out of <c>Update</c> as 9.1 demands. Nothing else in the game may advance time.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class SimClockHost : MonoBehaviour
    {
        [Tooltip("Game seconds per real second. 20 means one real minute is twenty game minutes.")]
        [SerializeField] private double timeScale = SimClock.DefaultTimeScale;

        [Tooltip("Start paused, so a loading screen or a menu does not burn game time.")]
        [SerializeField] private bool startPaused;

        /// <summary>
        /// The active clock, or null outside play mode. Systems that need the time should prefer
        /// an injected reference; this exists for the editor tools and for code that genuinely
        /// has no other route to it.
        /// </summary>
        public static SimClock Current { get; private set; }

        /// <summary>The clock this host owns.</summary>
        public SimClock Clock { get; private set; }

        private void Awake()
        {
            if (Current != null && Current != Clock)
            {
                Debug.LogError(
                    $"A second {nameof(SimClockHost)} was loaded. There must be exactly one, in " +
                    "the bootstrap scene; two clocks mean two simulations.",
                    this);
                enabled = false;
                return;
            }

            Clock = new SimClock
            {
                TimeScale = timeScale,
                IsPaused = startPaused,
            };

            Current = Clock;
        }

        private void OnDestroy()
        {
            if (Current == Clock)
            {
                Current = null;
            }
        }

        private void Update()
        {
            // unscaledDeltaTime on purpose: Unity's Time.timeScale is a debugging and
            // slow-motion tool, not this game's notion of time compression. Game time
            // compression lives in SimClock.TimeScale and nowhere else.
            Clock?.AdvanceReal(Time.unscaledDeltaTime);
        }

        private void OnValidate()
        {
            if (timeScale <= 0d)
            {
                timeScale = SimClock.DefaultTimeScale;
            }

            if (Clock == null)
            {
                return;
            }

            Clock.TimeScale = timeScale;
            Clock.IsPaused = startPaused;
        }
    }
}
