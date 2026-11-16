using System.Collections.Generic;

namespace Coldfall.Sim
{
    /// <summary>
    /// The five named conditions of docs/03-survival-systems.md, 3.10. Medium depth by design:
    /// simple state machines, no stacking subsystems.
    /// </summary>
    public enum IllnessKind
    {
        /// <summary>
        /// Sustained cold and wet exposure. Costs stamina and produces a recurring cough that
        /// is a noise event the player does not control.
        /// </summary>
        ChestInfection,

        /// <summary>Spoiled or raw food. Nausea, vomiting, lost stamina.</summary>
        FoodPoisoning,

        /// <summary>Tainted water. Triples hydration drain for two days.</summary>
        Dysentery,

        /// <summary>Untreated or dirty wounds. Fever and limb decay.</summary>
        WoundInfection,

        /// <summary>A zombie bite. Terminal without the antifungal course of 3.6.</summary>
        FungalInfection,
    }

    /// <summary>The authored row for an illness.</summary>
    public readonly struct IllnessDefinition
    {
        /// <summary>Game hours it runs for without treatment. Infinite means it will not pass.</summary>
        public float DurationHours { get; }

        /// <summary>Multiplier on the stamina ceiling while ill.</summary>
        public float StaminaMultiplier { get; }

        /// <summary>Multiplier on hydration drain while ill.</summary>
        public float HydrationDrainMultiplier { get; }

        /// <summary>True if it produces a fever.</summary>
        public bool CausesFever { get; }

        /// <summary>True if antibiotics shorten it.</summary>
        public bool TreatableWithAntibiotics { get; }

        /// <summary>True if it kills when it runs its course.</summary>
        public bool IsTerminal { get; }

        /// <summary>Creates a row.</summary>
        public IllnessDefinition(
            float durationHours, float staminaMultiplier, float hydrationDrainMultiplier,
            bool causesFever, bool treatableWithAntibiotics, bool isTerminal)
        {
            DurationHours = durationHours;
            StaminaMultiplier = staminaMultiplier;
            HydrationDrainMultiplier = hydrationDrainMultiplier;
            CausesFever = causesFever;
            TreatableWithAntibiotics = treatableWithAntibiotics;
            IsTerminal = isTerminal;
        }
    }

    /// <summary>The authored illness table.</summary>
    public static class IllnessTable
    {
        /// <summary>How much faster antibiotics clear an illness that answers to them.</summary>
        public const float AntibioticSpeedUp = 4f;

        /// <summary>
        /// Game hours between coughs with a chest infection. Short enough to matter while
        /// sneaking, long enough that the player can sometimes time a move between them.
        /// </summary>
        public const float CoughIntervalHours = 0.08f;

        /// <summary>Noise radius of a cough, in metres. Docs/03, 3.10.</summary>
        public const float CoughNoiseRadius = 10f;

        private static readonly IllnessDefinition[] Definitions =
        {
            //                      hours  stamina  hydration  fever  antibiotics  terminal
            new IllnessDefinition(96f, 0.65f, 1f, true, true, false),
            new IllnessDefinition(18f, 0.60f, 1.4f, false, false, false),
            new IllnessDefinition(48f, 0.85f, 3f, false, true, false),
            new IllnessDefinition(120f, 0.75f, 1f, true, true, false),
            new IllnessDefinition(84f, 0.70f, 1f, true, false, true),
        };

        /// <summary>The authored row for an illness.</summary>
        public static IllnessDefinition For(IllnessKind kind) => Definitions[(int)kind];
    }

    /// <summary>One illness the character currently has.</summary>
    public sealed class Illness
    {
        /// <summary>Which illness.</summary>
        public IllnessKind Kind { get; }

        /// <summary>Game hours it has run so far.</summary>
        public float ElapsedHours { get; private set; }

        /// <summary>Game hours of progress still to run.</summary>
        public float RemainingHours => Definition.DurationHours - ElapsedHours;

        /// <summary>True once it has run its course.</summary>
        public bool IsOver => ElapsedHours >= Definition.DurationHours;

        /// <summary>The authored row.</summary>
        public IllnessDefinition Definition => IllnessTable.For(Kind);

        /// <summary>Creates an illness.</summary>
        public Illness(IllnessKind kind)
        {
            Kind = kind;
        }

        /// <summary>Advances it.</summary>
        /// <param name="gameHours">Game hours elapsed.</param>
        /// <param name="onAntibiotics">True while the character is taking antibiotics.</param>
        public void Step(float gameHours, bool onAntibiotics)
        {
            if (gameHours <= 0f)
            {
                return;
            }

            var rate = onAntibiotics && Definition.TreatableWithAntibiotics
                ? IllnessTable.AntibioticSpeedUp
                : 1f;

            ElapsedHours += gameHours * rate;
        }
    }

    /// <summary>
    /// Everything the character has caught, and what it does to them.
    /// <para>
    /// The cough is the most important thing in here. A chest infection turns the player's own
    /// body into a noise emitter they cannot fully control, which docs/03, 3.10 calls the purest
    /// first-person survival idea in the design. It is also the direct mechanical payoff of the
    /// wetness system: it is what makes rain frightening rather than merely inconvenient.
    /// </para>
    /// </summary>
    public sealed class Illnesses
    {
        /// <summary>Core temperature below which exposure starts accumulating.</summary>
        public const float ExposureCoreCelsius = 35.5f;

