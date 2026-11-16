using System.Collections.Generic;

namespace Coldfall.Sim
{
    /// <summary>
    /// The per-body-part model of docs/03-survival-systems.md, 3.5: wounds, blood, pain, and
    /// the bacterial infection load, plus the functional consequences each part has.
    /// <para>
    /// The consequences are the reason the model is per part rather than a single health bar.
    /// A hurt torso makes the character breathe louder, which is a larger noise radius, which is
    /// the survival half of the design handing the combat half a problem. A hurt leg is slower,
    /// a hurt arm swings worse. None of it is a number the player reads; all of it is something
    /// they feel.
    /// </para>
    /// </summary>
    public sealed class Body
    {
        /// <summary>Blood at full, as a percentage.</summary>
        public const float MaxBlood = 100f;

        /// <summary>Blood below which the character is dead.</summary>
        public const float BleedOutBlood = 0f;

        /// <summary>Blood regained per game hour when not bleeding.</summary>
        public const float BloodRecoveryPerHour = 1.6f;

        /// <summary>Bacterial infection gained per game hour per unclean wound that is festering.</summary>
        public const float InfectionPerHourPerWound = 1.8f;

        /// <summary>Bacterial infection cleared per game hour on antibiotics.</summary>
        public const float InfectionClearedPerHour = 6f;

        /// <summary>Infection above which the character has a fever.</summary>
        public const float FeverInfection = 35f;

        /// <summary>Feet wetness above which trench foot starts accumulating.</summary>
        public const float TrenchFootWetness = 0.6f;

        /// <summary>Game hours of wet feet before trench foot appears.</summary>
        public const float TrenchFootHours = 24f;

        private readonly List<Wound> _wounds = new List<Wound>();
        private readonly Dictionary<BodyPart, float> _frostbite = new Dictionary<BodyPart, float>();

        private float _wetFeetHours;

        /// <summary>Every wound currently on the body, healed ones included until cleared.</summary>
        public IReadOnlyList<Wound> Wounds => _wounds;

        /// <summary>Blood remaining, 0 to 100.</summary>
        public float Blood { get; private set; } = MaxBlood;

        /// <summary>Bacterial infection load, 0 to 100. Separate from the fungal bite infection.</summary>
        public float BacterialInfection { get; private set; }

        /// <summary>True once the character has bled out.</summary>
        public bool HasBledOut => Blood <= BleedOutBlood;

        /// <summary>True while the bacterial infection is high enough to produce a fever.</summary>
        public bool HasFever => BacterialInfection >= FeverInfection;

        /// <summary>True if any wound is a bite. The fungal clock of docs/03, 3.6 starts here.</summary>
        public bool HasBite => Has(WoundType.Bite);

        /// <summary>True while the character is being given antibiotics.</summary>
        public bool OnAntibiotics { get; set; }

        /// <summary>
        /// Painkillers suppress pain and, deliberately, the player's ability to notice new
        /// wounds. Docs/03 calls that trade out explicitly.
        /// </summary>
        public bool OnPainkillers { get; set; }

        /// <summary>Total blood lost per game hour across every wound.</summary>
        public float BleedPerHour
        {
            get
            {
                var total = 0f;
                for (var i = 0; i < _wounds.Count; i++)
                {
                    total += _wounds[i].BleedPerHour;
                }

                return total;
            }
        }

        /// <summary>
        /// Pain, 0 to 100. Painkillers halve what the character feels, which is why they can
        /// walk around on a wound they have not noticed.
        /// </summary>
        public float Pain
        {
            get
            {
                var total = 0f;
                for (var i = 0; i < _wounds.Count; i++)
                {
                    total += _wounds[i].Pain;
                }

                if (OnPainkillers)
                {
                    total *= 0.45f;
                }

                return total > 100f ? 100f : total;
            }
        }

