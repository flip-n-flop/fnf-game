using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Records and renders the player's output signal trajectory as line points using a <see cref="LineRenderer"/>.
/// Interacts with <see cref="PlayerController"/> to track gravity state and position updates.
/// </summary>
[RequireComponent(typeof(LineRenderer), typeof(PlayerController))]
public class SignalPath : MonoBehaviour
{
    #region Inspector Fields

    [Header("Path Settings")]
    [Tooltip("Minimum distance the player must move before a new path point is added.")]
    [SerializeField] private float pointSpacing = 0.1f;

    [Tooltip("Minimum world X coordinate where the output signal line can start being drawn.")]
    [SerializeField] private float minDrawX = 4f;

    [Header("World Constraints")]
    [Tooltip("World Y coordinate representing the floor/ground level.")]
    [SerializeField] private float groundY = -2.5f;

    [Tooltip("World Y coordinate representing the ceiling level.")]
    [SerializeField] private float ceilingY = 1.5f;

    #endregion

    #region Properties & Private State

    private LineRenderer lineRenderer;
    private PlayerController playerController;
    private List<Vector3> pathPoints = new List<Vector3>();
    private Vector3 lastPointPosition;
    private bool isDrawing = true;
    private bool lastGravityInverted;

    /// <summary>
    /// Read-only access to the list of recorded path points.
    /// </summary>
    public List<Vector3> PathPoints => pathPoints;

