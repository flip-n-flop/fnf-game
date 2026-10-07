using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates the player path verification pipeline and draws visual feedback (solid and dashed lines).
/// Interacts with <see cref="GabaritoGenerator"/> to produce reference corners, <see cref="PathChecker"/> to evaluate accuracy,
/// <see cref="SignalPath"/> for recorded player coordinates, <see cref="SignalColorManager"/> for color synchronization,
/// <see cref="LevelManager"/> for stage boundaries, <see cref="ScoreController"/> for reporting scores,
/// and <see cref="ResultScreenController"/> to present final results.
/// </summary>
public class PathVerifier : MonoBehaviour
{
    #region Data Structures

    /// <summary>
    /// Represents a flip-flop output transition event at a specific X world coordinate.
    /// </summary>
    public struct SignalEvent
    {
        /// <summary>
        /// World X position where the signal transition occurs.
        /// </summary>
        public float x;

        /// <summary>
        /// Logical boolean value of the output signal after the transition.
        /// </summary>
        public bool value;

        /// <summary>
        /// Initializes a new instance of the <see cref="SignalEvent"/> struct.
        /// </summary>
        /// <param name="x">World X coordinate.</param>
        /// <param name="value">Logical output state.</param>
        public SignalEvent(float x, bool value) { this.x = x; this.value = value; }
    }

    #endregion

    #region Fields & Serialized Properties

    [Header("Output Settings")]
    [Tooltip("World Y position for logical LOW state (0).")]
    [SerializeField] private float lowY = -2.5f;

    [Tooltip("World Y position for logical HIGH state (1).")]
    [SerializeField] private float highY = 1.25f;

    [Header("Corner Answer Key (Auto-Generated)")]
    [SerializeField] private List<Vector3> correctCorners;

    [Header("References")]
    [SerializeField] private SignalPath signalPath;
    [SerializeField] private float cornerTolerance = 1.0f;

    /// <summary>
    /// Gets the active corner tolerance distance in world units.
    /// </summary>
    public float CornerTolerance => cornerTolerance;

    [Header("Tolerance Visual Guide")]
    [Tooltip("SpriteRenderer of the visual tolerance guide line (child of Player).")]
    [SerializeField] private SpriteRenderer toleranceGuide;

    [Header("Visual Feedback")]
    [Tooltip("Line color when the path segment is correct.")]
    [SerializeField] private Color successColor = Color.green;

    [Tooltip("Line color when the path segment is incorrect.")]
    [SerializeField] private Color failureColor = Color.red;

    [Tooltip("LineRenderer prefab instantiated for each feedback segment.")]
    [SerializeField] private LineRenderer linePrefab;

    [Tooltip("Parent Transform that groups feedback line GameObjects.")]
    [SerializeField] private Transform feedbackLinesParent;

    [Header("Dashed Line Settings")]
    [SerializeField] private float dashLength = 0.15f;
    [SerializeField] private float dashGap = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showMissedCornersInGame = false;
    [SerializeField] private bool realtimeFeedback = false;

    private GabaritoGenerator gabaritoGenerator;
    private PathChecker pathChecker;
    private List<Vector3> missedCorners = new();
    private LineRenderer signalLineRenderer;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Instantiates the reference generator and path checker, generating initial reference corners.
    /// Interacts with <see cref="GabaritoGenerator"/> and <see cref="PathChecker"/>.
    /// </summary>
    private void Awake()
    {
        gabaritoGenerator = new GabaritoGenerator(lowY, highY);
        gabaritoGenerator.Generate();
        correctCorners = gabaritoGenerator.CorrectCorners;

        pathChecker = new PathChecker(cornerTolerance, enableDebugLogs);
    }

    /// <summary>
    /// Subscribes to color changes from <see cref="SignalColorManager"/> and caches the LineRenderer component on <see cref="SignalPath"/>.
    /// </summary>
    private void Start()
    {
        if (signalPath != null)
            signalLineRenderer = signalPath.GetComponent<LineRenderer>();

        SignalColorManager.OnColorsChanged += SyncFeedbackColors;
        SyncFeedbackColors();
        UpdateToleranceGuide();
    }

    /// <summary>
    /// Configures the horizontal scale of the tolerance guide line to match double the corner tolerance.
    /// Interacts with <see cref="toleranceGuide"/> to adjust local scale.
    /// </summary>
    private void UpdateToleranceGuide()
    {
        if (toleranceGuide == null) return;

        Vector3 scale = toleranceGuide.transform.localScale;
        scale.x = cornerTolerance * 2f;
        toleranceGuide.transform.localScale = scale;
    }

    /// <summary>
    /// Unsubscribes from <see cref="SignalColorManager"/> events.
    /// </summary>
    private void OnDestroy()
    {
        SignalColorManager.OnColorsChanged -= SyncFeedbackColors;
    }

    /// <summary>
    /// Continuously updates realtime visual feedback if enabled.
    /// Interacts with <see cref="SignalPath"/>.
    /// </summary>
    private void Update()
    {
        if (!realtimeFeedback ||
            signalPath == null ||
            correctCorners == null ||
            signalPath.PathPoints.Count < 2)
            return;

        DrawFeedback(signalPath.PathPoints);
    }

