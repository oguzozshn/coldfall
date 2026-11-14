using Coldfall.Sim;
using UnityEngine;

namespace Coldfall.World
{
    /// <summary>
    /// A door or window joining two thermal volumes, or joining one to the outdoors.
    /// <para>
    /// <see cref="State"/> is the same state gameplay reads and writes. Boarding this window in
    /// docs/04, 4.9 raises the room's temperature in docs/03, 3.8 through nothing but this
    /// field, and docs/08, 8.2 will read it for audio occlusion.
    /// </para>
    /// </summary>
    public sealed class ThermalPortalComponent : MonoBehaviour
    {
        [Tooltip("One side. Leave empty to mean the outdoors.")]
        [SerializeField] private ThermalVolumeComponent volumeA;

        [Tooltip("The other side. Leave empty to mean the outdoors.")]
        [SerializeField] private ThermalVolumeComponent volumeB;

        [Tooltip("Opening area in square metres. A door is about 1.8, a window about 1.0.")]
        [SerializeField] private float areaSquareMetres = 1.8f;

        [SerializeField] private PortalState state = PortalState.Closed;

        /// <summary>Current state. Setting it takes effect on the next world tick.</summary>
        public PortalState State
        {
            get => state;
            set => state = value;
        }

        /// <summary>Opening area in square metres.</summary>
        public float AreaSquareMetres => areaSquareMetres;

        /// <summary>One side, or null for the outdoors.</summary>
        public ThermalVolumeComponent VolumeA => volumeA;

        /// <summary>The other side, or null for the outdoors.</summary>
        public ThermalVolumeComponent VolumeB => volumeB;

        /// <summary>
        /// Sets the authored values. Used by the greybox scene generator. Pass null for a side
        /// to mean the outdoors.
        /// </summary>
        public void Configure(
            ThermalVolumeComponent a, ThermalVolumeComponent b, float area, PortalState initialState)
        {
            volumeA = a;
            volumeB = b;
            areaSquareMetres = area;
            state = initialState;
        }

        /// <summary>Cycles the state, for testing the coupling by hand in the greybox.</summary>
        public void CycleState()
        {
            state = state switch
            {
                PortalState.Sealed => PortalState.Closed,
                PortalState.Closed => PortalState.Boarded,
                PortalState.Boarded => PortalState.Open,
                PortalState.Open => PortalState.Broken,
                _ => PortalState.Sealed,
            };
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = state switch
            {
                PortalState.Sealed => new Color(0.3f, 0.5f, 0.3f),
                PortalState.Closed => new Color(0.4f, 0.6f, 0.4f),
                PortalState.Boarded => new Color(0.7f, 0.6f, 0.3f),
                PortalState.Open => new Color(0.8f, 0.4f, 0.2f),
                _ => new Color(0.85f, 0.25f, 0.2f),
            };

            Gizmos.DrawWireCube(transform.position, new Vector3(1f, 2f, 0.1f));
        }
    }
}
