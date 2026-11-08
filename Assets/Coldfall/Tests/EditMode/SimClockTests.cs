using System;
using System.Collections.Generic;
using Coldfall.Sim.Time;
using NUnit.Framework;

namespace Coldfall.Tests.Time
{
    /// <summary>
    /// Guards the M0 exit criterion that changing the time scale changes no simulation outcome,
    /// and the 9.1 determinism requirement that the three simulation ticks are independent of
    /// frame rate. These tests need no scene, which is itself part of the architecture.
    /// </summary>
    [TestFixture]
    public sealed class SimClockTests
    {
        private SimClock _clock;
        private int _needs;
        private int _body;
        private int _world;
        private readonly List<double> _needsSteps = new List<double>();

        [SetUp]
        public void SetUp()
        {
            _clock = new SimClock();
            _needs = 0;
            _body = 0;
            _world = 0;
            _needsSteps.Clear();

            _clock.NeedsTick += step =>
            {
                _needs++;
                _needsSteps.Add(step);
            };
            _clock.BodyTick += _ => _body++;
            _clock.WorldTick += _ => _world++;
        }

        [Test]
        public void DefaultTimeScaleIsTwentyGameSecondsPerRealSecond()
        {
            _clock.AdvanceReal(1.0);

            Assert.That(_clock.ElapsedGameSeconds, Is.EqualTo(20.0).Within(1e-9));
        }

        [Test]
        public void OneGameHourFiresTheDocumentedNumberOfTicks()
        {
            _clock.AdvanceGame(3600.0);

            Assert.That(_needs, Is.EqualTo(180), "needs ticks per game hour");
            Assert.That(_body, Is.EqualTo(36), "body ticks per game hour");
            Assert.That(_world, Is.EqualTo(60), "world ticks per game hour");
        }

        [TestCase(1.0)]
        [TestCase(20.0)]
        [TestCase(240.0)]
        public void TickCountsDoNotDependOnTimeScale(double timeScale)
        {
            _clock.TimeScale = timeScale;

            // One game hour of real time at this scale.
            var realSeconds = 3600.0 / timeScale;
            _clock.AdvanceReal(realSeconds);

            Assert.That(_needs, Is.EqualTo(180));
            Assert.That(_body, Is.EqualTo(36));
            Assert.That(_world, Is.EqualTo(60));
            Assert.That(_clock.ElapsedGameSeconds, Is.EqualTo(3600.0).Within(1e-6));
        }

        [Test]
        public void TickCountsDoNotDependOnFrameRate()
        {
            var coarse = new SimClock();
            var coarseNeeds = 0;
            coarse.NeedsTick += _ => coarseNeeds++;

            var fine = new SimClock();
            var fineNeeds = 0;
            fine.NeedsTick += _ => fineNeeds++;

            // 10.5 real seconds, delivered as 21 frames and as 630 frames. The duration is
            // chosen to land halfway between two needs steps: on an exact step boundary the
            // accumulated error of 630 additions of 1/60 is enough to lose the final step, which
            // would make this test fail for a reason that has nothing to do with frame rate.
            for (var i = 0; i < 21; i++)
            {
                coarse.AdvanceReal(0.5);
            }

            for (var i = 0; i < 630; i++)
            {
                fine.AdvanceReal(1.0 / 60.0);
            }

            Assert.That(fineNeeds, Is.EqualTo(coarseNeeds));
            Assert.That(fine.ElapsedGameSeconds, Is.EqualTo(coarse.ElapsedGameSeconds).Within(1e-6));
        }

        [Test]
        public void EveryNeedsStepHasTheDocumentedLength()
        {
            _clock.AdvanceGame(600.0);

            Assert.That(_needsSteps, Is.Not.Empty);
            Assert.That(_needsSteps, Is.All.EqualTo(SimClock.NeedsStepGameSeconds));
        }

        [Test]
        public void PartialStepsAreCarriedRatherThanLost()
        {
            // Three advances of 7 game seconds is 21, which is one needs step plus a remainder.
            _clock.AdvanceGame(7.0);
            _clock.AdvanceGame(7.0);
            _clock.AdvanceGame(7.0);

            Assert.That(_needs, Is.EqualTo(1));
            Assert.That(_clock.ElapsedGameSeconds, Is.EqualTo(21.0).Within(1e-9));

            _clock.AdvanceGame(19.0);

            Assert.That(_needs, Is.EqualTo(2), "the carried remainder must count towards the next step");
        }

        [Test]
        public void PausingStopsRealAdvanceButNotExplicitFastForward()
        {
            _clock.IsPaused = true;
            _clock.AdvanceReal(10.0);

            Assert.That(_clock.ElapsedGameSeconds, Is.EqualTo(0.0));
            Assert.That(_needs, Is.EqualTo(0));

            _clock.AdvanceGame(60.0);

            Assert.That(_world, Is.EqualTo(1), "sleeping must still move time while paused");
        }

        [Test]
        public void ClockStartsAtSixInTheMorningOnDayOne()
        {
            Assert.That(_clock.Day, Is.EqualTo(1));
            Assert.That(_clock.HourOfDay, Is.EqualTo(SimClock.StartHourOfDay));
            Assert.That(_clock.MinuteOfHour, Is.EqualTo(0));
        }

        [Test]
        public void CalendarRollsOverAtMidnight()
        {
            // 18 game hours from 06:00 is midnight at the start of day 2.
            _clock.AdvanceGame(18.0 * 3600.0);

            Assert.That(_clock.Day, Is.EqualTo(2));
            Assert.That(_clock.HourOfDay, Is.EqualTo(0));

            _clock.AdvanceGame(90.0 * 60.0);

            Assert.That(_clock.Day, Is.EqualTo(2));
            Assert.That(_clock.HourOfDay, Is.EqualTo(1));
            Assert.That(_clock.MinuteOfHour, Is.EqualTo(30));
        }

        [Test]
        public void GridFailureDayIsReachedOnTheDocumentedDay()
        {
            // The decay calendar in docs/05-world-and-map.md, 5.5 puts grid failure on days 6-8.
            // Day 6 begins five midnights after the 06:00 start, which is 18 + 4 * 24 game hours.
            _clock.AdvanceGame((18.0 + 4.0 * 24.0) * 3600.0);

            Assert.That(_clock.Day, Is.EqualTo(6));
        }

        [Test]
        public void ResetReturnsToTheStartOfDayOne()
        {
            _clock.AdvanceGame(5.0 * 24.0 * 3600.0);
            _clock.Reset();

            Assert.That(_clock.ElapsedGameSeconds, Is.EqualTo(0.0));
            Assert.That(_clock.Day, Is.EqualTo(1));
            Assert.That(_clock.HourOfDay, Is.EqualTo(SimClock.StartHourOfDay));
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void NonPositiveOrNonFiniteTimeScaleIsRejected(double timeScale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _clock.TimeScale = timeScale);
        }

        [Test]
        public void TimeDoesNotRunBackwards()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _clock.AdvanceReal(-1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _clock.AdvanceGame(-1.0));
        }
    }
}
