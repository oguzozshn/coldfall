using System.Collections.Generic;
using Coldfall.Sim;
using UnityEngine;

namespace Coldfall.World
{
    /// <summary>
    /// A heat source placed in the scene. Burns fuel on the world tick, feeds its room's air
    /// temperature, and radiates to anything standing near it.
    /// </summary>
    public sealed class FireComponent : MonoBehaviour
    {
        [SerializeField] private HeatSourceKind kind = HeatSourceKind.WoodStove;

        [Tooltip("Kilograms of wood, or canisters for a gas heater.")]
        [SerializeField] private float startingFuel = 12f;

        [Tooltip("Start burning when the scene loads.")]
        [SerializeField] private bool startLit = true;

        [Tooltip("Fuel dryness, 0 to 1. Autumn firewood is damp and burns poorly.")]
        [Range(0f, 1f)]
        [SerializeField] private float fuelDryness = 1f;

        [Tooltip("The room this heats. Leave empty for an outdoor fire, which heats no room.")]
        [SerializeField] private ThermalVolumeComponent room;

        /// <summary>Every fire currently in the scene, for the character to sum radiant heat from.</summary>
        public static readonly List<FireComponent> Active = new List<FireComponent>();

        private SimClock _clock;

        /// <summary>The simulation object behind this component.</summary>
        public Fire Fire { get; private set; }

        /// <summary>True while burning.</summary>
        public bool IsLit => Fire != null && Fire.IsLit;

        private void Awake()
        {
            Fire = new Fire(kind, startingFuel) { FuelDryness = fuelDryness };

            if (startLit)
            {
                Fire.Light();
            }
        }

        private void OnEnable()
        {
            Active.Add(this);
            Subscribe(SimClockHost.Current);
        }

        private void OnDisable()
        {
            Active.Remove(this);

            if (_clock != null)
            {
                _clock.WorldTick -= OnWorldTick;
                _clock = null;
            }

            // Leave no heat behind in a room whose fire has been unloaded.
            if (room != null)
            {
                room.HeatInput = 0f;
            }
        }

        private void Update()
        {
            if (_clock == null)
            {
                Subscribe(SimClockHost.Current);
            }
        }

        /// <summary>Radiant heat this fire delivers at a world-space point.</summary>
        public float RadiantAt(Vector3 point)
        {
            if (Fire == null || !Fire.IsLit)
            {
                return 0f;
            }

            // Radiant heat does not pass through walls. Crediting it only inside the fire's own
            // room is crude compared with the occlusion docs/03, 3.3 asks for, but it is correct
            // where it matters: standing outside a wall from a stove must feel nothing.
            if (room != null)
            {
                var thermal = ThermalWorld.Current;
                if (thermal != null && thermal.RoomAt(point) != room)
                {
                    return 0f;
                }
            }

            return Fire.RadiantAt(Vector3.Distance(transform.position, point));
        }

        /// <summary>
        /// Sets the authored values. Used by the greybox scene generator. Pass null for the room
        /// to mean an outdoor fire, which radiates to anyone near it but heats no room.
        /// </summary>
        public void Configure(
            HeatSourceKind sourceKind, float fuel, bool lit, float dryness,
            ThermalVolumeComponent heatedRoom)
        {
            kind = sourceKind;
            startingFuel = fuel;
            startLit = lit;
            fuelDryness = Mathf.Clamp01(dryness);
            room = heatedRoom;
        }

        /// <summary>Lights the fire, if it can be lit.</summary>
        public bool Light() => Fire != null && Fire.Light();

        /// <summary>Puts the fire out.</summary>
        public void Extinguish() => Fire?.Extinguish();

        /// <summary>Adds fuel.</summary>
        public void AddFuel(float kilograms) => Fire?.AddFuel(kilograms);

        private void Subscribe(SimClock clock)
        {
            if (clock == null || _clock != null)
            {
                return;
            }

            _clock = clock;
            _clock.WorldTick += OnWorldTick;
        }

        private void OnWorldTick(double gameSeconds)
        {
            if (Fire == null)
            {
                return;
            }

            Fire.Step(gameSeconds);

            if (room != null)
            {
                room.HeatInput = Fire.RoomPower;
            }
        }

        private void OnDrawGizmos()
        {
            var lit = Application.isPlaying ? IsLit : startLit;
            Gizmos.color = lit ? new Color(0.88f, 0.54f, 0.18f) : new Color(0.35f, 0.35f, 0.33f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);

            if (!lit)
            {
                return;
            }

            // The light radius, which is also roughly how far this advertises the player at night.
            Gizmos.color = new Color(0.88f, 0.54f, 0.18f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, HeatSourceTable.For(kind).LightRadius);
        }
    }
}
