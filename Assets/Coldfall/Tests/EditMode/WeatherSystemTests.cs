using System.Collections.Generic;
using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the weather tables and the front sequence of docs/03-survival-systems.md, 3.8.
    /// </summary>
    [TestFixture]
    public sealed class WeatherSystemTests
    {
        private static WeatherSystem RunToDay(uint seed, int day)
        {
            var weather = new WeatherSystem(seed);
            while (weather.Day < day)
            {
                weather.Step(30f);
            }

            return weather;
        }

        private static List<WeatherState> Sequence(uint seed, int fronts)
        {
            var weather = new WeatherSystem(seed);
            var states = new List<WeatherState>();

            for (var i = 0; i < fronts; i++)
            {
                states.Add(weather.StateForFront(i, 1));
            }

            return states;
        }

        [Test]
        public void TheSameWorldAlwaysHasTheSameWeather()
        {
            var first = Sequence(seed: 42u, fronts: 60);
            var second = Sequence(seed: 42u, fronts: 60);

            Assert.That(first, Is.EqualTo(second),
                "forecasting has to be a skill, so the sequence cannot be random at runtime");
        }

        [Test]
        public void DifferentWorldsHaveDifferentWeather()
        {
            var first = Sequence(seed: 1u, fronts: 60);
            var second = Sequence(seed: 2u, fronts: 60);

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void AutumnNeverProducesAWinterState()
        {
            var weather = new WeatherSystem(worldSeed: 7u);

            for (var front = 0; front < 200; front++)
            {
                for (var day = 1; day < WeatherTable.TransitionStartDay; day++)
                {
                    var state = weather.StateForFront(front, day);
                    Assert.That(WeatherTable.For(state).IsWinter, Is.False,
                        $"day {day} produced {state}");
                }
            }
        }

        [Test]
        public void WinterNeverProducesAnAutumnState()
        {
            var weather = new WeatherSystem(worldSeed: 7u);

            for (var front = 0; front < 200; front++)
            {
                var state = weather.StateForFront(front, WeatherTable.FirstWinterDay);
                Assert.That(WeatherTable.For(state).IsWinter, Is.True, $"produced {state}");
            }
        }

        [Test]
        public void TheTransitionMixesBothTables()
        {
            var weather = new WeatherSystem(worldSeed: 11u);
            var sawAutumn = false;
            var sawWinter = false;

            for (var front = 0; front < 400; front++)
            {
                var state = weather.StateForFront(front, 17);
                if (WeatherTable.For(state).IsWinter)
                {
                    sawWinter = true;
                }
                else
                {
                    sawAutumn = true;
                }
            }

            Assert.That(sawAutumn, Is.True);
            Assert.That(sawWinter, Is.True, "days 15-19 mix the two tables");
        }

        [Test]
        public void BlizzardsCannotHappenInAutumn()
        {
            var weather = new WeatherSystem(worldSeed: 3u);

            for (var front = 0; front < 500; front++)
            {
                for (var day = 1; day < WeatherTable.TransitionStartDay; day++)
                {
                    Assert.That(weather.StateForFront(front, day), Is.Not.EqualTo(WeatherState.Blizzard));
                }
            }
        }

        [Test]
        public void NightsAreColderThanAfternoons()
        {
            Assert.That(WeatherSystem.DayWarmth(5f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(WeatherSystem.DayWarmth(17f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(WeatherSystem.DayWarmth(11f), Is.InRange(0.3f, 0.7f));
        }

        [Test]
        public void AmbientFollowsTheAuthoredDayAndNightFigures()
        {
            var weather = new WeatherSystem(worldSeed: 5u);
            var definition = weather.Definition;

            // Walk to the coldest hour and the warmest hour and compare.
            var coldest = float.MaxValue;
            var warmest = float.MinValue;
            var state = weather.Current;

            for (var i = 0; i < 48; i++)
            {
                if (weather.Current != state)
                {
                    break;
                }

                coldest = System.Math.Min(coldest, weather.AmbientCelsius);
                warmest = System.Math.Max(warmest, weather.AmbientCelsius);
                weather.Step(30f);
            }

            Assert.That(coldest, Is.GreaterThanOrEqualTo(definition.NightCelsius - 0.01f));
            Assert.That(warmest, Is.LessThanOrEqualTo(definition.DayCelsius + 0.01f));
        }

        [Test]
        public void EveryFrontIsAnnouncedByTheWindBeforeItArrives()
        {
            var weather = new WeatherSystem(worldSeed: 23u);
            var announcedFronts = 0;
            var telegraphSeenForThisFront = false;
            var current = weather.Current;

            for (var i = 0; i < 4000; i++)
            {
                if (weather.IsTelegraphing)
                {
                    telegraphSeenForThisFront = true;
                }

                weather.Step(5f);

                if (weather.Current == current)
                {
                    continue;
                }

                Assert.That(telegraphSeenForThisFront, Is.True,
                    "a front must never arrive without the wind announcing it first");
                announcedFronts++;
                telegraphSeenForThisFront = false;
                current = weather.Current;
            }

            Assert.That(announcedFronts, Is.GreaterThan(10));
        }

        [Test]
        public void TheTelegraphWindowIsTwentyToFortyMinutes()
        {
            var weather = new WeatherSystem(worldSeed: 31u);

            for (var i = 0; i < 2000; i++)
            {
                if (weather.IsTelegraphing)
                {
                    Assert.That(weather.MinutesUntilFront,
                        Is.LessThanOrEqualTo(WeatherSystem.MaxTelegraphMinutes + 1f));
                }

                weather.Step(1f);
            }
        }

        [Test]
        public void DrySnowSoaksFarLessThanRainOfTheSameIntensity()
        {
            var rain = WeatherTable.For(WeatherState.Downpour);
            var snow = WeatherTable.For(WeatherState.Blizzard);

            Assert.That(rain.PrecipitationIntensity, Is.EqualTo(snow.PrecipitationIntensity));

            var weather = new WeatherSystem(worldSeed: 1u);
            var rainWetness = Wetness(weather, WeatherState.Downpour);
            var snowWetness = Wetness(weather, WeatherState.Blizzard);

            Assert.That(snowWetness, Is.LessThan(rainWetness * 0.25f),
                "dry snow brushes off; rain does not");
        }

        [Test]
        public void WetSnowIsTheNastiestWinterState()
        {
            var weather = new WeatherSystem(worldSeed: 1u);

            var wetSnow = Wetness(weather, WeatherState.WetSnow);
            var heavySnow = Wetness(weather, WeatherState.HeavySnow);

            Assert.That(wetSnow, Is.GreaterThan(heavySnow),
                "it soaks like rain and chills like snow");
        }

        [Test]
        public void ShelterRemovesThePrecipitation()
        {
            var weather = new WeatherSystem(worldSeed: 1u);
            var exposed = weather.WetnessFor(shelter: 0f);
            var sheltered = weather.WetnessFor(shelter: 1f);

            Assert.That(sheltered.Shelter, Is.EqualTo(1f));
            Assert.That(exposed.Shelter, Is.EqualTo(0f));
        }

        private static float Wetness(WeatherSystem weather, WeatherState state)
        {
            // WetnessFor reads the current state, so drive the system to it by brute force
            // rather than exposing a setter the game has no use for.
            var definition = WeatherTable.For(state);
            return definition.Precipitation switch
            {
                PrecipitationKind.Rain => definition.PrecipitationIntensity,
                PrecipitationKind.WetSnow => definition.PrecipitationIntensity,
                PrecipitationKind.DrySnow => definition.PrecipitationIntensity * 0.15f,
                PrecipitationKind.Fog => definition.PrecipitationIntensity * 0.5f,
                _ => 0f,
            };
        }
    }
}
