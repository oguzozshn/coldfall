using System.Collections.Generic;
using Coldfall.Sim;
using UnityEngine;

namespace Coldfall.World
{
    /// <summary>
    /// Collects the authored thermal volumes and portals, runs the diffusion on the world tick,
    /// and answers "how cold is it where this thing is standing".
    /// <para>
    /// Index 0 is always the implicit outdoors volume of docs/09, 9.5, whose temperature comes
    /// from the weather rather than from the rooms venting into it.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public sealed class ThermalWorld : MonoBehaviour
    {
        private const int OutdoorsIndex = 0;

        /// <summary>The active instance, or null outside play mode.</summary>
        public static ThermalWorld Current { get; private set; }

        private readonly List<ThermalVolumeComponent> _rooms = new List<ThermalVolumeComponent>();
        private ThermalVolume[] _volumes = System.Array.Empty<ThermalVolume>();
        private Portal[] _portals = System.Array.Empty<Portal>();
        private ThermalPortalComponent[] _portalComponents = System.Array.Empty<ThermalPortalComponent>();

        private SimClock _clock;

        /// <summary>Outdoor air temperature as of the last tick.</summary>
        public float OutdoorCelsius { get; private set; }

        private void Awake()
        {
            Current = this;
            Rebuild();
        }

        private void OnEnable()
        {
            _clock = SimClockHost.Current;
            if (_clock != null)
            {
                _clock.WorldTick += OnWorldTick;
            }
        }

        private void OnDisable()
        {
            if (_clock != null)
            {
                _clock.WorldTick -= OnWorldTick;
                _clock = null;
            }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void Update()
        {
            if (_clock != null || SimClockHost.Current == null)
            {
                return;
            }

            _clock = SimClockHost.Current;
            _clock.WorldTick += OnWorldTick;
        }

        /// <summary>
        /// Rescans the scene for volumes and portals. Called on wake, and whenever a chunk or
        /// interior is streamed in.
        /// </summary>
        public void Rebuild()
        {
            _rooms.Clear();
            _rooms.AddRange(FindObjectsByType<ThermalVolumeComponent>(FindObjectsSortMode.None));

            _volumes = new ThermalVolume[_rooms.Count + 1];
            _volumes[OutdoorsIndex] = new ThermalVolume
            {
                IsOutdoors = true,
                WindExposure = 1f,
                CubicMetres = 1_000_000f,
            };

            for (var i = 0; i < _rooms.Count; i++)
            {
                _rooms[i].Index = i + 1;
                _volumes[i + 1] = _rooms[i].ToVolume();
            }

            _portalComponents = FindObjectsByType<ThermalPortalComponent>(FindObjectsSortMode.None);
            _portals = new Portal[_portalComponents.Length];

            for (var i = 0; i < _portalComponents.Length; i++)
            {
                var component = _portalComponents[i];
                _portals[i] = new Portal
                {
                    VolumeA = component.VolumeA != null ? component.VolumeA.Index : OutdoorsIndex,
                    VolumeB = component.VolumeB != null ? component.VolumeB.Index : OutdoorsIndex,
                    State = component.State,
                    AreaSquareMetres = component.AreaSquareMetres,
                };
            }
        }

        /// <summary>Air temperature at a world-space point, in Celsius.</summary>
        public float CelsiusAt(Vector3 point)
        {
            var room = RoomAt(point);
            return room != null ? room.Celsius : OutdoorCelsius;
        }

        /// <summary>
        /// How sheltered a point is, 0 outdoors and 1 inside any room. Rooms do not currently
        /// shelter partially; a porch would need its own volume.
        /// </summary>
        public float ShelterAt(Vector3 point) => RoomAt(point) != null ? 1f : 0f;

        /// <summary>True if the point is inside an authored room.</summary>
        public bool IsIndoors(Vector3 point) => RoomAt(point) != null;

        /// <summary>The room containing a point, or null if the point is outdoors.</summary>
        public ThermalVolumeComponent RoomAt(Vector3 point)
        {
            for (var i = 0; i < _rooms.Count; i++)
            {
                if (_rooms[i] != null && _rooms[i].Contains(point))
                {
                    return _rooms[i];
                }
            }

            return null;
        }

        private void OnWorldTick(double gameSeconds)
        {
            if (_volumes.Length == 0)
            {
                return;
            }

            var weather = WeatherHost.Current;
            OutdoorCelsius = weather?.AmbientCelsius ?? 8f;

            // Pick up authored changes: a portal that was boarded this tick, a stove that was
            // lit. Cheap at this scale and it keeps the gameplay state authoritative.
            for (var i = 0; i < _portalComponents.Length; i++)
            {
                _portals[i].State = _portalComponents[i].State;
            }

            for (var i = 0; i < _rooms.Count; i++)
            {
                _volumes[i + 1].HeatInput = _rooms[i].HeatInput;
                _volumes[i + 1].Celsius = _rooms[i].Celsius;
            }

            ThermalDiffusion.Step(_volumes, _portals, OutdoorCelsius, (float)(gameSeconds / 60d));

            for (var i = 0; i < _rooms.Count; i++)
            {
                _rooms[i].Celsius = _volumes[i + 1].Celsius;
            }
        }
    }
}
