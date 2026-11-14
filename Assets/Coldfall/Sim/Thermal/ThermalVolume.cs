namespace Coldfall.Sim
{
    /// <summary>
    /// How open a door or window is. The same state gameplay reads, so boarding a window in
    /// docs/04-zombies-and-combat.md, 4.9 warms the room in docs/03, 3.8 with no extra code,
    /// and the same value feeds audio occlusion in docs/08, 8.2. One authored structure, three
    /// systems reading it.
    /// </summary>
    public enum PortalState
    {
        /// <summary>Welded, bricked up, or structurally absent. No exchange at all.</summary>
        Sealed,

        /// <summary>Shut and intact. The ordinary state of a door or an unbroken window.</summary>
        Closed,

        /// <summary>Boarded over. Better than broken, worse than closed.</summary>
        Boarded,

        /// <summary>Standing open.</summary>
        Open,

        /// <summary>Smashed. The worst case, and the one the player usually creates.</summary>
        Broken,
    }

    /// <summary>
    /// One authored room, or the implicit outdoors volume of a district node.
    /// <para>
    /// A plain struct with no references, so the whole set lives in a NativeArray and the
    /// diffusion runs as a Burst job (docs/09, 9.2).
    /// </para>
    /// </summary>
    public struct ThermalVolume
    {
        /// <summary>Current air temperature in the volume, in Celsius.</summary>
        public float Celsius;

        /// <summary>
        /// How well the envelope holds heat, 0 to 1. A barn is near 0, a well-built house near
        /// 0.85. Never 1: no room holds heat forever.
        /// </summary>
        public float Insulation;

        /// <summary>How exposed the volume is to wind, 0 to 1. Outdoors volumes are 1.</summary>
        public float WindExposure;

        /// <summary>
        /// Air volume in cubic metres, which is the thermal mass. A church is enormous and
        /// therefore nearly impossible to heat; a bathroom warms in minutes.
        /// </summary>
        public float CubicMetres;

        /// <summary>
        /// Heating power from fires, stoves and heaters inside this volume, in degree-cubic-
        /// metres per game minute.
        /// <para>
        /// Power, not a temperature rate, because it is divided by <see cref="CubicMetres"/>.
        /// The same stove raises a 40 cubic metre room ten times faster than a 400 cubic metre
        /// church, which is exactly the trade docs/05, 5.4 puts in front of the player when it
        /// offers the church as a shelter: enormous and defensible, and impossible to heat.
        /// </para>
        /// </summary>
        public float HeatInput;

        /// <summary>True for the implicit outdoors volume of a district node.</summary>
        public bool IsOutdoors;
    }

    /// <summary>
    /// A door or a window, joining two volumes.
    /// </summary>
    public struct Portal
    {
        /// <summary>Index of the first volume.</summary>
        public int VolumeA;

        /// <summary>Index of the second volume.</summary>
        public int VolumeB;

        /// <summary>Current state.</summary>
        public PortalState State;

        /// <summary>Opening area in square metres.</summary>
        public float AreaSquareMetres;
    }

    /// <summary>
    /// Conductance constants for the diffusion.
    /// </summary>
    public static class ThermalConductance
    {
        /// <summary>Conductance per square metre per Jacobi pass, by portal state.</summary>
        public static float ForState(PortalState state)
        {
            return state switch
            {
                PortalState.Sealed => 0f,
                PortalState.Closed => 0.02f,
                PortalState.Boarded => 0.06f,
                PortalState.Open => 0.35f,
                PortalState.Broken => 0.50f,
                _ => 0f,
            };
        }

        /// <summary>
        /// Conductance of an uninsulated envelope, per pass. Scaled by (1 - insulation), so even
        /// a sealed room with no portals slowly loses heat to the outside.
        /// </summary>
        public const float EnvelopeBase = 0.004f;

        /// <summary>
        /// Extra envelope loss at full wind exposure, as a share of the envelope term. Wind
        /// strips heat from a building, which is why a hilltop cabin is colder than a sheltered
        /// one with the same walls.
        /// </summary>
        public const float WindEnvelopeBonus = 1.5f;

        /// <summary>
        /// Jacobi passes per world tick. Four is stable at one-minute steps and gives the
        /// behaviour docs/09, 9.5 asks for: a room with its door open to a blizzard cools in a
        /// believable handful of minutes.
        /// </summary>
        public const int PassesPerWorldTick = 4;

        /// <summary>
        /// Hard cap on the total coefficient a volume may take in one pass. Jacobi diverges
        /// above 1; clamping below it keeps a tiny room with a huge opening stable instead of
        /// oscillating, which a 40-day soak test would otherwise find the hard way.
        /// </summary>
        public const float MaxCoefficientPerPass = 0.5f;
    }
}
