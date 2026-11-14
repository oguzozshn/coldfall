using Coldfall.Sim;
using NUnit.Framework;

namespace Coldfall.Tests
{
    /// <summary>
    /// Guards the thermal volume model of docs/09-technical-architecture.md, 9.5 and the
    /// shelter behaviour it has to produce for docs/03, 3.8.
    /// </summary>
    [TestFixture]
    public sealed class ThermalDiffusionTests
    {
        private const int Outdoors = 0;
        private const int Room = 1;

        private static ThermalVolume[] RoomAndOutdoors(
            float roomCelsius = 18f, float insulation = 0.8f, float cubicMetres = 40f,
            float heatInput = 0f)
        {
            return new[]
            {
                new ThermalVolume
                {
                    IsOutdoors = true, WindExposure = 1f, CubicMetres = 100000f,
                },
                new ThermalVolume
                {
                    Celsius = roomCelsius,
                    Insulation = insulation,
                    CubicMetres = cubicMetres,
                    HeatInput = heatInput,
                },
            };
        }

        private static Portal[] OnePortal(PortalState state, float area = 2f)
        {
            return new[]
            {
                new Portal { VolumeA = Room, VolumeB = Outdoors, State = state, AreaSquareMetres = area },
            };
        }

        private static ThermalVolume[] Run(
            ThermalVolume[] volumes, Portal[] portals, float outdoorCelsius, int minutes)
        {
            for (var i = 0; i < minutes; i++)
            {
                ThermalDiffusion.Step(volumes, portals, outdoorCelsius);
            }

            return volumes;
        }

        [Test]
        public void AnOpenDoorToABlizzardCoolsTheRoomInMinutes()
        {
            var volumes = Run(RoomAndOutdoors(), OnePortal(PortalState.Open), -20f, minutes: 10);

            Assert.That(volumes[Room].Celsius, Is.LessThan(5f),
                "docs/09, 9.5: a believable handful of minutes");
            Assert.That(volumes[Room].Celsius, Is.GreaterThan(-20f));
        }

        [Test]
        public void AClosedDoorHoldsTheHeatFarLonger()
        {
            var open = Run(RoomAndOutdoors(), OnePortal(PortalState.Open), -20f, 10);
            var closed = Run(RoomAndOutdoors(), OnePortal(PortalState.Closed), -20f, 10);

            Assert.That(closed[Room].Celsius, Is.GreaterThan(open[Room].Celsius + 10f));
        }

        [Test]
        public void BoardingABrokenWindowWarmsTheRoom()
        {
            var broken = Run(RoomAndOutdoors(), OnePortal(PortalState.Broken), -10f, 30);
            var boarded = Run(RoomAndOutdoors(), OnePortal(PortalState.Boarded), -10f, 30);

            Assert.That(boarded[Room].Celsius, Is.GreaterThan(broken[Room].Celsius),
                "the gameplay verb in docs/04, 4.9 must move the temperature in docs/03, 3.8");
        }

        [Test]
        public void ThreeBrokenWindowsMeanTheBuildingIsNotShelter()
        {
            var one = RoomAndOutdoors();
            var three = RoomAndOutdoors();

            var onePortal = OnePortal(PortalState.Broken, area: 1f);
            var threePortals = new[]
            {
                new Portal { VolumeA = Room, VolumeB = Outdoors, State = PortalState.Broken, AreaSquareMetres = 1f },
                new Portal { VolumeA = Room, VolumeB = Outdoors, State = PortalState.Broken, AreaSquareMetres = 1f },
                new Portal { VolumeA = Room, VolumeB = Outdoors, State = PortalState.Broken, AreaSquareMetres = 1f },
            };

            Run(one, onePortal, -10f, 20);
            Run(three, threePortals, -10f, 20);

            Assert.That(three[Room].Celsius, Is.LessThan(one[Room].Celsius));
        }

