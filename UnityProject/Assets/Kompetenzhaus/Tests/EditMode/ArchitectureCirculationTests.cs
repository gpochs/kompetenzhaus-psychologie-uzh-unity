using System.Collections.Generic;
using Kompetenzhaus.Content;
using Kompetenzhaus.State;
using NUnit.Framework;
using UnityEngine;

namespace Kompetenzhaus.Tests
{
    public sealed class ArchitectureCirculationTests
    {
        [Test]
        public void PaintingFloorAroundARoomKeepsItsEntranceAndCreatesNoNewEnclosure()
        {
            var house = ArchitectureState.CreateDefault().houses[0];
            var tile = new GridTile(0, 0);
            house.rooms.Add(new RoomDesign { id = "room", tiles = new List<GridTile> { tile } });
            house.footprintTiles.Add(tile);
            var south = new GridTile(0, -1);
            Assert.That(new ArchitectureCirculationPlan(house).BoundaryModel(tile, south), Is.EqualTo("wall-door"));
            for (var x = -1; x <= 1; x++) for (var z = -1; z <= 1; z++)
                if (x != 0 || z != 0) house.footprintTiles.Add(new GridTile(x, z));
            var expanded = new ArchitectureCirculationPlan(house);
            Assert.That(expanded.BoundaryModel(tile, south), Is.EqualTo("wall-door"));
            Assert.That(expanded.BoundaryModel(south, tile), Is.Null);
            Assert.That(expanded.BoundaryModel(south, new GridTile(0, -2)), Is.Null);
            Assert.That(expanded.HasRoom(south), Is.False, "Unassigned floor must not receive a room roof.");
        }

        [TestCase(-1, 0, 90)]
        [TestCase(1, 0, 270)]
        [TestCase(0, -1, 0)]
        [TestCase(0, 1, 180)]
        public void StairFootFacesTheNeighbouringLowerRoomBay(int x, int z, int expectedYaw)
        {
            var house = ArchitectureState.CreateDefault().houses[0];
            var lower = new GridTile(0, 0);
            var upper = new GridTile(0, 0, 1);
            house.rooms.Add(new RoomDesign { id = "lower", tiles = new List<GridTile> { lower, new GridTile(x, z) } });
            house.rooms.Add(new RoomDesign { id = "upper", tiles = new List<GridTile> { upper } });
            var connection = new RoomConnection { kind = "stair", fromTile = lower, toTile = upper };
            Assert.That(new ArchitectureCirculationPlan(house).TryStairYaw(connection, out var yaw), Is.True);
            Assert.That(yaw, Is.EqualTo(expectedYaw));
            var worldEntry = Quaternion.Euler(0, yaw, 0) * Vector3.back;
            Assert.That(Vector3.Distance(worldEntry, new Vector3(x, 0, z)), Is.LessThan(0.001f));
        }

        [Test]
        public void GroundFloorSingleBayStairCanUseTheOutdoorEntrance()
        {
            var house = ArchitectureState.CreateDefault().houses[0];
            var lower = new GridTile(0, 0);
            var upper = new GridTile(0, 0, 1);
            house.rooms.Add(new RoomDesign { id = "lower", tiles = new List<GridTile> { lower } });
            house.rooms.Add(new RoomDesign { id = "upper", tiles = new List<GridTile> { upper } });
            Assert.That(new ArchitectureCirculationPlan(house).TryStairYaw(new RoomConnection { fromTile = lower, toTile = upper }, out var yaw), Is.True);
            Assert.That(yaw, Is.Zero);
        }

        [Test]
        public void StairWithNoLowerEntryCannotBecomeDurableArchitecture()
        {
            var data = new ProgressData();
            var catalog = new ContentCatalog(new ContentDocument { schemaVersion = 1,
                modules = new[] { new ModuleDefinition { id = "foundation", houseId = "bsc" } } });
            var service = new ArchitectureService(catalog, new ProgressionService(catalog, data, () => { }));
            var before = JsonUtility.ToJson(service.State);
            Assert.That(service.TryCommit(state =>
            {
                var house = state.houses[0]; house.floors = 3;
                for (var floor = 0; floor < 3; floor++) house.footprintTiles.Add(new GridTile(0, 0, floor));
                house.rooms.Add(new RoomDesign { id = "lower", tiles = new List<GridTile> { new GridTile(0, 0, 1) } });
                house.rooms.Add(new RoomDesign { id = "upper", tiles = new List<GridTile> { new GridTile(0, 0, 2) } });
                house.connections.Add(new RoomConnection { id = "stairs", kind = "stair", fromRoomId = "lower", toRoomId = "upper", fromTile = new GridTile(0, 0, 1), toTile = new GridTile(0, 0, 2) });
            }, out var error), Is.False);
            Assert.That(error, Does.Contain("accessible lower entry"));
            Assert.That(JsonUtility.ToJson(service.State), Is.EqualTo(before));
            Assert.That(service.CanUndo, Is.False);
        }
    }
}
