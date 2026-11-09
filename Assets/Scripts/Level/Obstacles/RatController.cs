using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Controls the sewer rat enemy that waits at its spawn position and runs left across the screen
/// when the player approaches within <see cref="activationDistanceX"/>.
/// Configured dynamically by <see cref="ObstacleSpawner"/> during level loading.
/// </summary>
public class RatController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Rat Settings")]
    [Tooltip("Horizontal running speed to the left (world units per second).")]
    public float speed = 7.0f;

    [Tooltip("Horizontal distance (world units) ahead of the player at which the rat starts running.")]
    public float activationDistanceX = 16.0f;

    #endregion

    #region Private State

    private Vector3 startPos;
    private bool isRunning;
    private Transform playerTarget;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        startPos = transform.position;
        ResolveTargetIfNeeded();

        isRunning = false;
        if (animator != null)
            animator.enabled = false;
    }

    private void Update()
    {
        if (speed <= 0f) return;

        if (!isRunning)
        {
            if (CheckActivation())
            {
                isRunning = true;
                if (animator != null)
                    animator.enabled = true;
            }
            else
            {
                return;
            }
        }

        Vector3 pos = transform.position;
        pos.x -= speed * Time.deltaTime;
        transform.position = pos;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Configures the rat's spawn position and speed from level data via <see cref="ObstacleSpawner"/>.
    /// </summary>
    public void ApplyObstacleData(
        int startTileX,
        int startTileY,
        float speedUnits,
        int globalStartX,
        int floorYRow,
        bool yRelativeToFloor,
        Tilemap floorTilemap)
    {
        if (speedUnits > 0f)
            speed = speedUnits;

        int cellX = globalStartX + startTileX;
        int cellY = yRelativeToFloor ? (floorYRow + startTileY) : startTileY;
        var cell = new Vector3Int(cellX, cellY, 0);
        Vector3 worldPos = (floorTilemap != null)
            ? floorTilemap.GetCellCenterWorld(cell)
            : new Vector3(cellX, cellY, 0f);

        float cellHeight = (floorTilemap != null && floorTilemap.layoutGrid != null)
            ? Mathf.Abs(floorTilemap.layoutGrid.cellSize.y)
            : 1f;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        float halfHeight = (spriteRenderer != null && spriteRenderer.sprite != null)
            ? spriteRenderer.bounds.extents.y
            : 0f;

        if (halfHeight > 0f)
        {
            float floorSurfaceY = worldPos.y - (cellHeight * 0.5f);
            worldPos.y = floorSurfaceY + halfHeight;
        }

        transform.position = worldPos;
        startPos = worldPos;
    }

    #endregion

    #region Internal Logic

    private void ResolveTargetIfNeeded()
    {
        if (playerTarget != null) return;

        var player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            playerTarget = player.transform;
            return;
        }

        if (Camera.main != null)
        {
            playerTarget = Camera.main.transform;
        }
    }

    private bool CheckActivation()
    {
        if (activationDistanceX <= 0f) return true;

        ResolveTargetIfNeeded();
        if (playerTarget == null) return true;

        float triggerX = startPos.x - activationDistanceX;
        return playerTarget.position.x >= triggerX;
    }

    #endregion
}
