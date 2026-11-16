namespace Coldfall.Sim
{
    /// <summary>
    /// The warmth sources of docs/03-survival-systems.md, 3.3.
    /// </summary>
    public enum HeatSourceKind
    {
        /// <summary>A lighter or a match. Enough to start something, never enough to live on.</summary>
        Lighter,

        /// <summary>An outdoor campfire. Loud, bright, and visible as smoke from a long way off.</summary>
        Campfire,

        /// <summary>A domestic fireplace. Most of its heat goes up the chimney.</summary>
        Fireplace,

        /// <summary>A wood stove. The best heat per kilogram of wood in the game.</summary>
        WoodStove,

        /// <summary>A propane heater. Quiet and smokeless, and the fuel is finite and findable.</summary>
        GasHeater,

        /// <summary>An electric heater. Free warmth until the grid fails on day 6-8.</summary>
        ElectricHeater,
    }

    /// <summary>
    /// The authored row for a heat source.
    /// </summary>
    public readonly struct HeatSourceDefinition
    {
        /// <summary>
        /// Heating power delivered to the room's air, in degree-cubic-metres per game minute.
        /// Divided by the room's volume by the diffusion, so the same stove barely touches a
        /// church.
        /// </summary>
        public float RoomPower { get; }

        /// <summary>
        /// Radiant heat felt at one metre, in heat-balance units. Falls off with distance, so
        /// sitting at the fire and standing across the room are different things.
        /// </summary>
        public float RadiantAtOneMetre { get; }

        /// <summary>Kilograms of wood burned per game hour, or canisters per hour for gas.</summary>
        public float FuelPerHour { get; }

        /// <summary>Noise radius in metres while burning. See docs/04, 4.3.</summary>
        public float NoiseRadius { get; }

        /// <summary>Light radius in metres.</summary>
        public float LightRadius { get; }

        /// <summary>True if it produces a smoke plume visible from the lookout tower.</summary>
        public bool ProducesSmoke { get; }

        /// <summary>True if it needs mains power, and therefore dies with the grid.</summary>
        public bool NeedsGrid { get; }

        /// <summary>Creates a row.</summary>
        public HeatSourceDefinition(
            float roomPower, float radiantAtOneMetre, float fuelPerHour,
            float noiseRadius, float lightRadius, bool producesSmoke, bool needsGrid)
        {
            RoomPower = roomPower;
            RadiantAtOneMetre = radiantAtOneMetre;
            FuelPerHour = fuelPerHour;
            NoiseRadius = noiseRadius;
            LightRadius = lightRadius;
            ProducesSmoke = producesSmoke;
            NeedsGrid = needsGrid;
        }
    }

    /// <summary>
    /// The authored heat source table.
    /// <para>
    /// The ordering is the fuel economy in one glance: a wood stove gives the most heat per
    /// kilogram, a campfire the least, and a campfire is also the loudest and the only one that
    /// advertises itself as smoke. The player who can get a stove working has solved winter; the
    /// player cooking on an open fire outdoors is paying three times over.
    /// </para>
    /// </summary>
    public static class HeatSourceTable
    {
        private static readonly HeatSourceDefinition[] Definitions =
        {
            //                       room  radiant  fuel/h  noise  light  smoke  grid
            new HeatSourceDefinition(3f, 1.5f, 0f, 0f, 2f, false, false),
            new HeatSourceDefinition(60f, 12f, 1.5f, 12f, 18f, true, false),
            new HeatSourceDefinition(75f, 10f, 1.2f, 8f, 14f, true, false),
            new HeatSourceDefinition(90f, 8f, 0.9f, 6f, 6f, true, false),
            new HeatSourceDefinition(70f, 7f, 0.25f, 10f, 0f, false, false),
            new HeatSourceDefinition(80f, 6f, 0f, 4f, 2f, false, true),
        };

        /// <summary>The authored row for a kind.</summary>
        public static HeatSourceDefinition For(HeatSourceKind kind) => Definitions[(int)kind];
    }

    /// <summary>
    /// One burning heat source: how much fuel is left, how much heat it is giving, and how far
    /// that heat reaches.
    /// <para>
    /// Fuel is the quiet clock of the whole middle game. Docs/02 counts a successful night as
    /// one that passes without incident, and docs/03 notes that drying a soaked set near a fire
    /// is fuel the player then does not have for the night. This class is where that arithmetic
    /// actually happens.
    /// </para>
    /// </summary>
    public sealed class Fire
    {
        /// <summary>
        /// How much worse damp wood is. Docs/03, 3.8: autumn firewood is damp and burns poorly,
        /// winter firewood is dry but demand roughly doubles. Damp wood gives less heat for the
        /// same weight, so the autumn player burns more of it to get less.
        /// </summary>
        public const float DampWoodPowerMultiplier = 0.6f;

        /// <summary>Which kind of source this is.</summary>
        public HeatSourceKind Kind { get; }

        /// <summary>Fuel remaining, in kilograms of wood or in canisters.</summary>
        public float Fuel { get; private set; }

        /// <summary>
        /// Fuel dryness, 0 to 1. Below 1 the fire gives less heat for the same fuel.
        /// </summary>
        public float FuelDryness { get; set; } = 1f;

        /// <summary>True while burning.</summary>
        public bool IsLit { get; private set; }

        /// <summary>True if the grid has failed and this source needs it.</summary>
        public bool GridFailed { get; set; }

        /// <summary>The authored row.</summary>
        public HeatSourceDefinition Definition => HeatSourceTable.For(Kind);

        /// <summary>Creates a source.</summary>
        public Fire(HeatSourceKind kind, float fuel = 0f)
        {
            Kind = kind;
            Fuel = fuel < 0f ? 0f : fuel;
        }

        /// <summary>
        /// Power delivered to the room's air right now, in degree-cubic-metres per game minute.
        /// Zero when out.
        /// </summary>
        public float RoomPower => IsLit ? Definition.RoomPower * PowerScale : 0f;

        /// <summary>Noise radius right now, in metres. Zero when out.</summary>
        public float NoiseRadius => IsLit ? Definition.NoiseRadius : 0f;

        /// <summary>Light radius right now, in metres. Zero when out.</summary>
        public float LightRadius => IsLit ? Definition.LightRadius : 0f;

        /// <summary>
        /// Radiant heat felt at a distance, in heat-balance units.
        /// <para>
        /// Falls off as one over distance squared past a metre, which is what makes sitting at
        /// the fire a real action rather than a room-wide buff. At three metres a campfire gives
        /// a seventh of what it gives at one.
        /// </para>
        /// </summary>
        public float RadiantAt(float metres)
        {
            if (!IsLit)
            {
                return 0f;
            }

            var distance = metres < 1f ? 1f : metres;
            return Definition.RadiantAtOneMetre * PowerScale / (distance * distance);
        }

        /// <summary>Lights it, if there is fuel and, for an electric heater, power.</summary>
        /// <returns>True if it is now burning.</returns>
        public bool Light()
        {
            if (Definition.NeedsGrid)
            {
                IsLit = !GridFailed;
                return IsLit;
            }

            if (Definition.FuelPerHour > 0f && Fuel <= 0f)
            {
                return false;
            }

            IsLit = true;
            return true;
        }

        /// <summary>Puts it out. The fuel still in it is not lost.</summary>
        public void Extinguish()
        {
            IsLit = false;
        }

        /// <summary>Adds fuel.</summary>
        public void AddFuel(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            Fuel += amount;
        }

        /// <summary>
        /// Burns fuel for one world tick, and goes out when it runs dry.
        /// </summary>
        /// <param name="gameSeconds">Tick length, from <see cref="SimClock"/>.</param>
        public void Step(double gameSeconds)
        {
            if (!IsLit || gameSeconds <= 0d)
            {
                return;
            }

            if (Definition.NeedsGrid)
            {
                if (GridFailed)
                {
                    IsLit = false;
                }

                return;
            }

            if (Definition.FuelPerHour <= 0f)
            {
                return;
            }

            var hours = (float)(gameSeconds / 3600d);
            Fuel -= Definition.FuelPerHour * hours;

            if (Fuel > 0f)
            {
                return;
            }

            Fuel = 0f;
            IsLit = false;
        }

        /// <summary>
        /// Game hours of burn left at the current rate. What the player is really asking when
        /// they look at the woodpile.
        /// </summary>
        public float HoursRemaining
        {
            get
            {
                if (Definition.FuelPerHour <= 0f)
                {
                    return float.PositiveInfinity;
                }

                return Fuel / Definition.FuelPerHour;
            }
        }

        private float PowerScale
        {
            get
            {
                if (Definition.FuelPerHour <= 0f)
                {
                    return 1f;
                }

                var dryness = FuelDryness < 0f ? 0f : FuelDryness > 1f ? 1f : FuelDryness;
                return DampWoodPowerMultiplier + (1f - DampWoodPowerMultiplier) * dryness;
            }
        }
    }
}
