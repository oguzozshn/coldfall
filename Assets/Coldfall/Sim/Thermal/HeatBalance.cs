using System.Collections.Generic;

namespace Coldfall.Sim
{
    /// <summary>What the character is doing, which sets how much heat they generate.</summary>
    public enum ActivityLevel
    {
        /// <summary>Standing, sitting, sleeping.</summary>
        Rest,

        /// <summary>Walking, searching, crafting.</summary>
        Light,

        /// <summary>Running, chopping wood, fighting.</summary>
        Hard,
    }

    /// <summary>
    /// Everything outside the body that the heat balance needs.
    /// </summary>
    public struct ThermalConditions
    {
        /// <summary>Air temperature at the character, after shelter, in Celsius.</summary>
        public float AmbientCelsius;

        /// <summary>Wind at the character, after shelter occlusion, in km/h.</summary>
        public float WindKilometresPerHour;

        /// <summary>What the character is doing.</summary>
        public ActivityLevel Activity;

        /// <summary>Radiant heat from fires and heaters, in degrees per hour before thermal mass.</summary>
        public float RadiantHeat;

        /// <summary>Conductive heat from hot food, drink and bedding, same units.</summary>
        public float ConductiveHeat;

        /// <summary>
        /// True below 500 kcal. Docs/03, 3.3: starving halves passive metabolic heat, which is
        /// why starving makes you cold rather than merely weak.
        /// </summary>
        public bool IsStarving;

        /// <summary>
        /// Insulation the bedding adds while asleep, 0 to 1, applied on top of clothing across
        /// every zone. Zero when awake.
        /// </summary>
        public float BeddingInsulation;
    }

    /// <summary>
    /// Every term of the balance, kept separately. The thermal visualizer in docs/09, 9.11 and
    /// the condition readout both need to show <i>why</i> the player is freezing, not only that
    /// they are, so nothing here collapses into a single number before it has to.
    /// </summary>
    public readonly struct HeatBalanceResult
    {
        /// <summary>Basal and activity heat, positive.</summary>
        public float Metabolic { get; }

        /// <summary>Shivering heat, positive. Zero when not shivering.</summary>
        public float Shivering { get; }

        /// <summary>Fires and heaters, positive.</summary>
        public float Radiant { get; }

        /// <summary>Hot food, drink and bedding, positive.</summary>
        public float Conductive { get; }

        /// <summary>Convection to the air, reported positive, subtracted below.</summary>
        public float Convective { get; }

        /// <summary>Evaporation from wet clothing and skin, reported positive.</summary>
        public float Evaporative { get; }

        /// <summary>Breathing cold air, reported positive.</summary>
        public float Respiratory { get; }

        /// <summary>Net change in core temperature, degrees Celsius per game hour.</summary>
        public float DegreesPerHour { get; }

        /// <summary>Creates a result and computes the net.</summary>
        public HeatBalanceResult(
            float metabolic, float shivering, float radiant, float conductive,
            float convective, float evaporative, float respiratory)
        {
            Metabolic = metabolic;
            Shivering = shivering;
            Radiant = radiant;
            Conductive = conductive;
            Convective = convective;
            Evaporative = evaporative;
            Respiratory = respiratory;

            var net = metabolic + shivering + radiant + conductive
                      - convective - evaporative - respiratory;

            DegreesPerHour = net / HeatBalance.BodyThermalMass;
        }
    }

    /// <summary>
    /// The heat balance of docs/03-survival-systems.md, 3.3. Pure, with no state and no Unity
    /// types, so it is unit tested as the function it is — which docs/09, 9.3 requires of
    /// everything in <c>Coldfall.Sim</c>.
    /// </summary>
    public static class HeatBalance
    {
        /// <summary>
        /// Divides the summed contributions into degrees per hour. A net of +3.5 raises the core
        /// by 1 degree in an hour, which is the calibration stated in docs/03, 3.3.
        /// </summary>
        public const float BodyThermalMass = 3.5f;

        /// <summary>Skin temperature the body tries to hold, in Celsius. Not core temperature.</summary>
        public const float SkinTargetCelsius = 33f;

        /// <summary>
        /// Scales convective loss into the same units as the metabolic terms. Tuned so that the
        /// M1 exit case holds: soaked, at rest, in 6 degC drizzle, the core reaches mild
        /// hypothermia in roughly half an hour.
        /// </summary>
        public const float ConvectiveScale = 0.33f;

        /// <summary>
        /// Evaporative loss at full wetness, as a share of convective loss.
        /// <para>
        /// <b>This departs from the document.</b> Docs/03, 3.3 puts it at +40%, but that figure
        /// cannot produce the document's own outcomes: at +40% the gap between soaked and dry is
        /// only about 1.4x, and no convective scale satisfies both "soaked and idle reaches mild
        /// hypothermia in 20-35 minutes" and "dry by a fire is safe" at the same time. Making
        /// evaporation the dominant term satisfies both, and is the physically honest answer
        /// anyway: evaporation from soaked clothing carries away far more heat than convection
        /// from dry clothing does. Doc 03 should be corrected to match.
        /// </para>
        /// </summary>
        public const float EvaporativeShareAtFullWetness = 1.6f;

        /// <summary>Core temperature at which shivering begins.</summary>
        public const float ShiverStartCelsius = 36.5f;

