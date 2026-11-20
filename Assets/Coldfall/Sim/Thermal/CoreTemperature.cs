using System.Collections.Generic;

namespace Coldfall.Sim
{
    /// <summary>
    /// The hypothermia stages of docs/03-survival-systems.md, 3.3.
    /// </summary>
    public enum HypothermiaStage
    {
        /// <summary>36.5 and above. No effects.</summary>
        Normal,

        /// <summary>35.0 to 36.5. Shivering, visible breath, mild aim sway.</summary>
        Cold,

        /// <summary>33.0 to 35.0. Heavy shivering, reduced stamina and fine motor speed.</summary>
        Mild,

        /// <summary>31.0 to 33.0. Shivering stops, vision desaturates, map memory unreliable.</summary>
        Moderate,

        /// <summary>28.0 to 31.0. Paradoxical warmth, tunnel vision, collapse risk.</summary>
        Severe,

        /// <summary>Below 28.0.</summary>
        Dead,
    }

    /// <summary>
    /// The character's core temperature: the master variable of the whole simulation
    /// (docs/01-vision-and-pillars.md, pillar 1).
    /// <para>
    /// Integrated on the needs tick, which fires a fixed 180 times per game hour whatever the
    /// time scale, so the same journey in the same weather always produces the same temperature
    /// (docs/09, 9.1).
    /// </para>
    /// </summary>
    public sealed class CoreTemperature
    {
        /// <summary>Starting and healthy core temperature, in Celsius.</summary>
        public const float NominalCelsius = 37f;

        /// <summary>Below this the character is dead.</summary>
        public const float DeathCelsius = 28f;

        /// <summary>
        /// The body cannot be pushed above this by the model. Fever is a separate system in
        /// docs/03, 3.7 and deliberately does not route through here.
        /// </summary>
        public const float MaxCelsius = 40f;

        /// <summary>Current core temperature in Celsius.</summary>
        public float Celsius { get; private set; } = NominalCelsius;

        /// <summary>The balance computed on the last step, for readouts and debugging.</summary>
        public HeatBalanceResult LastBalance { get; private set; }

        /// <summary>Which hypothermia stage the current temperature falls in.</summary>
        public HypothermiaStage Stage => StageFor(Celsius);

        /// <summary>True once the core has fallen below <see cref="DeathCelsius"/>.</summary>
        public bool IsDead => Celsius < DeathCelsius;

        /// <summary>
        /// Advances the core temperature by one tick.
        /// </summary>
        /// <param name="gameSeconds">
        /// Length of the tick in game seconds, as handed over by <see cref="SimClock"/>.
        /// </param>
        /// <param name="conditions">The world around the character.</param>
        /// <param name="garments">What they are wearing.</param>
        public void Step(double gameSeconds, ThermalConditions conditions, IEnumerable<Garment> garments)
        {
            if (gameSeconds <= 0d || IsDead)
            {
                // A corpse does not keep cooling in the simulation. Without this the integrator
                // runs for as long as the save does, which a ten-day soak found as a core
                // temperature of -1388 C.
                return;
            }

            var balance = HeatBalance.Compute(Celsius, conditions, garments);
            LastBalance = balance;

            var hours = (float)(gameSeconds / 3600d);
            var next = Celsius + balance.DegreesPerHour * hours;

            if (next > MaxCelsius)
            {
                next = MaxCelsius;
            }

            // A body cannot become colder than the air around it. The loss term is computed
            // against a fixed skin target rather than against the current core, so without this
            // floor a cooling body keeps losing heat at full rate no matter how cold it gets.
            var floor = conditions.AmbientCelsius;
            if (floor > Celsius)
            {
                floor = Celsius;
            }

            if (next < floor)
            {
                next = floor;
            }

            Celsius = next;
        }

        /// <summary>Sets the temperature directly. For loading a save and for tests.</summary>
        public void SetCelsius(float celsius)
        {
            Celsius = celsius > MaxCelsius ? MaxCelsius : celsius;
        }

        /// <summary>The stage a given temperature falls in.</summary>
        public static HypothermiaStage StageFor(float celsius)
        {
            if (celsius < DeathCelsius)
            {
                return HypothermiaStage.Dead;
            }

            if (celsius < 31f)
            {
                return HypothermiaStage.Severe;
            }

            if (celsius < 33f)
            {
                return HypothermiaStage.Moderate;
            }

            if (celsius < 35f)
            {
                return HypothermiaStage.Mild;
            }

            if (celsius < 36.5f)
            {
                return HypothermiaStage.Cold;
            }

            return HypothermiaStage.Normal;
        }
    }
}
