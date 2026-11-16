using System.Collections.Generic;
using System.Linq;
using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the condition card of docs/08-audio-and-ui.md, 8.5, and above all the rule that
    /// makes it worth having: no numbers.
    /// </summary>
    [TestFixture]
    public sealed class ConditionCardTests
    {
        private static CoreTemperature CoreAt(float celsius)
        {
            var core = new CoreTemperature();
            core.SetCelsius(celsius);
            return core;
        }

        private static SurvivalNeeds NeedsAt(float calories, float hydration, float fatigue)
        {
            var needs = new SurvivalNeeds();
            needs.Set(calories, hydration, fatigue);
            return needs;
        }

        private static List<Garment> Soaked(float wetness)
        {
            var set = Loadouts.Starting();
            foreach (var garment in set)
            {
                garment.Wetness = wetness;
            }

            return set;
        }

        [Test]
        public void TheCardNeverPrintsANumberAboutTheBody()
        {
            var report = ConditionCard.Build(
                CoreAt(34.2f), NeedsAt(380f, 12f, 92f), Soaked(1f), clock: null);

            var text = string.Join("\n",
                report.Body.Select(l => $"{l.Label} {l.Text}")
                    .Concat(report.Needs.Select(l => $"{l.Label} {l.Text}")));

            Assert.That(text.Any(char.IsDigit), Is.False,
                $"docs/08, 8.5: the card lists everything in words, never numbers. Got:\n{text}");
        }

        [Test]
        public void AHealthyCharacterIsSimplyFine()
        {
            var report = ConditionCard.Build(
                CoreAt(37f), NeedsAt(2200f, 90f, 5f), Loadouts.Starting(), null);

            Assert.That(report.Body[0].Label, Is.EqualTo("Body"));
            Assert.That(report.Body[0].Text, Is.EqualTo("fine"));
            Assert.That(report.MostUrgent, Is.Empty);
        }

        [Test]
        public void DryClothingIsNotWorthMentioning()
        {
            var report = ConditionCard.Build(
                CoreAt(37f), NeedsAt(2200f, 90f, 5f), Loadouts.Starting(), null);

            Assert.That(report.Body.Count, Is.EqualTo(1),
                "a card that lists every dry limb is noise");
        }

        [Test]
        public void SoakedClothingIsCalledOutPerZone()
        {
            var report = ConditionCard.Build(
                CoreAt(36.8f), NeedsAt(2200f, 90f, 5f), Soaked(1f), null);

            var labels = report.Body.Select(l => l.Label).ToList();

            Assert.That(labels, Contains.Item("Torso"));
            Assert.That(labels, Contains.Item("Legs"));
            Assert.That(labels, Contains.Item("Feet"));
            Assert.That(report.Body.First(l => l.Label == "Feet").Text, Is.EqualTo("soaked"));
        }

        [Test]
        public void SoakedFeetAreFlaggedBeforeTrenchFootExists()
        {
            var report = ConditionCard.Build(
                CoreAt(36.8f), NeedsAt(2200f, 90f, 5f), Soaked(1f), null);

            var feet = report.Body.First(l => l.Label == "Feet");

            Assert.That(feet.IsWarning, Is.True,
                "a day of inattention to wet feet is trench foot; the card warns first");
        }

        [Test]
        public void TheWarmthWordChangesCharacterWhenShiveringStops()
        {
            Assert.That(ConditionCard.WarmthWord(CoreAt(37f)), Is.Null);
            Assert.That(ConditionCard.WarmthWord(CoreAt(35.5f)), Is.EqualTo("cold"));
            Assert.That(ConditionCard.WarmthWord(CoreAt(34f)), Is.EqualTo("shivering hard"));
            Assert.That(ConditionCard.WarmthWord(CoreAt(32f)), Is.EqualTo("numb and clumsy"));
            Assert.That(ConditionCard.WarmthWord(CoreAt(29f)), Is.EqualTo("strangely warm"),
                "the paradoxical warmth of docs/03, 3.3, said plainly and still misleading");
        }

        [Test]
        public void TheMostUrgentProblemIsTheOneThatKillsSoonest()
        {
            // Freezing beats thirst beats hunger, because that is the order they kill in.
            Assert.That(
                ConditionCard.MostUrgent(CoreAt(32f), NeedsAt(0f, 5f, 100f)),
                Is.EqualTo("freezing to death"));

            Assert.That(
                ConditionCard.MostUrgent(CoreAt(37f), NeedsAt(0f, 5f, 100f)),
                Is.EqualTo("need water badly"));

            Assert.That(
                ConditionCard.MostUrgent(CoreAt(37f), NeedsAt(0f, 80f, 100f)),
                Is.EqualTo("need food badly"));

            Assert.That(
                ConditionCard.MostUrgent(CoreAt(37f), NeedsAt(2200f, 80f, 90f)),
                Is.EqualTo("need to sleep"));
        }

        [Test]
        public void NothingUrgentMeansNothingIsSaid()
        {
            Assert.That(
                ConditionCard.MostUrgent(CoreAt(37f), NeedsAt(2200f, 90f, 20f)),
                Is.Empty);
        }

        [TestCase(300f, "starving")]
        [TestCase(900f, "hungry")]
        [TestCase(1500f, "peckish")]
        [TestCase(2200f, "fine")]
        [TestCase(2900f, "full")]
        public void HungerWordsCoverTheWholeRange(float calories, string expected)
        {
            Assert.That(ConditionCard.HungerWord(calories), Is.EqualTo(expected));
        }

        [TestCase(5f, "parched")]
        [TestCase(25f, "thirsty")]
        [TestCase(50f, "dry")]
        [TestCase(85f, "fine")]
        public void ThirstWordsCoverTheWholeRange(float hydration, string expected)
        {
            Assert.That(ConditionCard.ThirstWord(hydration), Is.EqualTo(expected));
        }

        [TestCase(5f, "rested")]
        [TestCase(25f, "good")]
        [TestCase(45f, "fair")]
        [TestCase(70f, "poor")]
        [TestCase(90f, "spent")]
        public void RestWordsCoverTheWholeRange(float fatigue, string expected)
        {
            Assert.That(ConditionCard.RestWord(fatigue), Is.EqualTo(expected));
        }

        [Test]
        public void WetnessWordsIgnoreTheNegligible()
        {
            Assert.That(ConditionCard.WetnessWord(0.05f), Is.Null);
            Assert.That(ConditionCard.WetnessWord(0.25f), Is.EqualTo("damp"));
            Assert.That(ConditionCard.WetnessWord(0.55f), Is.EqualTo("wet"));
            Assert.That(ConditionCard.WetnessWord(0.9f), Is.EqualTo("soaked"));
        }

        [Test]
        public void StarvationShowsOnTheBodyAsWellAsInTheNeeds()
        {
            var report = ConditionCard.Build(
                CoreAt(37f), NeedsAt(200f, 80f, 10f), Loadouts.Starting(), null);

            Assert.That(report.Body[0].Text, Does.Contain("weak with hunger"),
                "starving halves heat production, so it belongs on the body line too");
        }

        [Test]
        public void TheTimestampIsTheOnlyPlaceDigitsAppear()
        {
            var clock = new SimClock();
            clock.AdvanceGame(3600d * 5d);

            var report = ConditionCard.Build(
                CoreAt(37f), NeedsAt(2200f, 90f, 10f), Loadouts.Starting(), clock);

            Assert.That(report.Timestamp, Does.Match(@"DAY \d+, \d\d:\d\d"));
        }
    }
}
