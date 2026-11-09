using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Handles spawning and configuration of obstacle GameObjects (such as <see cref="MaceController"/>) from level data definitions.
/// Instantiated and managed by <see cref="LevelJsonLoader"/> during level initialization.
/// </summary>
public class ObstacleSpawner
{
    #region Constructor & Fields

    private readonly Transform obstaclesParent;
    private readonly List<ObstaclePrefabEntry> obstaclePrefabs;
    private readonly Tilemap terrainTilemap;
    private readonly int startX;
    private readonly int floorYRow;
    private readonly bool obstacleYRelativeToFloor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObstacleSpawner"/> class.
    /// </summary>
    /// <param name="obstaclesParent">Transform parent container for spawned obstacles.</param>
    /// <param name="obstaclePrefabs">List of mapped prefab entries.</param>
    /// <param name="terrainTilemap">Terrain tilemap reference for cell-to-world position calculation.</param>
    /// <param name="startX">Starting X tile coordinate offset.</param>
    /// <param name="floorYRow">Floor Y tile row index.</param>
    /// <param name="obstacleYRelativeToFloor">If true, Y offset is relative to floorYRow; otherwise absolute.</param>
    public ObstacleSpawner(
        Transform obstaclesParent,
        List<ObstaclePrefabEntry> obstaclePrefabs,
        Tilemap terrainTilemap,
        int startX,
        int floorYRow,
        bool obstacleYRelativeToFloor)
    {
        this.obstaclesParent = obstaclesParent;
        this.obstaclePrefabs = obstaclePrefabs;
        this.terrainTilemap = terrainTilemap;
        this.startX = startX;
        this.floorYRow = floorYRow;
        this.obstacleYRelativeToFloor = obstacleYRelativeToFloor;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Spawns all obstacles defined in the provided obstacle data list.
    /// </summary>
    /// <param name="obstacles">List of obstacle configuration data objects from <see cref="LevelData"/>.</param>
    public void SpawnObstacles(List<ObstacleData> obstacles)
    {
        if (obstacles == null || obstacles.Count == 0) return;

        foreach (var obstacle in obstacles)
        {
            SpawnSingleObstacle(obstacle);
        }
    }

    #endregion

    #region Internal Spawning Logic

    /// <summary>
    /// Spawns a single obstacle prefab and attaches runtime configuration.
    /// </summary>
    /// <param name="obstacle">Obstacle data configuration to instantiate.</param>
    private void SpawnSingleObstacle(ObstacleData obstacle)
    {
        var prefab = ResolveObstaclePrefab(obstacle.obstacleName);
        if (prefab == null)
        {
            Debug.LogWarning($"ObstacleSpawner: No prefab mapped for obstacle type='{obstacle.obstacleName}'.");
            return;
        }

        int cellX = startX + obstacle.startX;
        int cellY = obstacleYRelativeToFloor ? (floorYRow + obstacle.startY) : obstacle.startY;
        var cell = new Vector3Int(cellX, cellY, 0);
        var worldPos = terrainTilemap.GetCellCenterWorld(cell);

        var instance = UnityEngine.Object.Instantiate(prefab, worldPos, Quaternion.identity, obstaclesParent);
        AttachObstacleConfig(instance, obstacle);
    }

    /// <summary>
    /// Resolves a prefab GameObject by obstacle type key.
    /// </summary>
    /// <param name="type">Type key name.</param>
    /// <returns>Matching prefab GameObject or null if unmapped.</returns>
    private GameObject ResolveObstaclePrefab(string type)
    {
        if (string.IsNullOrEmpty(type)) return null;

        foreach (var entry in obstaclePrefabs)
        {
            if (entry != null && entry.prefab != null && entry.obstacleName == type)
            {
                return entry.prefab;
            }
        }

        return null;
    }

    /// <summary>
    /// Applies obstacle configuration data to a spawned instance, configuring its <see cref="MaceController"/> if present.
    /// </summary>
    /// <param name="instance">Spawned obstacle GameObject instance.</param>
    /// <param name="data">Obstacle configuration data object.</param>
    private void AttachObstacleConfig(GameObject instance, ObstacleData data)
    {
        var mace = instance.GetComponent<MaceController>();
        if (mace != null)
        {
            var cellSize = terrainTilemap != null && terrainTilemap.layoutGrid != null
                ? terrainTilemap.layoutGrid.cellSize
                : Vector3.one;

            float speedUnits = data.speed * Mathf.Abs(cellSize.x);
            float horizUnits = data.horizontalDistance * Mathf.Abs(cellSize.x);
            float vertUnits = data.verticalDistance * Mathf.Abs(cellSize.y);

            mace.ApplyObstacleData(
                data.startX,
                data.startY,
                speedUnits,
                horizUnits,
                vertUnits,
                data.starterCorner,
                data.clockwise,
                startX,
                floorYRow,
                obstacleYRelativeToFloor,
                terrainTilemap);
        }

        var rat = instance.GetComponent<RatController>();
        if (rat != null)
        {
            var cellSize = terrainTilemap != null && terrainTilemap.layoutGrid != null
                ? terrainTilemap.layoutGrid.cellSize
                : Vector3.one;

            float speedUnits = data.speed * Mathf.Abs(cellSize.x);

            rat.ApplyObstacleData(
                data.startX,
                data.startY,
                speedUnits,
                startX,
                floorYRow,
                obstacleYRelativeToFloor,
                terrainTilemap);
        }
    }

    #endregion

    #region Data Structures

    /// <summary>
    /// Obstacle data structure matching JSON specification.
    /// Deserialized into <see cref="LevelData.Obstacles"/>.
    /// </summary>
    [Serializable]
    public class ObstacleData
    {
        /// <summary>
        /// Key name identifying the obstacle type prefab.
        /// </summary>
        public string obstacleName;

        /// <summary>
        /// X tile offset position.
        /// </summary>
        public int startX;

        /// <summary>
        /// Y tile offset position.
        /// </summary>
        public int startY;

        /// <summary>
        /// Movement speed scalar.
        /// </summary>
        public float speed;

        /// <summary>
        /// Horizontal movement distance in tiles.
        /// </summary>
        public int horizontalDistance;

        /// <summary>
        /// Vertical movement distance in tiles.
        /// </summary>
        public int verticalDistance;

        /// <summary>
        /// Starting corner key string (e.g. "bottom-left").
        /// </summary>
        public string starterCorner;

        /// <summary>
        /// Direction of traversal (true = clockwise, false = counter-clockwise).
        /// </summary>
        public bool clockwise;
    }

    /// <summary>
    /// Maps obstacle type string to prefab GameObject in Unity Inspector.
    /// </summary>
    [Serializable]
    public class ObstaclePrefabEntry
    {
        /// <summary>
        /// Key name identifying the obstacle type.
        /// </summary>
        public string obstacleName;

        /// <summary>
        /// Target prefab GameObject.
        /// </summary>
        public GameObject prefab;
    }

    #endregion
}
