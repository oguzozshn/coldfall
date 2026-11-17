namespace Coldfall.Sim
{
    /// <summary>
    /// What the character is doing while their needs are integrated.
    /// </summary>
    public struct NeedsContext
    {
        /// <summary>Exertion level, which sets calorie and water burn.</summary>
        public ActivityLevel Activity;

        /// <summary>
        /// True while the body is shivering. Shivering is expensive: it is the single largest
        /// unplanned calorie cost in the game, which is how the cold reaches into the food
        /// economy instead of staying a separate meter.
        /// </summary>
        public bool IsShivering;

        /// <summary>True while asleep. Fatigue falls, everything else slows.</summary>
        public bool IsAsleep;

        /// <summary>
        /// Multiplier on hydration drain from illness. Dysentery triples it, which is how an
        /// afternoon of bad water becomes two days of hunting for more.
        /// </summary>
        public float HydrationDrainMultiplier;
    }

    /// <summary>
    /// Calories, hydration and fatigue, from docs/03-survival-systems.md, 3.2.
    /// <para>
    /// Rates are stated per game hour, which only works because the needs tick fires a fixed
    /// 180 times per game hour whatever the time scale (docs/09, 9.1). Nothing here reads a
    /// frame delta.
    /// </para>
    /// <para>
    /// These three are deliberately slow. They are not the threat; the cold is. Their job is to
    /// make the player leave the fire, and to make a bad week compound: a hungry character
    /// generates less heat, which makes them shiver, which burns more calories.
    /// </para>
    /// </summary>
    public sealed class SurvivalNeeds
    {
        /// <summary>A full stomach, in kilocalories.</summary>
        public const float MaxCalories = 3000f;

        /// <summary>Below this the body halves its passive heat production.</summary>
        public const float StarvingCalories = 500f;

        /// <summary>Game hours at zero calories before death.</summary>
        public const float StarvationHoursToDeath = 36f;

        /// <summary>Game hours at zero hydration before death.</summary>
        public const float DehydrationHoursToDeath = 8f;

        /// <summary>Below this hydration, stamina regeneration is halved.</summary>
        public const float DehydratedPercent = 20f;

        /// <summary>Above this fatigue, stamina cap falls and aim sway doubles.</summary>
        public const float ExhaustedPercent = 75f;

        /// <summary>Above this fatigue, the body holds less core temperature.</summary>
        public const float TiredPercent = 60f;

        /// <summary>Core temperature the body gives up when tired, in Celsius.</summary>
        public const float TiredCoreTemperaturePenalty = 0.4f;

        /// <summary>Extra kilocalories per game hour while shivering.</summary>
        public const float ShiverCaloriesPerHour = 150f;

        /// <summary>Current calories, 0 to <see cref="MaxCalories"/>.</summary>
        public float Calories { get; private set; } = MaxCalories * 0.6f;

        /// <summary>Current hydration, 0 to 100.</summary>
        public float HydrationPercent { get; private set; } = 85f;

        /// <summary>Current fatigue, 0 to 100. Higher is worse.</summary>
        public float FatiguePercent { get; private set; }

        /// <summary>Game hours spent at zero calories.</summary>
        public float HoursStarving { get; private set; }

        /// <summary>Game hours spent at zero hydration.</summary>
        public float HoursDehydrated { get; private set; }

        /// <summary>True once the body halves its passive heat production.</summary>
        public bool IsStarving => Calories < StarvingCalories;

        /// <summary>True once stamina regeneration is halved.</summary>
        public bool IsDehydrated => HydrationPercent < DehydratedPercent;

        /// <summary>True once stamina and aim begin to suffer.</summary>
        public bool IsExhausted => FatiguePercent > ExhaustedPercent;

        /// <summary>True once starvation or dehydration has run its course.</summary>
        public bool IsDead =>
            HoursStarving >= StarvationHoursToDeath || HoursDehydrated >= DehydrationHoursToDeath;

        /// <summary>
        /// How much core temperature the body gives up through tiredness. Added to the thermal
        /// model rather than kept here, so exhaustion makes the cold worse instead of being a
        /// separate penalty the player can ignore.
        /// </summary>
        public float CoreTemperaturePenalty =>
            FatiguePercent > TiredPercent ? TiredCoreTemperaturePenalty : 0f;

        /// <summary>Advances all three needs by one tick.</summary>
        /// <param name="gameSeconds">Tick length, from <see cref="SimClock"/>.</param>
        /// <param name="context">What the character is doing.</param>
        public void Step(double gameSeconds, NeedsContext context)
        {
            if (gameSeconds <= 0d)
            {
                return;
            }

            var hours = (float)(gameSeconds / 3600d);

            Calories = Clamp(Calories - CalorieBurnPerHour(context) * hours, 0f, MaxCalories);
            HydrationPercent = Clamp(HydrationPercent - WaterLossPerHour(context) * hours, 0f, 100f);
            FatiguePercent = Clamp(FatiguePercent + FatigueChangePerHour(context) * hours, 0f, 100f);

            HoursStarving = Calories <= 0f ? HoursStarving + hours : 0f;
            HoursDehydrated = HydrationPercent <= 0f ? HoursDehydrated + hours : 0f;
        }

        /// <summary>Kilocalories burned per game hour in the given state.</summary>
        public static float CalorieBurnPerHour(NeedsContext context)
        {
            if (context.IsAsleep)
            {
                return 65f + (context.IsShivering ? ShiverCaloriesPerHour : 0f);
            }

            var basal = context.Activity switch
            {
                ActivityLevel.Rest => 85f,
                ActivityLevel.Light => 160f,
                ActivityLevel.Hard => 400f,
                _ => 85f,
            };

            return basal + (context.IsShivering ? ShiverCaloriesPerHour : 0f);
        }

        /// <summary>Hydration percentage points lost per game hour.</summary>
        public static float WaterLossPerHour(NeedsContext context)
        {
            var baseLoss = context.IsAsleep
                ? 1.5f
                : context.Activity switch
                {
                    ActivityLevel.Rest => 2.5f,
                    ActivityLevel.Light => 4f,
                    ActivityLevel.Hard => 7f,
                    _ => 2.5f,
                };

            // Defaults to 1 when the caller leaves it at zero, so existing callers and saves
            // behave as they did before illness existed.
            var multiplier = context.HydrationDrainMultiplier <= 0f
                ? 1f
                : context.HydrationDrainMultiplier;

            return baseLoss * multiplier;
        }

        /// <summary>Fatigue percentage points gained per game hour. Negative while asleep.</summary>
        public static float FatigueChangePerHour(NeedsContext context)
        {
            return context.IsAsleep ? -12f : 4.2f;
        }

        /// <summary>Eats food. Returns the calories actually taken in.</summary>
        public float Eat(float calories, float hydrationPercent = 0f)
        {
            var before = Calories;
            Calories = Clamp(Calories + calories, 0f, MaxCalories);
            Drink(hydrationPercent);
            return Calories - before;
        }

        /// <summary>Drinks water.</summary>
        public void Drink(float hydrationPercent)
        {
            HydrationPercent = Clamp(HydrationPercent + hydrationPercent, 0f, 100f);
        }

        /// <summary>Sets the three values directly. For loading a save and for tests.</summary>
        public void Set(float calories, float hydrationPercent, float fatiguePercent)
        {
            Calories = Clamp(calories, 0f, MaxCalories);
            HydrationPercent = Clamp(hydrationPercent, 0f, 100f);
            FatiguePercent = Clamp(fatiguePercent, 0f, 100f);
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
