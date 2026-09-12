using Kompetenzhaus.Content;
using Kompetenzhaus.State;
using NUnit.Framework;
using UnityEngine;

namespace Kompetenzhaus.Tests
{
    public sealed class ArchitectureTransactionsTests
    {
        [Test]
        public void OverlappingMoveIsRejectedWithoutChangingStateOrUndoHistory()
        {
            var service = Create(out var progression, out _);
            AddFloor(service);
            Assert.That(service.TryMoveModule("a", "bsc", 0, 0, 0, 0, out _), Is.True);
            Assert.That(service.TryMoveModule("b", "bsc", 1, 0, 0, 0, out _), Is.True);
            var before = JsonUtility.ToJson(service.State);
            Assert.That(service.TryMoveModule("b", "bsc", 0, 0, 0, 0, out _), Is.False);
            Assert.That(JsonUtility.ToJson(service.State), Is.EqualTo(before));
            Assert.That(service.Undo(out _), Is.True);
            Assert.That(service.State.houses[0].modulePlacements.Exists(p => p.moduleId == "b"), Is.False);
            Assert.That(progression.Data.placedModuleIds, Does.Contain("b"));
        }

        [Test]
        public void UpperBayWithoutSupportIsRejectedAtomically()
        {
            var service = Create(out _, out _);
            var before = JsonUtility.ToJson(service.State);
            Assert.That(service.TryCommit(state =>
            {
                state.houses[0].floors = 2;
                state.houses[0].footprintTiles.Add(new GridTile(0, 0, 1));
            }, out _), Is.False);
            Assert.That(JsonUtility.ToJson(service.State), Is.EqualTo(before));
            Assert.That(service.CanUndo, Is.False);
        }

        [Test]
        public void UnsupportedGridScaleIsRejectedBeforeRendering()
        {
            var service = Create(out _, out _);
            var before = JsonUtility.ToJson(service.State);
            Assert.That(service.TryCommit(state => state.gridSizeMetres = 1f, out _), Is.False);
            Assert.That(JsonUtility.ToJson(service.State), Is.EqualTo(before));
            Assert.That(service.CanUndo, Is.False);
        }

        [Test]
        public void ArchitectureUndoRetainsLearningProgressAndRewards()
        {
            var service = Create(out var progression, out _);
            AddFloor(service);
            service.TryMoveModule("a", "bsc", 0, 0, 0, 0, out _);
            service.TryMoveModule("a", "bsc", 1, 0, 0, 1, out _);
            progression.Data.completedQuestIds.Add("learning-goal");
            progression.Data.earnedExperience = 40;
            progression.Data.unlockedCosmeticIds.Add("archive-lamp");
            Assert.That(service.Undo(out _), Is.True);
            Assert.That(service.State.houses[0].modulePlacements[0].gridX, Is.Zero);
            Assert.That(progression.Data.completedQuestIds, Does.Contain("learning-goal"));
            Assert.That(progression.Data.earnedExperience, Is.EqualTo(40));
            Assert.That(progression.Data.unlockedCosmeticIds, Does.Contain("archive-lamp"));
            Assert.That(progression.Data.placedModuleIds, Does.Contain("a"));
        }

        [Test]
        public void CorruptNestedSaveIsRejectedBeforeActivation()
        {
            Create(out var progression, out var catalog);
            progression.Data.architecture.houses.Clear();
            var savedBytes = JsonUtility.ToJson(progression.Data);
            Assert.That(ProgressStore.TryParseValidated(savedBytes, catalog, out var loaded, out _), Is.False);
            Assert.That(loaded, Is.Null);
            Assert.That(savedBytes, Does.Contain("\"houses\":[]"));
        }

        [Test]
        public void SandboxBuildIgnoresAcademicPrerequisites()
        {
            var prerequisite = new ModuleDefinition { id = "a", houseId = "bsc" };
            var module = new ModuleDefinition { id = "b", houseId = "bsc", prerequisiteIds = new[] { "a" } };
            var catalog = new ContentCatalog(new ContentDocument { schemaVersion = 1, modules = new[] { prerequisite, module } });
            var progression = new ProgressionService(catalog, new ProgressData(), () => { });
            Assert.That(progression.TryPlace("b", out _), Is.True);
        }

