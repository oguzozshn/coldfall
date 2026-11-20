using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards what the hands report, which docs/08-audio-and-ui.md, 8.5 calls the fastest
    /// status check in the game. The mapping is tested here so that a real hand model later
    /// changes nothing but the mesh.
    /// </summary>
    [TestFixture]
    public sealed class HandsStateTests
    {
        private static CoreTemperature CoreAt(float celsius)
        {
            var core = new CoreTemperature();
            core.SetCelsius(celsius);
            return core;
        }

        private static SurvivalNeeds NeedsAt(float fatigue)
        {
            var needs = new SurvivalNeeds();
            needs.Set(2200f, 90f, fatigue);
            return needs;
        }

        private static HandsState Build(
            float coreCelsius = 37f, float fatigue = 0f, float handWetness = 0f, Body body = null)
        {
            var garments = Loadouts.Winter();
            foreach (var garment in garments)
            {
                if (garment.Covers(BodyZone.Hands))
                {
                    garment.Wetness = handWetness;
                }
            }

            return HandsState.Build(CoreAt(coreCelsius), NeedsAt(fatigue), garments, body);
        }

        [Test]
        public void WarmDryUnhurtHandsReportNothing()
        {
            var state = Build();

            Assert.That(state.Cold, Is.EqualTo(0f));
            Assert.That(state.Wet, Is.EqualTo(0f));
            Assert.That(state.Blood, Is.EqualTo(0f));
            Assert.That(state.Tremor, Is.EqualTo(0f));
            Assert.That(state.Bandaged, Is.False);
        }

        [Test]
        public void ColdReadsFromTheSameStageAsTheCard()
        {
            Assert.That(Build(coreCelsius: 36f).Cold, Is.GreaterThan(0f));
            Assert.That(Build(coreCelsius: 34f).Cold, Is.GreaterThan(Build(36f).Cold));
            Assert.That(Build(coreCelsius: 29f).Cold, Is.EqualTo(1f));
        }

        [Test]
        public void WetGlovesShowOnTheHands()
        {
            Assert.That(Build(handWetness: 0.9f).Wet, Is.GreaterThan(0.8f));
        }

        [Test]
        public void BleedingPutsBloodOnTheHands()
        {
            var body = new Body();
            body.Inflict(WoundType.DeepWound, BodyPart.Torso);

            Assert.That(Build(body: body).Blood, Is.GreaterThan(0.5f),
                "the character has been holding it");
        }

        [Test]
        public void BloodLingersAfterTheBleedingStops()
        {
            var body = new Body();
            var wound = body.Inflict(WoundType.Laceration, BodyPart.LeftArm);

            for (var i = 0; i < 40; i++)
            {
                body.Step(SimClock.BodyStepGameSeconds);
            }

            wound.IsSutured = true;

            Assert.That(body.BleedPerHour, Is.EqualTo(0f));
            Assert.That(Build(body: body).Blood, Is.GreaterThan(0f),
                "hands do not wipe themselves clean the moment a wound is stitched");
        }

        [Test]
        public void ABandagedArmWoundShowsABandage()
        {
            var body = new Body();
            body.Inflict(WoundType.Laceration, BodyPart.RightArm).IsBandaged = true;

            Assert.That(Build(body: body).Bandaged, Is.True);
        }

        [Test]
        public void ALegWoundDoesNotPutABandageOnTheHands()
        {
            var body = new Body();
            body.Inflict(WoundType.Laceration, BodyPart.LeftLeg).IsBandaged = true;

            Assert.That(Build(body: body).Bandaged, Is.False);
        }

        [Test]
        public void ShiveringShakesTheHands()
        {
            var core = CoreAt(34f);

            // The tremor reads the balance, so the core has to have been stepped once for the
            // shivering figure to exist. That coupling is deliberate: the shake and the heat
            // shivering buys can never disagree.
            core.Step(SimClock.NeedsStepGameSeconds, new ThermalConditions
            {
                AmbientCelsius = -5f,
                Activity = ActivityLevel.Rest,
            }, Loadouts.Winter());

            var state = HandsState.Build(core, NeedsAt(0f), Loadouts.Winter(), null);

            Assert.That(state.Tremor, Is.GreaterThan(0f));
        }

        [Test]
        public void ExhaustionShakesTheHandsToo()
        {
            Assert.That(Build(fatigue: 95f).Tremor, Is.GreaterThan(0f));
            Assert.That(Build(fatigue: 50f).Tremor, Is.EqualTo(0f),
                "being merely tired must not shake the hands");
        }

        [Test]
        public void EverythingStaysInRange()
        {
            var body = new Body();
            body.Inflict(WoundType.DeepWound, BodyPart.Torso);
            body.Inflict(WoundType.Frostbite, BodyPart.LeftArm);

            var state = Build(coreCelsius: 28.5f, fatigue: 100f, handWetness: 1f, body: body);

            Assert.That(state.Cold, Is.InRange(0f, 1f));
            Assert.That(state.Wet, Is.InRange(0f, 1f));
            Assert.That(state.Blood, Is.InRange(0f, 1f));
            Assert.That(state.Tremor, Is.InRange(0f, 1f));
            Assert.That(state.Frostbitten, Is.True);
        }
    }
}
