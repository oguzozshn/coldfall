using System;

namespace Coldfall.Sim
{
    /// <summary>
    /// The six zones heat is lost through, and the zones clothing covers.
    /// <para>
    /// A flags enum because a garment covers a set of zones, not one: a coat covers the torso
    /// and the arms, trousers cover the legs. Use <see cref="BodyZones.All"/> to iterate.
    /// </para>
    /// </summary>
    [Flags]
    public enum BodyZone
    {
        /// <summary>No zone.</summary>
        None = 0,

        /// <summary>Head, including the face.</summary>
        Head = 1 << 0,

        /// <summary>Chest, back and abdomen.</summary>
        Torso = 1 << 1,

        /// <summary>Upper and lower arms.</summary>
        Arms = 1 << 2,

        /// <summary>Hands. Small, and the first thing to fail.</summary>
        Hands = 1 << 3,

        /// <summary>Thighs and shins.</summary>
        Legs = 1 << 4,

        /// <summary>Feet. Wet feet are how a run quietly ends.</summary>
        Feet = 1 << 5,
    }

    /// <summary>
    /// Per-zone constants for the heat balance in docs/03-survival-systems.md, 3.3.
    /// </summary>
    public static class BodyZones
    {
        /// <summary>Every zone, in a fixed order, for iteration.</summary>
        public static readonly BodyZone[] All =
        {
            BodyZone.Head,
            BodyZone.Torso,
            BodyZone.Arms,
            BodyZone.Hands,
            BodyZone.Legs,
            BodyZone.Feet,
        };

        /// <summary>Everything a full set of clothing would cover.</summary>
        public const BodyZone Everything =
            BodyZone.Head | BodyZone.Torso | BodyZone.Arms |
            BodyZone.Hands | BodyZone.Legs | BodyZone.Feet;

        /// <summary>
        /// Share of total convective loss attributable to each zone. Sums to 1.
        /// <para>
        /// These are <b>not</b> surface areas. The head, hands and feet are weighted far above
        /// their share of the body because that is how heat actually leaves a person, and
        /// because docs/03, 3.3 requires losing gloves to be a crisis rather than a rounding
        /// error. Torso insulation keeps the core alive; extremity insulation is what stops the
        /// core from bleeding heat in the first place.
        /// </para>
        /// </summary>
        public static float LossWeight(BodyZone zone)
        {
            return zone switch
            {
                BodyZone.Head => 0.18f,
                BodyZone.Torso => 0.30f,
                BodyZone.Arms => 0.12f,
                BodyZone.Hands => 0.12f,
                BodyZone.Legs => 0.18f,
                BodyZone.Feet => 0.10f,
                _ => 0f,
            };
        }

        /// <summary>
        /// Whether a zone is an extremity, which is what the frostbite model in docs/03, 3.3
        /// tracks separately from the core.
        /// </summary>
        public static bool IsExtremity(BodyZone zone)
        {
            return zone is BodyZone.Head or BodyZone.Hands or BodyZone.Feet;
        }
    }
}
