using System.Collections.Generic;
using Coldfall.Sim;
using Coldfall.World;
using UnityEngine;

namespace Coldfall.Player
{
    /// <summary>
    /// Joins the player in the scene to the simulation: reads where they are standing and what
    /// the weather is doing, and steps their core temperature, needs and clothing wetness.
    /// <para>
    /// Everything of consequence happens in <c>Coldfall.Sim</c>. This component resolves the
    /// inputs and hands them over; it computes nothing itself, which is what keeps the model
    /// testable without a scene.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public sealed class SurvivalCharacter : MonoBehaviour
    {
        [Tooltip("Start in the winter set instead of the deliberately insufficient starting one.")]
        [SerializeField] private bool startInWinterClothing;

        [Tooltip("Radiant heat credited when standing in a room with a lit heat source.")]
        [SerializeField] private float radiantHeatNearFire = 9f;

        private FirstPersonController _controller;
        private SimClock _clock;

        /// <summary>The character's core temperature.</summary>
        public CoreTemperature Core { get; } = new CoreTemperature();

        /// <summary>Calories, hydration and fatigue.</summary>
        public SurvivalNeeds Needs { get; } = new SurvivalNeeds();

        /// <summary>What they are wearing.</summary>
        public List<Garment> Garments { get; private set; }

        /// <summary>Air temperature where they are standing, from the last tick.</summary>
        public float AmbientCelsius { get; private set; }

        /// <summary>Wind at the body, after shelter, from the last tick.</summary>
        public float WindKilometresPerHour { get; private set; }

        /// <summary>Radiant heat credited on the last tick.</summary>
        public float RadiantHeat { get; private set; }

        /// <summary>True when standing inside an authored room.</summary>
        public bool IsIndoors { get; private set; }

        /// <summary>Mean wetness across the worn set, 0 to 1.</summary>
        public float MeanWetness
        {
            get
            {
                if (Garments == null || Garments.Count == 0)
                {
                    return 0f;
                }

                var sum = 0f;
                foreach (var garment in Garments)
                {
                    sum += garment.Wetness;
                }

                return sum / Garments.Count;
            }
        }

        /// <summary>What the character is currently doing, derived from how fast they are moving.</summary>
        public ActivityLevel Activity
        {
            get
            {
                if (_controller == null)
                {
                    return ActivityLevel.Rest;
                }

                var speed = _controller.HorizontalSpeed;
                if (speed < 0.2f)
                {
                    return ActivityLevel.Rest;
                }

                return speed > 3f ? ActivityLevel.Hard : ActivityLevel.Light;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<FirstPersonController>();
            Garments = startInWinterClothing ? Loadouts.Winter() : Loadouts.Starting();
        }

        private void OnEnable()
        {
            Subscribe(SimClockHost.Current);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (_clock == null)
            {
                Subscribe(SimClockHost.Current);
            }
        }

        private void Subscribe(SimClock clock)
        {
            if (clock == null || _clock != null)
            {
                return;
            }

            _clock = clock;
            _clock.NeedsTick += OnNeedsTick;
            _clock.WorldTick += OnWorldTick;
        }

        private void Unsubscribe()
        {
            if (_clock == null)
            {
                return;
            }

            _clock.NeedsTick -= OnNeedsTick;
            _clock.WorldTick -= OnWorldTick;
            _clock = null;
        }

        /// <summary>
        /// Resolves the character's surroundings. Indoors the air comes from the room's own
        /// diffused temperature and the wind is gone; outdoors both come from the weather.
        /// </summary>
        private void ReadSurroundings()
        {
            var weather = WeatherHost.Current;
            var thermal = ThermalWorld.Current;
            var position = transform.position;

            if (thermal != null)
            {
                var room = thermal.RoomAt(position);
                IsIndoors = room != null;
                AmbientCelsius = room != null ? room.Celsius : thermal.OutdoorCelsius;
                RadiantHeat = room != null && room.HeatInput > 0f ? radiantHeatNearFire : 0f;
            }
            else
            {
                IsIndoors = false;
                AmbientCelsius = weather?.AmbientCelsius ?? 8f;
                RadiantHeat = 0f;
            }

            var outdoorWind = weather?.WindKilometresPerHour ?? 0f;
            WindKilometresPerHour = IsIndoors ? 0f : outdoorWind;
        }

        private void OnNeedsTick(double gameSeconds)
        {
            ReadSurroundings();

            var conditions = new ThermalConditions
            {
                AmbientCelsius = AmbientCelsius,
                WindKilometresPerHour = WindKilometresPerHour,
                Activity = Activity,
                RadiantHeat = RadiantHeat,
                IsStarving = Needs.IsStarving,
            };

            Core.Step(gameSeconds, conditions, Garments);

            Needs.Step(gameSeconds, new NeedsContext
            {
                Activity = Activity,
                // Shivering is read from the balance rather than from the temperature, so the
                // calorie cost and the heat it buys can never disagree.
                IsShivering = Core.LastBalance.Shivering > 0f,
                IsAsleep = false,
            });
        }

        private void OnWorldTick(double gameSeconds)
        {
            var weather = WeatherHost.Current;
            if (weather == null || Garments == null)
            {
                return;
            }

            ReadSurroundings();

            var conditions = weather.WetnessFor(
                shelter: IsIndoors ? 1f : 0f,
                radiantHeat: RadiantHeat,
                exertion: Activity == ActivityLevel.Hard ? 1f : Activity == ActivityLevel.Light ? 0.35f : 0f,
                indoors: IsIndoors,
                clothingBulk: ClothingInsulation.TotalBulk(Garments));

            WetnessSystem.Step(Garments, conditions, gameSeconds);
        }
    }
}
