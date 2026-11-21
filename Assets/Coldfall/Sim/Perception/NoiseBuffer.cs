using System;
using Unity.Mathematics;

namespace Coldfall.Sim
{
    /// <summary>
    /// The ring buffer of noise events from docs/09-technical-architecture.md, 9.2.
    /// <para>
    /// <b>This is the only way anything in the world can become aware of the player.</b> There
    /// is no aggro flag, no shared knowledge between zombies and no line from the player to the
    /// AI that does not pass through here. That absence is an architectural invariant, enforced
    /// by the assembly layout, and it is what lets the player always reason about why they were
    /// found.
    /// </para>
    /// <para>
    /// Fixed capacity and no allocation after construction: it is written to every time anything
    /// in the world makes a sound and read by every live agent's perception job.
    /// </para>
    /// </summary>
    public sealed class NoiseBuffer
    {
        /// <summary>How long an event stays audible to a listener, in game seconds.</summary>
        public const double RetentionSeconds = 10d;

        private readonly NoiseEvent[] _events;
        private int _next;
        private int _count;

        /// <summary>Creates a buffer.</summary>
        /// <param name="capacity">Maximum events retained at once.</param>
        public NoiseBuffer(int capacity = 256)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
            }

            _events = new NoiseEvent[capacity];
        }

        /// <summary>How many events are currently held.</summary>
        public int Count => _count;

        /// <summary>Maximum events the buffer can hold.</summary>
        public int Capacity => _events.Length;

        /// <summary>Adds an event, overwriting the oldest once full.</summary>
        public void Emit(float3 position, float radius, NoiseType type, double gameTime)
        {
            if (radius <= 0f)
            {
                return;
            }

            _events[_next] = new NoiseEvent(position, radius, type, gameTime);
            _next = (_next + 1) % _events.Length;

            if (_count < _events.Length)
            {
                _count++;
            }
        }

        /// <summary>Clears every event. For loading a save.</summary>
        public void Clear()
        {
            _next = 0;
            _count = 0;
        }

        /// <summary>The event at an index, newest last. Index must be below <see cref="Count"/>.</summary>
        public NoiseEvent this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                var start = _count == _events.Length ? _next : 0;
                return _events[(start + index) % _events.Length];
            }
        }

        /// <summary>
        /// Finds the most attention-worthy noise a listener can currently hear.
        /// </summary>
        /// <param name="listener">Where the listener is.</param>
        /// <param name="now">Current game time in seconds.</param>
        /// <param name="wallsBetween">
        /// A function giving the number of walls between the listener and a position. Supplied
        /// by the world, so this stays testable without a scene.
        /// </param>
        /// <param name="weatherModifier">Hearing modifier from the weather, see 4.3.</param>
        /// <param name="hearingMultiplier">The listener's own hearing, see 4.2.</param>
        /// <param name="heard">The event, if one was heard.</param>
        /// <returns>True if anything was heard.</returns>
        public bool TryHearLoudest(
            float3 listener, double now, Func<float3, int> wallsBetween,
            float weatherModifier, float hearingMultiplier, out NoiseEvent heard)
        {
            heard = default;
            var bestMargin = 0f;
            var found = false;

            for (var i = 0; i < _count; i++)
            {
                var candidate = this[i];

                if (now - candidate.GameTime > RetentionSeconds)
                {
                    continue;
                }

                var effective = EffectiveRadius(
                    candidate.Radius,
                    wallsBetween?.Invoke(candidate.Position) ?? 0,
                    weatherModifier,
                    hearingMultiplier);

                var distance = math.distance(listener, candidate.Position);
                var margin = effective - distance;

                if (margin <= 0f || margin <= bestMargin)
                {
                    continue;
                }

                bestMargin = margin;
                heard = candidate;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// How far a noise actually carries for a given listener.
        /// <para>
        /// Each wall removes 60% of what is left, so walls compound rather than subtract: a
        /// gunshot three rooms away is a different event from a gunshot in the next room, which
        /// is what makes interior layout a stealth system.
        /// </para>
        /// </summary>
        public static float EffectiveRadius(
            float radius, int wallsBetween, float weatherModifier, float hearingMultiplier)
        {
            var effective = radius;

            for (var i = 0; i < wallsBetween; i++)
            {
                effective *= 0.4f;
            }

            if (weatherModifier > 0f)
            {
                effective *= weatherModifier;
            }

            if (hearingMultiplier > 0f)
            {
                effective *= hearingMultiplier;
            }

            return effective < 0f ? 0f : effective;
        }

        /// <summary>
        /// Where a listener thinks a noise came from: the real position with an error that grows
        /// with distance.
        /// <para>
        /// Docs/04, 4.3 is explicit that hearing never produces a perfect fix on the player.
        /// Zombies walk towards roughly where the sound was, and that single detail is what
        /// makes stealth readable and survivable in first person. A perfect fix would turn every
        /// noise into a homing beacon.
        /// </para>
        /// </summary>
        /// <param name="listener">Where the listener is.</param>
        /// <param name="source">Where the noise really was.</param>
        /// <param name="random">Deterministic source of randomness, seeded per agent.</param>
        /// <param name="errorPerMetre">Error radius added per metre of distance.</param>
        public static float3 EstimateSource(
            float3 listener, float3 source, ref Unity.Mathematics.Random random,
            float errorPerMetre = 0.18f)
        {
            var distance = math.distance(listener, source);
            var error = distance * errorPerMetre;

            if (error <= 0.01f)
            {
                return source;
            }

            // Offset on the ground plane only: a zombie misjudging the height of a sound would
            // read as a bug rather than as imprecision.
            var angle = random.NextFloat(0f, math.PI * 2f);
            var magnitude = random.NextFloat(0f, error);

            return source + new float3(math.cos(angle) * magnitude, 0f, math.sin(angle) * magnitude);
        }
    }
}
