namespace Coldfall.Sim
{
    /// <summary>
    /// The six body parts of docs/03-survival-systems.md, 3.5. Distinct from
    /// <see cref="BodyZone"/>, which is about clothing coverage and heat loss: a character has
    /// two arms that can be hurt separately but one arms zone for insulation.
    /// </summary>
    public enum BodyPart
    {
        /// <summary>Head. Concussion makes audio direction unreliable, which is near fatal.</summary>
        Head,

        /// <summary>Torso. Sets the stamina cap and how loudly the character breathes.</summary>
        Torso,

        /// <summary>Left arm.</summary>
        LeftArm,

        /// <summary>Right arm.</summary>
        RightArm,

        /// <summary>Left leg.</summary>
        LeftLeg,

        /// <summary>Right leg.</summary>
        RightLeg,
    }

    /// <summary>The wound types of docs/03, 3.5.</summary>
    public enum WoundType
    {
        /// <summary>Claw, thorn or broken glass. Minor.</summary>
        Scratch,

        /// <summary>Blade or heavy impact.</summary>
        Laceration,

        /// <summary>Impalement or a bad fall. Bleeds out fast.</summary>
        DeepWound,

        /// <summary>A zombie bite. Bleeds, and carries the fungal infection.</summary>
        Bite,

        /// <summary>A broken bone. Immobilises the limb for weeks.</summary>
        Fracture,

        /// <summary>A burn. Never bandage a fresh one.</summary>
        Burn,

        /// <summary>Frostbite. Permanent, partial.</summary>
        Frostbite,

        /// <summary>Glass left in the wound. Will not heal until removed.</summary>
        GlassShard,

        /// <summary>Wet feet for a day. The wetness axis reaching the body directly.</summary>
        TrenchFoot,
    }

    /// <summary>The authored row for a wound type.</summary>
    public readonly struct WoundDefinition
    {
        /// <summary>Blood lost per game hour, as a percentage of total, while untreated.</summary>
        public float BleedPerHour { get; }

        /// <summary>Pain contributed, 0 to 100.</summary>
        public float Pain { get; }

        /// <summary>Chance of becoming a bacterial infection site if left unclean, 0 to 1.</summary>
        public float InfectionRisk { get; }

        /// <summary>Game hours to heal once treated. Infinite for the permanent ones.</summary>
        public float HealHours { get; }

        /// <summary>True if a bandage alone cannot stop the bleeding.</summary>
        public bool NeedsSuture { get; }

        /// <summary>True if the wound leaves a permanent mark.</summary>
        public bool IsPermanent { get; }

        /// <summary>Creates a row.</summary>
        public WoundDefinition(
            float bleedPerHour, float pain, float infectionRisk,
            float healHours, bool needsSuture, bool isPermanent)
        {
            BleedPerHour = bleedPerHour;
            Pain = pain;
            InfectionRisk = infectionRisk;
            HealHours = healHours;
            NeedsSuture = needsSuture;
            IsPermanent = isPermanent;
        }
    }

    /// <summary>
    /// The authored wound table.
    /// <para>
    /// Bleed rates are percentages of total blood per game hour, which puts the document's own
    /// outcomes into the arithmetic: a deep wound at 420 empties a character in a quarter of an
    /// hour, matching the four-to-twenty minute bleed-out, and a laceration at 45 gives them
    /// about two hours to notice.
    /// </para>
    /// </summary>
    public static class WoundTable
    {
        private static readonly WoundDefinition[] Definitions =
        {
            //                     bleed  pain  infection  heal     suture  permanent
            new WoundDefinition(6f, 4f, 0.07f, 24f, false, false),
            new WoundDefinition(45f, 12f, 0.25f, 72f, true, false),
            new WoundDefinition(420f, 28f, 0.35f, 144f, true, false),
            new WoundDefinition(180f, 22f, 0f, 96f, false, false),
            new WoundDefinition(0f, 34f, 0f, 360f, false, true),
            new WoundDefinition(0f, 26f, 0.30f, 120f, false, false),
            new WoundDefinition(0f, 10f, 0f, float.PositiveInfinity, false, true),
            new WoundDefinition(25f, 14f, 0.20f, 48f, false, false),
            new WoundDefinition(0f, 16f, 0.15f, 72f, false, false),
        };

