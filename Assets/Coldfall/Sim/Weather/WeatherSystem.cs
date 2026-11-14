using System;

namespace Coldfall.Sim
{
    /// <summary>
    /// Drives the outdoor temperature, wind, visibility and precipitation that every other
    /// system in M1 reads.
    /// <para>
    /// Fronts are a <b>deterministic sequence</b>, not random rolls. Docs/03-survival-systems.md,
    /// 3.8 requires the player to be able to learn to forecast, and docs/01 pillar 3 requires a
    /// world that runs on its own clock rather than reacting to the player. Both mean the same
    /// thing in code: the weather on day 14 of a given world is always the same weather, so
    /// knowledge of it survives the character's death (pillar 4).
    /// </para>
    /// <para>
    /// The wind telegraph is the one mercy. A front announces itself 20 to 40 minutes early
    /// through a wind shift, and docs/03 states plainly what that is for: learning to read it is
    /// the difference between a survivable blizzard and a fatal one.
    /// </para>
    /// </summary>
    public sealed class WeatherSystem
    {
        /// <summary>Shortest a front lasts, in game minutes.</summary>
        public const float MinFrontMinutes = 120f;

        /// <summary>Longest a front lasts, in game minutes.</summary>
        public const float MaxFrontMinutes = 480f;

        /// <summary>Earliest the wind announces the next front, in game minutes before it.</summary>
        public const float MinTelegraphMinutes = 20f;

        /// <summary>Latest the wind announces the next front, in game minutes before it.</summary>
        public const float MaxTelegraphMinutes = 40f;

        /// <summary>Hour of the day at which ambient temperature is lowest.</summary>
        public const float ColdestHour = 5f;

        private readonly uint _worldSeed;

        private int _frontIndex;
        private float _minutesIntoFront;
        private float _currentFrontMinutes;
        private float _telegraphMinutes;

        /// <summary>Creates the system for a world.</summary>
        /// <param name="worldSeed">
        /// Identifies the world. The same seed always produces the same weather, which is what
        /// makes forecasting a skill rather than a guess.
        /// </param>
        /// <param name="startDay">Day the run begins on, 1-based.</param>
        public WeatherSystem(uint worldSeed = 1u, int startDay = 1)
        {
            _worldSeed = worldSeed;
            Day = startDay < 1 ? 1 : startDay;
            Current = StateForFront(_frontIndex, Day);
            _currentFrontMinutes = FrontLength(_frontIndex);
            _telegraphMinutes = TelegraphLength(_frontIndex);
            Next = StateForFront(_frontIndex + 1, Day);
        }

        /// <summary>The weather now.</summary>
        public WeatherState Current { get; private set; }

        /// <summary>The front that will arrive next.</summary>
        public WeatherState Next { get; private set; }

        /// <summary>The current in-game day, 1-based.</summary>
        public int Day { get; private set; }

        /// <summary>Hour of the current day, 0 to 24, as a fraction.</summary>
        public float HourOfDay { get; private set; } = SimClock.StartHourOfDay;

        /// <summary>Game minutes until the next front arrives.</summary>
        public float MinutesUntilFront => Math.Max(0f, _currentFrontMinutes - _minutesIntoFront);

        /// <summary>
        /// True while the wind is announcing the coming front. The visible cue is the wind
        /// shifting direction and strength, not a UI element.
        /// </summary>
        public bool IsTelegraphing => MinutesUntilFront <= _telegraphMinutes;

        /// <summary>True once the run has crossed into the winter table.</summary>
        public bool IsWinter => Day >= WeatherTable.FirstWinterDay;

        /// <summary>The authored row for the current state.</summary>
        public WeatherDefinition Definition => WeatherTable.For(Current);

        /// <summary>
        /// Ambient temperature outdoors now, in Celsius, interpolated between the row's night
        /// and day figures by the hour.
        /// </summary>
        public float AmbientCelsius
        {
            get
            {
                var definition = Definition;
                var warmth = DayWarmth(HourOfDay);
                return definition.NightCelsius + (definition.DayCelsius - definition.NightCelsius) * warmth;
            }
        }

        /// <summary>
        /// Wind now, in kilometres per hour. Rises above the row's figure while a front is being
        /// telegraphed, which is the cue itself.
        /// </summary>
        public float WindKilometresPerHour
        {
            get
            {
                var baseWind = Definition.WindKilometresPerHour;
                if (!IsTelegraphing)
                {
                    return baseWind;
                }

                var incoming = WeatherTable.For(Next).WindKilometresPerHour;
                var progress = _telegraphMinutes <= 0f
                    ? 1f
                    : 1f - MinutesUntilFront / _telegraphMinutes;

                // Gusting towards whatever is coming, so a blizzard announces itself as rising
                // wind rather than as a notification.
                return baseWind + (incoming - baseWind) * Clamp01(progress) * 0.6f;
            }
        }

        /// <summary>Visibility in metres.</summary>
        public float VisibilityMetres => Definition.VisibilityMetres;

