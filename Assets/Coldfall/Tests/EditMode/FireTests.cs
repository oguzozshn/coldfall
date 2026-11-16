using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the heat source table and the fuel economy behind it.
    /// </summary>
    [TestFixture]
    public sealed class FireTests
    {
        private const double WorldStep = 60d;

        private static void Burn(Fire fire, float gameHours)
        {
            var ticks = (int)(gameHours * 3600d / WorldStep);
            for (var i = 0; i < ticks; i++)
            {
                fire.Step(WorldStep);
            }
        }

        [Test]
        public void AFireBurnsItsFuelAtTheAuthoredRate()
        {
            var fire = new Fire(HeatSourceKind.WoodStove, fuel: 9f);
            fire.Light();

            Burn(fire, 5f);

            // 0.9 kg per hour for five hours.
            Assert.That(fire.Fuel, Is.EqualTo(4.5f).Within(0.05f));
        }

        [Test]
        public void AFireGoesOutWhenItRunsDry()
        {
            var fire = new Fire(HeatSourceKind.Campfire, fuel: 1.5f);
            fire.Light();

            Assert.That(fire.IsLit, Is.True);

            Burn(fire, 2f);

            Assert.That(fire.IsLit, Is.False);
            Assert.That(fire.Fuel, Is.EqualTo(0f));
            Assert.That(fire.RoomPower, Is.EqualTo(0f));
        }

        [Test]
        public void AnUnlitFireGivesNothing()
        {
            var fire = new Fire(HeatSourceKind.WoodStove, fuel: 20f);

            Assert.That(fire.RoomPower, Is.EqualTo(0f));
            Assert.That(fire.RadiantAt(1f), Is.EqualTo(0f));
            Assert.That(fire.NoiseRadius, Is.EqualTo(0f));
            Assert.That(fire.LightRadius, Is.EqualTo(0f));
        }

        [Test]
        public void AFireWithNoFuelCannotBeLit()
        {
            var fire = new Fire(HeatSourceKind.Campfire);

            Assert.That(fire.Light(), Is.False);
            Assert.That(fire.IsLit, Is.False);
        }

        [Test]
        public void RadiantHeatFallsOffWithDistance()
        {
            var fire = new Fire(HeatSourceKind.Campfire, fuel: 10f);
            fire.Light();

            var atOne = fire.RadiantAt(1f);
            var atThree = fire.RadiantAt(3f);

            Assert.That(atThree, Is.EqualTo(atOne / 9f).Within(0.01f),
                "one over distance squared");
            Assert.That(atThree, Is.LessThan(atOne * 0.2f),
                "sitting at the fire has to be worth doing");
        }

        [Test]
        public void RadiantHeatDoesNotGrowWithoutBoundAtZeroDistance()
        {
            var fire = new Fire(HeatSourceKind.Campfire, fuel: 10f);
            fire.Light();

            Assert.That(fire.RadiantAt(0f), Is.EqualTo(fire.RadiantAt(1f)).Within(1e-4f),
                "standing in the fire is not infinitely warm; it is a different problem");
        }

        [Test]
        public void AWoodStoveIsTheBestHeatPerKilogram()
        {
            var stove = HeatSourceTable.For(HeatSourceKind.WoodStove);
            var fireplace = HeatSourceTable.For(HeatSourceKind.Fireplace);
            var campfire = HeatSourceTable.For(HeatSourceKind.Campfire);

            var stoveEfficiency = stove.RoomPower / stove.FuelPerHour;
            var fireplaceEfficiency = fireplace.RoomPower / fireplace.FuelPerHour;
            var campfireEfficiency = campfire.RoomPower / campfire.FuelPerHour;

            Assert.That(stoveEfficiency, Is.GreaterThan(fireplaceEfficiency));
            Assert.That(fireplaceEfficiency, Is.GreaterThan(campfireEfficiency));
        }

        [Test]
        public void ACampfireIsTheLoudestAndTheOnlyOneThatAdvertisesFromAfar()
        {
            var campfire = HeatSourceTable.For(HeatSourceKind.Campfire);
            var stove = HeatSourceTable.For(HeatSourceKind.WoodStove);

            Assert.That(campfire.NoiseRadius, Is.GreaterThan(stove.NoiseRadius));
            Assert.That(campfire.LightRadius, Is.GreaterThan(stove.LightRadius));
            Assert.That(campfire.ProducesSmoke, Is.True);
        }

        [Test]
        public void DampWoodBurnsPoorly()
        {
            var dry = new Fire(HeatSourceKind.Campfire, fuel: 10f) { FuelDryness = 1f };
            var damp = new Fire(HeatSourceKind.Campfire, fuel: 10f) { FuelDryness = 0f };
            dry.Light();
            damp.Light();

            Assert.That(damp.RoomPower, Is.LessThan(dry.RoomPower));
            Assert.That(damp.RadiantAt(1f), Is.LessThan(dry.RadiantAt(1f)));

            Burn(dry, 1f);
            Burn(damp, 1f);

            Assert.That(damp.Fuel, Is.EqualTo(dry.Fuel).Within(0.01f),
                "damp wood gives less heat for the same weight, so autumn burns more to get less");
        }

        [Test]
        public void AnElectricHeaterDiesWithTheGrid()
        {
            var heater = new Fire(HeatSourceKind.ElectricHeater);

            Assert.That(heater.Light(), Is.True);
            Assert.That(heater.RoomPower, Is.GreaterThan(0f));

            heater.GridFailed = true;
            heater.Step(WorldStep);

            Assert.That(heater.IsLit, Is.False,
                "docs/05, 5.5: the grid fails on day 6-8 and takes every electric heater with it");
            Assert.That(heater.Light(), Is.False);
        }

        [Test]
        public void HoursRemainingAnswersWhatThePlayerIsActuallyAsking()
        {
            var fire = new Fire(HeatSourceKind.WoodStove, fuel: 9f);

            Assert.That(fire.HoursRemaining, Is.EqualTo(10f).Within(0.01f));

            fire.Light();
            Burn(fire, 4f);

            Assert.That(fire.HoursRemaining, Is.EqualTo(6f).Within(0.05f));
        }

        [Test]
        public void ANightOfStoveHeatCostsAboutEightKilograms()
        {
            // Docs/02, 2.4 puts the night at eight game hours.
            var fire = new Fire(HeatSourceKind.WoodStove, fuel: 100f);
            fire.Light();

            var before = fire.Fuel;
            Burn(fire, 8f);

            Assert.That(before - fire.Fuel, Is.EqualTo(7.2f).Within(0.1f));
        }

        [Test]
        public void ExtinguishingKeepsTheFuel()
        {
            var fire = new Fire(HeatSourceKind.WoodStove, fuel: 10f);
            fire.Light();
            Burn(fire, 1f);
            fire.Extinguish();

            var remaining = fire.Fuel;
            Burn(fire, 5f);

            Assert.That(fire.Fuel, Is.EqualTo(remaining), "an unlit fire burns nothing");
            Assert.That(fire.Light(), Is.True);
        }
    }
}
