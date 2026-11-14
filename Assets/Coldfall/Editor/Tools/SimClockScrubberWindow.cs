using Coldfall.Sim;
using Coldfall.World;
using UnityEditor;
using UnityEngine;

namespace Coldfall.Editor.Tools
{
    /// <summary>
    /// The time scrubber from docs/09-technical-architecture.md, 9.11.
    /// <para>
    /// The decay calendar in docs/05-world-and-map.md, 5.5 is the spine of the whole run: the
    /// grid fails, the water stops, the first blizzard arrives, winter deepens. None of that can
    /// be tested by playing to day 20 in real time, so being able to put the world on any day in
    /// a second is not a convenience, it is the only way the late game ever gets looked at.
    /// </para>
    /// <para>
    /// <b>Weather is not here yet.</b> 9.11 calls this a time-and-weather scrubber, and the
    /// weather half arrives with the weather system in M1. The section is drawn disabled rather
    /// than hidden so it is obvious what is missing rather than forgotten.
    /// </para>
    /// <para>
    /// Time only ever moves forward. The simulation has no inverse — spoilage, wounds and the
    /// decay calendar are not reversible functions — so a scrubber that appeared to rewind would
    /// be lying. Jumping back means restarting.
    /// </para>
    /// </summary>
    public sealed class SimClockScrubberWindow : EditorWindow
    {
        private const double SecondsPerMinute = 60.0;
        private const double SecondsPerHour = 3600.0;
        private const double SecondsPerDay = 86400.0;

        /// <summary>
        /// A single advance is chunked to this much game time. SimClock discards any advance
        /// that would need more than MaxStepsPerAdvance steps of one rate, and a day already
        /// costs 4,320 needs ticks, so a multi-day jump has to arrive in pieces or it is
        /// silently truncated.
        /// </summary>
        private const double MaxChunkGameSeconds = SecondsPerDay;

        private int _targetDay = 1;
        private double _timeScaleField = SimClock.DefaultTimeScale;

        private static readonly (int Day, string Event)[] DecayCalendar =
        {
            (3, "First hard freeze"),
            (6, "Grid failure begins"),
            (10, "Water mains fail"),
            (12, "Vehicle fuel degrades"),
            (14, "First blizzard"),
            (20, "Deep winter, season switch"),
        };

        [MenuItem("Coldfall/Tools/Time scrubber")]
        public static void Open()
        {
            var window = GetWindow<SimClockScrubberWindow>();
            window.titleContent = new GUIContent("Time scrubber");
            window.minSize = new Vector2(320f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            var clock = SimClockHost.Current;

            if (clock == null)
            {
                EditorGUILayout.HelpBox(
                    "No clock. The scrubber drives the live SimClock owned by SimClockHost, so it " +
                    "only works in play mode with a scene that has one.",
                    MessageType.Info);
                return;
            }

            DrawReadout(clock);
            EditorGUILayout.Space();
            DrawRate(clock);
            EditorGUILayout.Space();
            DrawAdvance(clock);
            EditorGUILayout.Space();
            DrawCalendar(clock);
            EditorGUILayout.Space();
            DrawWeatherPlaceholder();
        }

        private static void DrawReadout(SimClock clock)
        {
            EditorGUILayout.LabelField("Now", EditorStyles.boldLabel);

            var style = new GUIStyle(EditorStyles.label) { fontSize = 20 };
            EditorGUILayout.LabelField(
                $"Day {clock.Day}   {clock.HourOfDay:00}:{clock.MinuteOfHour:00}", style);

            EditorGUILayout.LabelField($"Elapsed  {clock.ElapsedGameHours:F2} game hours");

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Ticks", EditorStyles.miniBoldLabel);

            // Per game hour these must read 180, 36 and 60. Anything else means the clock is
            // not doing the one thing it exists to do.
            var hours = clock.ElapsedGameHours;
            var needsPerHour = hours > 0.01 ? clock.NeedsTickCount / hours : 0d;
            var bodyPerHour = hours > 0.01 ? clock.BodyTickCount / hours : 0d;
            var worldPerHour = hours > 0.01 ? clock.WorldTickCount / hours : 0d;

            EditorGUILayout.LabelField(
                $"needs {clock.NeedsTickCount}  ({needsPerHour:F1}/h, expected 180)");
            EditorGUILayout.LabelField(
                $"body  {clock.BodyTickCount}  ({bodyPerHour:F1}/h, expected 36)");
            EditorGUILayout.LabelField(
                $"world {clock.WorldTickCount}  ({worldPerHour:F1}/h, expected 60)");
        }

        private void DrawRate(SimClock clock)
        {
            EditorGUILayout.LabelField("Rate", EditorStyles.boldLabel);

            var paused = EditorGUILayout.Toggle("Paused", clock.IsPaused);
            if (paused != clock.IsPaused)
            {
                clock.IsPaused = paused;
            }

            _timeScaleField = EditorGUILayout.DoubleField("Game seconds per real second", clock.TimeScale);
            if (_timeScaleField > 0d && !Mathf.Approximately((float)_timeScaleField, (float)clock.TimeScale))
            {
                clock.TimeScale = _timeScaleField;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var scale in new[] { 1d, 20d, 60d, 240d })
                {
                    if (GUILayout.Button($"1:{scale:0}"))
                    {
                        clock.TimeScale = scale;
                        _timeScaleField = scale;
                    }
                }
            }

            EditorGUILayout.LabelField(
                "1:20 is the shipping default. Changing it must not change any outcome.",
                EditorStyles.miniLabel);
        }