        /// <summary>Builds the wetness conditions for a character outdoors in this weather.</summary>
        /// <param name="shelter">How sheltered they are, 0 to 1.</param>
        /// <param name="radiantHeat">Radiant heat from a nearby fire.</param>
        /// <param name="exertion">How hard they are working, 0 to 1.</param>
        public WetnessConditions WetnessFor(
            float shelter = 0f, float radiantHeat = 0f, float exertion = 0f,
            bool indoors = false, float clothingBulk = 0f)
        {
            return new WetnessConditions
            {
                SoakPerHour = Definition.SoakPerHour,
                Shelter = Clamp01(shelter),
                RadiantHeat = radiantHeat,
                AmbientCelsius = AmbientCelsius,
                IsIndoors = indoors,
                Exertion = exertion,
                ClothingBulk = clothingBulk,
            };
        }

        /// <summary>Builds the thermal conditions for a character outdoors in this weather.</summary>
        public ThermalConditions ThermalFor(
            ActivityLevel activity, float windShelter = 0f, float radiantHeat = 0f, bool starving = false)
        {
            return new ThermalConditions
            {
                AmbientCelsius = AmbientCelsius,
                WindKilometresPerHour = WindKilometresPerHour * (1f - Clamp01(windShelter)),
                Activity = activity,
                RadiantHeat = radiantHeat,
                IsStarving = starving,
            };
        }

        /// <summary>Advances the weather. Driven by the world tick.</summary>
        /// <param name="gameMinutes">Minutes of game time elapsed.</param>
        public void Step(float gameMinutes)
        {
            if (gameMinutes <= 0f)
            {
                return;
            }

            HourOfDay += gameMinutes / 60f;
            while (HourOfDay >= 24f)
            {
                HourOfDay -= 24f;
                Day++;
            }

            _minutesIntoFront += gameMinutes;

            while (_minutesIntoFront >= _currentFrontMinutes)
            {
                _minutesIntoFront -= _currentFrontMinutes;
                _frontIndex++;

                Current = StateForFront(_frontIndex, Day);
                _currentFrontMinutes = FrontLength(_frontIndex);
                _telegraphMinutes = TelegraphLength(_frontIndex);
                Next = StateForFront(_frontIndex + 1, Day);
            }
        }

        /// <summary>
        /// How warm the hour is, 0 at the coldest point of the night and 1 at the warmest point
        /// of the afternoon. A cosine, so dawn and dusk change fastest.
        /// </summary>
        public static float DayWarmth(float hourOfDay)
        {
            var phase = (hourOfDay - ColdestHour) / 24f * 2f * (float)Math.PI;
            return 0.5f - 0.5f * (float)Math.Cos(phase);
        }

        /// <summary>
        /// The state of a given front. Pure in the front index and the day, so the whole
        /// sequence can be recomputed from nothing and never drifts.
        /// </summary>
        public WeatherState StateForFront(int frontIndex, int day)
        {
            var roll = Hash(_worldSeed, (uint)frontIndex);

            var winterShare = WinterShare(day);
            var useWinter = (roll & 0xFFFF) / 65535f < winterShare;

            var table = useWinter ? WeatherTable.Winter : WeatherTable.Autumn;
            var pick = (int)((roll >> 16) % (uint)table.Length);
            return table[pick];
        }

        /// <summary>
        /// How likely a front is to come from the winter table on a given day. Zero until the
        /// transition, then a ramp, then one. Days 15-19 mix the tables, which is what makes the
        /// first snow that does not settle feel like a warning rather than an event.
        /// </summary>
        public static float WinterShare(int day)
        {
            if (day < WeatherTable.TransitionStartDay)
            {
                return 0f;
            }

            if (day >= WeatherTable.FirstWinterDay)
            {
                return 1f;
            }

            var span = WeatherTable.FirstWinterDay - WeatherTable.TransitionStartDay;
            return (day - WeatherTable.TransitionStartDay + 1) / (float)(span + 1);
        }

        private float FrontLength(int frontIndex)
        {
            var roll = Hash(_worldSeed ^ 0x9E3779B9u, (uint)frontIndex);
            var t = (roll & 0xFFFF) / 65535f;
            return MinFrontMinutes + (MaxFrontMinutes - MinFrontMinutes) * t;
        }

        private float TelegraphLength(int frontIndex)
        {
            var roll = Hash(_worldSeed ^ 0x85EBCA6Bu, (uint)frontIndex);
            var t = (roll & 0xFFFF) / 65535f;
            return MinTelegraphMinutes + (MaxTelegraphMinutes - MinTelegraphMinutes) * t;
        }

        /// <summary>
        /// A fixed integer hash. Deliberately not <c>System.Random</c>: this must give the same
        /// answer on every machine, every build and every replay, and it must be queryable out
        /// of order so a forecast can look ahead without advancing anything.
        /// </summary>
        private static uint Hash(uint seed, uint index)
        {
            var x = seed + index * 0x9E3779B9u;
            x ^= x >> 16;
            x *= 0x85EBCA6Bu;
            x ^= x >> 13;
            x *= 0xC2B2AE35u;
            x ^= x >> 16;
            return x;
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
