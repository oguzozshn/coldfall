using System.Linq;
using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards how the condition card reports wounds and illness, and the rule that governs the
    /// whole card: words, never numbers.
    /// </summary>
    [TestFixture]
    public sealed class ConditionCardInjuryTests
    {
        private static CoreTemperature Healthy()
        {
            var core = new CoreTemperature();
            core.SetCelsius(37f);
            return core;
        }

        private static SurvivalNeeds Fed()
        {
            var needs = new SurvivalNeeds();
            needs.Set(2200f, 90f, 10f);
            return needs;
        }

        private static ConditionReport Build(Body body = null, Illnesses illnesses = null)
        {
            return ConditionCard.Build(
                Healthy(), Fed(), Loadouts.Starting(), null, body, illnesses);
        }

        [Test]
        public void WoundsAndIllnessStillNeverPrintANumber()
        {
            var body = new Body();
            body.Inflict(WoundType.Laceration, BodyPart.LeftArm);
            body.Inflict(WoundType.Fracture, BodyPart.RightLeg).IsSplinted = true;
            body.Inflict(WoundType.GlassShard, BodyPart.Torso);

            var illnesses = new Illnesses();
            illnesses.Catch(IllnessKind.ChestInfection);
            illnesses.Catch(IllnessKind.Dysentery);

            var report = Build(body, illnesses);

            var text = string.Join("\n",
                report.Body.Select(l => $"{l.Label} {l.Text}")
                    .Concat(report.Needs.Select(l => $"{l.Label} {l.Text}")));

            Assert.That(text.Any(char.IsDigit), Is.False, $"got:\n{text}");
        }

        [Test]
        public void AHurtPartGetsItsOwnLine()
        {
            var body = new Body();
            body.Inflict(WoundType.Fracture, BodyPart.RightLeg);

            var report = Build(body);
            var line = report.Body.FirstOrDefault(l => l.Label == "Right leg");

            Assert.That(line.Label, Is.EqualTo("Right leg"));
            Assert.That(line.Text, Does.Contain("broken"));
        }

        [Test]
        public void TreatmentShowsOnTheLine()
        {
            var body = new Body();
            var wound = body.Inflict(WoundType.Laceration, BodyPart.LeftArm);
            wound.IsBandaged = true;
            wound.IsCleaned = true;

            var report = Build(body);
            var line = report.Body.First(l => l.Label == "Left arm");

            Assert.That(line.Text, Does.Contain("bandaged, clean"));
        }

        [Test]
        public void BleedingIsSaidFirstAndFlagged()
        {
            var body = new Body();
            body.Inflict(WoundType.DeepWound, BodyPart.Torso);

            var report = Build(body);
            var line = report.Body.First(l => l.Label == "Torso");

            Assert.That(line.Words[0], Is.EqualTo("bleeding"));
            Assert.That(line.IsWarning, Is.True);
        }

        [Test]
        public void ASuturedWoundNoLongerSaysBleeding()
        {
            var body = new Body();
            body.Inflict(WoundType.DeepWound, BodyPart.Torso).IsSutured = true;

            var report = Build(body);
            var line = report.Body.First(l => l.Label == "Torso");

            Assert.That(line.Words, Does.Not.Contain("bleeding"));
            Assert.That(line.Text, Does.Contain("stitched"));
        }

        [Test]
        public void UnhurtPartsAreNotListed()
        {
            var body = new Body();
            body.Inflict(WoundType.Scratch, BodyPart.LeftArm);

            var report = Build(body);

            Assert.That(report.Body.Any(l => l.Label == "Right arm"), Is.False);
            Assert.That(report.Body.Any(l => l.Label == "Head"), Is.False);
        }

        [Test]
        public void IllnessesAreNamedInPlainWords()
        {
            var illnesses = new Illnesses();
            illnesses.Catch(IllnessKind.ChestInfection);

            var report = Build(illnesses: illnesses);
            var line = report.Body.First(l => l.Label == "Illness");

            Assert.That(line.Text, Is.EqualTo("a cough you cannot stop"));
        }

        [Test]
        public void TheBiteIsFlaggedAsSerious()
        {
            var illnesses = new Illnesses();
            illnesses.Catch(IllnessKind.FungalInfection);

            var report = Build(illnesses: illnesses);
            var line = report.Body.First(l => l.Label == "Illness");

            Assert.That(line.IsWarning, Is.True);
            Assert.That(line.Text, Is.EqualTo("something growing in you"));
        }

        [Test]
        public void BleedingOutranksEverythingElseOnTheCard()
        {
            var body = new Body();
            body.Inflict(WoundType.DeepWound, BodyPart.Torso);

            var freezing = new CoreTemperature();
            freezing.SetCelsius(31.5f);

            var starving = new SurvivalNeeds();
            starving.Set(0f, 5f, 100f);

            Assert.That(
                ConditionCard.MostUrgent(freezing, starving, body, null),
                Is.EqualTo("bleeding badly"),
                "a serious bleed kills inside the hour; the cold takes most of one");
        }

        [Test]
        public void TheBiteOutranksTheColdButNotABleed()
        {
            var illnesses = new Illnesses();
            illnesses.Catch(IllnessKind.FungalInfection);

            var freezing = new CoreTemperature();
            freezing.SetCelsius(31.5f);

            Assert.That(
                ConditionCard.MostUrgent(freezing, Fed(), null, illnesses),
                Is.EqualTo("infected by the bite"));
        }

        [Test]
        public void BloodLossShowsOnTheBodyLine()
        {
            var body = new Body();
            body.Inflict(WoundType.Laceration, BodyPart.Torso);

            for (var i = 0; i < 60; i++)
            {
                body.Step(SimClock.BodyStepGameSeconds);
            }

            var report = Build(body);

            Assert.That(report.Body[0].Text, Does.Contain("light-headed").Or.Contain("grey and faint"));
        }

        [Test]
        public void TheOldOverloadStillWorksForCallersWithoutABody()
        {
            var report = ConditionCard.Build(Healthy(), Fed(), Loadouts.Starting(), null);

            Assert.That(report.Body[0].Text, Is.EqualTo("fine"));
        }
    }
}
