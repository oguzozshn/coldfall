namespace Coldfall.Sim
{
    /// <summary>Why the character cannot lie down.</summary>
    public enum SleepRefusal
    {
        /// <summary>They can sleep.</summary>
        None,

        /// <summary>Nothing to sleep on. A floor is not a bed.</summary>
        NoBed,

        /// <summary>Too cold to fall asleep. Warm up first, which means fuel.</summary>
        TooCold,

        /// <summary>
        /// Wet clothing. The player has to undress and dry off, which means they wake up with
        /// nothing on if something comes through the door.
        /// </summary>
        ClothesTooWet,

        /// <summary>In too much pain. Painkillers, or wait it out.</summary>
        TooMuchPain,
    }

    /// <summary>What ended a sleep.</summary>
    public enum SleepInterruption
    {
        /// <summary>Still asleep.</summary>
        None,

        /// <summary>Slept through to the planned wake time.</summary>
        SleptThrough,

        /// <summary>Fatigue cleared before the wake time.</summary>
        Rested,

        /// <summary>The fire went out and the cold woke them. Usually fatal if ignored.</summary>
        CoreTooCold,

        /// <summary>A noise loud enough to wake them.</summary>
        Noise,

        /// <summary>Pain from an untreated wound.</summary>
        Pain,

        /// <summary>Something came into the building.</summary>
        Threat,
    }

    /// <summary>
    /// The sleep rules of docs/03-survival-systems.md, 3.11.
    /// <para>
    /// Sleep is where the survival half of the design meets the combat half. Every refusal above
    /// is a thing the player has to fix before they can rest, and every interruption is a reason
    /// to have fortified the building first.
    /// </para>
    /// <para>
    /// The wet-clothing refusal is the sharpest of them: it forces the player to undress, which
    /// means the worst possible state to be woken up in. Fortifying is not optional once you
    /// know that.
    /// </para>
    /// </summary>
    public static class Sleep
    {
        /// <summary>Core temperature needed to fall asleep at all, in Celsius.</summary>
        public const float MinimumCoreCelsius = 34f;

        /// <summary>Core temperature that wakes a sleeper, in Celsius.</summary>
        public const float InterruptCoreCelsius = 33f;

        /// <summary>Mean clothing wetness above which sleep is impossible.</summary>
        public const float MaximumWetness = 0.4f;

        /// <summary>Pain above which sleep is impossible, 0 to 100.</summary>
        public const float MaximumPain = 60f;

        /// <summary>Pain above which a sleeper is woken, 0 to 100.</summary>
        public const float InterruptPain = 75f;

        /// <summary>Fatigue cleared per game hour in a proper bed.</summary>
        public const float BaseFatigueRecoveryPerHour = 12f;

        /// <summary>Insulation a proper bed adds across every zone.</summary>
        public const float BaseBeddingInsulation = 0.30f;

        /// <summary>Noise radius, in metres, at which a sleeper wakes.</summary>
        public const float WakingNoiseRadius = 15f;

        /// <summary>Quality of a proper bed.</summary>
        public const float BedQuality = 1f;

        /// <summary>Quality of a bedroll.</summary>
        public const float BedrollQuality = 0.7f;

        /// <summary>Quality of an armchair or a car seat. Better than the floor, barely.</summary>
        public const float SeatQuality = 0.35f;

        /// <summary>
        /// Whether the character can lie down, and why not if they cannot.
        /// </summary>
        /// <param name="coreCelsius">Current core temperature.</param>
        /// <param name="meanWetness">Mean wetness across what they are wearing, 0 to 1.</param>
        /// <param name="bedQuality">0 for nothing to sleep on, up to 1 for a proper bed.</param>
        /// <param name="pain">Current pain, 0 to 100.</param>
        public static SleepRefusal CanSleep(
            float coreCelsius, float meanWetness, float bedQuality, float pain)
        {
            if (bedQuality <= 0f)
            {
                return SleepRefusal.NoBed;
            }

            if (coreCelsius < MinimumCoreCelsius)
            {
                return SleepRefusal.TooCold;
            }

            if (meanWetness > MaximumWetness)
            {
                return SleepRefusal.ClothesTooWet;
            }

            return pain > MaximumPain ? SleepRefusal.TooMuchPain : SleepRefusal.None;
        }

        /// <summary>
        /// Why a sleeper would be woken right now, or <see cref="SleepInterruption.None"/>.
        /// </summary>
        /// <param name="coreCelsius">Current core temperature.</param>
        /// <param name="pain">Current pain, 0 to 100.</param>
        /// <param name="loudestNearbyNoiseRadius">
        /// Radius of the loudest noise event nearby, in metres. See docs/04, 4.3.
        /// </param>
        /// <param name="threatInBuilding">True if something has come inside.</param>
        public static SleepInterruption CheckInterruption(
            float coreCelsius, float pain, float loudestNearbyNoiseRadius, bool threatInBuilding)
        {
            if (threatInBuilding)
            {
                return SleepInterruption.Threat;
            }

            if (coreCelsius < InterruptCoreCelsius)
            {
                return SleepInterruption.CoreTooCold;
            }

            if (pain > InterruptPain)
            {
                return SleepInterruption.Pain;
            }

            return loudestNearbyNoiseRadius >= WakingNoiseRadius
                ? SleepInterruption.Noise
                : SleepInterruption.None;
        }

        /// <summary>Fatigue cleared per game hour on the given bedding.</summary>
        public static float FatigueRecoveryPerHour(float bedQuality)
        {
            return BaseFatigueRecoveryPerHour * Clamp01(bedQuality);
        }

        /// <summary>
        /// Insulation the bedding adds while lying in it, across every zone. Enough to make a
        /// cold room survivable overnight, never enough to replace a fire.
        /// </summary>
        public static float BeddingInsulation(float bedQuality)
        {
            return BaseBeddingInsulation * Clamp01(bedQuality);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
