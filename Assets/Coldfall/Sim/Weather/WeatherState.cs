namespace Coldfall.Sim
{
    /// <summary>
    /// The weather states of docs/03-survival-systems.md, 3.8. Seven per season.
    /// </summary>
    public enum WeatherState
    {
        /// <summary>Autumn: the good travel day.</summary>
        ClearCool,

        /// <summary>Autumn: mild and dim.</summary>
        Overcast,

        /// <summary>Autumn: light, persistent wetting.</summary>
        Drizzle,

        /// <summary>Autumn: proper rain.</summary>
        Rain,

        /// <summary>Autumn: the worst autumn state.</summary>
        Downpour,

        /// <summary>Autumn: still air, short sight, sound carries unusually far.</summary>
        RiverFog,

        /// <summary>Autumn: first frost, mud freezes solid.</summary>
        ColdSnap,

        /// <summary>Winter: coldest nights, best travel.</summary>
        ClearCold,

        /// <summary>Winter: mild and dim.</summary>
        OvercastCold,

        /// <summary>Winter: dry snow, covers tracks.</summary>
        LightSnow,

        /// <summary>Winter: the nastiest winter state, because it soaks as well as chills.</summary>
        WetSnow,

        /// <summary>Winter: serious travel risk.</summary>
        HeavySnow,

        /// <summary>Winter: lethal outdoors.</summary>
        Blizzard,

        /// <summary>Winter: still air, freezes on contact.</summary>
        FreezingFog,
    }

    /// <summary>What the sky is dropping, which decides how clothing gets wet.</summary>
    public enum PrecipitationKind
    {
        /// <summary>Nothing falling.</summary>
        None,

        /// <summary>Rain, drizzle or downpour.</summary>
        Rain,

        /// <summary>Dry snow. Cold, but it brushes off rather than soaking in.</summary>
        DrySnow,

        /// <summary>Wet snow or sleet. Soaks like rain and chills like snow.</summary>
        WetSnow,

        /// <summary>Fog. Wets slowly but relentlessly, and freezing fog does it at below zero.</summary>
        Fog,
    }

    /// <summary>
    /// The authored row for one weather state.
    /// </summary>
    public readonly struct WeatherDefinition
    {
        /// <summary>Ambient temperature at the warmest point of the day, in Celsius.</summary>
        public float DayCelsius { get; }

        /// <summary>Ambient temperature at the coldest point of the night, in Celsius.</summary>
        public float NightCelsius { get; }

        /// <summary>Wind speed in kilometres per hour.</summary>
        public float WindKilometresPerHour { get; }

        /// <summary>Visibility in metres. The primary difficulty dial (docs/01, 1.3).</summary>
        public float VisibilityMetres { get; }

        /// <summary>What is falling.</summary>
        public PrecipitationKind Precipitation { get; }

        /// <summary>How hard it is falling, 0 to 1. For visuals and audio, not for wetting.</summary>
        public float PrecipitationIntensity { get; }

        /// <summary>
        /// Wetness added per game hour to an exposed, unprotected garment, straight from the
        /// authored table in docs/03-survival-systems.md, 3.4.
        /// <para>
        /// Authored per state rather than derived from intensity, because the two do not track
        /// each other: a blizzard and a downpour are both intensity 1, and the blizzard wets at
        /// a fifteenth of the rate. Dry snow brushes off.
        /// </para>
        /// </summary>
        public float SoakPerHour { get; }

        /// <summary>True for the winter table.</summary>
        public bool IsWinter { get; }

        /// <summary>Creates a row.</summary>
        public WeatherDefinition(
            float dayCelsius, float nightCelsius, float wind, float visibility,
            PrecipitationKind precipitation, float intensity, float soakPerHour, bool isWinter)
        {
            DayCelsius = dayCelsius;
            NightCelsius = nightCelsius;
            WindKilometresPerHour = wind;
            VisibilityMetres = visibility;
            Precipitation = precipitation;
            PrecipitationIntensity = intensity;
            SoakPerHour = soakPerHour;
            IsWinter = isWinter;
        }
    }

    /// <summary>
    /// The two weather tables of docs/03, 3.8, verbatim.
    /// </summary>
    public static class WeatherTable
    {
        /// <summary>The last day of the autumn table.</summary>
        public const int LastAutumnDay = 19;

        /// <summary>The first day of the winter table. A hard switch, with the art dressing.</summary>
        public const int FirstWinterDay = 20;

        /// <summary>First day on which autumn and winter states can both appear.</summary>
        public const int TransitionStartDay = 15;

        private static readonly WeatherDefinition[] Definitions =
        {
            // Autumn. Soak rates from docs/03, 3.4.
            new WeatherDefinition(11f, 3f, 8f, 400f, PrecipitationKind.None, 0f, 0f, false),
            new WeatherDefinition(9f, 4f, 8f, 250f, PrecipitationKind.None, 0f, 0f, false),
            new WeatherDefinition(7f, 3f, 9f, 180f, PrecipitationKind.Rain, 0.3f, 0.04f, false),
            new WeatherDefinition(6f, 2f, 20f, 120f, PrecipitationKind.Rain, 0.65f, 0.18f, false),
            new WeatherDefinition(5f, 2f, 38f, 60f, PrecipitationKind.Rain, 1f, 0.45f, false),
            new WeatherDefinition(6f, 1f, 0f, 30f, PrecipitationKind.Fog, 0.25f, 0.02f, false),
            new WeatherDefinition(2f, -4f, 20f, 300f, PrecipitationKind.None, 0f, 0f, false),

            // Winter
            new WeatherDefinition(-6f, -16f, 8f, 450f, PrecipitationKind.None, 0f, 0f, true),
            new WeatherDefinition(-3f, -9f, 8f, 250f, PrecipitationKind.None, 0f, 0f, true),
            new WeatherDefinition(-4f, -11f, 20f, 120f, PrecipitationKind.DrySnow, 0.4f, 0.03f, true),
            new WeatherDefinition(-1f, -4f, 20f, 100f, PrecipitationKind.WetSnow, 0.7f, 0.12f, true),
            new WeatherDefinition(-8f, -15f, 38f, 50f, PrecipitationKind.DrySnow, 0.8f, 0.03f, true),
            new WeatherDefinition(-14f, -22f, 70f, 15f, PrecipitationKind.DrySnow, 1f, 0.03f, true),
            new WeatherDefinition(-5f, -12f, 0f, 30f, PrecipitationKind.Fog, 0.3f, 0.02f, true),
        };

        /// <summary>The authored row for a state.</summary>
        public static WeatherDefinition For(WeatherState state) => Definitions[(int)state];

        /// <summary>Every autumn state.</summary>
        public static readonly WeatherState[] Autumn =
        {
            WeatherState.ClearCool, WeatherState.Overcast, WeatherState.Drizzle,
            WeatherState.Rain, WeatherState.Downpour, WeatherState.RiverFog, WeatherState.ColdSnap,
        };

        /// <summary>Every winter state.</summary>
        public static readonly WeatherState[] Winter =
        {
            WeatherState.ClearCold, WeatherState.OvercastCold, WeatherState.LightSnow,
            WeatherState.WetSnow, WeatherState.HeavySnow, WeatherState.Blizzard,
            WeatherState.FreezingFog,
        };
    }
}
