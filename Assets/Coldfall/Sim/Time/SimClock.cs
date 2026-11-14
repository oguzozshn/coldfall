using System;

// Namespace deliberately does not follow the folder. A namespace called Time nested under
// Coldfall.Sim shadows UnityEngine.Time for every file in Coldfall.Sim, so `Time.deltaTime`
// stops compiling across the assembly. The folder keeps the name; the namespace does not.
namespace Coldfall.Sim
{
    /// <summary>
    /// Which of the three simulation ticks a handler is reacting to.
    /// </summary>
    public enum SimTickKind
    {
        /// <summary>Calories, hydration, fatigue, core temperature, wetness integration.</summary>
        Needs,

        /// <summary>Wound progression, infection, illness, healing, frostbite.</summary>
        Body,

        /// <summary>Spoilage, fuel burn, drying, weather, thermal diffusion, scent, meta-simulation.</summary>
        World,
    }

    /// <summary>
    /// The single authoritative clock for the whole simulation. Implements the four-rate tick
    /// model in docs/09-technical-architecture.md, 9.1: physics runs on the engine's own fixed
    /// step, and the three simulation rates below are driven from here.
    /// <para>
    /// No gameplay system may integrate time on its own, read a frame delta, or keep its own
    /// notion of "what time is it". Everything comes from this class.
    /// </para>
    /// <para>
    /// <b>Why the steps are expressed in game time.</b> Document 9.1 states the three rates in
    /// real time (needs 1 Hz, body 0.2 Hz, world one per game-minute), while the M0 exit
    /// criteria require that changing the time scale changes no simulation outcome at all. Those
    /// two only hold together if the ticks are scheduled on accumulated <i>game</i> time, so
    /// that is what this clock does. The step sizes are chosen to coincide with the documented
    /// real-time rates at the default 1:20 scale, which makes the two readings agree in the only
    /// case the player actually plays in.
    /// </para>
    /// <para>
    /// The consequence worth remembering: one game hour always contains exactly 180 needs ticks,
    /// 36 body ticks and 60 world ticks, whatever the time scale and whatever the frame rate.
    /// That is why every rate in docs/03-survival-systems.md is stated per game hour.
    /// </para>
    /// </summary>
    public sealed class SimClock
    {
        /// <summary>Game seconds between needs ticks. One real second at the 1:20 default.</summary>
        public const double NeedsStepGameSeconds = 20.0;

        /// <summary>Game seconds between body ticks. Five real seconds at the 1:20 default.</summary>
        public const double BodyStepGameSeconds = 100.0;

        /// <summary>Game seconds between world ticks. One game-minute, by definition.</summary>
        public const double WorldStepGameSeconds = 60.0;

        /// <summary>Game seconds that pass per real second by default: a 1:20 compression.</summary>
        public const double DefaultTimeScale = 20.0;

        /// <summary>The hour a new game begins at. Day structure in docs/02-core-loop.md, 2.4.</summary>
        public const int StartHourOfDay = 6;

        private const double SecondsPerMinute = 60.0;
        private const double SecondsPerHour = 3600.0;
        private const double SecondsPerDay = 86400.0;

        /// <summary>
        /// Upper bound on how many steps of one rate a single advance call may drain. It exists
        /// only to stop a pathological delta (a breakpoint, a stalled frame, a bad fast-forward
        /// argument) from locking the main thread, and it is deliberately generous: a full
        /// in-game day of needs ticks is 4,320.
        /// </summary>
        public const int MaxStepsPerAdvance = 20000;

        private double _timeScale = DefaultTimeScale;
        private double _elapsedGameSeconds;
        private double _needsAccumulator;
        private double _bodyAccumulator;
        private double _worldAccumulator;

        /// <summary>
        /// Raised once per needs step. The argument is the step length in game seconds, which is
        /// always <see cref="NeedsStepGameSeconds"/>; it is passed so handlers integrate against
        /// the step rather than against a captured constant.
        /// </summary>
        public event Action<double> NeedsTick;

        /// <summary>Raised once per body step. The argument is the step length in game seconds.</summary>
        public event Action<double> BodyTick;

        /// <summary>Raised once per world step. The argument is the step length in game seconds.</summary>
        public event Action<double> WorldTick;

        /// <summary>Total game time elapsed since the run began, in game seconds.</summary>
        public double ElapsedGameSeconds => _elapsedGameSeconds;

