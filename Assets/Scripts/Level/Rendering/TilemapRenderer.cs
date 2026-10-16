using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Handles tilemap rendering operations for diagrams, terrain bands, and clock patterns.
/// Instantiated and managed by <see cref="LevelJsonLoader"/>.
/// </summary>
public class TilemapRenderer
{
    #region Constructor & Fields

    private readonly Tilemap inputTilemap;
    private readonly Tilemap terrainTilemap;
    private readonly Tilemap clockTilemap;
    private readonly TileBase[] diagramTiles;
    private readonly TileBase floorTile;
    private readonly TileBase ceilingTile;
    private readonly TileBase wallTile;
    private readonly bool flipCeilingY;
    private readonly int startX;

    /// <summary>
    /// Initializes a new instance of the <see cref="TilemapRenderer"/> class with target tilemaps and tile assets.
    /// </summary>
    /// <param name="inputTilemap">Tilemap target for input diagram signals.</param>
    /// <param name="terrainTilemap">Tilemap target for floor/ceiling/wall terrain elements.</param>
    /// <param name="clockTilemap">Tilemap target for clock waveform signals.</param>
    /// <param name="diagramTiles">Array of diagram tiles indexed by bitwise pattern (0..7).</param>
    /// <param name="floorTile">Tile asset for floor terrain.</param>
    /// <param name="ceilingTile">Tile asset for ceiling terrain.</param>
    /// <param name="wallTile">Tile asset for left boundary wall.</param>
    /// <param name="flipCeilingY">Whether to flip ceiling tile graphics on the Y axis.</param>
    /// <param name="startX">Starting X tile coordinate offset.</param>
    public TilemapRenderer(
        Tilemap inputTilemap,
        Tilemap terrainTilemap,
        Tilemap clockTilemap,
        TileBase[] diagramTiles,
        TileBase floorTile,
        TileBase ceilingTile,
        TileBase wallTile,
        bool flipCeilingY,
        int startX)
    {
        this.inputTilemap = inputTilemap;
        this.terrainTilemap = terrainTilemap;
        this.clockTilemap = clockTilemap;
        this.diagramTiles = diagramTiles;
        this.floorTile = floorTile;
        this.ceilingTile = ceilingTile;
        this.wallTile = wallTile;
        this.flipCeilingY = flipCeilingY;
        this.startX = startX;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Clears all tiles from input, clock, and terrain tilemaps.
    /// </summary>
    public void ClearAllTilemaps()
    {
        inputTilemap?.ClearAllTiles();
        clockTilemap?.ClearAllTiles();
        terrainTilemap?.ClearAllTiles();
    }

    /// <summary>
    /// Renders a signal line diagram on the input tilemap at the specified row and colors it.
    /// </summary>
    /// <param name="signal">Boolean array representing signal waveform over time.</param>
    /// <param name="yRow">Y tile row index.</param>
    /// <param name="color">Color tint to apply to signal tiles.</param>
    public void RenderDiagram(bool[] signal, int yRow, Color color)
    {
        if (inputTilemap == null || signal == null) return;

        var pattern = new int[signal.Length];
        for (int i = 0; i < signal.Length; i++)
        {
            pattern[i] = signal[i] ? 1 : 0;
        }

        DrawPattern(inputTilemap, pattern, yRow, startX);
        ColorRow(inputTilemap, pattern.Length, yRow, startX, color);
    }

    /// <summary>
    /// Renders the clock signal pattern on the clock tilemap.
    /// </summary>
    /// <param name="clockPattern">Integer array representing clock high/low pattern.</param>
    /// <param name="yRow">Y tile row index.</param>
    /// <param name="clockColor">Color tint for clock tiles.</param>
    public void RenderClock(int[] clockPattern, int yRow, Color clockColor)
    {
        if (clockTilemap == null || clockPattern == null) return;

        DrawPattern(clockTilemap, clockPattern, yRow, startX);
        ColorRow(clockTilemap, clockPattern.Length, yRow, startX, clockColor);
    }

    /// <summary>
    /// Renders floor and ceiling bands on the terrain tilemap based on boolean layout arrays.
    /// </summary>
    /// <param name="floorBand">Boolean array defining floor tile presence.</param>
    /// <param name="ceilingBand">Boolean array defining ceiling tile presence.</param>
    /// <param name="floorYRow">Y tile row index for floor.</param>
    /// <param name="ceilingYRow">Y tile row index for ceiling.</param>
    /// <param name="extendRight">Number of tiles to extend terrain past the end of signals.</param>
    public void RenderTerrain(bool[] floorBand, bool[] ceilingBand, int floorYRow, int ceilingYRow, int extendRight = 3)
    {
        if (terrainTilemap == null) return;

        if (floorBand != null && floorTile != null)
        {
            RenderBand(floorBand, floorYRow, floorTile, false, extendRight);
        }

        if (ceilingBand != null && ceilingTile != null)
        {
            RenderBand(ceilingBand, ceilingYRow, ceilingTile, flipCeilingY, extendRight);
        }
    }

    /// <summary>
    /// Places static scenery elements including the left boundary wall and complete floor/ceiling tiles
    /// filling the gap to the left of <see cref="startX"/> without being affected by JSON arrays.
    /// </summary>
    /// <param name="floorYRow">Y tile row index for floor.</param>
    /// <param name="ceilingYRow">Y tile row index for ceiling.</param>
    public void CompleteStaticScenery(int floorYRow, int ceilingYRow)
    {
        if (terrainTilemap == null) return;

        int leftFillStartX = Mathf.Min(-2, startX - 2);
        int xWall = leftFillStartX - 1;

        if (wallTile != null)
        {
            int yMin = Mathf.Min(floorYRow, ceilingYRow);
            int yMax = Mathf.Max(floorYRow, ceilingYRow);
            for (int y = yMin; y <= yMax; y++)
            {
                SetTileWithFlip(terrainTilemap, new Vector3Int(xWall, y, 0), wallTile, false);
            }
        }

        for (int x = leftFillStartX; x < startX; x++)
        {
            if (floorTile != null)
            {
                SetTileWithFlip(terrainTilemap, new Vector3Int(x, floorYRow, 0), floorTile, false);
            }

            if (ceilingTile != null)
            {
                SetTileWithFlip(terrainTilemap, new Vector3Int(x, ceilingYRow, 0), ceilingTile, flipCeilingY);
            }
        }
    }

    #endregion

    #region Private Rendering Helpers

    /// <summary>
    /// Renders a boolean band row at the given Y position with optional trailing tile extension.
    /// </summary>
    private void RenderBand(bool[] band, int yRow, TileBase tile, bool flipY, int extendRight)
    {
        if (band == null || tile == null) return;

        for (int i = 0; i < band.Length; i++)
        {
            if (!band[i]) continue;
            var pos = new Vector3Int(startX + i, yRow, 0);
            SetTileWithFlip(terrainTilemap, pos, tile, flipY);
        }

        if (extendRight > 0)
        {
            int lastIdx = -1;
            for (int i = band.Length - 1; i >= 0; i--)
            {
                if (band[i])
                {
                    lastIdx = i;
                    break;
                }
            }

            if (lastIdx >= 0)
            {
                for (int off = 1; off <= extendRight; off++)
                {
                    var posExt = new Vector3Int(startX + lastIdx + off, yRow, 0);
                    SetTileWithFlip(terrainTilemap, posExt, tile, flipY);
                }
            }
        }
    }

    /// <summary>
    /// Draws pattern tiles using bitwise neighbor lookup (prev, curr, next) into <see cref="diagramTiles"/>.
    /// </summary>
    private void DrawPattern(Tilemap map, int[] pattern, int yRow, int xStart)
    {
        if (map == null || pattern == null || pattern.Length == 0) return;

        for (int i = 0; i < pattern.Length; i++)
        {
            int curr = pattern[i];
            int prev = (i > 0) ? pattern[i - 1] : curr;
            int next = (i < pattern.Length - 1) ? pattern[i + 1] : curr;
            int idx = ((prev != 0 ? 1 : 0) << 2) | ((curr != 0 ? 1 : 0) << 1) | (next != 0 ? 1 : 0);
            var tile = SafeTile(idx);
            if (tile != null)
            {
                map.SetTile(new Vector3Int(xStart + i, yRow, 0), tile);
            }
        }
    }

    /// <summary>
    /// Sets a tile on a target tilemap with optional vertical flipping transformation matrix.
    /// </summary>
    private void SetTileWithFlip(Tilemap targetMap, Vector3Int position, TileBase tile, bool flipY)
    {
        if (targetMap == null || tile == null) return;

        targetMap.SetTile(position, tile);
        if (flipY)
        {
            targetMap.SetTileFlags(position, TileFlags.None);
            targetMap.SetTransformMatrix(position, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1f, -1f, 1f)));
        }
    }

    /// <summary>
    /// Colors a horizontal row of tiles on a tilemap.
    /// </summary>
    private void ColorRow(Tilemap map, int length, int yRow, int baseX, Color color)
    {
        if (map == null || length <= 0) return;

        for (int i = 0; i < length; i++)
        {
            var pos = new Vector3Int(baseX + i, yRow, 0);
            map.SetTileFlags(pos, TileFlags.None);
            map.SetColor(pos, color);
        }
    }

    /// <summary>
    /// Safely retrieves a tile from <see cref="diagramTiles"/> by index.
    /// </summary>
    private TileBase SafeTile(int idx)
    {
        if (diagramTiles == null || idx < 0 || idx >= diagramTiles.Length)
        {
            return null;
        }
        return diagramTiles[idx];
    }

    #endregion
}