    /// <summary>
    /// Minimum world X coordinate where the output signal line begins.
    /// </summary>
    public float MinDrawX => minDrawX;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Initializes required components.
    /// Interacts with <see cref="LineRenderer"/> and <see cref="PlayerController"/>.
    /// </summary>
    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        playerController = GetComponent<PlayerController>();
    }

    /// <summary>
    /// Initializes path data at level start.
    /// </summary>
    private void Start()
    {
        InitializePath();
    }

    /// <summary>
    /// Updates line recording based on player X position and gravity inversion changes.
    /// Interacts with <see cref="PlayerController"/>.
    /// </summary>
    private void Update()
    {
        if (!isDrawing) return;

        float currentX = transform.position.x;

        if (currentX < minDrawX)
        {
            ResetPath();
            return;
        }

        float targetY = playerController.IsGravityInverted ? ceilingY : groundY;

        if (pathPoints.Count == 0)
        {
            lastGravityInverted = playerController.IsGravityInverted;
            AddPointToPath(new Vector3(minDrawX, targetY, 0f));
        }

        bool gravityChanged = playerController.IsGravityInverted != lastGravityInverted;
        if (gravityChanged)
        {
            lastGravityInverted = playerController.IsGravityInverted;
            float oldY = playerController.IsGravityInverted ? groundY : ceilingY;

            RemoveVerticalSegmentsAtX(currentX);

            AddPointToPath(new Vector3(currentX, oldY, 0));
            AddPointToPath(new Vector3(currentX, targetY, 0));
            return;
        }

        Vector3 currentTargetPosition = new Vector3(currentX, targetY, 0);

        if (currentX < lastPointPosition.x)
        {
            RemovePointsAfter(currentX);
        }

        if (Vector3.Distance(currentTargetPosition, lastPointPosition) > pointSpacing)
        {
            AddPointToPath(currentTargetPosition);
        }
    }

    #endregion

    #region Path Generation & Recording

    /// <summary>
    /// Sets initial path state starting at <see cref="minDrawX"/>.
    /// Interacts with <see cref="PlayerController.IsGravityInverted"/>.
    /// </summary>
    private void InitializePath()
    {
        pathPoints.Clear();
        lineRenderer.positionCount = 0;
        lastGravityInverted = playerController.IsGravityInverted;
        float startY = lastGravityInverted ? ceilingY : groundY;
        lastPointPosition = new Vector3(minDrawX, startY, 0f);

        if (transform.position.x >= minDrawX)
        {
            AddPointToPath(new Vector3(minDrawX, startY, 0f));
            if (transform.position.x > minDrawX + pointSpacing)
            {
                AddPointToPath(new Vector3(transform.position.x, startY, 0f));
            }
        }
    }

    /// <summary>
    /// Clears recorded path points to append a new point and updates the <see cref="LineRenderer"/>.
    /// </summary>
    /// <param name="point">World coordinates of new point.</param>
    private void AddPointToPath(Vector3 point)
    {
        pathPoints.Add(point);
        lastPointPosition = point;
        lineRenderer.positionCount = pathPoints.Count;
        lineRenderer.SetPosition(pathPoints.Count - 1, point);
    }

    /// <summary>
    /// Trims path points if the player moves backward past previously recorded X coordinates.
    /// </summary>
    /// <param name="currentX">Current player X position threshold.</param>
    private void RemovePointsAfter(float currentX)
    {
        int removalIndex = pathPoints.FindIndex(p => p.x > currentX);

        if (removalIndex != -1)
        {
            int removeCount = pathPoints.Count - removalIndex;
            pathPoints.RemoveRange(removalIndex, removeCount);
            lineRenderer.positionCount = pathPoints.Count;

            if (pathPoints.Count > 0)
            {
                lastPointPosition = pathPoints[pathPoints.Count - 1];
            }
            else
            {
                InitializePath();
            }
        }
    }

    /// <summary>
    /// Removes all points that form vertical segments at the specified X coordinate while keeping the preceding horizontal anchor.
    /// </summary>
    /// <param name="x">Target X coordinate.</param>
    private void RemoveVerticalSegmentsAtX(float x)
    {
        int firstAtX = pathPoints.FindIndex(p => Mathf.Abs(p.x - x) <= 0.01f);
        if (firstAtX < 0) return;

        int removeCount = pathPoints.Count - firstAtX;
        pathPoints.RemoveRange(firstAtX, removeCount);
        lineRenderer.positionCount = pathPoints.Count;

        if (pathPoints.Count > 0)
            lastPointPosition = pathPoints[pathPoints.Count - 1];
        else
            InitializePath();
    }

    #endregion

    #region Path Finalization & Reset

    /// <summary>
    /// Clears the path completely and resets state when player retreats to or behind X <= 0.
    /// Interacts with <see cref="PlayerController.IsGravityInverted"/>.
    /// </summary>
    private void ResetPath()
    {
        pathPoints.Clear();
        lineRenderer.positionCount = 0;
        lastGravityInverted = playerController.IsGravityInverted;
        float startY = lastGravityInverted ? ceilingY : groundY;
        lastPointPosition = new Vector3(minDrawX, startY, 0);
    }

    /// <summary>
    /// Extends the final point of the recorded path to the specified end boundary X coordinate.
    /// Called when level finishes or player dies.
    /// </summary>
    /// <param name="finalX">Ending X coordinate limit.</param>
    public void FinalizePath(float finalX)
    {
        if (pathPoints.Count == 0) return;

        Vector3 lastPoint = pathPoints[pathPoints.Count - 1];
        Vector3 finalPoint = new Vector3(finalX, lastPoint.y, 0);
        AddPointToPath(finalPoint);
    }

    #endregion

    #region Visual Styling

    /// <summary>
    /// Changes line trail color to a solid single color.
    /// Interacts with <see cref="LineRenderer"/>.
    /// </summary>
    /// <param name="newColor">Solid color to apply.</param>
    public void SetTrailColor(Color newColor)
    {
        if (lineRenderer == null) return;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(newColor, 0.0f), new GradientColorKey(newColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
        );
        lineRenderer.colorGradient = gradient;
    }

    /// <summary>
    /// Changes line trail color to a custom gradient.
    /// Interacts with <see cref="LineRenderer"/>.
    /// </summary>
    /// <param name="newGradient">Color gradient to apply.</param>
    public void SetTrailColor(Gradient newGradient)
    {
        if (lineRenderer != null)
        {
            lineRenderer.colorGradient = newGradient;
        }
    }

    #endregion
}