        /// <summary>Clothing wetness above which exposure accumulates even when not yet cold.</summary>
        public const float ExposureWetness = 0.6f;

        /// <summary>Game hours of exposure before a chest infection takes hold.</summary>
        public const float ExposureHoursToChestInfection = 8f;

        private readonly List<Illness> _active = new List<Illness>();

        private float _exposureHours;
        private float _sinceCough;

        /// <summary>Everything currently active.</summary>
        public IReadOnlyList<Illness> Active => _active;

        /// <summary>True while the character is taking antibiotics.</summary>
        public bool OnAntibiotics { get; set; }

        /// <summary>
        /// True on the tick a cough happened. The noise system reads this and emits a
        /// <see cref="IllnessTable.CoughNoiseRadius"/> metre event.
        /// </summary>
        public bool CoughedThisTick { get; private set; }

        /// <summary>True if the character has the given illness.</summary>
        public bool Has(IllnessKind kind)
        {
            for (var i = 0; i < _active.Count; i++)
            {
                if (_active[i].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Gives the character an illness, if they do not already have it.</summary>
        public bool Catch(IllnessKind kind)
        {
            if (Has(kind))
            {
                return false;
            }

            _active.Add(new Illness(kind));
            return true;
        }

        /// <summary>Removes an illness outright. For the antifungal cure and for debug tools.</summary>
        public void Cure(IllnessKind kind)
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Kind == kind)
                {
                    _active.RemoveAt(i);
                }
            }
        }

        /// <summary>Multiplier on the stamina ceiling, from every active illness.</summary>
        public float StaminaMultiplier
        {
            get
            {
                var multiplier = 1f;
                for (var i = 0; i < _active.Count; i++)
                {
                    multiplier *= _active[i].Definition.StaminaMultiplier;
                }

                return multiplier;
            }
        }

        /// <summary>Multiplier on hydration drain, from every active illness.</summary>
        public float HydrationDrainMultiplier
        {
            get
            {
                var multiplier = 1f;
                for (var i = 0; i < _active.Count; i++)
                {
                    multiplier *= _active[i].Definition.HydrationDrainMultiplier;
                }

                return multiplier;
            }
        }

        /// <summary>True while any active illness produces a fever.</summary>
        public bool HasFever
        {
            get
            {
                for (var i = 0; i < _active.Count; i++)
                {
                    if (_active[i].Definition.CausesFever)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>True once a terminal illness has run its course.</summary>
        public bool IsDead
        {
            get
            {
                for (var i = 0; i < _active.Count; i++)
                {
                    if (_active[i].Definition.IsTerminal && _active[i].IsOver)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Advances every illness, accumulates cold-and-wet exposure, and decides whether the
        /// character coughs this tick.
        /// </summary>
        /// <param name="gameSeconds">Tick length, from <see cref="SimClock"/>.</param>
        /// <param name="coreCelsius">Current core temperature.</param>
        /// <param name="meanWetness">Mean wetness across what they are wearing.</param>
        public void Step(double gameSeconds, float coreCelsius, float meanWetness)
        {
            CoughedThisTick = false;

            if (gameSeconds <= 0d)
            {
                return;
            }

            var hours = (float)(gameSeconds / 3600d);

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].Step(hours, OnAntibiotics);

                // Terminal illnesses are not removed when they finish: finishing is the death.
                if (_active[i].IsOver && !_active[i].Definition.IsTerminal)
                {
                    _active.RemoveAt(i);
                }
            }

            StepExposure(hours, coreCelsius, meanWetness);
            StepCough(hours);
        }

        /// <summary>
        /// Cold and wet together are what make a chest infection, not either alone. A dry
        /// character who gets cold warms up; a wet one in the cold is ill by morning.
        /// </summary>
        private void StepExposure(float hours, float coreCelsius, float meanWetness)
        {
            var exposed = coreCelsius < ExposureCoreCelsius && meanWetness >= ExposureWetness * 0.5f
                          || meanWetness >= ExposureWetness && coreCelsius < 36.5f;

            if (!exposed)
            {
                _exposureHours -= hours * 1.5f;
                if (_exposureHours < 0f)
                {
                    _exposureHours = 0f;
                }

                return;
            }

            _exposureHours += hours;

            if (_exposureHours >= ExposureHoursToChestInfection)
            {
                if (Catch(IllnessKind.ChestInfection))
                {
                    _exposureHours = 0f;
                }
            }
        }

        private void StepCough(float hours)
        {
            if (!Has(IllnessKind.ChestInfection))
            {
                _sinceCough = 0f;
                return;
            }

            _sinceCough += hours;

            if (_sinceCough < IllnessTable.CoughIntervalHours)
            {
                return;
            }

            _sinceCough -= IllnessTable.CoughIntervalHours;
            CoughedThisTick = true;
        }
    }
}
