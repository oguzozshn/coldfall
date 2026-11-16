using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the five named conditions of docs/03-survival-systems.md, 3.10.
    /// </summary>
    [TestFixture]
    public sealed class IllnessTests
    {
        private const double BodyStep = SimClock.BodyStepGameSeconds;

        private static int Advance(
            Illnesses illnesses, float gameHours, float coreCelsius = 37f, float wetness = 0f)
        {
            var ticks = (int)(gameHours * 3600d / BodyStep);
            var coughs = 0;

            for (var i = 0; i < ticks; i++)
            {
                illnesses.Step(BodyStep, coreCelsius, wetness);
                if (illnesses.CoughedThisTick)
                {
                    coughs++;
                }
            }

            return coughs;
        }

        // --- The cough -------------------------------------------------------------------

        [Test]
        public void AChestInfectionCoughsRepeatedly()
        {
            var illnesses = new Illnesses();
            illnesses.Catch(IllnessKind.ChestInfection);

            var coughs = Advance(illnesses, 1f);

            Assert.That(coughs, Is.GreaterThan(5),
                "docs/03, 3.10: your illness gives you away");
        }

        [Test]
        public void AHealthyCharacterNeverCoughs()
        {
            var illnesses = new Illnesses();

            var coughs = Advance(illnesses, 10f);

            Assert.That(coughs, Is.EqualTo(0));
        }

        [Test]
        public void TheCoughIsLoudEnoughToBeHeardThroughAWall()
        {
            // 10 m, against a crouch-walk at 3 m and a window break at 45 m (docs/04, 4.3).
            Assert.That(IllnessTable.CoughNoiseRadius, Is.EqualTo(10f));
        }

        [Test]
        public void CuringTheInfectionStopsTheCough()
        {
            var illnesses = new Illnesses();
            illnesses.Catch(IllnessKind.ChestInfection);

            Assert.That(Advance(illnesses, 0.5f), Is.GreaterThan(0));

            illnesses.Cure(IllnessKind.ChestInfection);

            Assert.That(Advance(illnesses, 2f), Is.EqualTo(0));
        }

        // --- Catching things -------------------------------------------------------------

        [Test]
        public void ColdAndWetTogetherCauseAChestInfection()
        {
            var illnesses = new Illnesses();

            Advance(illnesses, 9f, coreCelsius: 35f, wetness: 0.8f);

            Assert.That(illnesses.Has(IllnessKind.ChestInfection), Is.True,
                "the direct mechanical payoff of the wetness system");
        }

        [Test]
        public void ColdAloneDoesNotCauseAChestInfection()
        {
            var illnesses = new Illnesses();

            Advance(illnesses, 24f, coreCelsius: 35f, wetness: 0f);

            Assert.That(illnesses.Has(IllnessKind.ChestInfection), Is.False,
                "a dry character who gets cold warms up");
        }

        [Test]
        public void BeingWetWhileWarmIsNotEnough()
        {
            var illnesses = new Illnesses();

            Advance(illnesses, 24f, coreCelsius: 37f, wetness: 1f);

            Assert.That(illnesses.Has(IllnessKind.ChestInfection), Is.False);
        }

        [Test]
        public void DryingOffBeforeTheClockRunsOutAvoidsIt()
        {
            var illnesses = new Illnesses();

            Advance(illnesses, 6f, coreCelsius: 35f, wetness: 0.8f);
            Advance(illnesses, 6f, coreCelsius: 37f, wetness: 0f);
            Advance(illnesses, 6f, coreCelsius: 35f, wetness: 0.8f);

            Assert.That(illnesses.Has(IllnessKind.ChestInfection), Is.False,
                "getting dry has to be worth doing");
        }

        [Test]
        public void TheSameIllnessIsNotCaughtTwice()
        {
            var illnesses = new Illnesses();

            Assert.That(illnesses.Catch(IllnessKind.Dysentery), Is.True);
            Assert.That(illnesses.Catch(IllnessKind.Dysentery), Is.False);
            Assert.That(illnesses.Active.Count, Is.EqualTo(1));
        }

        // --- Effects ---------------------------------------------------------------------

        [Test]
        public void DysenteryTriplesHydrationDrain()
        {
            var illnesses = new Illnesses();
            illnesses.Catch(IllnessKind.Dysentery);

            Assert.That(illnesses.HydrationDrainMultiplier, Is.EqualTo(3f).Within(0.01f));
        }

        [Test]
        public void IllnessesStackTheirPenalties()
        {
            var illnesses = new Illnesses();
            illnesses.Catch(IllnessKind.ChestInfection);
            illnesses.Catch(IllnessKind.FoodPoisoning);

            var expected = IllnessTable.For(IllnessKind.ChestInfection).StaminaMultiplier
                           * IllnessTable.For(IllnessKind.FoodPoisoning).StaminaMultiplier;

            Assert.That(illnesses.StaminaMultiplier, Is.EqualTo(expected).Within(0.001f));
            Assert.That(illnesses.StaminaMultiplier, Is.LessThan(0.45f),
                "two illnesses at once has to be close to crippling");
        }

        [Test]
        public void WoundInfectionAndChestInfectionBothCauseFever()
        {
            var chest = new Illnesses();
            chest.Catch(IllnessKind.ChestInfection);

            var wound = new Illnesses();
            wound.Catch(IllnessKind.WoundInfection);

            var gut = new Illnesses();
            gut.Catch(IllnessKind.FoodPoisoning);

            Assert.That(chest.HasFever, Is.True);
            Assert.That(wound.HasFever, Is.True);
            Assert.That(gut.HasFever, Is.False);
        }

        // --- Running their course --------------------------------------------------------

        [Test]
        public void FoodPoisoningPassesOnItsOwn()
        {
            var illnesses = new Illnesses();
            illnesses.Catch(IllnessKind.FoodPoisoning);

            Advance(illnesses, 20f);

            Assert.That(illnesses.Has(IllnessKind.FoodPoisoning), Is.False);
            Assert.That(illnesses.StaminaMultiplier, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void AntibioticsShortenWhatTheyCanAndNotWhatTheyCannot()
        {
            var treated = new Illnesses { OnAntibiotics = true };
            treated.Catch(IllnessKind.ChestInfection);
            Advance(treated, 30f);

            var untreatable = new Illnesses { OnAntibiotics = true };
            untreatable.Catch(IllnessKind.FoodPoisoning);
            Advance(untreatable, 10f);

            Assert.That(treated.Has(IllnessKind.ChestInfection), Is.False,
                "96 hours becomes 24 on antibiotics");
            Assert.That(untreatable.Has(IllnessKind.FoodPoisoning), Is.True,
                "food poisoning wants rest, water and time");
        }

        [Test]
        public void TheFungalInfectionIsTerminalAndDoesNotSimplyExpire()
        {
            var illnesses = new Illnesses();
            illnesses.Catch(IllnessKind.FungalInfection);

            Advance(illnesses, 83f);
            Assert.That(illnesses.IsDead, Is.False);

            Advance(illnesses, 3f);

            Assert.That(illnesses.IsDead, Is.True,
                "docs/03, 3.10: terminal in 72-96 hours unless cured");
            Assert.That(illnesses.Has(IllnessKind.FungalInfection), Is.True,
                "it does not quietly clear itself the way the others do");
        }

        [Test]
        public void AntibioticsDoNothingForTheFungalInfection()
        {
            var illnesses = new Illnesses { OnAntibiotics = true };
            illnesses.Catch(IllnessKind.FungalInfection);

            Advance(illnesses, 50f);

            Assert.That(illnesses.IsDead, Is.False,
                "the bite needs the antifungal course of 3.6, not antibiotics");
        }

        [Test]
        public void TheAntifungalCureRemovesItOutright()
        {
            var illnesses = new Illnesses();
            illnesses.Catch(IllnessKind.FungalInfection);
            Advance(illnesses, 40f);

            illnesses.Cure(IllnessKind.FungalInfection);
            Advance(illnesses, 100f);

            Assert.That(illnesses.IsDead, Is.False);
            Assert.That(illnesses.Has(IllnessKind.FungalInfection), Is.False);
        }

        [Test]
        public void IdenticalInputsProduceIdenticalResults()
        {
            var first = new Illnesses();
            var second = new Illnesses();

            var a = Advance(first, 12f, 34.8f, 0.9f);
            var b = Advance(second, 12f, 34.8f, 0.9f);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(first.Active.Count, Is.EqualTo(second.Active.Count));
        }
    }
}
