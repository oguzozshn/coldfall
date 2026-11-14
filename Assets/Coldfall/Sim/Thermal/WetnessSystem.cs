using System.Collections.Generic;

namespace Coldfall.Sim
{
    /// <summary>
    /// Everything that wets or dries clothing in a given moment.
    /// </summary>
    public struct WetnessConditions
    {
        /// <summary>
        /// Wetness an exposed, unprotected garment would gain per game hour, from the weather
        /// table. Authored per weather state in docs/03-survival-systems.md, 3.4.
        /// </summary>
        public float SoakPerHour;

        /// <summary>
        /// How sheltered the character is from that, 0 to 1. A roof is 1, a tree is partial,
        /// open ground is 0.
        /// </summary>
        public float Shelter;

        /// <summary>Radiant heat from a fire or heater, in heat-balance units.</summary>
        public float RadiantHeat;

        /// <summary>Air temperature at the character, in Celsius.</summary>
        public float AmbientCelsius;

        /// <summary>True when under a roof, which dries slowly even with no fire.</summary>
        public bool IsIndoors;

        /// <summary>How hard the character is working, 0 to 1.</summary>
        public float Exertion;

        /// <summary>
        /// Total bulk of what they are wearing. Sweat scales with it: the warmest loadout is
        /// also the one that soaks itself from the inside on a hard climb.
        /// </summary>
        public float ClothingBulk;
    }

    /// <summary>
    /// Wets and dries garments over time, from the authored tables in docs/03, 3.4.
    /// <para>
    /// Separate from the heat balance on purpose. Wetness is slow state that the balance reads,
    /// not something the balance computes: a soaked coat is still soaked after the fire goes
    /// out, and that persistence is what turns one careless river crossing into the rest of the
    /// day.
    /// </para>
    /// <para>
    /// Docs/03 calls drying the maintenance action players underestimate, and the numbers are
    /// chosen to make that true: a soaked three-layer torso set takes most of an evening near a
    /// fire, which is fuel the player then does not have for the night.
    /// </para>
    /// </summary>
    public static class WetnessSystem
    {
        /// <summary>Wetness lost per game hour worn outdoors in humid air. Effectively never.</summary>
        public const float DryWornOutdoors = 0.01f;

        /// <summary>Wetness lost per game hour worn indoors with no fire.</summary>
        public const float DryWornIndoors = 0.05f;

        /// <summary>
        /// Wetness lost per game hour worn next to a fire.
        /// <para>
        /// Docs/03, 3.4 prints 0.22, which dries a full soak in 4.5 game hours and misses the
        /// 2.5-4 hour band that docs/10 sets as M1 exit criterion 3. The roadmap is the gate, so
        /// the rate is 0.28 and doc 03 is corrected to match.
        /// </para>
        /// </summary>
        public const float DryWornByFire = 0.28f;

        /// <summary>Wetness lost per game hour hung up indoors, off the body.</summary>
        public const float DryRemovedIndoors = 0.14f;

        /// <summary>Wetness lost per game hour hung near a fire, off the body.</summary>
        public const float DryRemovedByFire = 0.55f;

        /// <summary>Radiant heat at which drying is considered to be "next to a fire".</summary>
        public const float FullDryingRadiance = 8f;

        /// <summary>
        /// At or below this ambient temperature a wet garment freezes stiff instead of drying.
        /// It must be thawed somewhere warm first, which is why hanging washing in an unheated
        /// winter room does nothing at all.
        /// </summary>
        public const float FreezingCelsius = -2f;

        /// <summary>Wetness gained per game hour from sweat at full exertion in bulky clothing.</summary>
        public const float SweatPerHour = 0.15f;

        /// <summary>Instant wetness added to legs and feet by wading shin deep.</summary>
        public const float WadeAmount = 0.30f;

        /// <summary>Instant wetness added to every layer by going fully under.</summary>
        public const float SwimAmount = 1f;

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

