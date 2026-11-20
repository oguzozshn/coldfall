using System.Collections.Generic;
using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// The soak test of docs/10-roadmap.md M1 exit criterion 7 and docs/09, 9.10: ten game days
    /// of simulation with no NaN, no runaway values and no unbounded growth.
    /// <para>
    /// Docs/09 calls the soak the single most valuable test in the list, because the design's
    /// whole thesis is a world that degrades correctly over many days and that is not something
    /// anyone can playtest repeatedly by hand. It runs against the simulation directly rather
    /// than through the scene, which is the point of keeping every system in
    /// <c>Coldfall.Sim</c> free of Unity types.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class SoakTests
    {
        private const int Days = 10;
        private const double TotalGameSeconds = Days * 24d * 3600d;

        /// <summary>One simulated character and the world around them.</summary>
        private sealed class Harness
        {
            public readonly SimClock Clock = new SimClock();
            public readonly WeatherSystem Weather = new WeatherSystem(worldSeed: 1234u);
            public readonly CoreTemperature Core = new CoreTemperature();
            public readonly SurvivalNeeds Needs = new SurvivalNeeds();
            public readonly Body Body = new Body();
            public readonly Illnesses Illnesses = new Illnesses();
            public readonly List<Garment> Garments = Loadouts.Winter();

            public ThermalVolume[] Volumes;
            public Portal[] Portals;

            public bool Sheltered;
            public float RadiantHeat;
            public bool TopUpFoodAndWater;

            public int WorldTicks;
            public int NeedsTicks;
            public int BodyTicks;

            public Harness()
            {
                Volumes = new[]
                {
                    new ThermalVolume { IsOutdoors = true, CubicMetres = 1_000_000f, WindExposure = 1f },
                    new ThermalVolume
                    {
                        Celsius = 10f, Insulation = 0.8f, CubicMetres = 60f, HeatInput = 0f,
                    },
                };

                Portals = new[]
                {
                    new Portal
                    {
                        VolumeA = 1, VolumeB = 0,
                        State = PortalState.Closed, AreaSquareMetres = 1.8f,
                    },
                };

                Clock.NeedsTick += OnNeeds;
                Clock.BodyTick += OnBody;
                Clock.WorldTick += OnWorld;
            }

            public float AmbientCelsius => Sheltered ? Volumes[1].Celsius : Weather.AmbientCelsius;

            public float MeanWetness
            {
                get
                {
                    var sum = 0f;
                    foreach (var garment in Garments)
                    {
                        sum += garment.Wetness;
                    }

                    return sum / Garments.Count;
                }
            }

            public void Run()
            {
                // Advanced in hour-long slices so the clock's own runaway guard is never the
                // thing under test.
                var remaining = TotalGameSeconds;
                while (remaining > 0d)
                {
                    var slice = remaining > 3600d ? 3600d : remaining;
                    Clock.AdvanceGame(slice);
                    remaining -= slice;
                }
            }

            private void OnWorld(double gameSeconds)
            {
                WorldTicks++;

                Weather.Step((float)(gameSeconds / 60d));

                Volumes[1].HeatInput = RadiantHeat > 0f ? 300f : 0f;
                ThermalDiffusion.Step(
                    Volumes, Portals, Weather.AmbientCelsius, (float)(gameSeconds / 60d));

                var conditions = Weather.WetnessFor(
                    shelter: Sheltered ? 1f : 0f,
                    radiantHeat: RadiantHeat,
                    exertion: 0f,
                    indoors: Sheltered,
                    clothingBulk: ClothingInsulation.TotalBulk(Garments));

                WetnessSystem.Step(Garments, conditions, gameSeconds);
            }

            private void OnNeeds(double gameSeconds)
            {
                NeedsTicks++;

                Core.Step(gameSeconds, new ThermalConditions
                {
                    AmbientCelsius = AmbientCelsius,
                    WindKilometresPerHour = Sheltered ? 0f : Weather.WindKilometresPerHour,
                    Activity = ActivityLevel.Rest,
                    RadiantHeat = RadiantHeat,
                    IsStarving = Needs.IsStarving,
                }, Garments);

                Needs.Step(gameSeconds, new NeedsContext
                {
                    Activity = ActivityLevel.Rest,
                    IsShivering = Core.LastBalance.Shivering > 0f,
                    HydrationDrainMultiplier = Illnesses.HydrationDrainMultiplier,
                });

                if (TopUpFoodAndWater && Needs.Calories < 1500f)
                {
                    Needs.Eat(1200f, 40f);
                }
            }

            private void OnBody(double gameSeconds)
            {
                BodyTicks++;

                Body.Step(gameSeconds, ClothingInsulation.ForZone(Garments, BodyZone.Feet).Wetness);
                Illnesses.Step(gameSeconds, Core.Celsius, MeanWetness);

                // What the game does: healed wounds that leave no mark are cleared, so the list
                // cannot grow without bound over a long run.
                Body.ClearHealed();
            }
        }

        private static void AssertFinite(float value, string what)
        {
            Assert.That(float.IsNaN(value), Is.False, $"{what} went NaN");
            Assert.That(float.IsInfinity(value), Is.False, $"{what} went infinite");
        }

        [Test]
        public void TenDaysBesideAFireStaysSaneAndAlive()
        {
            var harness = new Harness
            {
                Sheltered = true,
                RadiantHeat = 9f,
                TopUpFoodAndWater = true,
            };

            harness.Run();

            AssertFinite(harness.Core.Celsius, "core temperature");
            Assert.That(harness.Core.Celsius, Is.InRange(33f, CoreTemperature.MaxCelsius),
                "a sheltered character beside a fire must not freeze over ten days");
            Assert.That(harness.Core.IsDead, Is.False);

            AssertFinite(harness.Needs.HydrationPercent, "hydration");
            Assert.That(harness.Needs.HydrationPercent, Is.InRange(0f, 100f));
            Assert.That(harness.Needs.Calories, Is.InRange(0f, SurvivalNeeds.MaxCalories));
            Assert.That(harness.Needs.FatiguePercent, Is.InRange(0f, 100f));
        }

        [Test]
        public void TenDaysExposedKillsCleanlyWithoutBlowingUp()
        {
            var harness = new Harness { Sheltered = false, RadiantHeat = 0f };

            harness.Run();

            AssertFinite(harness.Core.Celsius, "core temperature");
            Assert.That(harness.Core.IsDead, Is.True,
                "ten days outdoors in the open, at rest, with no fire is fatal");

            // The soak originally found this at -1388 C: the integrator kept running after
            // death, and the loss term never shrank because it is measured against a fixed
            // skin target rather than the current core.
            Assert.That(harness.Core.Celsius, Is.GreaterThan(-40f),
                "a body cannot cool below the air around it, and a corpse does not keep cooling");
        }

        [Test]
        public void NothingGrowsWithoutBound()
        {
            var harness = new Harness
            {
                Sheltered = false,
                RadiantHeat = 0f,
                TopUpFoodAndWater = true,
            };

            harness.Run();

            Assert.That(harness.Body.Wounds.Count, Is.LessThan(40),
                "the wound list must not grow for every re-inflicted trench foot");
            Assert.That(harness.Illnesses.Active.Count, Is.LessThanOrEqualTo(5),
                "there are five named conditions and no more");
            Assert.That(harness.Garments.Count, Is.EqualTo(Loadouts.Winter().Count));
        }

        [Test]
        public void TheTickRatiosHoldOverTenDays()
        {
            var harness = new Harness { Sheltered = true, RadiantHeat = 9f, TopUpFoodAndWater = true };

            harness.Run();

            var hours = Days * 24;

            Assert.That(harness.NeedsTicks, Is.EqualTo(hours * 180));
            Assert.That(harness.BodyTicks, Is.EqualTo(hours * 36));
            Assert.That(harness.WorldTicks, Is.EqualTo(hours * 60));
        }

        [Test]
        public void TheCalendarReachesDayElevenAndTheWeatherFollowsIt()
        {
            var harness = new Harness { Sheltered = true, RadiantHeat = 9f, TopUpFoodAndWater = true };

            harness.Run();

            // Starting at 06:00 on day 1, ten days later is 06:00 on day 11.
            Assert.That(harness.Clock.Day, Is.EqualTo(11));
            Assert.That(harness.Weather.Day, Is.EqualTo(11));
            Assert.That(harness.Weather.IsWinter, Is.False, "day 11 is still the autumn table");
        }

        [Test]
        public void RoomTemperaturesStayPhysicalForTenDays()
        {
            var harness = new Harness { Sheltered = true, RadiantHeat = 0f };

            harness.Run();

            var room = harness.Volumes[1].Celsius;

            AssertFinite(room, "room temperature");
            Assert.That(room, Is.InRange(-40f, 45f),
                "an unheated room must settle towards the weather, not oscillate away from it");
            Assert.That(harness.Volumes[0].Celsius,
                Is.EqualTo(harness.Weather.AmbientCelsius).Within(0.01f),
                "the outdoors is the weather, never the rooms venting into it");
        }

        [Test]
        public void WetnessStaysInRangeThroughEveryFront()
        {
            var harness = new Harness { Sheltered = false, RadiantHeat = 0f, TopUpFoodAndWater = true };

            harness.Run();

            foreach (var garment in harness.Garments)
            {
                AssertFinite(garment.Wetness, $"{garment.Name} wetness");
                Assert.That(garment.Wetness, Is.InRange(0f, 1f), garment.Name);
            }
        }

        [Test]
        public void BloodAndInfectionStayInRange()
        {
            var harness = new Harness { Sheltered = false, RadiantHeat = 0f, TopUpFoodAndWater = true };
            harness.Body.Inflict(WoundType.Laceration, BodyPart.LeftArm);

            harness.Run();

            AssertFinite(harness.Body.Blood, "blood");
            Assert.That(harness.Body.Blood, Is.InRange(0f, Body.MaxBlood));
            Assert.That(harness.Body.BacterialInfection, Is.InRange(0f, 100f));
        }

        [Test]
        public void TheSameSeedProducesTheSameTenDays()
        {
            var first = new Harness { Sheltered = false, RadiantHeat = 0f, TopUpFoodAndWater = true };
            var second = new Harness { Sheltered = false, RadiantHeat = 0f, TopUpFoodAndWater = true };

            first.Run();
            second.Run();

            Assert.That(first.Core.Celsius, Is.EqualTo(second.Core.Celsius));
            Assert.That(first.Weather.Current, Is.EqualTo(second.Weather.Current));
            Assert.That(first.MeanWetness, Is.EqualTo(second.MeanWetness));
            Assert.That(first.Needs.HydrationPercent, Is.EqualTo(second.Needs.HydrationPercent));
        }
    }
}
