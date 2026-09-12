using System;
using System.Collections.Generic;
using System.Linq;

namespace Kompetenzhaus.State
{
    // Shared by validation and rendering: painted floor is not an enclosing room.
    public sealed class ArchitectureCirculationPlan
    {
        private readonly HouseDesign house;
        private readonly Dictionary<string, string> rooms = new(StringComparer.Ordinal);
        private readonly HashSet<string> doors = new(StringComparer.Ordinal);
        private readonly HashSet<string> entrances = new(StringComparer.Ordinal);
        private static readonly (int x, int z, int yaw)[] Directions =
            { (0, -1, 0), (1, 0, 270), (0, 1, 180), (-1, 0, 90) };

        public ArchitectureCirculationPlan(HouseDesign design)
        {
            house = design;
            foreach (var room in house.rooms) foreach (var tile in room.tiles) rooms[tile.Key] = room.id;
            foreach (var connection in house.connections.Where(c => c.kind == "door"))
                doors.Add(EdgeKey(connection.fromTile, connection.toTile));
            foreach (var room in house.rooms.Where(r => r.tiles.Count > 0 && r.tiles[0].floor == 0))
            {
                var found = false;
                foreach (var tile in room.tiles.OrderBy(t => t.z).ThenBy(t => t.x))
                {
                    foreach (var direction in Directions)
                    {
                        var neighbour = new GridTile(tile.x + direction.x, tile.z + direction.z, tile.floor);
                        if (rooms.ContainsKey(neighbour.Key)) continue;
                        entrances.Add(EdgeKey(tile, neighbour)); found = true; break;
                    }
                    if (found) break;
                }
            }
        }

        public bool HasRoom(GridTile tile) => rooms.ContainsKey(tile.Key);

        public string BoundaryModel(GridTile tile, GridTile neighbour)
        {
            if (!rooms.TryGetValue(tile.Key, out var room)) return null;
            var edge = EdgeKey(tile, neighbour);
            if (rooms.TryGetValue(neighbour.Key, out var adjacentRoom))
            {
                if (room == adjacentRoom || string.CompareOrdinal(tile.Key, neighbour.Key) > 0) return null;
                return doors.Contains(edge) ? "wall-partition" : "wall-solid";
            }
            return entrances.Contains(edge) ? "wall-door" : house.facadeStyle == "solid" ? "wall-solid" : "wall-window";
        }

        public bool TryStairYaw(RoomConnection stair, out int yaw)
        {
            var lower = stair.fromTile.floor < stair.toTile.floor ? stair.fromTile : stair.toTile;
            rooms.TryGetValue(lower.Key, out var room);
            // Prefer walking within the lower room; then use its explicit door or outdoor entrance.
            foreach (var insideOnly in new[] { true, false }) foreach (var direction in Directions)
            {
                var neighbour = new GridTile(lower.x + direction.x, lower.z + direction.z, lower.floor);
                var sameRoom = rooms.TryGetValue(neighbour.Key, out var adjacentRoom) && room == adjacentRoom;
                var edge = EdgeKey(lower, neighbour);
                if (sameRoom || (!insideOnly && (doors.Contains(edge) || entrances.Contains(edge))))
                { yaw = direction.yaw; return true; }
            }
            yaw = 0;
            return false;
        }

        public static string EdgeKey(GridTile a, GridTile b) => string.CompareOrdinal(a.Key, b.Key) < 0
            ? a.Key + "|" + b.Key : b.Key + "|" + a.Key;
    }
}