        /// <summary>Total game time elapsed since the run began, in game minutes.</summary>
        public double ElapsedGameMinutes => _elapsedGameSeconds / SecondsPerMinute;

        /// <summary>Total game time elapsed since the run began, in game hours.</summary>
        public double ElapsedGameHours => _elapsedGameSeconds / SecondsPerHour;

        /// <summary>
        /// Game seconds that pass per real second. The 1:20 default means one real minute is
        /// twenty game minutes. Must be greater than zero; pause with <see cref="IsPaused"/>
        /// rather than by setting this to zero.
        /// </summary>
        public double TimeScale
        {
            get => _timeScale;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "TimeScale must be a positive, finite number. Use IsPaused to stop the clock.");
                }

                _timeScale = value;
            }
        }

        /// <summary>
        /// While true, <see cref="AdvanceReal"/> does nothing. Explicit game-time advances still
        /// work, so sleeping and other deliberate fast-forwards are unaffected by pausing.
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>The current in-game day, counting the first day as day 1.</summary>
        public int Day => 1 + (int)((StartHourOfDay * SecondsPerHour + _elapsedGameSeconds) / SecondsPerDay);

        /// <summary>The hour of the current in-game day, 0 to 23.</summary>
        public int HourOfDay => (int)(SecondsOfDay / SecondsPerHour);

        /// <summary>The minute of the current in-game hour, 0 to 59.</summary>
        public int MinuteOfHour => (int)(SecondsOfDay % SecondsPerHour / SecondsPerMinute);

        /// <summary>Seconds elapsed in the current in-game day, 0 to 86,399.</summary>
        public double SecondsOfDay => (StartHourOfDay * SecondsPerHour + _elapsedGameSeconds) % SecondsPerDay;

        /// <summary>
        /// Advance the clock by a real-time delta, scaled by <see cref="TimeScale"/>. This is the
        /// only call presentation code should make, once per frame. Does nothing while paused.
        /// </summary>
        /// <param name="realDeltaSeconds">Real seconds elapsed. Must not be negative.</param>
        public void AdvanceReal(double realDeltaSeconds)
        {
            if (realDeltaSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(realDeltaSeconds), realDeltaSeconds, "Time does not run backwards.");
            }

            if (IsPaused || realDeltaSeconds == 0.0)
            {
                return;
            }

            AdvanceGame(realDeltaSeconds * _timeScale);
        }

        /// <summary>
        /// Advance the clock by an explicit amount of game time, ignoring both the time scale and
        /// the paused state. This is how sleeping and other deliberate fast-forwards move time:
        /// the steps still fire at their normal resolution, so nothing is skipped.
        /// </summary>
        /// <param name="gameDeltaSeconds">Game seconds to advance. Must not be negative.</param>
        public void AdvanceGame(double gameDeltaSeconds)
        {
            if (gameDeltaSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gameDeltaSeconds), gameDeltaSeconds, "Time does not run backwards.");
            }

            if (gameDeltaSeconds == 0.0)
            {
                return;
            }

            _elapsedGameSeconds += gameDeltaSeconds;
            _needsAccumulator += gameDeltaSeconds;
            _bodyAccumulator += gameDeltaSeconds;
            _worldAccumulator += gameDeltaSeconds;

            Drain(ref _needsAccumulator, NeedsStepGameSeconds, NeedsTick);
            Drain(ref _bodyAccumulator, BodyStepGameSeconds, BodyTick);
            Drain(ref _worldAccumulator, WorldStepGameSeconds, WorldTick);
        }

        /// <summary>
        /// Reset the clock to the start of day 1 and clear every pending partial step. Does not
        /// detach handlers and does not change <see cref="TimeScale"/>.
        /// </summary>
        public void Reset()
        {
            _elapsedGameSeconds = 0.0;
            _needsAccumulator = 0.0;
            _bodyAccumulator = 0.0;
            _worldAccumulator = 0.0;
        }

        private static void Drain(ref double accumulator, double step, Action<double> handler)
        {
            var fired = 0;
            while (accumulator >= step)
            {
                accumulator -= step;
                fired++;

                handler?.Invoke(step);

                if (fired < MaxStepsPerAdvance)
                {
                    continue;
                }

                // Discard the rest rather than stalling. Losing simulated time is bad, but a
                // frozen main thread is worse, and reaching this point means a caller passed a
                // delta no legitimate code path produces.
                accumulator = 0.0;
                return;
            }
        }
    }
}