        private void DrawAdvance(SimClock clock)
        {
            EditorGUILayout.LabelField("Advance", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+10 min"))
                {
                    Advance(clock, 10d * SecondsPerMinute);
                }

                if (GUILayout.Button("+1 hour"))
                {
                    Advance(clock, SecondsPerHour);
                }

                if (GUILayout.Button("+6 hours"))
                {
                    Advance(clock, 6d * SecondsPerHour);
                }

                if (GUILayout.Button("+1 day"))
                {
                    Advance(clock, SecondsPerDay);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _targetDay = Mathf.Max(1, EditorGUILayout.IntField("Jump to day", _targetDay));

                using (new EditorGUI.DisabledScope(_targetDay <= clock.Day))
                {
                    if (GUILayout.Button("Go", GUILayout.Width(60f)))
                    {
                        JumpToDay(clock, _targetDay);
                    }
                }
            }

            if (_targetDay <= clock.Day)
            {
                EditorGUILayout.LabelField(
                    "Time only moves forward; restart to go back.", EditorStyles.miniLabel);
            }
        }

        private void DrawCalendar(SimClock clock)
        {
            EditorGUILayout.LabelField("Decay calendar", EditorStyles.boldLabel);

            foreach (var entry in DecayCalendar)
            {
                var passed = clock.Day >= entry.Day;
                var style = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = passed ? FontStyle.Normal : FontStyle.Italic,
                };

                using (new EditorGUI.DisabledScope(!passed))
                {
                    EditorGUILayout.LabelField($"Day {entry.Day,2}   {entry.Event}", style);
                }
            }
        }

        private static void DrawWeatherPlaceholder()
        {
            EditorGUILayout.LabelField("Weather", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Popup("Force state", 0, new[] { "Not implemented until M1" });
                EditorGUILayout.Slider("Wind", 0f, 0f, 1f);
                EditorGUILayout.Slider("Wetness", 0f, 0f, 1f);
            }

            EditorGUILayout.LabelField(
                "Drawn disabled on purpose: the weather system lands in M1 and this is where it hangs.",
                EditorStyles.miniLabel);
        }

        private static void JumpToDay(SimClock clock, int day)
        {
            // Day N begins at midnight, which is (N - 1) days after day 1's midnight. The run
            // starts at 06:00 on day 1, so the offset is measured from that.
            var targetElapsed = (day - 1) * SecondsPerDay - SimClock.StartHourOfDay * SecondsPerHour;
            var delta = targetElapsed - clock.ElapsedGameSeconds;

            if (delta <= 0d)
            {
                return;
            }

            Advance(clock, delta);
        }

        /// <summary>
        /// Advances in chunks, because a single oversized advance is truncated by the clock's
        /// own runaway guard rather than simulated.
        /// </summary>
        private static void Advance(SimClock clock, double gameSeconds)
        {
            var remaining = gameSeconds;

            while (remaining > 0d)
            {
                var chunk = System.Math.Min(remaining, MaxChunkGameSeconds);
                clock.AdvanceGame(chunk);
                remaining -= chunk;
            }
        }
    }
}