        /// <summary>Advances every worn garment's wetness.</summary>
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
            var exposure = 1f - Clamp01(conditions.Shelter);
            var soak = conditions.SoakPerHour * exposure;
            var drying = WornDryingPerHour(conditions);
            var sweat = Clamp01(conditions.Exertion) * SweatPerHour
                        * Clamp01(conditions.ClothingBulk / 2f + 0.3f);

            foreach (var zone in BodyZones.All)
            {
                StepZone(garments, zone, soak, drying, sweat, hours);
            }
        }

        /// <summary>
        /// Dries a garment that is off the body, hung up. Much faster than wearing it, which is
        /// what makes stripping the soaked shell and travelling cold-but-dry a real option.
        /// </summary>
        public static void DryRemoved(Garment garment, WetnessConditions conditions, double gameSeconds)
        {
            if (garment == null || gameSeconds <= 0d)
            {
                return;
            }

            var hours = (float)(gameSeconds / 3600d);
            garment.Wetness = Clamp01(garment.Wetness - RemovedDryingPerHour(conditions) * hours);
        }

        /// <summary>Wades shin deep. Soaks legs and feet at once.</summary>
        public static void Wade(IReadOnlyList<Garment> garments)
        {
            AddTo(garments, BodyZone.Legs | BodyZone.Feet, WadeAmount);
        }

        /// <summary>Goes fully under. Outside summer this is close to a death sentence.</summary>
        public static void Submerge(IReadOnlyList<Garment> garments)
        {
            AddTo(garments, BodyZones.Everything, SwimAmount);
        }

        /// <summary>Wetness lost per game hour by a worn garment in these conditions.</summary>
        public static float WornDryingPerHour(WetnessConditions conditions)
        {
            if (conditions.AmbientCelsius <= FreezingCelsius)
            {
                return 0f;
            }

            var fire = Clamp01(conditions.RadiantHeat / FullDryingRadiance);
            var baseRate = conditions.IsIndoors ? DryWornIndoors : DryWornOutdoors;
            var rate = baseRate + (DryWornByFire - baseRate) * fire;

            // Standing in falling weather, nothing dries however good the fire at your back is,
            // and partial shelter dries partially.
            if (conditions.SoakPerHour <= 0f)
            {
                return rate;
            }

            return rate * Clamp01(conditions.Shelter);
        }

        /// <summary>Wetness lost per game hour by a garment hung up off the body.</summary>
        public static float RemovedDryingPerHour(WetnessConditions conditions)
        {
            if (conditions.AmbientCelsius <= FreezingCelsius)
            {
                return 0f;
            }

            var fire = Clamp01(conditions.RadiantHeat / FullDryingRadiance);
            return DryRemovedIndoors + (DryRemovedByFire - DryRemovedIndoors) * fire;
        }

        private static void StepZone(
            IReadOnlyList<Garment> garments, BodyZone zone,
            float soakPerHour, float dryingPerHour, float sweatPerHour, float hours)
        {
            var remaining = 1f;

            foreach (var slot in OutermostFirst)
            foreach (var garment in garments)
            {
                if (garment == null || garment.Slot != slot || !garment.Covers(zone))
                {
                    continue;
                }

                var waterproofing = Clamp01(garment.Waterproofing);

                var gain = soakPerHour * remaining * (1f - waterproofing) * hours
                           + sweatPerHour * hours;

                var loss = dryingPerHour * hours;

                garment.Wetness = Clamp01(garment.Wetness + gain - loss);

                remaining *= (1f - waterproofing) * (1f - LayerShielding) + waterproofing * GapLeak;
            }
        }

        private static void AddTo(IReadOnlyList<Garment> garments, BodyZone zones, float amount)
        {
            if (garments == null)
            {
                return;
            }

            foreach (var garment in garments)
            {
                if (garment == null || (garment.Coverage & zones) == 0)
                {
                    continue;
                }

                garment.Wetness = Clamp01(garment.Wetness + amount * (1f - Clamp01(garment.Waterproofing)));
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
