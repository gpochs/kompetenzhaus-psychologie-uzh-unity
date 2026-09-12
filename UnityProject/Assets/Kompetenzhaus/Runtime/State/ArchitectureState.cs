using System;
using System.Collections.Generic;

namespace Kompetenzhaus.State
{
    [Serializable]
    public sealed class ArchitectureState
    {
        public int schemaVersion = 1;
        public float gridSizeMetres = 4f;
        public float timeOfDay = 14f;
        public string weather = "clear";
        public List<HouseDesign> houses = new List<HouseDesign>();

        public static ArchitectureState CreateDefault() => new ArchitectureState
        {
            houses = new List<HouseDesign>
            {
                new HouseDesign { houseId = "bsc", theme = "warm-timber" },
                new HouseDesign { houseId = "msc", theme = "light-mineral" }
            }
        };
    }

    [Serializable]
    public sealed class HouseDesign
    {
        public string houseId;
        public string theme = "warm-timber";
        public string facadeStyle = "open-bays";
        public string roofStyle = "flat";
        public string starterPreset = "custom";
        public int floors = 1;
        public string wallColor = "#E9E2D2";
        public string accentColor = "#247B80";
        public List<GridTile> footprintTiles = new List<GridTile>();
        public List<GridTile> courtyardCutouts = new List<GridTile>();
        public List<RoomDesign> rooms = new List<RoomDesign>();
        public List<RoomConnection> connections = new List<RoomConnection>();
        public List<ModulePlacement> modulePlacements = new List<ModulePlacement>();
        public List<DecorationPlacement> decorations = new List<DecorationPlacement>();
    }

    [Serializable]
    public sealed class GridTile
    {
        public int x, z, floor;
        public GridTile() { }
        public GridTile(int x, int z, int floor = 0) { this.x = x; this.z = z; this.floor = floor; }
        public string Key => x + ":" + z + ":" + floor;
    }

    [Serializable]
    public sealed class RoomDesign
    {
        public string id;
        public string name;
        public string function = "learning";
        // A connected tile polygon supports rectangles, L/U forms and user-drawn room shapes.
        public List<GridTile> tiles = new List<GridTile>();
    }

    [Serializable]
    public sealed class RoomConnection
    {
        public string id;
        public string fromRoomId;
        public string toRoomId;
        public string kind = "door";
        public GridTile fromTile;
        public GridTile toTile;
    }

    [Serializable]
    public sealed class ModulePlacement
    {
        public string moduleId;
        public string roomId;
        public int gridX, gridZ, floor;
        public int width = 1, depth = 1;
        public int quarterTurns;
    }

    [Serializable]
    public sealed class DecorationPlacement
    {
        public string instanceId;
        public string cosmeticId;
        public string roomId;
        public float x, z;
        public int floor;
        public float rotationDegrees;
        public float scale = 1f;
    }
}
