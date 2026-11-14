using System;

namespace Coldfall.Sim
{
    /// <summary>
    /// Converts an ambient temperature and a wind speed into the temperature the skin actually
    /// faces, which is the <c>T_effective</c> term of the heat balance in
    /// docs/03-survival-systems.md, 3.3.
    /// <para>
    /// Uses the standard North American wind chill curve in metric form. It is an empirical fit
    /// rather than a derivation, which suits the project rule in docs/00: relative outcomes stay
    /// true to the real world, absolute numbers may be tuned.
    /// </para>
    /// </summary>
    public static class WindChill
    {
        /// <summary>
        /// Above this ambient temperature the curve is not defined and wind is treated as
        /// having no chilling effect. Wind still matters above it, through evaporation.
        /// </summary>
        public const float MaxValidCelsius = 10f;

        /// <summary>Below this wind speed the curve is not defined and the chill is zero.</summary>
        public const float MinValidKilometresPerHour = 4.8f;

        /// <summary>Clamp on the computed chill, so a storm cannot produce an absurd figure.</summary>
        public const float MaxChillCelsius = 30f;

        /// <summary>
        /// The temperature the skin faces, in Celsius.
        /// </summary>
        /// <param name="ambientCelsius">Air temperature.</param>
        /// <param name="windKilometresPerHour">Wind speed at the body, after shelter occlusion.</param>
        /// <param name="windproofing">
        /// 0 to 1, from the clothing. A fully windproof shell removes the chill entirely but
        /// does nothing about the ambient temperature itself, which is why a windproof layer
        /// over nothing warm is still cold.
        /// </param>
        public static float EffectiveCelsius(
            float ambientCelsius, float windKilometresPerHour, float windproofing = 0f)
        {
            var chill = ChillCelsius(ambientCelsius, windKilometresPerHour);
            var blocked = chill * (1f - Clamp01(windproofing));
            return ambientCelsius - blocked;
        }

        /// <summary>
        /// How many degrees the wind takes off, as a non-negative number.
        /// </summary>
        public static float ChillCelsius(float ambientCelsius, float windKilometresPerHour)
        {
            if (ambientCelsius > MaxValidCelsius || windKilometresPerHour < MinValidKilometresPerHour)
            {
                return 0f;
            }

            var v = (float)Math.Pow(windKilometresPerHour, 0.16);
            var apparent = 13.12f + 0.6215f * ambientCelsius - 11.37f * v + 0.3965f * ambientCelsius * v;

            var chill = ambientCelsius - apparent;
            if (chill <= 0f)
            {
                return 0f;
            }

            return chill > MaxChillCelsius ? MaxChillCelsius : chill;
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
