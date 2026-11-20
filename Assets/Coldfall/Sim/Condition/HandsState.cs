using System.Collections.Generic;

namespace Coldfall.Sim
{
    /// <summary>
    /// What the player's hands look like right now, as numbers the view can render without
    /// knowing anything about the simulation.
    /// <para>
    /// Docs/08-audio-and-ui.md, 8.5 makes the hands the fastest status check in the game: blood
    /// when bleeding, red and stiff in the cold, visibly wet, shaking when shivering or
    /// exhausted, bandaged where bandaged. Keeping the mapping here rather than in the view
    /// means it is unit tested, and means a real hand model later changes nothing but the mesh.
    /// </para>
    /// </summary>
    public readonly struct HandsState
    {
        /// <summary>How cold the hands look, 0 to 1. Red, then white at the extreme.</summary>
        public float Cold { get; }

        /// <summary>How wet they look, 0 to 1.</summary>
        public float Wet { get; }

        /// <summary>How bloody they look, 0 to 1.</summary>
        public float Blood { get; }

        /// <summary>How hard they shake, 0 to 1.</summary>
        public float Tremor { get; }

        /// <summary>True if a hand or arm wound has been bandaged.</summary>
        public bool Bandaged { get; }

        /// <summary>True once frostbite has marked the hands permanently.</summary>
        public bool Frostbitten { get; }

        /// <summary>Creates a state.</summary>
        public HandsState(float cold, float wet, float blood, float tremor, bool bandaged, bool frostbitten)
        {
            Cold = cold;
            Wet = wet;
            Blood = blood;
            Tremor = tremor;
            Bandaged = bandaged;
            Frostbitten = frostbitten;
        }

        /// <summary>
        /// Builds the state from the simulation.
        /// </summary>
        /// <param name="core">Core temperature, for cold and shivering.</param>
        /// <param name="needs">Fatigue, which also makes hands shake.</param>
        /// <param name="garments">Worn clothing, for how wet the hands are.</param>
        /// <param name="body">Wounds, for blood and bandages. May be null.</param>
        public static HandsState Build(
            CoreTemperature core, SurvivalNeeds needs, IReadOnlyList<Garment> garments, Body body)
        {
            // Cold reads from the stage rather than the raw degrees, so the hands and the
            // condition card can never disagree about how cold the character is.
            var cold = core.Stage switch
            {
                HypothermiaStage.Normal => 0f,
                HypothermiaStage.Cold => 0.35f,
                HypothermiaStage.Mild => 0.65f,
                HypothermiaStage.Moderate => 0.85f,
                _ => 1f,
            };

            var wet = ClothingInsulation.ForZone(garments, BodyZone.Hands).Wetness;

            var blood = 0f;
            var bandaged = false;
            var frostbitten = false;

            if (body != null)
            {
                // Any bleeding anywhere ends up on the hands: the character has been holding it.
                blood = Clamp01(body.BleedPerHour / 60f);

                if (body.Blood < 70f)
                {
                    blood = Max(blood, 0.35f);
                }

                foreach (var wound in body.Wounds)
                {
                    if (wound.IsHealed)
                    {
                        continue;
                    }

                    var onArms = wound.Part == BodyPart.LeftArm || wound.Part == BodyPart.RightArm;
                    if (onArms && (wound.IsBandaged || wound.IsSutured))
                    {
                        bandaged = true;
                    }

                    if (wound.Type == WoundType.Frostbite && onArms)
                    {
                        frostbitten = true;
                    }
                }
            }

            // Shivering and exhaustion both shake the hands, and they add rather than compete:
            // a cold, exhausted character cannot hold anything steady.
            var shiverTremor = core.LastBalance.Shivering / HeatBalance.ShiverPeakHeat;
            var fatigueTremor = needs.FatiguePercent >= SurvivalNeeds.ExhaustedPercent
                ? (needs.FatiguePercent - SurvivalNeeds.ExhaustedPercent) / 25f
                : 0f;

            var tremor = Clamp01(Clamp01(shiverTremor) * 0.7f + Clamp01(fatigueTremor) * 0.5f);

            return new HandsState(cold, wet, blood, tremor, bandaged, frostbitten);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        private static float Max(float a, float b) => a > b ? a : b;
    }
}