        /// <summary>The authored row for a wound type.</summary>
        public static WoundDefinition For(WoundType type) => Definitions[(int)type];
    }

    /// <summary>
    /// One wound on one body part.
    /// </summary>
    public sealed class Wound
    {
        /// <summary>What kind of wound it is.</summary>
        public WoundType Type { get; }

        /// <summary>Where it is.</summary>
        public BodyPart Part { get; }

        /// <summary>Bandaged, which slows most bleeding to almost nothing.</summary>
        public bool IsBandaged { get; set; }

        /// <summary>Cleaned, which removes the bacterial infection risk.</summary>
        public bool IsCleaned { get; set; }

        /// <summary>Sutured, which is what actually stops a laceration or a deep wound.</summary>
        public bool IsSutured { get; set; }

        /// <summary>Splinted, for a fracture.</summary>
        public bool IsSplinted { get; set; }

        /// <summary>Glass still in the wound. Nothing heals while it is there.</summary>
        public bool HasEmbeddedGlass { get; set; }

        /// <summary>Healing progress, 0 to 1.</summary>
        public float Healing { get; private set; }

        /// <summary>Game hours since it was inflicted.</summary>
        public float AgeHours { get; private set; }

        /// <summary>True once it has healed as far as it ever will.</summary>
        public bool IsHealed => Healing >= 1f;

        /// <summary>The authored row.</summary>
        public WoundDefinition Definition => WoundTable.For(Type);

        /// <summary>Creates a wound.</summary>
        public Wound(WoundType type, BodyPart part)
        {
            Type = type;
            Part = part;
            HasEmbeddedGlass = type == WoundType.GlassShard;
        }

        /// <summary>
        /// Blood lost per game hour right now.
        /// <para>
        /// A bandage is first aid, not a fix: it takes most of the bleeding off a scratch and
        /// buys time on a laceration, but a deep wound needs suturing or the character dies with
        /// a tidy bandage on.
        /// </para>
        /// </summary>
        public float BleedPerHour
        {
            get
            {
                if (IsHealed)
                {
                    return 0f;
                }

                var bleed = Definition.BleedPerHour;
                if (bleed <= 0f)
                {
                    return 0f;
                }

                if (IsSutured)
                {
                    return 0f;
                }

                if (HasEmbeddedGlass)
                {
                    // Nothing stops a wound with glass still in it.
                    return bleed * (IsBandaged ? 0.6f : 1f);
                }

                if (!IsBandaged)
                {
                    return bleed;
                }

                return Definition.NeedsSuture ? bleed * 0.25f : bleed * 0.1f;
            }
        }

        /// <summary>Pain contributed right now.</summary>
        public float Pain
        {
            get
            {
                if (IsHealed)
                {
                    return 0f;
                }

                var pain = Definition.Pain * (1f - Healing * 0.7f);

                if (Type == WoundType.Fracture && IsSplinted)
                {
                    pain *= 0.55f;
                }
                else if (IsBandaged)
                {
                    pain *= 0.8f;
                }

                return pain;
            }
        }

        /// <summary>True if this wound can still become a bacterial infection site.</summary>
        public bool CanFester => !IsCleaned && !IsHealed && Definition.InfectionRisk > 0f;

        /// <summary>Advances healing and age.</summary>
        public void Step(float gameHours)
        {
            if (gameHours <= 0f)
            {
                return;
            }

            AgeHours += gameHours;

            if (IsHealed || float.IsPositiveInfinity(Definition.HealHours))
            {
                return;
            }

            // Glass in the wound, or a fracture without a splint, stops healing entirely.
            if (HasEmbeddedGlass)
            {
                return;
            }

            if (Type == WoundType.Fracture && !IsSplinted)
            {
                return;
            }

            // Untreated wounds still close, just far more slowly and with worse outcomes.
            // What counts as treated depends on the wound: a fracture is never bandaged, it is
            // splinted, and a splinted fracture is a treated one.
            var rate = 1f / Definition.HealHours;
            var treated = Type == WoundType.Fracture ? IsSplinted : IsBandaged;
            if (!treated)
            {
                rate *= 0.4f;
            }

            Healing += rate * gameHours;
            if (Healing > 1f)
            {
                Healing = 1f;
            }
        }
    }
}
