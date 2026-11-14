using System.Collections.Generic;

namespace Coldfall.Sim
{
    /// <summary>
    /// Everything that wets or dries clothing in a given moment.
    /// </summary>
    public struct WetnessConditions
    {
        /// <summary>Rain or wet snow falling on the character, 0 to 1.</summary>
        public float Precipitation;

        /// <summary>
        /// How sheltered the character is from that precipitation, 0 to 1. A roof is 1, a tree
        /// is partial, open ground is 0.
        /// </summary>
        public float Shelter;

        /// <summary>Radiant heat from a fire or heater, in the same units as the heat balance.</summary>
        public float RadiantHeat;

        /// <summary>Air temperature at the character, in Celsius.</summary>
        public float AmbientCelsius;

        /// <summary>
        /// How hard the character is working, 0 to 1. Sweat wets clothing from the inside, which
        /// is why sprinting to the cabin can arrive wetter than walking to it.
        /// </summary>
        public float Exertion;

        /// <summary>Standing in water: a river crossing, a flooded underpass.</summary>
        public bool InWater;
    }

    /// <summary>
    /// Wets and dries garments over time.
    /// <para>
    /// Separate from the heat balance on purpose. Wetness is a slow state that the balance
    /// reads, not something the balance computes: a soaked coat is still soaked after the fire
    /// goes out, and that persistence is what turns one careless river crossing into the rest
    /// of the day.
    /// </para>
    /// <para>
    /// Tuned to the M1 exit criteria: a soaked three-layer torso set dries in 2.5 to 4 game
    /// hours next to a fire, and does not meaningfully dry outdoors in the rain.
    /// </para>
    /// </summary>
    public static class WetnessSystem
    {
        /// <summary>Wetness gained per game hour by an unprotected garment in full rain.</summary>
        public const float SoakRatePerHour = 1.2f;

        /// <summary>Wetness gained per game hour while standing in water. Fast and merciless.</summary>
        public const float ImmersionRatePerHour = 6f;

        /// <summary>Wetness gained per game hour from sweat at full exertion.</summary>
        public const float SweatRatePerHour = 0.35f;

        /// <summary>Wetness lost per game hour at full drying power.</summary>
        public const float DryRatePerHour = 0.30f;

        /// <summary>Radiant heat at which drying power is considered full.</summary>
        public const float FullDryingRadiance = 8f;

        /// <summary>
        /// How much of the rain an outer layer keeps off the layers beneath it, before its own
        /// waterproofing is considered.
        /// </summary>
        public const float LayerShielding = 0.55f;

        /// <summary>
        /// What still gets past a perfectly waterproof layer, through the neck and the cuffs.
        /// Nothing is ever fully sealed, for the same reason insulation is capped below 1.
        /// </summary>
        public const float GapLeak = 0.08f;

        /// <summary>
        /// Advances every garment's wetness.
        /// </summary>
        /// <param name="garments">The worn set. Modified in place.</param>
        /// <param name="conditions">The world around the character.</param>
        /// <param name="gameSeconds">Tick length, from <see cref="SimClock"/>.</param>
        public static void Step(
            IReadOnlyList<Garment> garments, WetnessConditions conditions, double gameSeconds)
        {
            if (garments == null || gameSeconds <= 0d)
            {
                return;
            }

            var hours = (float)(gameSeconds / 3600d);
            var exposure = Clamp01(conditions.Precipitation) * (1f - Clamp01(conditions.Shelter));
            var drying = DryingPower(conditions);

            foreach (var zone in BodyZones.All)
            {
                StepZone(garments, zone, exposure, drying, conditions, hours);
            }
        }

        /// <summary>
        /// Drying power, 0 to 1. A fire dominates it; still, warm air dries slowly on its own,
        /// and cold air barely dries at all, which is why a wet coat left in an unheated room
        /// overnight is still wet in the morning.
        /// </summary>
        public static float DryingPower(WetnessConditions conditions)
        {
            if (conditions.InWater)
            {
                return 0f;
            }

            var radiant = Clamp01(conditions.RadiantHeat / FullDryingRadiance);

            // Air drying only becomes meaningful above about 10 degrees, and never reaches more
            // than a fraction of what a fire does.
            var air = Clamp01((conditions.AmbientCelsius - 10f) / 20f) * 0.35f;

            var wetAir = Clamp01(conditions.Precipitation) * (1f - Clamp01(conditions.Shelter));
            var power = radiant + air;

            // Standing in the rain, nothing dries however warm the fire is at your back.
            power *= 1f - wetAir;

            return Clamp01(power);
        }

        /// <summary>
        /// Slots from outermost inwards. Rain reaches layers in this order, so the list drives
        /// shielding rather than the order the garments happen to sit in the inventory.
        /// </summary>
        private static readonly GarmentSlot[] OutermostFirst =
        {
            GarmentSlot.Back,
            GarmentSlot.TorsoOuter,
            GarmentSlot.TorsoMid,
            GarmentSlot.TorsoBase,
            GarmentSlot.Face,
            GarmentSlot.Head,
            GarmentSlot.Hands,
            GarmentSlot.Legs,
            GarmentSlot.Feet,
        };

        private static void StepZone(
            IReadOnlyList<Garment> garments, BodyZone zone, float exposure, float drying,
            WetnessConditions conditions, float hours)
        {
            var remaining = 1f;

            foreach (var slot in OutermostFirst)
            foreach (var garment in garments)
            {
                if (garment == null || garment.Slot != slot || !garment.Covers(zone))
                {
                    continue;
                }

                var reaching = exposure * remaining;
                var absorbed = reaching * (1f - Clamp01(garment.Waterproofing));

                var gain = absorbed * SoakRatePerHour * hours;

                if (conditions.InWater && (zone == BodyZone.Feet || zone == BodyZone.Legs))
                {
                    gain += ImmersionRatePerHour * hours * (1f - Clamp01(garment.Waterproofing));
                }

                gain += Clamp01(conditions.Exertion) * SweatRatePerHour * hours;

                var loss = drying * DryRatePerHour * hours;

                garment.Wetness = Clamp01(garment.Wetness + gain - loss);

                // What gets through this layer to the one beneath. A non-waterproof garment
                // still blocks some rain by absorbing it; a waterproof shell blocks nearly all
                // of it, and what gets past a good shell comes in at the neck and cuffs rather
                // than through the fabric, which is what GapLeak stands for.
                var waterproofing = Clamp01(garment.Waterproofing);
                remaining *= (1f - waterproofing) * (1f - LayerShielding) + waterproofing * GapLeak;
            }
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
