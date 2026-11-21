using Unity.Mathematics;

namespace Coldfall.Sim
{
    /// <summary>
    /// What made a noise. Zombie audio has to encode this (docs/08, 8.3), and the AI uses it to
    /// decide how interesting an investigation is.
    /// </summary>
    public enum NoiseType
    {
        /// <summary>Footsteps, at whatever pace.</summary>
        Footstep,

        /// <summary>A door, a window, a drawer.</summary>
        Interaction,

        /// <summary>Glass breaking. Loud and unmistakable.</summary>
        Breaking,

        /// <summary>A melee impact.</summary>
        Impact,

        /// <summary>Work: chopping, hammering, boarding. Repeating and locatable.</summary>
        Work,

        /// <summary>A machine running. Continuous, which is worse than loud.</summary>
        Machine,

        /// <summary>A gunshot.</summary>
        Gunshot,

        /// <summary>The player's own body: coughing, heavy breathing.</summary>
        Body,

        /// <summary>A zombie vocalising, which is how one mistake becomes a horde.</summary>
        Vocalisation,
    }

    /// <summary>
    /// One noise in the world: where it was, how far it carried, what it was and when.
    /// <para>
    /// Docs/09, 9.2 makes this buffer the only route by which anything can become aware of the
    /// player. There is no aggro flag and no shared knowledge anywhere else, so the player can
    /// always reason about why they were found.
    /// </para>
    /// </summary>
    public readonly struct NoiseEvent
    {
        /// <summary>Where it happened.</summary>
        public float3 Position { get; }

        /// <summary>How far it carries before attenuation, in metres.</summary>
        public float Radius { get; }

        /// <summary>What made it.</summary>
        public NoiseType Type { get; }

        /// <summary>Game time it happened, in seconds since the run began.</summary>
        public double GameTime { get; }

        /// <summary>Creates an event.</summary>
        public NoiseEvent(float3 position, float radius, NoiseType type, double gameTime)
        {
            Position = position;
            Radius = radius;
            Type = type;
            GameTime = gameTime;
        }
    }

    /// <summary>
    /// The authored noise radii of docs/04-zombies-and-combat.md, 4.3.
    /// <para>
    /// This table is the stealth system. Every number in it is a decision the player makes
    /// dozens of times an hour: walk or run, pick the lock or break the window, chop the wood
    /// now or after dark.
    /// </para>
    /// </summary>
    public static class NoiseRadius
    {
        /// <summary>Crouch-walking.</summary>
        public const float CrouchWalk = 3f;

        /// <summary>Walking.</summary>
        public const float Walk = 8f;

        /// <summary>Running.</summary>
        public const float Run = 22f;

        /// <summary>Sprinting.</summary>
        public const float Sprint = 34f;

        /// <summary>Moving through standing water or deep mud.</summary>
        public const float Wading = 14f;

        /// <summary>Opening a door carefully.</summary>
        public const float CarefulDoor = 4f;

        /// <summary>Breaking a window. The loud way in.</summary>
        public const float BreakWindow = 45f;

        /// <summary>A melee hit landing on a zombie.</summary>
        public const float MeleeOnFlesh = 12f;

        /// <summary>A melee hit landing on a wall, railing or metal.</summary>
        public const float MeleeOnHard = 30f;

        /// <summary>Chopping wood. Repeating, which is what makes it dangerous.</summary>
        public const float ChopWood = 30f;

        /// <summary>A generator running.</summary>
        public const float Generator = 40f;

        /// <summary>A pistol shot.</summary>
        public const float Pistol = 180f;

        /// <summary>A shotgun shot.</summary>
        public const float Shotgun = 320f;

        /// <summary>A car alarm.</summary>
        public const float CarAlarm = 450f;

        /// <summary>The player coughing with a chest infection.</summary>
        public const float Cough = 10f;
    }
}
