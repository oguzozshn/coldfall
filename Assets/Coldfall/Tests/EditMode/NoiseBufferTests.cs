using Coldfall.Sim;
using NUnit.Framework;
using Unity.Mathematics;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the noise buffer of docs/09-technical-architecture.md, 9.2 and the hearing rules
    /// of docs/04-zombies-and-combat.md, 4.3.
    /// </summary>
    [TestFixture]
    public sealed class NoiseBufferTests
    {
        private static int NoWalls(float3 _) => 0;

        private static bool Hear(
            NoiseBuffer buffer, float3 listener, double now, out NoiseEvent heard,
            float weather = 1f, float hearing = 1f, int walls = 0)
        {
            return buffer.TryHearLoudest(
                listener, now, _ => walls, weather, hearing, out heard);
        }

        [Test]
        public void ANoiseInsideItsRadiusIsHeard()
        {
            var buffer = new NoiseBuffer();
            buffer.Emit(new float3(0f, 0f, 0f), NoiseRadius.Walk, NoiseType.Footstep, 100d);

            Assert.That(Hear(buffer, new float3(5f, 0f, 0f), 100d, out var heard), Is.True);
            Assert.That(heard.Type, Is.EqualTo(NoiseType.Footstep));
        }

        [Test]
        public void ANoiseOutsideItsRadiusIsNotHeard()
        {
            var buffer = new NoiseBuffer();
            buffer.Emit(float3.zero, NoiseRadius.CrouchWalk, NoiseType.Footstep, 100d);

            Assert.That(Hear(buffer, new float3(10f, 0f, 0f), 100d, out _), Is.False,
                "crouch-walking at 3 m must not be audible from ten");
        }

        [Test]
        public void EventsExpire()
        {
            var buffer = new NoiseBuffer();
            buffer.Emit(float3.zero, NoiseRadius.Shotgun, NoiseType.Gunshot, 100d);

            Assert.That(Hear(buffer, new float3(20f, 0f, 0f), 105d, out _), Is.True);
            Assert.That(Hear(buffer, new float3(20f, 0f, 0f), 120d, out _), Is.False,
                "ten seconds of retention, not a permanent record");
        }

        [Test]
        public void TheLoudestRelevantNoiseWins()
        {
            var buffer = new NoiseBuffer();
            buffer.Emit(new float3(3f, 0f, 0f), NoiseRadius.CrouchWalk, NoiseType.Footstep, 100d);
            buffer.Emit(new float3(40f, 0f, 0f), NoiseRadius.Shotgun, NoiseType.Gunshot, 100d);

            Assert.That(Hear(buffer, float3.zero, 100d, out var heard), Is.True);
            Assert.That(heard.Type, Is.EqualTo(NoiseType.Gunshot),
                "a shotgun forty metres away beats a footstep three metres away");
        }

        // --- Walls -----------------------------------------------------------------------

        [Test]
        public void EachWallRemovesSixtyPercentOfWhatIsLeft()
        {
            Assert.That(NoiseBuffer.EffectiveRadius(100f, 0, 1f, 1f), Is.EqualTo(100f).Within(0.01f));
            Assert.That(NoiseBuffer.EffectiveRadius(100f, 1, 1f, 1f), Is.EqualTo(40f).Within(0.01f));
            Assert.That(NoiseBuffer.EffectiveRadius(100f, 2, 1f, 1f), Is.EqualTo(16f).Within(0.01f));
            Assert.That(NoiseBuffer.EffectiveRadius(100f, 3, 1f, 1f), Is.EqualTo(6.4f).Within(0.01f));
        }

        [Test]
        public void WallsCompoundRatherThanSubtract()
        {
            var oneRoom = NoiseBuffer.EffectiveRadius(NoiseRadius.Pistol, 1, 1f, 1f);
            var threeRooms = NoiseBuffer.EffectiveRadius(NoiseRadius.Pistol, 3, 1f, 1f);

            Assert.That(threeRooms, Is.LessThan(oneRoom * 0.2f),
                "which is what makes interior layout a stealth system");
        }

        [Test]
        public void AWallCanPutANoiseOutOfEarshot()
        {
            var buffer = new NoiseBuffer();
            buffer.Emit(float3.zero, NoiseRadius.Walk, NoiseType.Footstep, 100d);

            Assert.That(Hear(buffer, new float3(5f, 0f, 0f), 100d, out _, walls: 0), Is.True);
            Assert.That(Hear(buffer, new float3(5f, 0f, 0f), 100d, out _, walls: 1), Is.False);
        }

        // --- Weather ---------------------------------------------------------------------

        [Test]
        public void RainIsCoverAndFogIsATrap()
        {
            var downpour = WeatherTable.HearingModifier(WeatherState.Downpour);
            var clear = WeatherTable.HearingModifier(WeatherState.ClearCool);
            var fog = WeatherTable.HearingModifier(WeatherState.RiverFog);

            Assert.That(downpour, Is.LessThan(clear));
            Assert.That(fog, Is.GreaterThan(clear),
                "the still fog that feels safe carries footsteps further");
        }

        [Test]
        public void ABlizzardNearlyDeafensTheDead()
        {
            Assert.That(WeatherTable.HearingModifier(WeatherState.Blizzard), Is.EqualTo(0.35f));
        }

        [Test]
        public void RunningInADownpourIsQuieterThanWalkingInTheClear()
        {
            var runningInRain = NoiseBuffer.EffectiveRadius(
                NoiseRadius.Run, 0, WeatherTable.HearingModifier(WeatherState.Downpour), 1f);

            var walkingInClear = NoiseBuffer.EffectiveRadius(
                NoiseRadius.Walk, 0, WeatherTable.HearingModifier(WeatherState.ClearCool), 1f);

            Assert.That(runningInRain, Is.GreaterThan(walkingInClear),
                "rain is cover, not a cloak: running is still louder than walking");
            Assert.That(runningInRain, Is.LessThan(NoiseRadius.Run),
                "but it is meaningfully quieter than running in the clear");
        }

        // --- The listener ------------------------------------------------------------------

        [Test]
        public void AFrozenListenerHearsLess()
        {
            var buffer = new NoiseBuffer();
            buffer.Emit(float3.zero, NoiseRadius.Walk, NoiseType.Footstep, 100d);

            Assert.That(Hear(buffer, new float3(6f, 0f, 0f), 100d, out _, hearing: 1f), Is.True);
            Assert.That(Hear(buffer, new float3(6f, 0f, 0f), 100d, out _, hearing: 0.25f), Is.False,
                "docs/04, 4.2: below -15 degC they slow and go partly deaf");
        }

        // --- Positional error ----------------------------------------------------------------

        [Test]
        public void HearingNeverGivesAPerfectFix()
        {
            var random = new Unity.Mathematics.Random(1234u);
            var listener = float3.zero;
            var source = new float3(30f, 0f, 0f);

            var estimate = NoiseBuffer.EstimateSource(listener, source, ref random);

            Assert.That(math.distance(estimate, source), Is.GreaterThan(0.01f),
                "zombies walk towards roughly where the sound was");
        }

        [Test]
        public void TheErrorGrowsWithDistance()
        {
            var near = 0f;
            var far = 0f;

            for (var seed = 1u; seed <= 200u; seed++)
            {
                var a = new Unity.Mathematics.Random(seed);
                var b = new Unity.Mathematics.Random(seed);

                var nearSource = new float3(5f, 0f, 0f);
                var farSource = new float3(60f, 0f, 0f);

                near += math.distance(
                    NoiseBuffer.EstimateSource(float3.zero, nearSource, ref a), nearSource);
                far += math.distance(
                    NoiseBuffer.EstimateSource(float3.zero, farSource, ref b), farSource);
            }

            Assert.That(far, Is.GreaterThan(near * 3f),
                "a distant noise is a direction, a close one is a place");
        }

        [Test]
        public void TheErrorStaysOnTheGroundPlane()
        {
            var random = new Unity.Mathematics.Random(7u);
            var source = new float3(10f, 4f, 10f);

            var estimate = NoiseBuffer.EstimateSource(float3.zero, source, ref random);

            Assert.That(estimate.y, Is.EqualTo(source.y).Within(1e-4f),
                "misjudging the height of a sound would read as a bug, not as imprecision");
        }

        [Test]
        public void TheSameSeedEstimatesTheSamePlace()
        {
            var a = new Unity.Mathematics.Random(99u);
            var b = new Unity.Mathematics.Random(99u);
            var source = new float3(25f, 0f, 12f);

            Assert.That(
                NoiseBuffer.EstimateSource(float3.zero, source, ref a),
                Is.EqualTo(NoiseBuffer.EstimateSource(float3.zero, source, ref b)));
        }

        // --- The buffer itself ----------------------------------------------------------------

        [Test]
        public void TheBufferOverwritesItsOldestAndNeverGrows()
        {
            var buffer = new NoiseBuffer(capacity: 8);

            for (var i = 0; i < 100; i++)
            {
                buffer.Emit(new float3(i, 0f, 0f), 10f, NoiseType.Footstep, i);
            }

            Assert.That(buffer.Count, Is.EqualTo(8));
            Assert.That(buffer[7].GameTime, Is.EqualTo(99d), "newest last");
            Assert.That(buffer[0].GameTime, Is.EqualTo(92d), "oldest first");
        }

        [Test]
        public void ASilentEventIsNotRecorded()
        {
            var buffer = new NoiseBuffer();
            buffer.Emit(float3.zero, 0f, NoiseType.Footstep, 1d);

            Assert.That(buffer.Count, Is.EqualTo(0));
        }

        [Test]
        public void AnEmptyBufferHearsNothing()
        {
            var buffer = new NoiseBuffer();

            Assert.That(buffer.TryHearLoudest(float3.zero, 0d, NoWalls, 1f, 1f, out _), Is.False);
        }
    }
}
