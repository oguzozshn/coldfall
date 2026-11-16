using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the sleep rules of docs/03-survival-systems.md, 3.11.
    /// </summary>
    [TestFixture]
    public sealed class SleepTests
    {
        [Test]
        public void AWarmDryCharacterInABedCanSleep()
        {
            Assert.That(
                Sleep.CanSleep(coreCelsius: 36.8f, meanWetness: 0.1f, bedQuality: Sleep.BedQuality, pain: 0f),
                Is.EqualTo(SleepRefusal.None));
        }

        [Test]
        public void TheFloorIsNotABed()
        {
            Assert.That(
                Sleep.CanSleep(36.8f, 0f, bedQuality: 0f, pain: 0f),
                Is.EqualTo(SleepRefusal.NoBed));
        }

        [Test]
        public void TooColdToSleepMeansWarmUpFirst()
        {
            Assert.That(
                Sleep.CanSleep(33.5f, 0f, Sleep.BedQuality, 0f),
                Is.EqualTo(SleepRefusal.TooCold),
                "which means fuel, which means the player cannot simply skip the night");
        }

        [Test]
        public void WetClothingForcesTheCharacterToUndress()
        {
            Assert.That(
                Sleep.CanSleep(36.8f, meanWetness: 0.6f, bedQuality: Sleep.BedQuality, pain: 0f),
                Is.EqualTo(SleepRefusal.ClothesTooWet),
                "the sharpest refusal: it means waking up with nothing on if something comes in");
        }

        [Test]
        public void PainKeepsYouAwake()
        {
            Assert.That(
                Sleep.CanSleep(36.8f, 0f, Sleep.BedQuality, pain: 80f),
                Is.EqualTo(SleepRefusal.TooMuchPain));
        }

        [Test]
        public void TheRefusalOrderPutsTheCheapestFixFirst()
        {
            // Everything wrong at once: the player is told about the bed before the cold,
            // because finding somewhere to lie down is the thing they can act on immediately.
            Assert.That(
                Sleep.CanSleep(30f, 1f, bedQuality: 0f, pain: 100f),
                Is.EqualTo(SleepRefusal.NoBed));
        }

        [Test]
        public void AThreatInTheBuildingBeatsEveryOtherInterruption()
        {
            Assert.That(
                Sleep.CheckInterruption(
                    coreCelsius: 30f, pain: 100f, loudestNearbyNoiseRadius: 100f, threatInBuilding: true),
                Is.EqualTo(SleepInterruption.Threat));
        }

        [Test]
        public void TheColdWakesASleeperBeforeItKillsThem()
        {
            Assert.That(
                Sleep.CheckInterruption(32.5f, 0f, 0f, false),
                Is.EqualTo(SleepInterruption.CoreTooCold));

            Assert.That(Sleep.InterruptCoreCelsius, Is.GreaterThan(CoreTemperature.DeathCelsius),
                "waking up has to happen while there is still something to be done about it");
        }

        [Test]
        public void ALoudNoiseWakesASleeper()
        {
            Assert.That(
                Sleep.CheckInterruption(36.5f, 0f, loudestNearbyNoiseRadius: 30f, threatInBuilding: false),
                Is.EqualTo(SleepInterruption.Noise));

            Assert.That(
                Sleep.CheckInterruption(36.5f, 0f, loudestNearbyNoiseRadius: 4f, threatInBuilding: false),
                Is.EqualTo(SleepInterruption.None),
                "crouch-walking outside must not wake the player");
        }

        [Test]
        public void ABetterBedClearsFatigueFaster()
        {
            var bed = Sleep.FatigueRecoveryPerHour(Sleep.BedQuality);
            var bedroll = Sleep.FatigueRecoveryPerHour(Sleep.BedrollQuality);
            var seat = Sleep.FatigueRecoveryPerHour(Sleep.SeatQuality);

            Assert.That(bed, Is.GreaterThan(bedroll));
            Assert.That(bedroll, Is.GreaterThan(seat));
            Assert.That(bed, Is.EqualTo(Sleep.BaseFatigueRecoveryPerHour).Within(1e-4f));
        }

        [Test]
        public void BeddingWarmsButNeverReplacesAFire()
        {
            var conditions = new ThermalConditions
            {
                AmbientCelsius = -5f,
                Activity = ActivityLevel.Rest,
                BeddingInsulation = Sleep.BeddingInsulation(Sleep.BedQuality),
            };

            var awake = conditions;
            awake.BeddingInsulation = 0f;

            var clothing = Loadouts.Starting();

            var inBed = HeatBalance.Compute(36.5f, conditions, clothing);
            var onTheFloor = HeatBalance.Compute(36.5f, awake, clothing);

            Assert.That(inBed.DegreesPerHour, Is.GreaterThan(onTheFloor.DegreesPerHour));
            Assert.That(inBed.DegreesPerHour, Is.LessThan(0f),
                "a cold room is survivable in a bed, not comfortable");
        }

        [Test]
        public void BeddingObeysTheInsulationCeiling()
        {
            var conditions = new ThermalConditions
            {
                AmbientCelsius = -30f,
                Activity = ActivityLevel.Rest,
                BeddingInsulation = 1f,
            };

            var result = HeatBalance.Compute(36.5f, conditions, Loadouts.Winter());

            Assert.That(result.Convective, Is.GreaterThan(0f),
                "perfect insulation must not exist, even in bed");
        }

        [Test]
        public void SleepingClearsADayOfFatigueInANight()
        {
            var needs = new SurvivalNeeds();
            needs.Set(2000f, 80f, 95f);

            var asleep = new NeedsContext { IsAsleep = true };
            for (var i = 0; i < 8 * 180; i++)
            {
                needs.Step(SimClock.NeedsStepGameSeconds, asleep);
            }

            Assert.That(needs.FatiguePercent, Is.EqualTo(0f).Within(0.01f));
        }
    }
}