        /// <summary>True if the body carries a wound of the given type that has not healed.</summary>
        public bool Has(WoundType type)
        {
            for (var i = 0; i < _wounds.Count; i++)
            {
                if (_wounds[i].Type == type && !_wounds[i].IsHealed)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True if the part carries an unhealed wound of the given type.</summary>
        public bool Has(BodyPart part, WoundType type)
        {
            for (var i = 0; i < _wounds.Count; i++)
            {
                if (_wounds[i].Part == part && _wounds[i].Type == type && !_wounds[i].IsHealed)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Inflicts a wound and returns it.</summary>
        public Wound Inflict(WoundType type, BodyPart part)
        {
            var wound = new Wound(type, part);
            _wounds.Add(wound);

            if (type == WoundType.Frostbite)
            {
                _frostbite[part] = 1f;
            }

            return wound;
        }

        /// <summary>Permanent frostbite damage on a part, 0 to 1.</summary>
        public float FrostbiteOn(BodyPart part) =>
            _frostbite.TryGetValue(part, out var value) ? value : 0f;

        /// <summary>
        /// Advances wounds, bleeding, blood recovery and infection.
        /// </summary>
        /// <param name="gameSeconds">Tick length, from <see cref="SimClock"/>.</param>
        /// <param name="feetWetness">Mean wetness on the feet, for trench foot.</param>
        public void Step(double gameSeconds, float feetWetness = 0f)
        {
            if (gameSeconds <= 0d)
            {
                return;
            }

            var hours = (float)(gameSeconds / 3600d);

            var bleed = BleedPerHour;
            if (bleed > 0f)
            {
                Blood -= bleed * hours;
                if (Blood < 0f)
                {
                    Blood = 0f;
                }
            }
            else if (Blood < MaxBlood)
            {
                Blood += BloodRecoveryPerHour * hours;
                if (Blood > MaxBlood)
                {
                    Blood = MaxBlood;
                }
            }

            var festering = 0;
            for (var i = 0; i < _wounds.Count; i++)
            {
                _wounds[i].Step(hours);
                if (_wounds[i].CanFester)
                {
                    festering++;
                }
            }

            if (OnAntibiotics)
            {
                BacterialInfection -= InfectionClearedPerHour * hours;
            }
            else if (festering > 0)
            {
                BacterialInfection += InfectionPerHourPerWound * festering * hours;
            }
            else
            {
                BacterialInfection -= InfectionClearedPerHour * 0.25f * hours;
            }

            BacterialInfection = Clamp(BacterialInfection, 0f, 100f);

            StepTrenchFoot(hours, feetWetness);
        }

        /// <summary>
        /// Trench foot: the wetness axis reaching the body directly, and the cheapest possible
        /// way to teach the player that dry socks are loot.
        /// </summary>
        private void StepTrenchFoot(float hours, float feetWetness)
        {
            if (feetWetness >= TrenchFootWetness)
            {
                _wetFeetHours += hours;

                if (_wetFeetHours >= TrenchFootHours
                    && !Has(BodyPart.LeftLeg, WoundType.TrenchFoot))
                {
                    Inflict(WoundType.TrenchFoot, BodyPart.LeftLeg);
                    Inflict(WoundType.TrenchFoot, BodyPart.RightLeg);
                }

                return;
            }

            // Dry feet let the clock run back down, so drying off actually helps rather than
            // merely pausing the damage.
            _wetFeetHours -= hours * 2f;
            if (_wetFeetHours < 0f)
            {
                _wetFeetHours = 0f;
            }
        }

        /// <summary>Removes healed wounds that leave no permanent mark.</summary>
        public void ClearHealed()
        {
            for (var i = _wounds.Count - 1; i >= 0; i--)
            {
                if (_wounds[i].IsHealed && !_wounds[i].Definition.IsPermanent)
                {
                    _wounds.RemoveAt(i);
                }
            }
        }

        // --- Functional consequences -----------------------------------------------------

        /// <summary>
        /// Multiplier on walk and sprint speed. Legs, plus the weakness of blood loss.
        /// </summary>
        public float MovementMultiplier
        {
            get
            {
                var multiplier = 1f;
                multiplier *= LegPenalty(BodyPart.LeftLeg);
                multiplier *= LegPenalty(BodyPart.RightLeg);
                multiplier *= BloodWeakness;
                return multiplier;
            }
        }

        /// <summary>Multiplier on melee damage and swing speed. Arms, plus blood loss.</summary>
        public float MeleeMultiplier
        {
            get
            {
                var multiplier = 1f;
                multiplier *= ArmPenalty(BodyPart.LeftArm);
                multiplier *= ArmPenalty(BodyPart.RightArm);
                multiplier *= BloodWeakness;
                return multiplier;
            }
        }

        /// <summary>Multiplier on the stamina ceiling. Torso wounds and blood loss.</summary>
        public float StaminaCapMultiplier
        {
            get
            {
                var multiplier = BloodWeakness;

                if (Has(BodyPart.Torso, WoundType.Fracture))
                {
                    multiplier *= 0.55f;
                }
                else if (HasAnyWound(BodyPart.Torso))
                {
                    multiplier *= 0.85f;
                }

                return multiplier;
            }
        }

        /// <summary>
        /// Multiplier on how far the character's breathing carries. A hurt torso makes them
        /// audible from further away, which hands the combat layer a problem the survival layer
        /// created.
        /// </summary>
        public float BreathingNoiseMultiplier
        {
            get
            {
                var multiplier = 1f;

                if (HasAnyWound(BodyPart.Torso))
                {
                    multiplier += 0.35f;
                }

                if (HasFever)
                {
                    multiplier += 0.25f;
                }

                if (Blood < 50f)
                {
                    multiplier += 0.2f;
                }

                return multiplier;
            }
        }

        /// <summary>
        /// Aim steadiness, 1 being steady. Falls with arm wounds, pain, fever and blood loss.
        /// </summary>
        public float AimSteadiness
        {
            get
            {
                var steadiness = 1f;
                steadiness *= ArmPenalty(BodyPart.LeftArm);
                steadiness *= ArmPenalty(BodyPart.RightArm);
                steadiness *= 1f - Pain / 250f;
                steadiness *= BloodWeakness;
                return steadiness < 0.1f ? 0.1f : steadiness;
            }
        }

        /// <summary>True while a head wound makes audio direction unreliable.</summary>
        public bool HasConcussion => Has(BodyPart.Head, WoundType.Fracture)
                                     || Has(BodyPart.Head, WoundType.DeepWound);

        private float BloodWeakness
        {
            get
            {
                if (Blood >= 80f)
                {
                    return 1f;
                }

                // Linear down to a floor: at 20% blood the character is barely functional but
                // still moving, because collapsing without warning reads as a bug.
                var t = (Blood - 20f) / 60f;
                return 0.45f + 0.55f * Clamp(t, 0f, 1f);
            }
        }

        private float LegPenalty(BodyPart leg)
        {
            var penalty = 1f;

            if (Has(leg, WoundType.Fracture))
            {
                penalty *= 0.35f;
            }

            if (Has(leg, WoundType.DeepWound))
            {
                penalty *= 0.7f;
            }

            if (Has(leg, WoundType.TrenchFoot))
            {
                penalty *= 0.8f;
            }

            penalty *= 1f - FrostbiteOn(leg) * 0.15f;
            return penalty;
        }

        private float ArmPenalty(BodyPart arm)
        {
            var penalty = 1f;

            if (Has(arm, WoundType.Fracture))
            {
                penalty *= 0.3f;
            }

            if (Has(arm, WoundType.DeepWound))
            {
                penalty *= 0.65f;
            }

            if (Has(arm, WoundType.Laceration))
            {
                penalty *= 0.85f;
            }

            penalty *= 1f - FrostbiteOn(arm) * 0.2f;
            return penalty;
        }

        private bool HasAnyWound(BodyPart part)
        {
            for (var i = 0; i < _wounds.Count; i++)
            {
                if (_wounds[i].Part == part && !_wounds[i].IsHealed)
                {
                    return true;
                }
            }

            return false;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
