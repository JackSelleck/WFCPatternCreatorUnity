using UnityEngine;

namespace WFCPatternCreator
{ 
    public class CellData
    {
        // Done when the CellDatas tile is instantiated
        public bool collapsed;
    
        // The array of possible tile options
        public Tile[] tileOptions;
    
        // Each cell data has its own tile
        public Tile chosenTile;
    
        // Determines which tile zone the tiles will adhere to
        public ZoneType zone;
    
        // Determines where the tile will go
        // Vector3Int is a little more performant than a regular vector3 and creating a rigid grid is a perfect use case for it
        public Vector3Int position; 
    }
}