    /// <summary>
    /// Draws gizmos in the Scene view visualizing reference corners and missed corners.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (correctCorners == null || correctCorners.Count < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < correctCorners.Count - 1; i++)
        {
            Gizmos.DrawSphere(correctCorners[i], 0.2f);
            Gizmos.DrawLine(correctCorners[i], correctCorners[i + 1]);
        }
        if (correctCorners.Count > 0)
            Gizmos.DrawSphere(correctCorners[correctCorners.Count - 1], 0.2f);

        if (showMissedCornersInGame && Application.isPlaying && missedCorners?.Count > 0)
        {
            Gizmos.color = Color.red;
            foreach (var corner in missedCorners)
            {
                Gizmos.DrawWireSphere(corner, cornerTolerance);
                Gizmos.DrawSphere(corner, 0.3f);
            }
        }
    }

    #endregion

    #region Public Verification API

    /// <summary>
    /// Finalizes the player's recorded path and runs the full verification pipeline.
    /// Typically invoked by <see cref="PlayerController"/> upon death or level completion.
    /// Interacts with <see cref="SignalPath"/> and <see cref="LevelManager"/>.
    /// </summary>
    /// <param name="endX">Optional right boundary cap for the path. Defaults to <see cref="LevelManager.phaseEndX"/> if null.</param>
    public void FinalizeAndCheckPath(float? endX = null)
    {
        if (signalPath == null)
        {
            Debug.LogError("[PathVerifier] Reference to SignalPath is not set!");
            return;
        }

        float finalX = endX ?? LevelManager.Instance.phaseEndX;
        signalPath.FinalizePath(finalX);

        RunPipeline();
    }

    /// <summary>
    /// Rebuilds the reference answer key corners from external signal events.
    /// Used by testing frameworks or editor utilities.
    /// Interacts with <see cref="GabaritoGenerator"/>.
    /// </summary>
    /// <param name="events">List of output signal transition events.</param>
    /// <param name="initialX">Starting X world coordinate.</param>
    /// <param name="initialState">Initial logical state of the signal.</param>
    public void BuildCorrectCornersFromSignalEvents(
        List<SignalEvent> events,
        float initialX = 0f,
        bool initialState = false)
    {
        gabaritoGenerator.BuildFromEvents(events, initialX, initialState);
        correctCorners = gabaritoGenerator.CorrectCorners;
    }

    #endregion

    #region Verification Pipeline

    /// <summary>
    /// Executes the full verification sequence: evaluates the player path, renders visual feedback, and notifies score and UI controllers.
    /// Interacts with <see cref="PathChecker"/>, <see cref="ScoreController"/>, and <see cref="ResultScreenController"/>.
    /// </summary>
    private void RunPipeline()
    {
        SyncFeedbackColors();

        if (signalPath == null || signalPath.PathPoints.Count < 2)
        {
            Debug.LogError("[PathVerifier] Invalid player path!");
            ResultScreenController.Instance?.Show(false);
            return;
        }

        if (signalLineRenderer != null)
            signalLineRenderer.enabled = false;

        List<Vector3> playerPath = signalPath.PathPoints;

        PathCheckResult result = pathChecker.Evaluate(correctCorners, playerPath);
        missedCorners = new List<Vector3>(result.MissedCorners);

        DrawFeedback(playerPath);

        ScoreController.Instance?.ReportResult(result.CoveredSegments, result.GabaritoTotal, result.IsCorrect);
        ResultScreenController.Instance?.Show(result.IsCorrect);
    }

    #endregion

    #region Feedback Drawing Logic

    /// <summary>
    /// Clears previous feedback line objects and instantiates new line segments (solid for correct, dashed for incorrect).
    /// </summary>
    /// <param name="playerPath">List of recorded player path coordinates.</param>
    private void DrawFeedback(List<Vector3> playerPath)
    {
        if (linePrefab == null || feedbackLinesParent == null) return;

        ClearFeedbackLines();

        float accumulator = 0f;
        bool isDrawingDash = true;

        for (int i = 0; i < playerPath.Count - 1; i++)
        {
            Vector3 segStart = playerPath[i];
            Vector3 segEnd = playerPath[i + 1];

            if (IsSegmentValid(segStart, segEnd))
            {
                DrawSolidLine(segStart, segEnd, successColor);
                accumulator = 0f;
                isDrawingDash = true;
            }
            else
            {
                DrawDashedLine(segStart, segEnd, ref accumulator, ref isDrawingDash);
            }
        }
    }

    /// <summary>
    /// Destroys all instantiated feedback line GameObjects inside the parent container.
    /// </summary>
    private void ClearFeedbackLines()
    {
        foreach (Transform child in feedbackLinesParent)
            Destroy(child.gameObject);
    }

    /// <summary>
    /// Evaluates if a player path segment falls within orthogonal tolerance of any reference answer key segment.
    /// </summary>
    /// <param name="pStart">Segment start point.</param>
    /// <param name="pEnd">Segment end point.</param>
    /// <returns>True if the segment closely aligns with a valid reference segment.</returns>
    private bool IsSegmentValid(Vector3 pStart, Vector3 pEnd)
    {
        if ((pEnd - pStart).sqrMagnitude < 0.0001f) return true;

        for (int i = 0; i < correctCorners.Count - 1; i++)
        {
            Vector3 c1 = correctCorners[i];
            Vector3 c2 = correctCorners[i + 1];
            bool isHorizontal = Mathf.Abs(c1.y - c2.y) <= 0.1f;

            if (isHorizontal)
            {
                if (Mathf.Abs(pStart.y - c1.y) <= cornerTolerance &&
                    Mathf.Abs(pEnd.y - c1.y) <= cornerTolerance)
                {
                    float minX = Mathf.Min(c1.x, c2.x) - 0.1f;
                    float maxX = Mathf.Max(c1.x, c2.x) + 0.1f;
                    if (pStart.x >= minX && pStart.x <= maxX &&
                        pEnd.x >= minX && pEnd.x <= maxX)
                        return true;
                }
            }
            else
            {
                if (Mathf.Abs(pStart.x - c1.x) <= cornerTolerance &&
                    Mathf.Abs(pEnd.x - c1.x) <= cornerTolerance)
                {
                    float minY = Mathf.Min(c1.y, c2.y) - 0.1f;
                    float maxY = Mathf.Max(c1.y, c2.y) + 0.1f;
                    if (pStart.y >= minY && pStart.y <= maxY &&
                        pEnd.y >= minY && pEnd.y <= maxY)
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Instantiates a solid LineRenderer GameObject representing a correct path segment.
    /// </summary>
    /// <param name="start">Start coordinate.</param>
    /// <param name="end">End coordinate.</param>
    /// <param name="color">Segment color.</param>
    private void DrawSolidLine(Vector3 start, Vector3 end, Color color)
    {
        LineRenderer line = Instantiate(linePrefab, feedbackLinesParent);
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startColor = color;
        line.endColor = color;
    }

    /// <summary>
    /// Instantiates dashed LineRenderer segments representing an incorrect path segment.
    /// </summary>
    /// <param name="start">Start coordinate.</param>
    /// <param name="end">End coordinate.</param>
    /// <param name="accumulator">Accumulated distance helper for dash patterns.</param>
    /// <param name="isDrawingDash">Flag toggling dash drawing state.</param>
    private void DrawDashedLine(Vector3 start, Vector3 end, ref float accumulator, ref bool isDrawingDash)
    {
        float length = Vector3.Distance(start, end);
        Vector3 direction = (end - start).normalized;
        float traveled = 0f;

        while (traveled < length - 0.001f)
        {
            float target = isDrawingDash ? dashLength : dashGap;
            float step = Mathf.Min(target - accumulator, length - traveled);

            if (isDrawingDash)
            {
                Vector3 subStart = start + direction * traveled;
                DrawSolidLine(subStart, subStart + direction * step, failureColor);
            }

            traveled += step;
            accumulator += step;

            if (accumulator >= target - 0.001f)
            {
                accumulator = 0f;
                isDrawingDash = !isDrawingDash;
            }
        }
    }

    #endregion

    #region Color Synchronization

    /// <summary>
    /// Synchronizes feedback success and failure colors with <see cref="SignalColorManager"/>.
    /// </summary>
    private void SyncFeedbackColors()
    {
        if (SignalColorManager.Instance == null) return;
        successColor = SignalColorManager.Instance.ColorFeedbackSuccess;
        failureColor = SignalColorManager.Instance.ColorFeedbackFailure;
    }

    #endregion

    #region Debug & Editor Utilities

    /// <summary>
    /// Logs detailed information about the reference answer key corners to the Unity console.
    /// </summary>
    [ContextMenu("Log Gabarito Info")]
    private void LogGabaritoInfo()
    {
        if (correctCorners == null || correctCorners.Count == 0)
        {
            Debug.LogWarning("[PathVerifier] Gabarito list is empty or not generated!");
            return;
        }

        Debug.Log("<color=cyan>===== GABARITO INFO =====</color>");
        Debug.Log($"Total corners: {correctCorners.Count}");
        Debug.Log($"First corner: {correctCorners[0]}");
        Debug.Log($"Last corner: {correctCorners[correctCorners.Count - 1]}");
        Debug.Log($"Tolerance: {cornerTolerance}");

        for (int i = 0; i < correctCorners.Count; i++)
        {
            string level = Mathf.Approximately(correctCorners[i].y, lowY) ? "LOW" : "HIGH";
            Debug.Log($"  Corner #{i}: X={correctCorners[i].x:F2} Y={correctCorners[i].y:F2} ({level})");
        }
    }

    /// <summary>
    /// Toggles debug log output for path evaluation.
    /// </summary>
    [ContextMenu("Toggle Debug Logs")]
    private void ToggleDebugLogs()
    {
        enableDebugLogs = !enableDebugLogs;
        pathChecker = new PathChecker(cornerTolerance, enableDebugLogs);
        Debug.Log($"<color=yellow>[PathVerifier] Debug logs {(enableDebugLogs ? "ENABLED" : "DISABLED")}</color>");
    }

    #endregion
}