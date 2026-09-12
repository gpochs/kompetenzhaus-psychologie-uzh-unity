using System;
using System.Collections.Generic;
using System.Linq;
using Kompetenzhaus.Content;
using UnityEngine;

namespace Kompetenzhaus.State
{
    // Architectural state is independent of curriculum coordinates and learning achievements.
    // Geometry consumers subscribe to Changed; only valid transactions become durable state.
    public sealed class ArchitectureService
    {
        public const int LotRadius = 4;
        public const int MaximumFloors = 4;
        public const int MaximumTilesPerHouse = 64;
        public const int MaximumDecorationsPerHouse = 128;
        public event Action Changed;
        public ArchitectureState State => progression.Data.architecture;
        public bool CanUndo => undo.Count > 0;
        private readonly ContentCatalog catalog;
        private readonly ProgressionService progression;
        private readonly List<string> undo = new List<string>();
        private readonly HashSet<string> starterCosmetics = new HashSet<string>(StringComparer.Ordinal)
            { "plant", "bench", "lamp", "noticeboard", "desk", "chair", "bookshelf" };

        public ArchitectureService(ContentCatalog catalog, ProgressionService progression)
        {
            this.catalog = catalog;
            this.progression = progression;
            progression.Data.architecture ??= ArchitectureState.CreateDefault();
        }

        public bool TryCommit(Action<ArchitectureState> edit, out string error)
        {
            var before = JsonUtility.ToJson(State);
            var candidate = JsonUtility.FromJson<ArchitectureState>(before);
            try { edit(candidate); }
            catch (Exception exception) { error = exception.Message; return false; }
            if (!Validate(candidate, out error)) return false;
            undo.Add(before);
            if (undo.Count > 50) undo.RemoveAt(0);
            progression.Data.architecture = candidate;
            progression.SaveAndNotify();
            Changed?.Invoke();
            return true;
        }

        public bool Undo(out string error)
        {
            if (undo.Count == 0) { error = "Nothing to undo."; return false; }
            var candidate = JsonUtility.FromJson<ArchitectureState>(undo[undo.Count - 1]);
            if (!Validate(candidate, out error)) return false;
            undo.RemoveAt(undo.Count - 1);
            progression.Data.architecture = candidate;
            progression.SaveAndNotify();
            Changed?.Invoke();
            return true;
        }

        public bool TryMoveModule(string moduleId, string houseId, int x, int z, int floor, int rotation,
            out string error, int width = 1, int depth = 1, string roomId = null)
        {
            return TryCommit(candidate =>
            {
                var house = candidate.houses.Single(h => h.houseId == houseId);
                foreach (var entry in candidate.houses) entry.modulePlacements.RemoveAll(p => p.moduleId == moduleId);
                house.modulePlacements.Add(new ModulePlacement
                {
                    moduleId = moduleId, roomId = roomId, gridX = x, gridZ = z, floor = floor,
                    width = width, depth = depth, quarterTurns = ((rotation % 4) + 4) % 4
                });
            }, out error);
        }

        public bool IsPositioned(string moduleId) => State.houses.Any(house => house.modulePlacements.Any(placement => placement.moduleId == moduleId));

        public bool TryBuildModuleInFreeBay(string moduleId, out string error)
        {
            var module = catalog.GetModule(moduleId);
            if (module == null) return Invalid("Unknown module.", out error);
            var eligibility = progression.GetEligibility(moduleId);
            if (!eligibility.Allowed) return Invalid("Complete the guided learning requirements before building this module.", out error);
            if (IsPositioned(moduleId)) return Invalid("This module is already positioned. Use the move tool to rearrange it.", out error);
            var before = JsonUtility.ToJson(State);
            var candidate = JsonUtility.FromJson<ArchitectureState>(before);
            var house = candidate.houses.Single(h => h.houseId == module.houseId);
            foreach (var tile in house.footprintTiles.OrderBy(tile => tile.floor).ThenBy(tile => tile.x).ThenBy(tile => tile.z))
            {
                var room = house.rooms.FirstOrDefault(room => room.tiles.Any(roomTile => roomTile.Key == tile.Key));
                if (house.rooms.Count > 0 && room == null) continue;
                var placement = new ModulePlacement { moduleId = moduleId, roomId = room?.id, gridX = tile.x, gridZ = tile.z, floor = tile.floor };
                house.modulePlacements.Add(placement);
                if (ValidateCandidate(candidate, out _, moduleId))
                {
                    undo.Add(before);
                    if (undo.Count > 50) undo.RemoveAt(0);
                    progression.CommitModulePlacement(moduleId, candidate);
                    Changed?.Invoke();
                    error = null;
                    return true;
                }
                house.modulePlacements.Remove(placement);
            }
            return Invalid("Create or enlarge a room in this degree house to make a free bay for the module.", out error);
        }

        public bool Validate(ArchitectureState candidate, out string error) => ValidateCandidate(candidate, out error, null);

