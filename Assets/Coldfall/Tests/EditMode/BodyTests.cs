using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the wound model of docs/03-survival-systems.md, 3.5.
    /// </summary>
    [TestFixture]
    public sealed class BodyTests
    {
        private const double BodyStep = SimClock.BodyStepGameSeconds;

        private static void Advance(Body body, float gameHours, float feetWetness = 0f)
        {
            var ticks = (int)(gameHours * 3600d / BodyStep);
            for (var i = 0; i < ticks; i++)
            {
                body.Step(BodyStep, feetWetness);
            }
        }

        [Test]
        public void ADeepWoundBleedsOutInMinutesIfIgnored()
        {
            var body = new Body();
            body.Inflict(WoundType.DeepWound, BodyPart.Torso);

            Advance(body, 0.35f);

            Assert.That(body.HasBledOut, Is.True,
                "docs/03, 3.5: bleed-out in four to twenty minutes");
        }

        [Test]
        public void ABandageIsNotEnoughForADeepWound()
        {
            var body = new Body();
            var wound = body.Inflict(WoundType.DeepWound, BodyPart.Torso);
            wound.IsBandaged = true;

            Advance(body, 1f);

            Assert.That(body.HasBledOut, Is.True,
                "a deep wound needs suturing; a tidy bandage still kills you");
        }

        [Test]
        public void SuturingStopsTheBleedingEntirely()
        {
            var body = new Body();
            var wound = body.Inflict(WoundType.DeepWound, BodyPart.Torso);
            wound.IsSutured = true;

            Advance(body, 6f);

            Assert.That(body.Blood, Is.EqualTo(Body.MaxBlood).Within(0.01f));
            Assert.That(body.HasBledOut, Is.False);
        }

        [Test]
        public void ABandagedScratchIsSurvivable()
        {
            var body = new Body();
            var wound = body.Inflict(WoundType.Scratch, BodyPart.LeftArm);
            wound.IsBandaged = true;
            wound.IsCleaned = true;

            // A little past the authored 24 hours: healing accumulates per tick, so landing
            // exactly on the boundary leaves it a rounding error short of closed.
            Advance(body, 30f);

            Assert.That(body.HasBledOut, Is.False);
            Assert.That(wound.IsHealed, Is.True);
        }

        [Test]
        public void BloodComesBackOnlyOnceTheBleedingStops()
        {
            var body = new Body();
            var wound = body.Inflict(WoundType.Laceration, BodyPart.LeftArm);

            Advance(body, 1f);
            var lost = body.Blood;
            Assert.That(lost, Is.LessThan(Body.MaxBlood));

            wound.IsSutured = true;
            Advance(body, 5f);

            Assert.That(body.Blood, Is.GreaterThan(lost));
        }

        [Test]
        public void GlassStopsAWoundHealingUntilItIsRemoved()
        {
            var body = new Body();
            var wound = body.Inflict(WoundType.GlassShard, BodyPart.LeftLeg);
            wound.IsBandaged = true;

            Advance(body, 60f);
            Assert.That(wound.IsHealed, Is.False);

            wound.HasEmbeddedGlass = false;
            Advance(body, 60f);

            Assert.That(wound.IsHealed, Is.True);
        }

        [Test]
        public void AFractureDoesNotKnitWithoutASplint()
        {
            var body = new Body();
            var wound = body.Inflict(WoundType.Fracture, BodyPart.RightArm);

            Advance(body, 400f);
            Assert.That(wound.IsHealed, Is.False);

            wound.IsSplinted = true;
            Advance(body, 400f);

            Assert.That(wound.IsHealed, Is.True);
        }

        [Test]
        public void UncleanWoundsFesterAndCleanOnesDoNot()
        {
            var dirty = new Body();
            dirty.Inflict(WoundType.Laceration, BodyPart.LeftArm).IsBandaged = true;

            var clean = new Body();
            var cleaned = clean.Inflict(WoundType.Laceration, BodyPart.LeftArm);
            cleaned.IsBandaged = true;
            cleaned.IsCleaned = true;

            Advance(dirty, 20f);
            Advance(clean, 20f);

            Assert.That(dirty.BacterialInfection, Is.GreaterThan(Body.FeverInfection));
            Assert.That(dirty.HasFever, Is.True);
            Assert.That(clean.BacterialInfection, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void AntibioticsClearAnInfection()
        {
            var body = new Body();
            var wound = body.Inflict(WoundType.Burn, BodyPart.LeftArm);
            wound.IsBandaged = true;

            Advance(body, 30f);
            Assert.That(body.HasFever, Is.True);

            body.OnAntibiotics = true;
            Advance(body, 20f);

            Assert.That(body.BacterialInfection, Is.EqualTo(0f).Within(0.01f));
            Assert.That(body.HasFever, Is.False);
        }

        [Test]
        public void PainkillersHideThePainTheyDoNotFixIt()
        {
            var body = new Body();
            body.Inflict(WoundType.Fracture, BodyPart.LeftLeg);

            var felt = body.Pain;
            body.OnPainkillers = true;

            Assert.That(body.Pain, Is.LessThan(felt));
            Assert.That(body.Has(BodyPart.LeftLeg, WoundType.Fracture), Is.True,
                "the wound is still there, which is the trade docs/03 is making");
        }

        // --- Trench foot ---------------------------------------------------------------

        [Test]
        public void WetFeetForADayBecomeTrenchFoot()
        {
            var body = new Body();

            Advance(body, 23f, feetWetness: 0.9f);
            Assert.That(body.Has(WoundType.TrenchFoot), Is.False);

            Advance(body, 2f, feetWetness: 0.9f);
            Assert.That(body.Has(WoundType.TrenchFoot), Is.True,
                "the cheapest possible way to teach the player that dry socks are loot");
        }

        [Test]
        public void DryingYourFeetRunsTheClockBack()
        {
            var body = new Body();

            Advance(body, 20f, feetWetness: 0.9f);
            Advance(body, 12f, feetWetness: 0f);
            Advance(body, 3f, feetWetness: 0.9f);

            Assert.That(body.Has(WoundType.TrenchFoot), Is.False,
                "drying off has to help, not merely pause the damage");
        }

        [Test]
        public void DampFeetAreNotWetEnough()
        {
            var body = new Body();

            Advance(body, 40f, feetWetness: 0.5f);

            Assert.That(body.Has(WoundType.TrenchFoot), Is.False);
        }

        // --- Functional consequences ---------------------------------------------------

        [Test]
        public void AHurtTorsoMakesTheCharacterLouder()
        {
            var body = new Body();
            var quiet = body.BreathingNoiseMultiplier;

            body.Inflict(WoundType.Laceration, BodyPart.Torso);

            Assert.That(body.BreathingNoiseMultiplier, Is.GreaterThan(quiet),
                "the survival layer handing the combat layer a problem");
        }

        [Test]
        public void ABrokenLegIsCripplingAndABrokenArmRuinsMelee()
        {
            var legs = new Body();
            legs.Inflict(WoundType.Fracture, BodyPart.LeftLeg);

            var arms = new Body();
            arms.Inflict(WoundType.Fracture, BodyPart.RightArm);

            Assert.That(legs.MovementMultiplier, Is.LessThan(0.5f));
            Assert.That(arms.MeleeMultiplier, Is.LessThan(0.5f));
            Assert.That(legs.MeleeMultiplier, Is.EqualTo(1f).Within(0.01f),
                "a broken leg must not quietly ruin the swing as well");
        }

        [Test]
        public void BloodLossWeakensEverythingWithoutCollapsingTheCharacter()
        {
            var body = new Body();
            body.Inflict(WoundType.Laceration, BodyPart.Torso);

            Advance(body, 1.5f);

            Assert.That(body.Blood, Is.LessThan(50f));
            Assert.That(body.MovementMultiplier, Is.LessThan(1f));
            Assert.That(body.MovementMultiplier, Is.GreaterThan(0.3f),
                "collapsing without warning reads as a bug");
        }

        [Test]
        public void FrostbiteIsPermanent()
        {
            var body = new Body();
            body.Inflict(WoundType.Frostbite, BodyPart.LeftLeg);

            Advance(body, 1000f);
            body.ClearHealed();

            Assert.That(body.FrostbiteOn(BodyPart.LeftLeg), Is.GreaterThan(0f));
            Assert.That(body.MovementMultiplier, Is.LessThan(1f),
                "a character who survives thirty days should be visibly diminished");
        }

        [Test]
        public void ABiteIsRecordedSoTheFungalClockCanStart()
        {
            var body = new Body();
            Assert.That(body.HasBite, Is.False);

            body.Inflict(WoundType.Bite, BodyPart.LeftArm);

            Assert.That(body.HasBite, Is.True);
        }

        [Test]
        public void IdenticalInputsProduceIdenticalResults()
        {
            var first = new Body();
            var second = new Body();
            first.Inflict(WoundType.Laceration, BodyPart.Torso).IsBandaged = true;
            second.Inflict(WoundType.Laceration, BodyPart.Torso).IsBandaged = true;

            Advance(first, 12f, 0.7f);
            Advance(second, 12f, 0.7f);

            Assert.That(first.Blood, Is.EqualTo(second.Blood));
            Assert.That(first.BacterialInfection, Is.EqualTo(second.BacterialInfection));
        }
    }
}
