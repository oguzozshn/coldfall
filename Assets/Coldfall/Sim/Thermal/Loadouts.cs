using System.Collections.Generic;

namespace Coldfall.Sim
{
    /// <summary>
    /// Authored clothing sets. Temporary: these belong in ScriptableObject data under
    /// <c>data/</c> (docs/09, 9.6), and live here only until that authoring pipeline exists.
    /// </summary>
    public static class Loadouts
    {
        /// <summary>
        /// What the character wakes up in, from docs/02-core-loop.md, 2.6.
        /// <para>
        /// Deliberately insufficient. The jacket is the first lesson of the game: it is not
        /// enough, and the player has to notice that before the weather notices them.
        /// </para>
        /// </summary>
        public static List<Garment> Starting()
        {
            return new List<Garment>
            {
                new Garment
                {
                    Name = "worn shirt",
                    Slot = GarmentSlot.TorsoBase,
                    Coverage = BodyZone.Torso | BodyZone.Arms,
                    Insulation = 0.10f,
                    Bulk = 0.05f,
                },
                new Garment
                {
                    Name = "worn jacket",
                    Slot = GarmentSlot.TorsoOuter,
                    Coverage = BodyZone.Torso | BodyZone.Arms,
                    Insulation = 0.18f,
                    Windproofing = 0.30f,
                    Waterproofing = 0.20f,
                    Bulk = 0.20f,
                    Condition = 0.7f,
                    BiteResistance = 0.10f,
                },
                new Garment
                {
                    Name = "jeans",
                    Slot = GarmentSlot.Legs,
                    Coverage = BodyZone.Legs,
                    Insulation = 0.12f,
                    Bulk = 0.10f,
                },
                new Garment
                {
                    Name = "work boots",
                    Slot = GarmentSlot.Feet,
                    Coverage = BodyZone.Feet,
                    Insulation = 0.14f,
                    Waterproofing = 0.45f,
                    Bulk = 0.10f,
                },
            };
        }

        /// <summary>A full winter set, for testing the other end of the range.</summary>
        public static List<Garment> Winter()
        {
            return new List<Garment>
            {
                new Garment
                {
                    Name = "wool hat", Slot = GarmentSlot.Head, Coverage = BodyZone.Head,
                    Insulation = 0.28f, Windproofing = 0.5f, Bulk = 0.05f,
                },
                new Garment
                {
                    Name = "thermal top", Slot = GarmentSlot.TorsoBase,
                    Coverage = BodyZone.Torso | BodyZone.Arms, Insulation = 0.22f, Bulk = 0.05f,
                },
                new Garment
                {
                    Name = "fleece", Slot = GarmentSlot.TorsoMid,
                    Coverage = BodyZone.Torso | BodyZone.Arms, Insulation = 0.28f, Bulk = 0.15f,
                },
                new Garment
                {
                    Name = "parka", Slot = GarmentSlot.TorsoOuter,
                    Coverage = BodyZone.Torso | BodyZone.Arms, Insulation = 0.32f,
                    Windproofing = 0.9f, Waterproofing = 0.85f, Bulk = 0.35f, BiteResistance = 0.25f,
                },
                new Garment
                {
                    Name = "gloves", Slot = GarmentSlot.Hands, Coverage = BodyZone.Hands,
                    Insulation = 0.30f, Windproofing = 0.6f, Bulk = 0.20f,
                },
                new Garment
                {
                    Name = "lined trousers", Slot = GarmentSlot.Legs, Coverage = BodyZone.Legs,
                    Insulation = 0.30f, Windproofing = 0.5f, Waterproofing = 0.4f, Bulk = 0.20f,
                },
                new Garment
                {
                    Name = "winter boots", Slot = GarmentSlot.Feet, Coverage = BodyZone.Feet,
                    Insulation = 0.30f, Windproofing = 0.5f, Waterproofing = 0.7f, Bulk = 0.15f,
                },
            };
        }
    }
}
