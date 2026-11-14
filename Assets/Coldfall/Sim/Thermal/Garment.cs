using System;

namespace Coldfall.Sim
{
    /// <summary>
    /// Which layer a garment occupies. Docs/03-survival-systems.md, 3.3 allows at most three
    /// torso layers; the slots here are what enforces that.
    /// </summary>
    public enum GarmentSlot
    {
        /// <summary>Hat, hood, balaclava.</summary>
        Head,

        /// <summary>Scarf, mask.</summary>
        Face,

        /// <summary>Next to the skin.</summary>
        TorsoBase,

        /// <summary>Jumper, fleece.</summary>
        TorsoMid,

        /// <summary>Coat, shell.</summary>
        TorsoOuter,

        /// <summary>Gloves, mittens.</summary>
        Hands,

        /// <summary>Trousers.</summary>
        Legs,

        /// <summary>Socks and boots together.</summary>
        Feet,

        /// <summary>Rucksack. Carries capacity rather than warmth.</summary>
        Back,
    }

    /// <summary>
    /// One piece of clothing, as the thermal model sees it. Clothing is an insulation system,
    /// not armour (docs/03, 3.3): warmth is bought with bulk and carried weight, which is the
    /// Warmth-versus-Weight conflict of the core loop expressed as inventory.
    /// <para>
    /// A plain class with no Unity types, so the whole thermal model stays unit testable without
    /// a scene. The authored data in <c>data/</c> maps onto this.
    /// </para>
    /// </summary>
    public sealed class Garment
    {
        /// <summary>Name as the player sees it.</summary>
        public string Name { get; set; } = "garment";

        /// <summary>Which layer it occupies.</summary>
        public GarmentSlot Slot { get; set; }

        /// <summary>Zones it covers. A coat covers torso and arms.</summary>
        public BodyZone Coverage { get; set; }

        /// <summary>Insulation when dry and undamaged, 0 to 0.35 for a single garment.</summary>
        public float Insulation { get; set; }

        /// <summary>How much of the wind chill it blocks, 0 to 1.</summary>
        public float Windproofing { get; set; }

        /// <summary>How well it keeps water out of itself and the layers beneath, 0 to 1.</summary>
        public float Waterproofing { get; set; }

        /// <summary>Movement and swing penalty, 0 to 1. The price of warmth.</summary>
        public float Bulk { get; set; }

        /// <summary>How wet it is now, 0 to 1. Rises in rain and from sweat, falls near a fire.</summary>
        public float Wetness { get; set; }

        /// <summary>Condition, 0 to 1. Scales insulation and bite resistance.</summary>
        public float Condition { get; set; } = 1f;

        /// <summary>Chance a bite is downgraded to a scratch, 0 to 0.8.</summary>
        public float BiteResistance { get; set; }

        /// <summary>
        /// Insulation as it actually counts right now.
        /// <para>
        /// Wet clothing does not merely insulate less, it insulates far less: water conducts
        /// heat roughly twenty times better than the trapped air that makes a garment warm. A
        /// soaked garment keeps 15% of its dry value, which is what makes "get dry or die" the
        /// real rule of M1 rather than a slogan.
        /// </para>
        /// </summary>
        public float EffectiveInsulation
        {
            get
            {
                var wetnessFactor = 1f - 0.85f * Clamp01(Wetness);
                return Insulation * wetnessFactor * Clamp01(Condition);
            }
        }

        /// <summary>Windproofing as it counts now. A torn shell stops less wind.</summary>
        public float EffectiveWindproofing => Windproofing * Clamp01(Condition);

        /// <summary>True if this garment covers the given zone.</summary>
        public bool Covers(BodyZone zone) => (Coverage & zone) != 0;

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