        private bool ValidateCandidate(ArchitectureState candidate, out string error, string pendingModuleId)
        {
            error = null;
            if (candidate == null || candidate.schemaVersion != 1 || Mathf.Abs(candidate.gridSizeMetres - 4f) > 0.0001f ||
                !Finite(candidate.gridSizeMetres) || !Finite(candidate.timeOfDay) || candidate.timeOfDay < 0f || candidate.timeOfDay >= 24f)
                return Invalid("Invalid architecture schema, grid scale or time of day.", out error);
            if (candidate.houses == null || candidate.houses.Count != 2 ||
                !candidate.houses.Select(h => h?.houseId).OrderBy(id => id).SequenceEqual(new[] { "bsc", "msc" }))
                return Invalid("One BSc and one MSc house are required.", out error);
            var placedModules = new HashSet<string>(StringComparer.Ordinal);
            var decorations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var house in candidate.houses)
            {
                if (house.floors < 1 || house.floors > MaximumFloors || house.footprintTiles == null || house.courtyardCutouts == null ||
                    house.rooms == null || house.connections == null || house.modulePlacements == null || house.decorations == null)
                    return Invalid("Invalid floor count or missing architecture collection.", out error);
                if (house.footprintTiles.Count > MaximumTilesPerHouse) return Invalid("A house can contain at most 64 floor bays.", out error);
                if (house.decorations.Count > MaximumDecorationsPerHouse) return Invalid("A house can contain at most 128 decorations.", out error);
                if (!new[] { "warm-timber", "light-mineral", "terracotta" }.Contains(house.theme) ||
                    !new[] { "open-bays", "solid", "glass" }.Contains(house.facadeStyle) ||
                    !new[] { "flat", "gabled", "terrace" }.Contains(house.roofStyle) ||
                    !new[] { "custom", "courtyard", "linear", "pavilion-cluster", "empty" }.Contains(house.starterPreset) ||
                    !ColorUtility.TryParseHtmlString(house.wallColor, out _) || !ColorUtility.TryParseHtmlString(house.accentColor, out _))
                    return Invalid("Unknown house style or invalid color.", out error);
                var floorTiles = new HashSet<string>(StringComparer.Ordinal);
                foreach (var tile in house.footprintTiles)
                {
                    if (!ValidTile(tile, house.floors) || !floorTiles.Add(tile.Key)) return Invalid("Invalid or duplicate footprint tile.", out error);
                }
                var cuts = new HashSet<string>(StringComparer.Ordinal);
                foreach (var tile in house.courtyardCutouts)
                    if (!ValidTile(tile, house.floors) || !cuts.Add(tile.Key) || floorTiles.Contains(tile.Key))
                        return Invalid("Courtyard cutouts must be unique empty cells inside the lot bounds.", out error);
                foreach (var tile in house.footprintTiles)
                    if (tile.floor > 0 && !floorTiles.Contains(new GridTile(tile.x, tile.z, tile.floor - 1).Key))
                        return Invalid("Each upper-floor bay needs a supporting bay below it.", out error);
                var roomById = new Dictionary<string, RoomDesign>(StringComparer.Ordinal);
                var roomTiles = new HashSet<string>(StringComparer.Ordinal);
                foreach (var room in house.rooms)
                {
                    if (room == null || string.IsNullOrWhiteSpace(room.id) || !roomById.TryAdd(room.id, room) || room.tiles == null || room.tiles.Count == 0)
                        return Invalid("Rooms need unique ids and at least one tile.", out error);
                    if (!Connected(room.tiles)) return Invalid("A room must be a connected shape on one floor.", out error);
                    foreach (var tile in room.tiles)
                        if (!ValidTile(tile, house.floors) || !floorTiles.Contains(tile.Key) || !roomTiles.Add(tile.Key))
                            return Invalid("Rooms must fit inside the footprint and cannot overlap.", out error);
                }
                var connectionIds = new HashSet<string>(StringComparer.Ordinal);
                var stairTiles = new HashSet<string>(StringComparer.Ordinal);
                foreach (var connection in house.connections)
                {
                    if (connection == null || string.IsNullOrWhiteSpace(connection.id) || !connectionIds.Add(connection.id) ||
                        (connection.kind != "door" && connection.kind != "stair") ||
                        !roomById.TryGetValue(connection.fromRoomId ?? "", out var from) || !roomById.TryGetValue(connection.toRoomId ?? "", out var to) ||
                        connection.fromRoomId == connection.toRoomId || connection.fromTile == null || connection.toTile == null ||
                        !from.tiles.Any(t => t.Key == connection.fromTile.Key) || !to.tiles.Any(t => t.Key == connection.toTile.Key))
                        return Invalid("Connections need two existing rooms and endpoints inside those rooms.", out error);
                    var distance = Math.Abs(connection.fromTile.x - connection.toTile.x) + Math.Abs(connection.fromTile.z - connection.toTile.z);
                    var floorDistance = Math.Abs(connection.fromTile.floor - connection.toTile.floor);
                    if (connection.kind == "stair" ? floorDistance != 1 || distance != 0 : floorDistance != 0 || distance != 1)
                        return Invalid("Doors connect adjacent bays on one floor; stairs connect the same bay on adjacent floors.", out error);
                    if (connection.kind == "stair" && (!stairTiles.Add(connection.fromTile.Key) || !stairTiles.Add(connection.toTile.Key)))
                        return Invalid("Each stair needs its own clear bay and landing; stair flights cannot share an endpoint bay.", out error);
                }
                var circulation = new ArchitectureCirculationPlan(house);
                foreach (var stair in house.connections.Where(c => c.kind == "stair"))
                    if (!circulation.TryStairYaw(stair, out _))
                        return Invalid("A staircase needs an accessible lower entry: enlarge its room or add a doorway first.", out error);
                var occupied = new HashSet<string>(StringComparer.Ordinal);
                foreach (var placement in house.modulePlacements)
                {
                    var module = placement == null ? null : catalog.GetModule(placement.moduleId);
                    if (module == null || module.houseId != house.houseId || !placedModules.Add(placement.moduleId) ||
                        !(progression.Data.placedModuleIds.Contains(placement.moduleId) || placement.moduleId == pendingModuleId))
                        return Invalid("Place built modules once inside their degree house.", out error);
                    if (placement.width < 1 || placement.depth < 1 || placement.width > 8 || placement.depth > 8 || placement.quarterTurns < 0 || placement.quarterTurns > 3)
                        return Invalid("Invalid module dimensions or rotation.", out error);
                    var width = placement.quarterTurns % 2 == 0 ? placement.width : placement.depth;
                    var depth = placement.quarterTurns % 2 == 0 ? placement.depth : placement.width;
                    for (var x = 0; x < width; x++) for (var z = 0; z < depth; z++)
                    {
                        var tile = new GridTile(placement.gridX + x, placement.gridZ + z, placement.floor);
                        if (!ValidTile(tile, house.floors) || !floorTiles.Contains(tile.Key) || !occupied.Add(tile.Key))
                            return Invalid("Modules must fit inside floor bays and cannot overlap.", out error);
                        if (stairTiles.Contains(tile.Key)) return Invalid("Keep stair and landing bays clear of module furniture.", out error);
                        if (!string.IsNullOrEmpty(placement.roomId) &&
                            (!roomById.TryGetValue(placement.roomId, out var room) || !room.tiles.Any(t => t.Key == tile.Key)))
                            return Invalid("Module footprint must fit inside its selected room.", out error);
                    }
                }
                foreach (var decoration in house.decorations)
                {
                    if (decoration == null || string.IsNullOrWhiteSpace(decoration.instanceId) || !decorations.Add(decoration.instanceId) ||
                        string.IsNullOrWhiteSpace(decoration.cosmeticId) ||
                        !(starterCosmetics.Contains(decoration.cosmeticId) || progression.Data.unlockedCosmeticIds.Contains(decoration.cosmeticId)))
                        return Invalid("Decoration must be in the starter or earned inventory and have a unique instance id.", out error);
                    if (!Finite(decoration.x) || !Finite(decoration.z) || !Finite(decoration.rotationDegrees) || !Finite(decoration.scale) ||
                        decoration.scale < 0.25f || decoration.scale > 3f || decoration.rotationDegrees < 0f || decoration.rotationDegrees >= 360f)
                        return Invalid("Invalid decoration transform.", out error);
                    var tile = new GridTile(Mathf.FloorToInt(decoration.x / candidate.gridSizeMetres), Mathf.FloorToInt(decoration.z / candidate.gridSizeMetres), decoration.floor);
                    if (!ValidTile(tile, house.floors) || !floorTiles.Contains(tile.Key)) return Invalid("Place decoration inside an existing floor bay.", out error);
                    if (stairTiles.Contains(tile.Key)) return Invalid("Keep stair and landing bays clear of decoration.", out error);
                    if (!string.IsNullOrEmpty(decoration.roomId) &&
                        (!roomById.TryGetValue(decoration.roomId, out var room) || !room.tiles.Any(t => t.Key == tile.Key)))
                        return Invalid("Decoration must be inside its selected room.", out error);
                }
            }
            return true;
        }

        private static bool Connected(List<GridTile> tiles)
        {
            if (tiles.Any(t => t == null || t.floor != tiles[0].floor)) return false;
            var remaining = new HashSet<string>(tiles.Select(t => t.Key), StringComparer.Ordinal);
            var queue = new Queue<GridTile>();
            queue.Enqueue(tiles[0]); remaining.Remove(tiles[0].Key);
            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                foreach (var next in new[] { new GridTile(tile.x + 1, tile.z, tile.floor), new GridTile(tile.x - 1, tile.z, tile.floor),
                    new GridTile(tile.x, tile.z + 1, tile.floor), new GridTile(tile.x, tile.z - 1, tile.floor) })
                    if (remaining.Remove(next.Key)) queue.Enqueue(next);
            }
            return remaining.Count == 0;
        }

        private static bool ValidTile(GridTile tile, int floors) => tile != null && tile.x >= -LotRadius && tile.x < LotRadius &&
            tile.z >= -LotRadius && tile.z < LotRadius && tile.floor >= 0 && tile.floor < floors;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Invalid(string message, out string error) { error = message; return false; }
    }
}
