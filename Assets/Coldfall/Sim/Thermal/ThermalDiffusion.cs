using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Coldfall.Sim
{
    /// <summary>
    /// Spreads heat between thermal volumes through their portals, once per world tick.
    /// <para>
    /// Four Jacobi passes per tick, as docs/09-technical-architecture.md, 9.5 specifies. Jacobi
    /// rather than Gauss-Seidel because every volume must see the same starting state: with
    /// in-place updates the result would depend on the order rooms happen to sit in the array,
    /// and the simulation has to be reproducible (9.1).
    /// </para>
    /// </summary>
    [BurstCompile]
    public struct ThermalDiffusionJob : IJob
    {
        /// <summary>The volumes. Temperatures are updated in place.</summary>
        public NativeArray<ThermalVolume> Volumes;

        /// <summary>The portals joining them. Not modified.</summary>
        [ReadOnly] public NativeArray<Portal> Portals;

        /// <summary>Scratch buffer, same length as <see cref="Volumes"/>.</summary>
        public NativeArray<float> Next;

        /// <summary>Outdoor air temperature from the weather system, in Celsius.</summary>
        public float OutdoorCelsius;

        /// <summary>Length of the tick in game minutes. One, for the world tick.</summary>
        public float GameMinutes;

        /// <summary>Number of Jacobi passes.</summary>
        public int Passes;

        /// <summary>Runs the diffusion.</summary>
        public void Execute()
        {
            var count = Volumes.Length;
            if (count == 0 || Passes <= 0)
            {
                return;
            }

            for (var pass = 0; pass < Passes; pass++)
            {
                for (var i = 0; i < count; i++)
                {
                    var volume = Volumes[i];

                    if (volume.IsOutdoors)
                    {
                        // The outdoors is driven by the weather, not by the rooms venting into
                        // it. A house full of fires does not warm a valley.
                        Next[i] = OutdoorCelsius;
                        continue;
                    }

                    var mass = volume.CubicMetres > 1f ? volume.CubicMetres : 1f;

                    var envelope = ThermalConductance.EnvelopeBase
                                   * (1f - Clamp01(volume.Insulation))
                                   * (1f + Clamp01(volume.WindExposure) * ThermalConductance.WindEnvelopeBonus);

                    var weighted = envelope * (OutdoorCelsius - volume.Celsius);
                    var total = envelope;

                    for (var p = 0; p < Portals.Length; p++)
                    {
                        var portal = Portals[p];
                        int other;

                        if (portal.VolumeA == i)
                        {
                            other = portal.VolumeB;
                        }
                        else if (portal.VolumeB == i)
                        {
                            other = portal.VolumeA;
                        }
                        else
                        {
                            continue;
                        }

                        if (other < 0 || other >= count)
                        {
                            continue;
                        }

                        var conductance = ThermalConductance.ForState(portal.State)
                                          * portal.AreaSquareMetres / mass;

                        if (conductance <= 0f)
                        {
                            continue;
                        }

                        var otherTemperature = Volumes[other].IsOutdoors
                            ? OutdoorCelsius
                            : Volumes[other].Celsius;

                        weighted += conductance * (otherTemperature - volume.Celsius);
                        total += conductance;
                    }

                    // Clamping the summed coefficient rather than each term keeps a small room
                    // with a large opening stable; without it Jacobi overshoots and oscillates.
                    if (total > ThermalConductance.MaxCoefficientPerPass)
                    {
                        weighted *= ThermalConductance.MaxCoefficientPerPass / total;
                    }

                    var heat = volume.HeatInput * GameMinutes / Passes / mass;
                    Next[i] = volume.Celsius + weighted + heat;
                }

                for (var i = 0; i < count; i++)
                {
                    var volume = Volumes[i];
                    volume.Celsius = Next[i];
                    Volumes[i] = volume;
                }
            }
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }

    /// <summary>
    /// Managed entry point for the diffusion, for callers that hold ordinary arrays. The world
    /// tick uses the job directly against its persistent NativeArrays; this exists for tests and
    /// for editor tools.
    /// </summary>
    public static class ThermalDiffusion
    {
        /// <summary>
        /// Runs one world tick of diffusion over the given volumes and portals, in place.
        /// </summary>
        public static void Step(
            ThermalVolume[] volumes, Portal[] portals, float outdoorCelsius, float gameMinutes = 1f)
        {
            if (volumes == null || volumes.Length == 0)
            {
                return;
            }

            portals ??= System.Array.Empty<Portal>();

            var nativeVolumes = new NativeArray<ThermalVolume>(volumes, Allocator.TempJob);
            var nativePortals = new NativeArray<Portal>(portals, Allocator.TempJob);
            var scratch = new NativeArray<float>(volumes.Length, Allocator.TempJob);

            try
            {
                new ThermalDiffusionJob
                {
                    Volumes = nativeVolumes,
                    Portals = nativePortals,
                    Next = scratch,
                    OutdoorCelsius = outdoorCelsius,
                    GameMinutes = gameMinutes,
                    Passes = ThermalConductance.PassesPerWorldTick,
                }.Run();

                nativeVolumes.CopyTo(volumes);
            }
            finally
            {
                nativeVolumes.Dispose();
                nativePortals.Dispose();
                scratch.Dispose();
            }
        }
    }
}
