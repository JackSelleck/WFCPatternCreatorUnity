#if DOTWEEN
using DG.Tweening;
using System;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WFCPatternCreator
{
    /// <summary>
    /// Map generation Logic
    /// I left detailed comments here to hopefully help anyone learn or debug
    /// </summary>
    public class WaveFunctionCollapse : MonoBehaviour
    {
        [Tooltip("Dimensions of the to be generated map")]
        [SerializeField] private int width = 0;
        [SerializeField] private int depth = 0;
        [SerializeField] private int height = 0;

        [Header("Tiles")]
        [Tooltip("Assigned tiles to use")]
        public Tile[] tileObjects;
        [Tooltip("List of all generated cells in the map, no need to add anything here")]
        public List<CellData> gridComponents;

        [Tooltip("Maximum restart attempts before giving up")]
        public int maxRestartAttempts = 10;
        private int restartAttempts = 0;

        [Header("Mesh Combining")]
        [Tooltip("Combine all tiles into a single mesh after generation, (highly reccomended to keep on for performance)")]
        [SerializeField] private bool combineMeshes = true;
        [Tooltip("Gives the combined mesh a collider")]
        [SerializeField] private bool giveCombinedMeshACollider = true;
        [Tooltip("Keep original tile GameObjects after combining (debug option incase you want to check for flaws in the mesh combining)")]
        [SerializeField] private bool keepOriginalTiles = false;
        [Tooltip("Name for the combined mesh GameObject")]
        [SerializeField] private string combinedMeshName = "Map";
        [Tooltip("Delay before combining for safety")]
        private float combineDelay = 2.67f;
        [Tooltip("List of spawned tiles for mesh combiner")]
        private List<GameObject> spawnedTiles = new();

        [Tooltip("Counts till the amount of collapsed tiles matches the dimensions")]
        private int iteration;

        [Tooltip("Reference to tiles via integers (runs faster)")]
        private Dictionary<Tile, int> tileToIndex;

        [Tooltip("Propagation queue for efficient constraint propagation")]
        private Queue<int> propagationQueue;

        [Tooltip("Optional Zone Template, Find in ZoneTemplates folder")]
        private IZoneTemplateManager zoneTemplate;

        private void Awake()
        {
            tileToIndex = new Dictionary<Tile, int>();
            propagationQueue = new Queue<int>();

            var components = GetComponents<MonoBehaviour>();
            foreach (var c in components)
            {
                if (c is IZoneTemplateManager manager)
                {
                    zoneTemplate = manager;
                    break;
                }
            }
        }

        private void Start()
        {
#if DOTWEEN
            DOTween.SetTweensCapacity(1067, 1);
#endif
            if (zoneTemplate != null)
            {
                Debug.Log($"Using Zone Template: {zoneTemplate}");
            }

            InitializeGrid();
        }

        /// <summary>
        /// Create the grid of cells for tiles to be spawned on
        /// </summary>
        void InitializeGrid()
        {
            gridComponents = new List<CellData>();
            spawnedTiles.Clear();
            iteration = 0;

            // Check if there are tiles assigned
            if (tileObjects == null || tileObjects.Length == 0)
            {
                Debug.LogError("No tile objects assigned! Please assign tiles into the inspector from the project window.");
                return;
            }

            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        ZoneType cellZone = ZoneType.General;

                        if (zoneTemplate != null)
                        {
                            cellZone = DetermineZone(x, y, z);
                        }

                        // Skip this cell if zone is SpawnNothing
                        // Using nothing zones is reccomended over just using empty tiles for performance
                        if (cellZone == ZoneType.SpawnNothing)
                        {
                            // Add null placeholder to maintain grid
                            gridComponents.Add(null);
                            continue;
                        }

                        // Filter tile zone options
                        var allowed = new List<Tile>();
                        foreach (var tile in tileObjects)
                        {
                            if (tile.allowedZone == cellZone)
                                allowed.Add(tile);
                        }

                        // if no tiles match the zone, use all tiles as a fallback
                        if (allowed.Count == 0)
                        {
                            //  Debug.LogWarning($"No tiles found for zone {cellZone} at ({x}, {y}, {z}). Using all tiles as fallback, " +
                            //      $"{"\n"} if you are doing this intentionally you can remove the debug warning for a little extra performance.");
                            foreach (var tile in tileObjects)
                            {
                                allowed.Add(tile);
                            }
                        }

                        // Create and initialize CellData
                        // A new cell data is created per x,y,z
                        // And is used to manage the tile and its position
                        CellData data = new()
                        {
                            collapsed = false,
                            tileOptions = allowed.ToArray(),
                            zone = cellZone,
                            // Note the cell data is not a gameObject 
                            // So an artifical position is created for each one based on every grid position
                            // To later tell its tile where to instantiate
                            position = new Vector3Int(x, y, z)
                        };

                        gridComponents.Add(data);
                    }
                }
            }

            // Create index dictionary for fast lookup
            tileToIndex.Clear();
            for (int i = 0; i < tileObjects.Length; i++)
            {
                tileToIndex[tileObjects[i]] = i;
            }

            Debug.Log($"Grid initialized: {width}x{height}x{depth} = {gridComponents.Count} cells");
            StartCoroutine(CheckEntropy());
        }

        /// <summary>
        /// Determine zone type based on grid position
        /// there are a few neat things you can do with this
        /// </summary>
        protected virtual ZoneType DetermineZone(int x, int y, int z)
        {
            return zoneTemplate.GetZoneType(x, y, z);
        }

        /// <summary>
        /// Select the cell with the fewest tile options
        /// </summary>
        IEnumerator CheckEntropy()
        {
            // Create list of uncollapsed cells
            List<CellData> tempGrid = new();

            foreach (CellData c in gridComponents)
            {
                // Skip nothing zones and collapsed cells
                if (c != null && !c.collapsed)
                    tempGrid.Add(c);
            }

            // leave if all cells are collapsed and combine mesh
            if (tempGrid.Count == 0)
            {
                Debug.Log("Wave function generation complete.");

                if (combineMeshes)
                {
                    Debug.Log($"Waiting {combineDelay} seconds before combining meshes...");
                    yield return new WaitForSeconds(combineDelay);
                    CombineAllMeshes();
                }
                yield break;
            }

            // Sort by entropy (number of possible options)
            tempGrid.Sort((a, b) =>
            {
                if (a == null || a.tileOptions == null) return 1;
                if (b == null || b.tileOptions == null) return -1;
                return a.tileOptions.Length - b.tileOptions.Length;
            });

            // If no valid options
            if (tempGrid[0].tileOptions == null || tempGrid[0].tileOptions.Length == 0)
            {
                Debug.Log("No tile options, restarting...");
                ReverseGenerationOnNullTiles();
                yield break;
            }

            // Keep cells with lowest entropy
            int lowestEntropy = tempGrid[0].tileOptions.Length;
            tempGrid.RemoveAll(a => a == null || a.tileOptions == null || a.tileOptions.Length != lowestEntropy);

            // Yield for one frame to prevent stack overflow
            yield return null;

            // Randomly select one cell from the lowest entropy cells
            CellData cellToCollapse = tempGrid[Random.Range(0, tempGrid.Count)];
            CollapseCell(cellToCollapse);
        }

        /// <summary>
        /// Restart the generation if it messes up
        /// It usually only messes up at the very start of generation when the tile neighbors are good
        /// I plan to implement a proper backtracking system in the future, apologies for the janky method atm
        /// </summary>
        private void ReverseGenerationOnNullTiles()
        {
            restartAttempts++;

            if (restartAttempts >= maxRestartAttempts)
            {
                Debug.LogError($"Failed to generate after {maxRestartAttempts} attempts. Check your tile constraints and zones.");
                return;
            }

            Debug.Log($"Restart attempt {restartAttempts}/{maxRestartAttempts}");

            // Clear spawned tiles before restarting
            foreach (var tile in spawnedTiles)
            {
                if (tile != null)
                    Destroy(tile);
            }

            InitializeGrid();
        }

        /// <summary>
        /// Now that we have the lowest entropy cell we collapse it
        /// </summary>
        /// <param name="tempGrid"> the current lowest entropy cell </param>
        void CollapseCell(CellData cellToCollapse)
        {
            if (cellToCollapse == null || cellToCollapse.tileOptions == null || cellToCollapse.tileOptions.Length == 0)
            {
                Debug.LogError("Attempted to collapse invalid cell!");
                return;
            }

            cellToCollapse.collapsed = true;

            // Randomly pick a tile from the cells remaining options
            Tile selectedTile = cellToCollapse.tileOptions[Random.Range(0, cellToCollapse.tileOptions.Length)];
            cellToCollapse.tileOptions = new Tile[] { selectedTile };
            cellToCollapse.chosenTile = selectedTile;

            GameObject instantiatedTile;
            // Instantiate the tile at the correct position
            if (selectedTile != null)
            {
                Vector3 tilePosition;
                if (selectedTile.adjustYPosition == false)
                {
                    tilePosition = cellToCollapse.position;
                }
                else // if the adjust y position bool of the tile is true then adjust it
                {
                    tilePosition = new(cellToCollapse.position.x,
                                       cellToCollapse.position.y + selectedTile.yPosAdjustment, 
                                       cellToCollapse.position.z);
                }

                instantiatedTile = Instantiate(selectedTile.gameObject, tilePosition, selectedTile.transform.rotation);

                // Add to spawned tiles list to be used by the mesh combiner later
                if (instantiatedTile != null)
                {
                    spawnedTiles.Add(instantiatedTile);
                }
            }

            // Get the index of the collapsed cell
            int collapsedIndex = gridComponents.IndexOf(cellToCollapse);

            // Add all neighbors to the propagation queue
            AddNeighborsToQueue(collapsedIndex);

            // Check the neighbors that AddNeighborsToQueue() deem nessacary
            ProcessPropagationQueue();

            // iteration goes up until it matches the number of cells
            iteration++;
        
            // Go again until all cells are collapsed
            if (iteration <= width * height * depth)
            {
                StartCoroutine(CheckEntropy());
            }
        }

        // convert 3D grid to 1D to optimally iterate from
        // X = offset inside the current row
        // Z * width = skip a row of X for each Z
        // Y * width * depth = skip a full layer for each y level
        // Returns the position in the grid in an integer
        int Dimensions(int x, int y, int z)
        {
            return x + z * width + y * width * depth;
        }

        // Turn the current collapsed tile index into something that can easily have its neighbors checked
        // For example I can do z + 1 to check whats to the north of the current tile
        void FromIndex(int i, out int x, out int z, out int y)
        {
            x = i % width;
            z = (i / width) % depth;
            y = i / (width * depth);
        }

        /// <summary>
        /// Adds all valid neighbors of the current cell to the propagation queue
        /// </summary>
        void AddNeighborsToQueue(int index)
        {
            // x = east west, z = north south, y = up down
            FromIndex(index, out int x, out int z, out int y);
            
            // Add North neighbor to queue if it exists and isnt a nothing zone
            if (z < depth - 1)
            {
                // get the north position to the currently collapsing cell
                int neighborIndex = Dimensions(x, y, z + 1);
                if (neighborIndex < gridComponents.Count && gridComponents[neighborIndex] != null)
                    // add it to the queue to later have its available neighbors checked
                    propagationQueue.Enqueue(neighborIndex);
            }

            // Add South neighbor to queue
            if (z > 0)
            {
                int neighborIndex = Dimensions(x, y, z - 1);
                if (neighborIndex < gridComponents.Count && gridComponents[neighborIndex] != null)
                    propagationQueue.Enqueue(neighborIndex);
            }

            // Add East neighbor to queue
            if (x < width - 1)
            {
                int neighborIndex = Dimensions(x + 1, y, z);
                if (neighborIndex < gridComponents.Count && gridComponents[neighborIndex] != null)
                    propagationQueue.Enqueue(neighborIndex);
            }

            // Add West neighbor to queue
            if (x > 0)
            {
                int neighborIndex = Dimensions(x - 1, y, z);
                if (neighborIndex < gridComponents.Count && gridComponents[neighborIndex] != null)
                    propagationQueue.Enqueue(neighborIndex);
            }

            // Add Up neighbor to queue
            if (y < height - 1)
            {
                int neighborIndex = Dimensions(x, y + 1, z);
                if (neighborIndex < gridComponents.Count && gridComponents[neighborIndex] != null)
                    propagationQueue.Enqueue(neighborIndex);
            }

            // Add Down neighbor to queue
            if (y > 0)
            {
                int neighborIndex = Dimensions(x, y - 1, z);
                if (neighborIndex < gridComponents.Count && gridComponents[neighborIndex] != null)
                    propagationQueue.Enqueue(neighborIndex);
            }
        }

        /// <summary>
        /// Ensures that only neighbors that need to be checked are checked
        /// </summary>
        void ProcessPropagationQueue()
        {
            while (propagationQueue.Count > 0)
            {
                int index = propagationQueue.Dequeue();

                // Validate index
                if (index < 0 || index >= gridComponents.Count)
                    continue;

                CellData cell = gridComponents[index];

                // Skip if already collapsed or invalid
                if (cell == null || cell.collapsed)
                    continue;

                // Store the old option count
                int oldOptionCount = gridComponents[index].tileOptions.Length;

                // Update this cell's options based on its neighbors
                UpdateCellOptions(index);

                // Get the new option count
                int newOptionCount = cell.tileOptions != null ? cell.tileOptions.Length : 0;

                // If the options changed, we need to propagate to this cell's neighbors
                if (oldOptionCount != newOptionCount && newOptionCount > 0)
                {
                    AddNeighborsToQueue(index);
                }
            }
        }

        /// <summary>
        /// Updates current cells tile options based on its tile neighbors
        /// </summary>
        /// <param name="index"></param>
        void UpdateCellOptions(int index)
        {
            if (index < 0 || index >= gridComponents.Count)
                return;

            FromIndex(index, out int x, out int z, out int y);

            CellData currentCell = gridComponents[index];
            if (currentCell == null)
                return;

            // List of options for this tile
            List<Tile> options = new();

            if (currentCell.tileOptions != null && currentCell.tileOptions.Length > 0)
            {
                options.AddRange(currentCell.tileOptions);
            }
            else
            {
                // Initialize with all tiles matching the zone
                foreach (var tile in tileObjects)
                {
                    if (tile != null && tile.allowedZone == currentCell.zone)
                        options.Add(tile);
                }

                // Fallback to all tiles if none match zone
                if (options.Count == 0)
                {
                    options.AddRange(tileObjects);
                }
            }

            // Options list is now limited to what is allowed for the current cell zone
            // The list still needs more constraints to be accurate though
            // So now it will check all of its direct tile neighbors at the north, east, south, west, up, down
            // And further constrain this tiles options based on what this tiles neighboring tiles allow there

            #region Neighbor Checks for North, South, East, West, Up, Down (repetitive)

            // -------------------------------------------------------------\\
            // Check the nighbor to the (North) of the current tile (z + 1) \\
            // -------------------------------------------------------------\\
            if (z < depth - 1) // Only perform if north neighbor exists
            {
                // Get northern neighbor from the grid
                int northIndex = Dimensions(x, y, z + 1);
                if (northIndex < gridComponents.Count)
                {
                    CellData north = gridComponents[northIndex];

                    // If there is a cell to the north
                    if (north != null && north.tileOptions != null && north.tileOptions.Length > 0)
                    {
                        // Create hashset to collect valid tiles
                        HashSet<Tile> validOptions = new();

                        // For each tile allowed to the current cells north:
                        foreach (Tile possibleOptions in north.tileOptions)
                        {
                            // Use cached dictionary to get the index of this tile in the tileObjects array
                            int validOption = tileToIndex[possibleOptions];

                            // The current cell sits to the south of its north neighbor.
                            // To be compatible, the current cell can only use tiles that this north neighbor allows to its south
                            // So, for each of the north neighbors possible tiles, get the allowed tiles to the south of it
                            var allowedSouthernTiles = tileObjects[validOption].southNeighbours;

                            if (allowedSouthernTiles != null)
                            {
                                // Add valid tiles to the hashset
                                foreach (var v in allowedSouthernTiles)
                                    validOptions.Add(v);
                            }
                        }

                        CheckValidity(options, validOptions);
                    }
                }
            }

            // -------------------------------------------------------------\\
            // Check the nighbor to the (South) of the current tile (z - 1) \\
            // -------------------------------------------------------------\\
            if (z > 0) // Only perform if south neighbor exists
            {
                // Get southern neighbor from the grid
                int southIndex = Dimensions(x, y, z - 1);
                if (southIndex < gridComponents.Count)
                {
                    CellData south = gridComponents[southIndex];

                    // Create hashset to collect valid tiles
                    if (south != null && south.tileOptions != null && south.tileOptions.Length > 0)
                    {
                        HashSet<Tile> validOptions = new();

                        // For each tile allowed to the current cells south:
                        foreach (Tile possibleOptions in south.tileOptions)
                        {
                            // Use cached dictionary to get the index of this tile in the tileObjects array
                            var validOption = tileToIndex[possibleOptions];

                            // The current cell sits to the north of its south neighbor.
                            // To be compatible, the current cell can only use tiles that this south neighbor allows to its north
                            // So, for each of the south neighbors possible tiles, get the allowed tiles to the north of it
                            var allowedNorthernTiles = tileObjects[validOption].northNeighbours;

                            if (allowedNorthernTiles != null)
                            {
                                // Add valid tiles to the hashset
                                foreach (var v in allowedNorthernTiles)
                                    validOptions.Add(v);
                            }
                        }

                        CheckValidity(options, validOptions);
                    }
                }
            }

            // -------------------------------------------------------------\\
            // Check the nighbor to the (East) of the current tile  (x + 1) \\
            // -------------------------------------------------------------\\

            if (x < width - 1) // Only perform if east neighbor exists
            {
                // Get eastern neighbor from the grid
                int eastIndex = Dimensions(x + 1, y, z);
                if (eastIndex < gridComponents.Count)
                {
                    CellData east = gridComponents[eastIndex];

                    // For each tile allowed to the current cells east
                    if (east != null && east.tileOptions != null && east.tileOptions.Length > 0)
                    {
                        // Create hashset to collect valid tiles
                        HashSet<Tile> validOptions = new();

                        // For each tile allowed to the current cells east:
                        foreach (Tile possibleOptions in east.tileOptions)
                        {
                            // Use cached dictionary to get the index of this tile in the tileObjects array
                            var validOption = tileToIndex[possibleOptions];

                            // The current cell sits to the west of its east neighbor.
                            // To be compatible, the current cell can only use tiles that this east neighbor allows to its west
                            // So, for each of the east neighbors possible tiles, get the allowed tiles to the west of it
                            var allowedWesternTiles = tileObjects[validOption].westNeighbours;

                            if (allowedWesternTiles != null)
                            {
                                // Add valid tiles to the hashset
                                foreach (var v in allowedWesternTiles)
                                    validOptions.Add(v);
                            }
                        }

                        CheckValidity(options, validOptions);
                    }
                }
            }

            // -------------------------------------------------------------\\
            // Check the neighbor to the (West) of the current tile (x - 1) \\
            // -------------------------------------------------------------\\
            if (x > 0) // Only perform if west neighbor exists
            {
                // Get western neighbor from the grid
                int westIndex = Dimensions(x - 1, y, z);
                if (westIndex < gridComponents.Count)
                {
                    CellData west = gridComponents[westIndex];

                    // For each tile allowed to the current cells east
                    if (west != null && west.tileOptions != null && west.tileOptions.Length > 0)
                    {
                        // Create hashset to collect valid tiles
                        HashSet<Tile> validOptions = new();

                        // For each tile allowed to the current cells west:
                        foreach (Tile possibleOptions in west.tileOptions)
                        {
                            // Use cached dictionary to get the index of this tile in the tileObjects array
                            var validOption = tileToIndex[possibleOptions];

                            // The current cell sits to the east of its west neighbor.
                            // To be compatible, the current cell can only use tiles that this west neighbor allows to its east
                            // So, for each of the west neighbors possible tiles, get the allowed tiles to the east of it
                            var allowedEasternTiles = tileObjects[validOption].eastNeighbours;

                            if (allowedEasternTiles != null)
                            {
                                // Add valid tiles to the hashset
                                foreach (var v in allowedEasternTiles)
                                    validOptions.Add(v);
                            }
                        }

                        CheckValidity(options, validOptions);
                    }
                }
            }

            // -------------------------------------------------------------\\
            // Check the neighbor to the (Up) of the current tile (y + 1)   \\
            // -------------------------------------------------------------\\
            if (y < height - 1) // Only perform if up neighbor exists
            {
                // Get upper neighbor from the grid
                int upIndex = Dimensions(x, y + 1, z);
                if (upIndex < gridComponents.Count)
                {
                    CellData up = gridComponents[upIndex];

                    // For each tile allowed to the current cells east
                    if (up != null && up.tileOptions != null && up.tileOptions.Length > 0)
                    {
                        // Create hashset to collect valid tiles
                        HashSet<Tile> validOptions = new();

                        // For each tile allowed to the current cells up:
                        foreach (Tile possibleOptions in up.tileOptions)
                        {
                            // Use cached dictionary to get the index of this tile in the tileObjects array
                            var validOption = tileToIndex[possibleOptions];

                            // The current cell sits to the down position of its upward neighbor.
                            // To be compatible, the current cell can only use tiles that this upward neighbor allows downwards
                            // So, for each of the up neighbors possible tiles, get the allowed tiles downwards to it
                            var allowedDownwardTiles = tileObjects[validOption].downNeighbours;

                            if (allowedDownwardTiles != null)
                            {
                                // Add valid tiles to the hashset
                                foreach (var v in allowedDownwardTiles)
                                    validOptions.Add(v);
                            }
                        }

                        CheckValidity(options, validOptions);
                    }

                }

            }


            // -------------------------------------------------------------\\
            // Check the nighbor to the (Down) of the current tile (y - 1) \\
            // -------------------------------------------------------------\\
            if (y > 0)
            {
                int downIndex = Dimensions(x, y - 1, z);
                if (downIndex < gridComponents.Count)
                {
                    CellData down = gridComponents[downIndex];

                    // For each tile allowed to the current cells east
                    if (down != null && down.tileOptions != null && down.tileOptions.Length > 0)
                    {
                        // Create hashset to collect valid tiles
                        HashSet<Tile> validOptions = new();

                        // For each tile allowed to the current cells up:
                        foreach (Tile possibleOptions in down.tileOptions)
                        {
                            // Use cached dictionary to get the index of this tile in the tileObjects array
                            var validOption = tileToIndex[possibleOptions];

                            // The current cell sits to the down position of its upward neighbor.
                            // To be compatible, the current cell can only use tiles that this upward neighbor allows downwards
                            // So, for each of the up neighbors possible tiles, get the allowed tiles downwards to it
                            var allowedUpwardTiles = tileObjects[validOption].upNeighbours;

                            if (allowedUpwardTiles != null)
                            {
                                // Add valid tiles to the hashset
                                foreach (var v in allowedUpwardTiles)
                                    validOptions.Add(v);
                            }
                        }

                        CheckValidity(options, validOptions);
                    }
                }
            }

            #endregion

            // Update the cell with new constrained options
            Tile[] newTileList = options.ToArray();

            gridComponents[index].tileOptions = newTileList;
        }

        /// <summary>
        /// Removes tiles from options list which are not valid
        /// </summary>
        /// <param name="optionList"> The list of possible tiles for the cell </param>
        /// <param name="validOption"> Hashet of tiles which are allowed based on tile neighbors </param>
        void CheckValidity(List<Tile> optionList, HashSet<Tile> validOption)
        {
            if (optionList == null || validOption == null || validOption.Count == 0)
                return;

            // Iterate backwards to safely remove while iterating, if iterated forwards elements can be shifted upon deletion
            for (int x = optionList.Count - 1; x >= 0; x--)
            {
                // Element = Currently checked tile
                var element = optionList[x];

                // Is this tile amongst the valid tiles defined in the validOption hashset?
                if (!validOption.Contains(element))
                {
                    // If not, remove it from potential options
                    optionList.RemoveAt(x);
                }
            }
            // optionList now only contains valid tiles
        }

        /// <summary>
        /// Final step in map generation
        /// Combines all spawned tiles into optimised meshes grouped by material.
        /// </summary>
        void CombineAllMeshes()
        {
            Debug.Log($"Starting mesh combination... Found {spawnedTiles.Count} spawned tiles");

            // Check that there are tiles available
            if (spawnedTiles.Count == 0)
            {
                Debug.LogWarning("No tiles to combine!");
                return;
            }

            // Remove any null entries from the list (tiles that may have been destroyed)
            spawnedTiles.RemoveAll(tile => tile == null);

            if (spawnedTiles.Count == 0)
            {
                Debug.LogWarning("All spawned tiles are null!");
                return;
            }

            Debug.Log($"Valid tiles to combine: {spawnedTiles.Count}");

            // Group meshes by material
            // Because one Unity mesh can only render one material at a time
            Dictionary<Material, List<CombineInstance>> materialGroups = new();
            int meshesFound = 0; // Counter for total meshes processed

            // go through all spawned tiles and extract their mesh data
            foreach (GameObject tile in spawnedTiles)
            {
                if (tile == null) continue;

                // Find mesh filters on all tiles and their children
                MeshFilter[] meshFilters = tile.GetComponentsInChildren<MeshFilter>();

                Debug.Log($"Tile '{tile.name}' has {meshFilters.Length} mesh filters");

                // Process each mesh filter found in this tile
                foreach (MeshFilter mf in meshFilters)
                {
                    // Ensure there is a mesh, if not, skip it.
                    if (mf.sharedMesh == null)
                    {
                        Debug.LogWarning($"Mesh filter on '{mf.gameObject.name}' has null mesh");
                        continue;
                    }

                    // the MeshRenderer component is needed to see the material, so skip it if there is none
                    if (!mf.TryGetComponent<MeshRenderer>(out var mr))
                    {
                        Debug.LogWarning($"Mesh filter on '{mf.gameObject.name}' has no MeshRenderer");
                        continue;
                    }
                    
                    // Submeshes are the peices of a larger mesh which have their own materials 
                    // The submeshes need to be separated and combined with eachover.
                    // This is ideal for optimising a tile based system, since for example all the thousands of grass tiles will be combined into one.
                    Material[] materials = mr.sharedMaterials;

                    if (materials == null || materials.Length == 0)
                    {
                        Debug.LogWarning($"Mesh renderer on '{mf.gameObject.name}' has no materials");
                        continue;
                    }

                    // Check if this mesh has multiple materials (multi-material mesh)
                    if (materials.Length > 1 && mf.sharedMesh.subMeshCount > 1)
                    {
                        Debug.Log($"Mesh '{mf.gameObject.name}' has {materials.Length} materials and {mf.sharedMesh.subMeshCount} submeshes");

                        // Separate submeshes by material
                        for (int subMeshIndex = 0; subMeshIndex < Mathf.Min(materials.Length, mf.sharedMesh.subMeshCount); subMeshIndex++)
                        {
                            Material mat = materials[subMeshIndex];

                            if (mat == null)
                            {
                                Debug.LogWarning($"Material at index {subMeshIndex} is null on '{mf.gameObject.name}'");
                                continue;
                            }

                            // Create or get the list for this material
                            if (!materialGroups.ContainsKey(mat))
                            {
                                materialGroups[mat] = new List<CombineInstance>();
                                Debug.Log($"Created new material group for '{mat.name}'");
                            }

                            // Create a new mesh containing only the vertice and triangles
                            // that belong to this specific submesh
                            Mesh submesh = new()
                            {
                                vertices = mf.sharedMesh.vertices,   // Copy vertex positions
                                normals = mf.sharedMesh.normals,     // Copy surface normals for lighting
                                uv = mf.sharedMesh.uv,               // Copy UV coordinates for textures
                                triangles = mf.sharedMesh.GetTriangles(subMeshIndex) // Get this submesh's triangles
                            };

                            // Create a CombineInstance for this submesh
                            // CombineInstance holds the mesh data and its transform in world space
                            CombineInstance ci = new()
                            {
                                mesh = submesh,
                                // localToWorldMatrix converts the mesh from its local transform
                                // to world space, so all meshes are positioned correctly when combined
                                transform = mf.transform.localToWorldMatrix
                            };

                            materialGroups[mat].Add(ci);
                            meshesFound++;
                        }
                    }
                    else
                    {
                        // This mesh only uses one material so we can add it
                        // without needing to separate submeshes
                        Material mat = materials[0];

                        if (mat == null)
                        {
                            Debug.LogWarning($"Material is null on '{mf.gameObject.name}'");
                            continue;
                        }

                        // Create or get the list for this material
                        if (!materialGroups.ContainsKey(mat))
                        {
                            materialGroups[mat] = new List<CombineInstance>();
                            Debug.Log($"Created new material group for '{mat.name}'");
                        }

                        // Create CombineInstance with the full mesh
                        CombineInstance ci = new()
                        {
                            mesh = mf.sharedMesh,
                            transform = mf.transform.localToWorldMatrix
                        };

                        materialGroups[mat].Add(ci);
                        meshesFound++;
                    }
                }
            }

            Debug.Log($"Found {meshesFound} total meshes across {materialGroups.Count} materials");

            // Dont continue if no meshes to combine were found
            if (materialGroups.Count == 0)
            {
                Debug.LogError($"No valid meshes found to combine!" + $"{"\n"} " +
                                "Make sure your tiles have MeshFilter and MeshRenderer components, " + $"{"\n"} " +
                                "and that their import settings have Read/Write enabled!");
                return;
            }

            // Create the parent which holds the mesh groups
            GameObject combinedParent = new(combinedMeshName);
            combinedParent.transform.position = this.transform.position;

            // Create combined meshes per material group
            // Since you cant combine meshes with different materials into one
            int meshIndex = 0;
            foreach (var kvp in materialGroups)
            {
                // The material for this group
                Material material = kvp.Key;
                // All meshes that use this material
                List<CombineInstance> combines = kvp.Value;

                Debug.Log($"Combining {combines.Count} meshes for material '{material.name}'");

                // Create a new GameObject to hold this combined mesh
                GameObject meshObject = new($"{combinedMeshName}_Material_{meshIndex}");
                meshObject.transform.SetParent(combinedParent.transform);
                meshObject.transform.localPosition = Vector3.zero;

                // Add needed components for rendering a mesh
                MeshFilter mf = meshObject.AddComponent<MeshFilter>();
                MeshRenderer mr = meshObject.AddComponent<MeshRenderer>();

                // Create an empty mesh
                Mesh combinedMesh = new()
                {
                    name = $"Combined_{material.name}",

                    // Set to UInt32 to support large meshes up to 4 billion vertices
                    // Default is UInt16 which only supports 65,535 vertices
                    // It would be nice to have a middle ground lol
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
                };

                try
                {
                    // Array of all CombineInstances to merge
                    // true 1 = Merge into single submesh (since all use same material)
                    // true 2 = Use transform matrices (positions meshes correctly in world space)
                    combinedMesh.CombineMeshes(combines.ToArray(), true, true);
                }
                catch (System.Exception e) // if it fails to combine meshes
                {
                    Debug.LogError($"Error combining meshes: {e.Message}");
                    Destroy(meshObject);
                    continue;
                }

                // Optimize and fix up the combined mesh with unitys helpful functions
                
                // for culling
                combinedMesh.RecalculateBounds();
                // fixes lighting errors
                combinedMesh.RecalculateNormals();
                // optimises it!
                combinedMesh.Optimize();

                // Assign the combined mesh and material to the GameObject
                mf.sharedMesh = combinedMesh;
                mr.sharedMaterial = material;

                // Give the combined mesh a collider if you want
                if (giveCombinedMeshACollider == true)
                {
                    MeshCollider mc = meshObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = combinedMesh;
                }

                meshIndex++;
            }

            // Combined mesh should be complete now

            Debug.Log($"Successfully combined {spawnedTiles.Count} tiles into {materialGroups.Count} meshes.");

            // Destroy tiles now that we have the combined map mesh

            if (!keepOriginalTiles)
            {
                // I highly reccomend you have this on for builds
                Debug.Log("Destroying original tiles...");
                foreach (GameObject tile in spawnedTiles)
                {
                    if (tile != null)
                        Destroy(tile);
                }
                spawnedTiles.Clear();
                Debug.Log("All done!");
            }
            else
            {
                // Keep original tiles but disable them
                // For potential debugging incase combining goes wrong
                // You can show me and ill look into it
                Debug.Log("Hiding original tiles...");
                foreach (GameObject tile in spawnedTiles)
                {
                    if (tile != null)
                        tile.SetActive(false);
                }
                Debug.Log("All done!");
            }
        }
    }
}