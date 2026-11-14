using System.Collections.Generic;

namespace Coldfall.Sim
{
    /// <summary>
    /// Per-zone insulation and windproofing, summed from whatever the character is wearing.
    /// </summary>
    public readonly struct ZoneInsulation
    {
        /// <summary>Total insulation for the zone, 0 to <see cref="ClothingInsulation.MaxInsulation"/>.</summary>
        public float Insulation { get; }

        /// <summary>Total windproofing for the zone, 0 to 1.</summary>
        public float Windproofing { get; }

        /// <summary>Mean wetness of the garments covering the zone, 0 to 1.</summary>
        public float Wetness { get; }

        /// <summary>Creates a result.</summary>
        public ZoneInsulation(float insulation, float windproofing, float wetness)
        {
            Insulation = insulation;
            Windproofing = windproofing;
            Wetness = wetness;
        }
    }

    /// <summary>
    /// Turns a set of garments into the per-zone numbers the heat balance needs.
    /// </summary>
    public static class ClothingInsulation
    {
        /// <summary>
        /// The hard ceiling from docs/03-survival-systems.md, 3.3. Perfect insulation does not
        /// exist, so there is no loadout that makes the cold irrelevant: a player in the best
        /// clothing in the valley is buying hours, never immunity. Removing this cap would
        /// quietly delete the entire pressure the milestone is built on.
        /// </summary>
        public const float MaxInsulation = 0.92f;

        /// <summary>
        /// Insulation for one zone, with diminishing returns across layers.
        /// <para>
        /// Layers combine as remaining heat loss rather than by addition: each garment removes a
        /// share of what the previous ones left. Three 0.3 garments give 0.657, not 0.9. That is
        /// both physically right and the reason piling on identical layers stops paying, which
        /// pushes the player towards better clothing instead of more of it.
        /// </para>
        /// </summary>
        public static ZoneInsulation ForZone(IEnumerable<Garment> garments, BodyZone zone)
        {
            if (garments == null)
            {
                return new ZoneInsulation(0f, 0f, 0f);
            }

            var remainingLoss = 1f;
            var remainingWind = 1f;
            var wetnessSum = 0f;
            var covering = 0;

            foreach (var garment in garments)
            {
                if (garment == null || !garment.Covers(zone))
                {
                    continue;
                }

                covering++;
                remainingLoss *= 1f - Clamp01(garment.EffectiveInsulation);
                remainingWind *= 1f - Clamp01(garment.EffectiveWindproofing);
                wetnessSum += Clamp01(garment.Wetness);
            }

            var insulation = 1f - remainingLoss;
            if (insulation > MaxInsulation)
            {
                insulation = MaxInsulation;
            }

            var wetness = covering == 0 ? 0f : wetnessSum / covering;
            return new ZoneInsulation(insulation, 1f - remainingWind, wetness);
        }

        /// <summary>
        /// Total bulk across everything worn. Feeds the movement and swing penalties.
        /// </summary>
        public static float TotalBulk(IEnumerable<Garment> garments)
        {
            if (garments == null)
            {
                return 0f;
            }

            var bulk = 0f;
            foreach (var garment in garments)
            {
                if (garment != null)
                {
                    bulk += Clamp01(garment.Bulk);
                }
            }

            return bulk;
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
