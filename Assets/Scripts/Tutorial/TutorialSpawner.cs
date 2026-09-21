using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Handles spawning of TutorialZone GameObjects from level data definitions.
/// Mirrors the pattern used by <see cref="ObstacleSpawner"/>: takes JSON-parsed
/// tile-position data, resolves it against a catalog, and instantiates world objects.
/// Intended to be constructed and called by LevelJsonLoader alongside ObstacleSpawner.
/// </summary>
public class TutorialSpawner
{
    #region Constructor & Fields

    private readonly Transform tutorialsParent;
    private readonly TutorialCatalog catalog;
    private readonly Tilemap terrainTilemap;
    private readonly int startX;
    private readonly int floorYRow;
    private readonly bool tutorialYRelativeToFloor;

    public TutorialSpawner(
        Transform tutorialsParent,
        TutorialCatalog catalog,
        Tilemap terrainTilemap,
        int startX,
        int floorYRow,
        bool tutorialYRelativeToFloor)
    {
        this.tutorialsParent = tutorialsParent;
        this.catalog = catalog;
        this.terrainTilemap = terrainTilemap;
        this.startX = startX;
        this.floorYRow = floorYRow;
        this.tutorialYRelativeToFloor = tutorialYRelativeToFloor;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Spawns all tutorial zones defined in the provided list.
    /// </summary>
    public void SpawnTutorials(List<TutorialSpawnData> tutorials)
    {
        if (tutorials == null || tutorials.Count == 0) return;

        foreach (var tutorial in tutorials)
        {
            SpawnSingleTutorial(tutorial);
        }
    }

    #endregion

    #region Internal Spawning Logic

    private void SpawnSingleTutorial(TutorialSpawnData data)
    {
        var tutorialData = catalog != null ? catalog.ObterPorId(data.tutorialId) : null;
        if (tutorialData == null)
        {
            Debug.LogWarning($"TutorialSpawner: No TutorialData found for id='{data.tutorialId}'.");
            return;
        }

        int cellX = startX + data.startX;
        int cellY = tutorialYRelativeToFloor ? (floorYRow + data.startY) : data.startY;
        var cell = new Vector3Int(cellX, cellY, 0);
        Vector3 worldPos = terrainTilemap.GetCellCenterWorld(cell);

        var cellSize = terrainTilemap != null && terrainTilemap.layoutGrid != null
            ? terrainTilemap.layoutGrid.cellSize
            : Vector3.one;

        float widthUnits = Mathf.Max(1, data.widthTiles) * Mathf.Abs(cellSize.x);
        float heightUnits = Mathf.Max(1, data.heightTiles) * Mathf.Abs(cellSize.y);

        var obj = new GameObject($"TutorialZone_{tutorialData.id}");
        obj.transform.SetParent(tutorialsParent);
        obj.transform.position = worldPos;

        var collider = obj.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(widthUnits, heightUnits);

        var zone = obj.AddComponent<TutorialZone>();
        zone.tutorial = tutorialData;
    }

    #endregion

    #region Data Structures

    /// <summary>
    /// Tutorial zone data structure matching future JSON specification.
    /// Deserialized into a level data list, analogous to <see cref="ObstacleSpawner.ObstacleData"/>.
    /// </summary>
    [Serializable]
    public class TutorialSpawnData
    {
        /// <summary>Key referencing a TutorialData id inside the TutorialCatalog.</summary>
        public string tutorialId;

        /// <summary>X tile offset position (zone anchor).</summary>
        public int startX;

        /// <summary>Y tile offset position (zone anchor).</summary>
        public int startY;

        /// <summary>Zone width, in tiles.</summary>
        public int widthTiles = 1;

        /// <summary>Zone height, in tiles.</summary>
        public int heightTiles = 1;
    }

    #endregion
}