        [Test]
        public void BuildAndPositionCommitTogetherAndUnplacedInventoryCanBePositionedAgain()
        {
            var service = Create(out var progression, out _);
            progression.Data.placedModuleIds.Clear();
            Assert.That(service.TryBuildModuleInFreeBay("a", out _), Is.False);
            Assert.That(progression.Data.placedModuleIds, Is.Empty);
            AddFloor(service);
            Assert.That(service.TryBuildModuleInFreeBay("a", out _), Is.True);
            Assert.That(progression.Data.placedModuleIds, Does.Contain("a"));
            Assert.That(service.IsPositioned("a"), Is.True);
            Assert.That(service.Undo(out _), Is.True);
            Assert.That(progression.Data.placedModuleIds, Does.Contain("a"));
            Assert.That(service.IsPositioned("a"), Is.False);
            Assert.That(service.TryBuildModuleInFreeBay("a", out _), Is.True);
            Assert.That(service.IsPositioned("a"), Is.True);
        }

        [Test]
        public void StairBaysRejectOffsetConnectionsAndFurniture()
        {
            var service = Create(out _, out _);
            AddStair(service);
            Assert.That(service.TryMoveModule("a", "bsc", 0, 0, 0, 0, out _), Is.False);
            Assert.That(service.TryMoveModule("a", "bsc", 1, 0, 0, 0, out _), Is.True);
            var before = JsonUtility.ToJson(service.State);
            Assert.That(service.TryCommit(state => state.houses[0].decorations.Add(new DecorationPlacement
            {
                instanceId = "blocking-plant", cosmeticId = "plant", x = 2f, z = 2f, floor = 1
            }), out _), Is.False);
            Assert.That(JsonUtility.ToJson(service.State), Is.EqualTo(before));
            Assert.That(service.TryCommit(state => state.houses[0].connections[0].toTile.x = 1, out var error), Is.False);
            Assert.That(error, Does.Contain("same bay"));
        }

        [Test]
        public void StackedStairFlightsCannotShareLandingBay()
        {
            var service = Create(out _, out _);
            AddStair(service);
            var before = JsonUtility.ToJson(service.State);
            Assert.That(service.TryCommit(state =>
            {
                var house = state.houses[0];
                house.floors = 3;
                house.footprintTiles.Add(new GridTile(0, 0, 2));
                house.rooms.Add(new RoomDesign { id = "third", tiles = new System.Collections.Generic.List<GridTile> { new GridTile(0, 0, 2) } });
                house.connections.Add(new RoomConnection
                {
                    id = "second-flight", kind = "stair", fromRoomId = "upper", toRoomId = "third",
                    fromTile = new GridTile(0, 0, 1), toTile = new GridTile(0, 0, 2)
                });
            }, out _), Is.False);
            Assert.That(JsonUtility.ToJson(service.State), Is.EqualTo(before));
        }

        private static void AddStair(ArchitectureService service)
        {
            Assert.That(service.TryCommit(state =>
            {
                var house = state.houses[0]; house.floors = 2;
                foreach (var floor in new[] { 0, 1 })
                {
                    var room = new RoomDesign { id = floor == 0 ? "lower" : "upper" };
                    foreach (var x in new[] { 0, 1 })
                    {
                        house.footprintTiles.Add(new GridTile(x, 0, floor));
                        room.tiles.Add(new GridTile(x, 0, floor));
                    }
                    house.rooms.Add(room);
                }
                house.connections.Add(new RoomConnection
                {
                    id = "stair", kind = "stair", fromRoomId = "lower", toRoomId = "upper",
                    fromTile = new GridTile(0, 0), toTile = new GridTile(0, 0, 1)
                });
            }, out var error), Is.True, error);
        }

        private static ArchitectureService Create(out ProgressionService progression, out ContentCatalog catalog)
        {
            catalog = new ContentCatalog(new ContentDocument
            {
                schemaVersion = 1,
                modules = new[] { new ModuleDefinition { id = "a", houseId = "bsc" }, new ModuleDefinition { id = "b", houseId = "bsc" } }
            });
            var data = new ProgressData();
            data.placedModuleIds.AddRange(new[] { "a", "b" });
            progression = new ProgressionService(catalog, data, () => { });
            return new ArchitectureService(catalog, progression);
        }

        private static void AddFloor(ArchitectureService service)
        {
            Assert.That(service.TryCommit(state =>
            {
                state.houses[0].footprintTiles.Add(new GridTile(0, 0));
                state.houses[0].footprintTiles.Add(new GridTile(1, 0));
            }, out _), Is.True);
        }
    }
}
