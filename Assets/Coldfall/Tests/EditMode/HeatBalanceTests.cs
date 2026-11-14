using System.Collections.Generic;
using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the heat balance of docs/03-survival-systems.md, 3.3, and with it the first exit
    /// criterion of M1. None of this needs a scene, which is the point of keeping the whole
    /// thermal model in <c>Coldfall.Sim</c> as plain C#.
    /// </summary>
    [TestFixture]
    public sealed class HeatBalanceTests
    {
        private const double NeedsStep = SimClock.NeedsStepGameSeconds;

        private static Garment Make(
            string name, GarmentSlot slot, BodyZone coverage,
            float insulation, float windproofing = 0f, float wetness = 0f)
        {
            return new Garment
            {
                Name = name,
                Slot = slot,
                Coverage = coverage,
                Insulation = insulation,
                Windproofing = windproofing,
                Wetness = wetness,
            };
        }

        /// <summary>The starting loadout of docs/02-core-loop.md, 2.6: one worn jacket, not enough.</summary>
        private static List<Garment> LightClothing(float wetness)
        {
            return new List<Garment>
            {
                Make("shirt", GarmentSlot.TorsoBase, BodyZone.Torso, 0.10f, 0f, wetness),
                Make("jacket", GarmentSlot.TorsoOuter, BodyZone.Torso | BodyZone.Arms, 0.18f, 0.3f, wetness),
                Make("trousers", GarmentSlot.Legs, BodyZone.Legs, 0.12f, 0f, wetness),
                Make("boots", GarmentSlot.Feet, BodyZone.Feet, 0.10f, 0f, wetness),
            };
        }

        private static List<Garment> WinterClothing()
        {
            return new List<Garment>
            {
                Make("hat", GarmentSlot.Head, BodyZone.Head, 0.28f, 0.5f),
                Make("thermal top", GarmentSlot.TorsoBase, BodyZone.Torso | BodyZone.Arms, 0.22f),
                Make("fleece", GarmentSlot.TorsoMid, BodyZone.Torso | BodyZone.Arms, 0.28f),
                Make("parka", GarmentSlot.TorsoOuter, BodyZone.Torso | BodyZone.Arms, 0.32f, 0.9f),
                Make("gloves", GarmentSlot.Hands, BodyZone.Hands, 0.30f, 0.6f),
                Make("lined trousers", GarmentSlot.Legs, BodyZone.Legs, 0.30f, 0.5f),
                Make("winter boots", GarmentSlot.Feet, BodyZone.Feet, 0.30f, 0.5f),
            };
        }

        private static float MinutesToStage(
            ThermalConditions conditions, IEnumerable<Garment> garments, HypothermiaStage stage,
            float limitMinutes = 240f)
        {
            var core = new CoreTemperature();
            var elapsedSeconds = 0d;

            while (core.Stage < stage)
            {
                core.Step(NeedsStep, conditions, garments);
                elapsedSeconds += NeedsStep;

                if (elapsedSeconds / 60d > limitMinutes)
                {
                    return float.PositiveInfinity;
                }
            }

            return (float)(elapsedSeconds / 60d);
        }

        // --- M1 exit criterion 1 -------------------------------------------------------------

        [Test]
        public void SoakedAndIdleInDrizzleReachesMildHypothermiaInTwentyToThirtyFiveMinutes()
        {
            var conditions = new ThermalConditions
            {
                AmbientCelsius = 6f,
                WindKilometresPerHour = 0f,
                Activity = ActivityLevel.Rest,
            };

            var minutes = MinutesToStage(conditions, LightClothing(wetness: 1f), HypothermiaStage.Mild);

            Assert.That(minutes, Is.InRange(20f, 35f),
                "docs/10-roadmap.md M1 exit criterion 1 states 20-35 game minutes");
        }

        [Test]
        public void DryByAFireDoesNotGetCold()
        {
            var conditions = new ThermalConditions
            {
                AmbientCelsius = 6f,
                WindKilometresPerHour = 0f,
                Activity = ActivityLevel.Rest,
                RadiantHeat = 8f,
            };

            var minutes = MinutesToStage(conditions, LightClothing(wetness: 0f), HypothermiaStage.Cold);

            Assert.That(minutes, Is.EqualTo(float.PositiveInfinity),
                "a fire must hold the core above the cold threshold indefinitely");
        }

        [Test]
        public void GoodClothingWithoutFireStabilisesInsteadOfSpiralling()
        {
            var conditions = new ThermalConditions
            {
                AmbientCelsius = 6f,
                WindKilometresPerHour = 0f,
                Activity = ActivityLevel.Rest,
            };

            // Shivering should be enough to hold the line here: cold and miserable, not dying.
            var minutes = MinutesToStage(conditions, WinterClothing(), HypothermiaStage.Mild);

            Assert.That(minutes, Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void BeingSoakedIsWorseThanBeingDryInTheSameWeather()
        {
            var conditions = new ThermalConditions
            {
                AmbientCelsius = 6f,
                Activity = ActivityLevel.Rest,
            };

            var wet = HeatBalance.Compute(37f, conditions, LightClothing(wetness: 1f));
            var dry = HeatBalance.Compute(37f, conditions, LightClothing(wetness: 0f));

            Assert.That(wet.DegreesPerHour, Is.LessThan(dry.DegreesPerHour));
            Assert.That(wet.Evaporative, Is.GreaterThan(0f));
            Assert.That(dry.Evaporative, Is.EqualTo(0f).Within(1e-6f));
        }

        // --- Clothing ------------------------------------------------------------------------

        [Test]
        public void LayersCombineWithDiminishingReturns()
        {
            var garments = new List<Garment>
            {
                Make("a", GarmentSlot.TorsoBase, BodyZone.Torso, 0.30f),
                Make("b", GarmentSlot.TorsoMid, BodyZone.Torso, 0.30f),
                Make("c", GarmentSlot.TorsoOuter, BodyZone.Torso, 0.30f),
            };

            var result = ClothingInsulation.ForZone(garments, BodyZone.Torso);

            // 1 - 0.7^3 = 0.657, not 0.9.
            Assert.That(result.Insulation, Is.EqualTo(0.657f).Within(0.001f));
        }

        [Test]
        public void InsulationIsCappedBelowPerfect()
        {
            var garments = new List<Garment>();
            for (var i = 0; i < 20; i++)
            {
                garments.Add(Make($"layer{i}", GarmentSlot.TorsoMid, BodyZone.Torso, 0.35f));
            }

            var result = ClothingInsulation.ForZone(garments, BodyZone.Torso);

            Assert.That(result.Insulation, Is.EqualTo(ClothingInsulation.MaxInsulation).Within(1e-5f));
            Assert.That(result.Insulation, Is.LessThan(1f), "perfect insulation must not exist");
        }

        [Test]
        public void WetClothingKeepsOnlyAFractionOfItsInsulation()
        {
            var dry = Make("coat", GarmentSlot.TorsoOuter, BodyZone.Torso, 0.4f);
            var soaked = Make("coat", GarmentSlot.TorsoOuter, BodyZone.Torso, 0.4f, wetness: 1f);

            Assert.That(dry.EffectiveInsulation, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(soaked.EffectiveInsulation, Is.EqualTo(0.06f).Within(1e-5f));
        }

        [Test]
        public void UncoveredZonesHaveNoInsulation()
        {
            var result = ClothingInsulation.ForZone(LightClothing(0f), BodyZone.Hands);

            Assert.That(result.Insulation, Is.EqualTo(0f));
        }

        [Test]
        public void LosingGlovesCostsMoreThanLosingTheSameInsulationOnTheArms()
        {
            var conditions = new ThermalConditions { AmbientCelsius = -10f, Activity = ActivityLevel.Rest };

            var full = WinterClothing();

            var withoutGloves = WinterClothing();
            withoutGloves.RemoveAll(g => g.Slot == GarmentSlot.Hands);

            var baseline = HeatBalance.Compute(37f, conditions, full);
            var noGloves = HeatBalance.Compute(37f, conditions, withoutGloves);

            Assert.That(noGloves.Convective, Is.GreaterThan(baseline.Convective),
                "hands are weighted heavily on purpose; losing gloves must be a crisis");
        }

        // --- Wind ----------------------------------------------------------------------------

        [Test]
        public void WindDoesNotChillAboveTheValidRange()
        {
            Assert.That(WindChill.ChillCelsius(15f, 40f), Is.EqualTo(0f));
        }

        [Test]
        public void StillAirDoesNotChill()
        {
            Assert.That(WindChill.ChillCelsius(-10f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void WindMakesColdAirColder()
        {
            var effective = WindChill.EffectiveCelsius(-10f, 30f);

            Assert.That(effective, Is.LessThan(-10f));
        }

        [Test]
        public void WindproofingBlocksTheChillButNotTheCold()
        {
            var exposed = WindChill.EffectiveCelsius(-10f, 30f);
            var sealed_ = WindChill.EffectiveCelsius(-10f, 30f, windproofing: 1f);

            Assert.That(sealed_, Is.EqualTo(-10f).Within(1e-4f));
            Assert.That(exposed, Is.LessThan(sealed_));
        }

        // --- Shivering -----------------------------------------------------------------------

        [Test]
        public void ShiveringIsAbsentWhenWarm()
        {
            Assert.That(HeatBalance.ShiverHeat(37f), Is.EqualTo(0f));
        }

        [Test]
        public void ShiveringReachesFullStrengthAtTheMildThreshold()
        {
            Assert.That(HeatBalance.ShiverHeat(35f), Is.EqualTo(HeatBalance.ShiverPeakHeat).Within(1e-4f));
        }

        [Test]
        public void ShiveringFailsCompletelyBelowThirtyOne()
        {
            Assert.That(HeatBalance.ShiverHeat(30.5f), Is.EqualTo(0f),
                "docs/03, 3.3: shivering stops, and the fall gets faster from here");
        }

        [Test]
        public void TheFallAcceleratesOnceShiveringFails()
        {
            var conditions = new ThermalConditions { AmbientCelsius = -5f, Activity = ActivityLevel.Rest };
            var clothing = LightClothing(0f);

            var whileShivering = HeatBalance.Compute(34f, conditions, clothing).DegreesPerHour;
            var afterItFails = HeatBalance.Compute(30.5f, conditions, clothing).DegreesPerHour;

            Assert.That(afterItFails, Is.LessThan(whileShivering));
        }

        // --- Metabolism ----------------------------------------------------------------------

        [Test]
        public void StarvingHalvesMetabolicHeat()
        {
            var fed = new ThermalConditions { AmbientCelsius = 0f, Activity = ActivityLevel.Rest };
            var starving = fed;
            starving.IsStarving = true;

            var fedResult = HeatBalance.Compute(37f, fed, LightClothing(0f));
            var starvingResult = HeatBalance.Compute(37f, starving, LightClothing(0f));

            Assert.That(starvingResult.Metabolic, Is.EqualTo(fedResult.Metabolic * 0.5f).Within(1e-5f));
            Assert.That(starvingResult.DegreesPerHour, Is.LessThan(fedResult.DegreesPerHour),
                "starving makes you cold, not merely weak");
        }

        [Test]
        public void WorkingHardGeneratesMoreHeatThanResting()
        {
            Assert.That(
                HeatBalance.BasalHeat(ActivityLevel.Hard),
                Is.GreaterThan(HeatBalance.BasalHeat(ActivityLevel.Rest)));
        }

        [Test]
        public void BreathingCostsNothingAboveFreezing()
        {
            Assert.That(HeatBalance.RespiratoryLoss(1f), Is.EqualTo(0f));
            Assert.That(HeatBalance.RespiratoryLoss(-20f), Is.EqualTo(0.6f).Within(1e-5f));
        }

        // --- Integration ---------------------------------------------------------------------

        [Test]
        public void IdenticalInputsProduceIdenticalResults()
        {
            var conditions = new ThermalConditions { AmbientCelsius = -8f, Activity = ActivityLevel.Light };

            var first = new CoreTemperature();
            var second = new CoreTemperature();

            for (var i = 0; i < 500; i++)
            {
                first.Step(NeedsStep, conditions, LightClothing(0.4f));
                second.Step(NeedsStep, conditions, LightClothing(0.4f));
            }

            Assert.That(first.Celsius, Is.EqualTo(second.Celsius));
        }

        [Test]
        public void StageBoundariesMatchTheDocument()
        {
            Assert.That(CoreTemperature.StageFor(37f), Is.EqualTo(HypothermiaStage.Normal));
            Assert.That(CoreTemperature.StageFor(36.0f), Is.EqualTo(HypothermiaStage.Cold));
            Assert.That(CoreTemperature.StageFor(34f), Is.EqualTo(HypothermiaStage.Mild));
            Assert.That(CoreTemperature.StageFor(32f), Is.EqualTo(HypothermiaStage.Moderate));
            Assert.That(CoreTemperature.StageFor(29f), Is.EqualTo(HypothermiaStage.Severe));
            Assert.That(CoreTemperature.StageFor(27f), Is.EqualTo(HypothermiaStage.Dead));
        }

        [Test]
        public void TheCoreNeverRisesAboveTheFeverCeiling()
        {
            var conditions = new ThermalConditions
            {
                AmbientCelsius = 20f,
                Activity = ActivityLevel.Hard,
                RadiantHeat = 60f,
            };

            var core = new CoreTemperature();
            for (var i = 0; i < 2000; i++)
            {
                core.Step(NeedsStep, conditions, WinterClothing());
            }

            Assert.That(core.Celsius, Is.LessThanOrEqualTo(CoreTemperature.MaxCelsius));
        }
    }
}