        [Test]
        public void AStoveHeatsASmallRoomFarFasterThanAChurch()
        {
            var room = RoomAndOutdoors(roomCelsius: 0f, cubicMetres: 40f, heatInput: 60f);
            var church = RoomAndOutdoors(roomCelsius: 0f, cubicMetres: 400f, heatInput: 60f);

            Run(room, OnePortal(PortalState.Closed), -5f, 30);
            Run(church, OnePortal(PortalState.Closed), -5f, 30);

            Assert.That(room[Room].Celsius, Is.GreaterThan(church[Room].Celsius + 5f),
                "docs/05, 5.4: the church is defensible and impossible to heat");
        }

        [Test]
        public void TheOutdoorsIsDrivenByWeatherNotByRoomsVentingIntoIt()
        {
            var volumes = RoomAndOutdoors(roomCelsius: 40f, heatInput: 500f);

            Run(volumes, OnePortal(PortalState.Open), -15f, 60);

            Assert.That(volumes[Outdoors].Celsius, Is.EqualTo(-15f).Within(1e-3f),
                "a house full of fires does not warm a valley");
        }

        [Test]
        public void ARoomNeverCoolsBelowTheOutsideAirOnItsOwn()
        {
            var volumes = Run(RoomAndOutdoors(roomCelsius: 18f), OnePortal(PortalState.Broken), -25f, 600);

            Assert.That(volumes[Room].Celsius, Is.GreaterThanOrEqualTo(-25.01f));
        }

        [Test]
        public void InsulationSlowsTheLossWithoutStoppingIt()
        {
            var leaky = Run(RoomAndOutdoors(insulation: 0.1f), System.Array.Empty<Portal>(), -10f, 120);
            var tight = Run(RoomAndOutdoors(insulation: 0.9f), System.Array.Empty<Portal>(), -10f, 120);

            Assert.That(tight[Room].Celsius, Is.GreaterThan(leaky[Room].Celsius));
            Assert.That(tight[Room].Celsius, Is.LessThan(18f), "no room holds heat forever");
        }

        [Test]
        public void WindStripsHeatFromABuilding()
        {
            var sheltered = RoomAndOutdoors();
            var exposed = RoomAndOutdoors();
            exposed[Room].WindExposure = 1f;

            Run(sheltered, System.Array.Empty<Portal>(), -10f, 120);
            Run(exposed, System.Array.Empty<Portal>(), -10f, 120);

            Assert.That(exposed[Room].Celsius, Is.LessThan(sheltered[Room].Celsius));
        }

        [Test]
        public void ASmallRoomWithAHugeOpeningStaysStable()
        {
            // The case that makes Jacobi diverge if the coefficient is not clamped: tiny
            // thermal mass, enormous conductance.
            var volumes = RoomAndOutdoors(roomCelsius: 20f, cubicMetres: 2f);
            var portals = OnePortal(PortalState.Broken, area: 40f);

            Run(volumes, portals, -30f, 2000);

            Assert.That(float.IsNaN(volumes[Room].Celsius), Is.False);
            Assert.That(volumes[Room].Celsius, Is.InRange(-30.01f, 20.01f));
        }

        [Test]
        public void IdenticalInputsProduceIdenticalResults()
        {
            var first = Run(RoomAndOutdoors(), OnePortal(PortalState.Open), -12f, 100);
            var second = Run(RoomAndOutdoors(), OnePortal(PortalState.Open), -12f, 100);

            Assert.That(first[Room].Celsius, Is.EqualTo(second[Room].Celsius));
        }

        [Test]
        public void HeatFlowsBetweenTwoRoomsThroughAnOpenDoor()
        {
            var volumes = new[]
            {
                new ThermalVolume { IsOutdoors = true, CubicMetres = 100000f, WindExposure = 1f },
                new ThermalVolume { Celsius = 20f, Insulation = 0.9f, CubicMetres = 40f },
                new ThermalVolume { Celsius = 0f, Insulation = 0.9f, CubicMetres = 40f },
            };

            var portals = new[]
            {
                new Portal { VolumeA = 1, VolumeB = 2, State = PortalState.Open, AreaSquareMetres = 2f },
            };

            Run(volumes, portals, -5f, 30);

            Assert.That(volumes[2].Celsius, Is.GreaterThan(2f), "the cold room should have warmed");
            Assert.That(volumes[1].Celsius, Is.LessThan(18f), "the warm room should have paid for it");
        }
    }
}
