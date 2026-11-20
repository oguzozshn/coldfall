using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the two floors on core temperature that the ten-day soak found missing.
    /// </summary>
    [TestFixture]
    public sealed class CoreTemperatureFloorTests
    {
        private const double NeedsStep = SimClock.NeedsStepGameSeconds;

        private static ThermalConditions Freezing(float ambient) => new ThermalConditions
        {
            AmbientCelsius = ambient,
            WindKilometresPerHour = 40f,
            Activity = ActivityLevel.Rest,
        };

        [Test]
        public void ABodyNeverCoolsBelowTheAirAroundIt()
        {
            var core = new CoreTemperature();
            var conditions = Freezing(-15f);

            for (var i = 0; i < 20000; i++)
            {
                core.Step(NeedsStep, conditions, null);
            }

            Assert.That(core.Celsius, Is.GreaterThanOrEqualTo(-15.01f));
        }

        [Test]
        public void ACorpseStopsCooling()
        {
            var core = new CoreTemperature();
            core.SetCelsius(28.5f);
            var conditions = Freezing(-30f);

            // Step until dead, then well past it.
            for (var i = 0; i < 50000; i++)
            {
                core.Step(NeedsStep, conditions, null);
            }

            Assert.That(core.IsDead, Is.True);
            Assert.That(core.Celsius, Is.GreaterThan(-30.01f));
            Assert.That(float.IsNaN(core.Celsius), Is.False);
        }

        [Test]
        public void SteppingADeadCharacterChangesNothing()
        {
            var core = new CoreTemperature();
            core.SetCelsius(20f);

            var before = core.Celsius;
            core.Step(NeedsStep, Freezing(-20f), null);

            Assert.That(core.Celsius, Is.EqualTo(before));
        }

        [Test]
        public void WarmAirStillWarmsALivingCharacter()
        {
            var core = new CoreTemperature();
            core.SetCelsius(34f);

            var conditions = new ThermalConditions
            {
                AmbientCelsius = 22f,
                Activity = ActivityLevel.Rest,
                RadiantHeat = 10f,
            };

            for (var i = 0; i < 2000; i++)
            {
                core.Step(NeedsStep, conditions, Loadouts.Winter());
            }

            Assert.That(core.Celsius, Is.GreaterThan(34f),
                "the floor must not stop a cold character warming back up");
        }
    }
}
