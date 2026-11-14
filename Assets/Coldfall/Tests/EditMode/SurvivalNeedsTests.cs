using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the needs rates of docs/03-survival-systems.md, 3.2, and the interactions that
    /// connect them to the thermal model.
    /// </summary>
    [TestFixture]
    public sealed class SurvivalNeedsTests
    {
        private const double NeedsStep = SimClock.NeedsStepGameSeconds;

        private static void Advance(SurvivalNeeds needs, NeedsContext context, float gameHours)
        {
            var ticks = (int)(gameHours * 3600d / NeedsStep);
            for (var i = 0; i < ticks; i++)
            {
                needs.Step(NeedsStep, context);
            }
        }

        [Test]
        public void RestingBurnsTheDocumentedCalories()
        {
            var context = new NeedsContext { Activity = ActivityLevel.Rest };
            var needs = new SurvivalNeeds();
            needs.Set(2000f, 80f, 0f);

            Advance(needs, context, 1f);

            Assert.That(needs.Calories, Is.EqualTo(2000f - 85f).Within(0.5f));
        }

        [Test]
        public void ShiveringIsTheLargestUnplannedCalorieCost()
        {
            var warm = new NeedsContext { Activity = ActivityLevel.Rest };
            var cold = new NeedsContext { Activity = ActivityLevel.Rest, IsShivering = true };

            var warmBurn = SurvivalNeeds.CalorieBurnPerHour(warm);
            var coldBurn = SurvivalNeeds.CalorieBurnPerHour(cold);

            Assert.That(coldBurn - warmBurn, Is.EqualTo(SurvivalNeeds.ShiverCaloriesPerHour).Within(0.01f));
            Assert.That(coldBurn, Is.GreaterThan(warmBurn * 2f),
                "being cold must cost more than simply existing");
        }

        [Test]
        public void HardWorkCostsMoreWaterThanRest()
        {
            Assert.That(
                SurvivalNeeds.WaterLossPerHour(new NeedsContext { Activity = ActivityLevel.Hard }),
                Is.GreaterThan(SurvivalNeeds.WaterLossPerHour(new NeedsContext { Activity = ActivityLevel.Rest })));
        }

        [Test]
        public void SleepClearsFatigueFasterThanWakingBuildsIt()
        {
            var awake = SurvivalNeeds.FatigueChangePerHour(new NeedsContext());
            var asleep = SurvivalNeeds.FatigueChangePerHour(new NeedsContext { IsAsleep = true });

            Assert.That(awake, Is.GreaterThan(0f));
            Assert.That(asleep, Is.LessThan(0f));
            Assert.That(-asleep, Is.GreaterThan(awake));
        }

        [Test]
        public void AFullNightOfSleepClearsAFullDayOfFatigue()
        {
            var needs = new SurvivalNeeds();
            needs.Set(2000f, 80f, 0f);

            Advance(needs, new NeedsContext { Activity = ActivityLevel.Light }, 16f);
            var beforeSleep = needs.FatiguePercent;

            Advance(needs, new NeedsContext { IsAsleep = true }, 8f);

            Assert.That(beforeSleep, Is.GreaterThan(60f));
            Assert.That(needs.FatiguePercent, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void StarvingFeedsBackIntoTheThermalModel()
        {
            var needs = new SurvivalNeeds();
            needs.Set(400f, 80f, 0f);

            Assert.That(needs.IsStarving, Is.True);

            var conditions = new ThermalConditions
            {
                AmbientCelsius = 0f,
                Activity = ActivityLevel.Rest,
                IsStarving = needs.IsStarving,
            };

            var fed = conditions;
            fed.IsStarving = false;

            var starvingResult = HeatBalance.Compute(37f, conditions, null);
            var fedResult = HeatBalance.Compute(37f, fed, null);

            Assert.That(starvingResult.DegreesPerHour, Is.LessThan(fedResult.DegreesPerHour),
                "a hungry character generates less heat, which is how a bad week compounds");
        }

        [Test]
        public void TirednessCostsCoreTemperature()
        {
            var needs = new SurvivalNeeds();

            needs.Set(2000f, 80f, 50f);
            Assert.That(needs.CoreTemperaturePenalty, Is.EqualTo(0f));

            needs.Set(2000f, 80f, 70f);
            Assert.That(needs.CoreTemperaturePenalty, Is.EqualTo(SurvivalNeeds.TiredCoreTemperaturePenalty));
        }

        [Test]
        public void DehydrationKillsFasterThanStarvation()
        {
            Assert.That(
                SurvivalNeeds.DehydrationHoursToDeath,
                Is.LessThan(SurvivalNeeds.StarvationHoursToDeath));
        }

        [Test]
        public void StarvationTakesTheDocumentedTimeToKill()
        {
            var needs = new SurvivalNeeds();

            // Hydration starts full so thirst cannot finish first. At 2.5 points per hour it
            // never reaches zero inside this window, which is itself the point: with half a
            // tank of water a starving character dies of thirst at 28 hours, long before the
            // 36 hours starvation needs.
            needs.Set(0f, 100f, 0f);

            Advance(needs, new NeedsContext { Activity = ActivityLevel.Rest }, 35f);
            Assert.That(needs.IsDead, Is.False, "36 game hours, not 35");

            Advance(needs, new NeedsContext { Activity = ActivityLevel.Rest }, 2f);
            Assert.That(needs.IsDead, Is.True);
        }

        [Test]
        public void EatingResetsTheStarvationClock()
        {
            var needs = new SurvivalNeeds();
            needs.Set(0f, 50f, 0f);

            Advance(needs, new NeedsContext { Activity = ActivityLevel.Rest }, 10f);
            Assert.That(needs.HoursStarving, Is.GreaterThan(9f));

            needs.Eat(800f);
            needs.Step(NeedsStep, new NeedsContext { Activity = ActivityLevel.Rest });

            Assert.That(needs.HoursStarving, Is.EqualTo(0f));
            Assert.That(needs.IsStarving, Is.False);
        }

        [Test]
        public void NeedsNeverLeaveTheirRanges()
        {
            var needs = new SurvivalNeeds();
            needs.Set(10f, 2f, 99f);

            Advance(needs, new NeedsContext { Activity = ActivityLevel.Hard, IsShivering = true }, 50f);

            Assert.That(needs.Calories, Is.InRange(0f, SurvivalNeeds.MaxCalories));
            Assert.That(needs.HydrationPercent, Is.InRange(0f, 100f));
            Assert.That(needs.FatiguePercent, Is.InRange(0f, 100f));

            needs.Eat(99999f, 99999f);

            Assert.That(needs.Calories, Is.EqualTo(SurvivalNeeds.MaxCalories));
            Assert.That(needs.HydrationPercent, Is.EqualTo(100f));
        }

        [Test]
        public void IdenticalInputsProduceIdenticalResults()
        {
            var context = new NeedsContext { Activity = ActivityLevel.Light, IsShivering = true };

            var first = new SurvivalNeeds();
            var second = new SurvivalNeeds();

            for (var i = 0; i < 1000; i++)
            {
                first.Step(NeedsStep, context);
                second.Step(NeedsStep, context);
            }

            Assert.That(first.Calories, Is.EqualTo(second.Calories));
            Assert.That(first.HydrationPercent, Is.EqualTo(second.HydrationPercent));
            Assert.That(first.FatiguePercent, Is.EqualTo(second.FatiguePercent));
        }
    }
}
