using System.Collections.Generic;
using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the wetness tables of docs/03-survival-systems.md, 3.4, and M1 exit criterion 3:
    /// a soaked three-layer torso set dries in 2.5 to 4 game hours next to a fire, and does not
    /// meaningfully dry outdoors in the rain.
    /// </summary>
    [TestFixture]
    public sealed class WetnessSystemTests
    {
        private const double NeedsStep = SimClock.NeedsStepGameSeconds;

        /// <summary>Soak rate of a downpour, from the authored table.</summary>
        private const float Downpour = 0.45f;

        private static List<Garment> TorsoSet(float wetness, float shellWaterproofing = 0f)
        {
            return new List<Garment>
            {
                new Garment
                {
                    Name = "thermal top", Slot = GarmentSlot.TorsoBase,
                    Coverage = BodyZone.Torso, Insulation = 0.22f, Wetness = wetness,
                },
                new Garment
                {
                    Name = "fleece", Slot = GarmentSlot.TorsoMid,
                    Coverage = BodyZone.Torso, Insulation = 0.28f, Wetness = wetness,
                },
                new Garment
                {
                    Name = "coat", Slot = GarmentSlot.TorsoOuter,
                    Coverage = BodyZone.Torso, Insulation = 0.32f,
                    Waterproofing = shellWaterproofing, Wetness = wetness,
                },
            };
        }

        private static float MeanWetness(IReadOnlyList<Garment> garments)
        {
            var sum = 0f;
            foreach (var garment in garments)
            {
                sum += garment.Wetness;
            }

            return sum / garments.Count;
        }

        private static float HoursToDry(
            List<Garment> garments, WetnessConditions conditions, float limitHours = 24f)
        {
            var seconds = 0d;

            while (MeanWetness(garments) > 0.05f)
            {
                WetnessSystem.Step(garments, conditions, NeedsStep);
                seconds += NeedsStep;

                if (seconds / 3600d > limitHours)
                {
                    return float.PositiveInfinity;
                }
            }

            return (float)(seconds / 3600d);
        }

        [Test]
        public void SoakedTorsoSetDriesByAFireInTwoAndAHalfToFourHours()
        {
            var conditions = new WetnessConditions
            {
                RadiantHeat = 8f,
                AmbientCelsius = 15f,
                Shelter = 1f,
                IsIndoors = true,
            };

            var hours = HoursToDry(TorsoSet(wetness: 1f), conditions);

            Assert.That(hours, Is.InRange(2.5f, 4f), "docs/10 M1 exit criterion 3");
        }

        [Test]
        public void NothingDriesStandingOutsideInTheRain()
        {
            var conditions = new WetnessConditions
            {
                SoakPerHour = Downpour,
                Shelter = 0f,
                AmbientCelsius = 6f,
            };

            var garments = TorsoSet(wetness: 1f);
            var hours = HoursToDry(garments, conditions, limitHours: 8f);

            Assert.That(hours, Is.EqualTo(float.PositiveInfinity));
            Assert.That(MeanWetness(garments), Is.GreaterThan(0.9f));
        }

        [Test]
        public void ADownpourSoaksAnUnprotectedGarmentInAboutTwoHours()
        {
            var conditions = new WetnessConditions
            {
                SoakPerHour = Downpour, Shelter = 0f, AmbientCelsius = 5f,
            };

            var garments = TorsoSet(wetness: 0f);
            var coat = garments.Find(g => g.Slot == GarmentSlot.TorsoOuter);

            var seconds = 0d;
            while (coat.Wetness < 0.99f && seconds < 6d * 3600d)
            {
                WetnessSystem.Step(garments, conditions, NeedsStep);
                seconds += NeedsStep;
            }

            Assert.That(seconds / 3600d, Is.InRange(1.8d, 2.6d), "docs/03, 3.4");
        }

        [Test]
        public void AFireCannotDryYouWhileYouStandInTheRain()
        {
            var sheltered = new WetnessConditions
            {
                RadiantHeat = 8f, AmbientCelsius = 10f, Shelter = 1f, IsIndoors = true,
            };
            var exposed = new WetnessConditions
            {
                RadiantHeat = 8f, AmbientCelsius = 10f, Shelter = 0f, SoakPerHour = Downpour,
            };

            Assert.That(WetnessSystem.WornDryingPerHour(sheltered),
                Is.EqualTo(WetnessSystem.DryWornByFire).Within(1e-4f));
            Assert.That(WetnessSystem.WornDryingPerHour(exposed), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void FrozenGarmentsDoNotDryAtAll()
        {
            var freezing = new WetnessConditions
            {
                RadiantHeat = 0f, AmbientCelsius = -8f, Shelter = 1f, IsIndoors = true,
            };

            Assert.That(WetnessSystem.WornDryingPerHour(freezing), Is.EqualTo(0f));
            Assert.That(WetnessSystem.RemovedDryingPerHour(freezing), Is.EqualTo(0f),
                "a frozen garment stiffens and must be thawed before it can dry");
        }

        [Test]
        public void HangingAGarmentDriesItFasterThanWearingIt()
        {
            var byTheFire = new WetnessConditions
            {
                RadiantHeat = 8f, AmbientCelsius = 15f, Shelter = 1f, IsIndoors = true,
            };

            Assert.That(
                WetnessSystem.RemovedDryingPerHour(byTheFire),
                Is.GreaterThan(WetnessSystem.WornDryingPerHour(byTheFire) * 1.5f),
                "stripping the soaked shell has to be worth doing");
        }

        [Test]
        public void ARainShellKeepsTheLayersBeneathItDrier()
        {
            var conditions = new WetnessConditions
            {
                SoakPerHour = Downpour, Shelter = 0f, AmbientCelsius = 8f,
            };

            var unprotected = TorsoSet(wetness: 0f);
            var shelled = TorsoSet(wetness: 0f, shellWaterproofing: 1f);

            for (var i = 0; i < 540; i++)
            {
                WetnessSystem.Step(unprotected, conditions, NeedsStep);
                WetnessSystem.Step(shelled, conditions, NeedsStep);
            }

            var unprotectedBase = unprotected.Find(g => g.Slot == GarmentSlot.TorsoBase);
            var shelledBase = shelled.Find(g => g.Slot == GarmentSlot.TorsoBase);

            Assert.That(shelledBase.Wetness, Is.LessThan(unprotectedBase.Wetness));
        }

        [Test]
        public void WadingSoaksLegsAndFeetAndLeavesTheTorsoAlone()
        {
            var garments = new List<Garment>
            {
                new Garment { Name = "coat", Slot = GarmentSlot.TorsoOuter, Coverage = BodyZone.Torso },
                new Garment { Name = "trousers", Slot = GarmentSlot.Legs, Coverage = BodyZone.Legs },
                new Garment { Name = "boots", Slot = GarmentSlot.Feet, Coverage = BodyZone.Feet },
            };

            WetnessSystem.Wade(garments);

            Assert.That(garments[0].Wetness, Is.EqualTo(0f));
            Assert.That(garments[1].Wetness, Is.EqualTo(WetnessSystem.WadeAmount).Within(1e-5f));
            Assert.That(garments[2].Wetness, Is.EqualTo(WetnessSystem.WadeAmount).Within(1e-5f));
        }

        [Test]
        public void GoingUnderSoaksEverything()
        {
            var garments = TorsoSet(wetness: 0f);

            WetnessSystem.Submerge(garments);

            foreach (var garment in garments)
            {
                Assert.That(garment.Wetness, Is.EqualTo(1f).Within(1e-5f));
            }
        }

        [Test]
        public void SweatWetsClothingEvenUnderShelter()
        {
            var conditions = new WetnessConditions
            {
                Shelter = 1f,
                IsIndoors = false,
                AmbientCelsius = 2f,
                Exertion = 1f,
                ClothingBulk = 1.4f,
            };

            var garments = TorsoSet(wetness: 0f);

            for (var i = 0; i < 540; i++)
            {
                WetnessSystem.Step(garments, conditions, NeedsStep);
            }

            Assert.That(MeanWetness(garments), Is.GreaterThan(0.05f),
                "sprinting to the cabin can arrive wetter than walking to it");
        }

        [Test]
        public void WetnessNeverLeavesTheValidRange()
        {
            var soaking = new WetnessConditions { SoakPerHour = 5f, Exertion = 1f, ClothingBulk = 3f };
            var drying = new WetnessConditions
            {
                RadiantHeat = 40f, AmbientCelsius = 30f, Shelter = 1f, IsIndoors = true,
            };

            var garments = TorsoSet(wetness: 0.5f);

            for (var i = 0; i < 2000; i++)
            {
                WetnessSystem.Step(garments, soaking, NeedsStep);
            }

            foreach (var garment in garments)
            {
                Assert.That(garment.Wetness, Is.InRange(0f, 1f));
            }

            for (var i = 0; i < 4000; i++)
            {
                WetnessSystem.Step(garments, drying, NeedsStep);
            }

            foreach (var garment in garments)
            {
                Assert.That(garment.Wetness, Is.InRange(0f, 1f));
            }
        }
    }
}