        /// <summary>Core temperature at which shivering is at full strength.</summary>
        public const float ShiverPeakCelsius = 35f;

        /// <summary>
        /// Core temperature below which shivering begins to fail, and at
        /// <see cref="ShiverLostCelsius"/> stops entirely. Docs/03, 3.3 marks this as the
        /// moderate stage, where shivering stops.
        /// </summary>
        public const float ShiverFailCelsius = 33f;

        /// <summary>Core temperature at which shivering has stopped completely.</summary>
        public const float ShiverLostCelsius = 31f;

        /// <summary>Peak heat from shivering, in the same units as the metabolic terms.</summary>
        public const float ShiverPeakHeat = 3.5f;

        /// <summary>
        /// Computes the balance for one moment.
        /// </summary>
        /// <param name="coreCelsius">Current core temperature, which drives shivering.</param>
        /// <param name="conditions">The world around the character.</param>
        /// <param name="garments">What they are wearing. Null or empty means naked.</param>
        public static HeatBalanceResult Compute(
            float coreCelsius, ThermalConditions conditions, IEnumerable<Garment> garments)
        {
            var worn = Materialise(garments);

            var metabolic = BasalHeat(conditions.Activity);
            if (conditions.IsStarving)
            {
                metabolic *= 0.5f;
            }

            var shivering = ShiverHeat(coreCelsius);

            var convective = 0f;
            var wetnessWeighted = 0f;

            foreach (var zone in BodyZones.All)
            {
                var insulation = ClothingInsulation.ForZone(worn, zone);
                var effective = WindChill.EffectiveCelsius(
                    conditions.AmbientCelsius, conditions.WindKilometresPerHour, insulation.Windproofing);

                var gradient = SkinTargetCelsius - effective;
                if (gradient < 0f)
                {
                    // Warmer than skin: the body is gaining, not losing. Heat illness is out of
                    // scope for the slice, so this is clamped rather than modelled.
                    gradient = 0f;
                }

                // Bedding layers on top of clothing with the same diminishing returns, and is
                // subject to the same ceiling: a sleeping bag in a freezing room buys hours, not
                // safety.
                var bedding = Clamp01(conditions.BeddingInsulation);
                var total = 1f - (1f - insulation.Insulation) * (1f - bedding);
                if (total > ClothingInsulation.MaxInsulation)
                {
                    total = ClothingInsulation.MaxInsulation;
                }

                var weight = BodyZones.LossWeight(zone);
                convective += gradient * (1f - total) * weight;
                wetnessWeighted += insulation.Wetness * weight;
            }

            convective *= ConvectiveScale;

            var evaporative = convective * EvaporativeShareAtFullWetness * Clamp01(wetnessWeighted);
            var respiratory = RespiratoryLoss(conditions.AmbientCelsius);

            return new HeatBalanceResult(
                metabolic,
                shivering,
                conditions.RadiantHeat,
                conditions.ConductiveHeat,
                convective,
                evaporative,
                respiratory);
        }

        /// <summary>Heat generated by simply being alive and doing the given activity.</summary>
        public static float BasalHeat(ActivityLevel activity)
        {
            return activity switch
            {
                ActivityLevel.Rest => 2f,
                ActivityLevel.Light => 5.5f,
                ActivityLevel.Hard => 11f,
                _ => 2f,
            };
        }

        /// <summary>
        /// Heat from shivering, which ramps in as the core falls and then <b>fails</b> as it
        /// falls further.
        /// <para>
        /// The failure is the cruelty at the centre of the model and it is faithful to both the
        /// document and the real thing: between 33 and 31 degrees the body stops shivering, so
        /// the one defence that was slowing the fall switches off exactly when it is needed
        /// most. Past that point the curve is steeper than it was before, and the player who
        /// notices they have stopped feeling cold has already lost.
        /// </para>
        /// </summary>
        public static float ShiverHeat(float coreCelsius)
        {
            if (coreCelsius >= ShiverStartCelsius)
            {
                return 0f;
            }

            if (coreCelsius <= ShiverLostCelsius)
            {
                return 0f;
            }

            if (coreCelsius >= ShiverPeakCelsius)
            {
                var ramp = (ShiverStartCelsius - coreCelsius) / (ShiverStartCelsius - ShiverPeakCelsius);
                return ShiverPeakHeat * Clamp01(ramp);
            }

            if (coreCelsius >= ShiverFailCelsius)
            {
                return ShiverPeakHeat;
            }

            var fade = (coreCelsius - ShiverLostCelsius) / (ShiverFailCelsius - ShiverLostCelsius);
            return ShiverPeakHeat * Clamp01(fade);
        }

        /// <summary>
        /// Loss from breathing cold air. Docs/03, 3.3: 0.3 per 10 degrees below freezing.
        /// </summary>
        public static float RespiratoryLoss(float ambientCelsius)
        {
            if (ambientCelsius >= 0f)
            {
                return 0f;
            }

            return -ambientCelsius / 10f * 0.3f;
        }

        private static IReadOnlyList<Garment> Materialise(IEnumerable<Garment> garments)
        {
            if (garments is IReadOnlyList<Garment> list)
            {
                return list;
            }

            var copy = new List<Garment>();
            if (garments == null)
            {
                return copy;
            }

            foreach (var garment in garments)
            {
                copy.Add(garment);
            }

            return copy;
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
