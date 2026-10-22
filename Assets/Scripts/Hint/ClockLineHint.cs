using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders vertical hint lines across the level grid for clock cycles and asynchronous signal transitions.
/// Interacts with <see cref="LevelManager"/> for level boundary limits and <see cref="LevelJsonLoader"/> for async signal definitions.
/// </summary>
public class ClockLineHint : MonoBehaviour
{
    #region Enums

    /// <summary>
    /// Defines the active visual display mode for grid hint lines.
    /// </summary>
    public enum HintMode
    {
        Off,
        ClockOnly,
        ClockAndAsync
    }

    #endregion

    #region Serialized Fields

    [Header("Line Appearance")]
    [Tooltip("Vertical height of rendered hint lines.")]
    [SerializeField] private float lineLength = 10f;

    [Tooltip("Width of individual line segments.")]
    [SerializeField] private float lineWidth = 0.1f;

    [Header("Dashed Line Settings")]
    [Tooltip("LineRenderer prefab instantiated to form dashed segments.")]
    [SerializeField] private LineRenderer linePrefab;

    [Tooltip("Length of drawn dash segments.")]
    [SerializeField] private float dashLength = 0.15f;

    [Tooltip("Gap distance between dash segments.")]
    [SerializeField] private float dashGap = 0.1f;

    [Header("Clock Lines")]
    [Tooltip("Horizontal interval distance between adjacent clock cycle lines.")]
    [SerializeField] private float clockStep = 6f;

    [Tooltip("Display color for standard clock lines.")]
    [SerializeField] private Color clockLineColor = new Color(0f, 0.9f, 1f, 0.6f);

    [Header("Hint Mode")]
    [Tooltip("Currently selected hint display mode.")]
    [SerializeField] private HintMode hintMode = HintMode.ClockOnly;

    [Header("Async Transition Hints")]
    [Tooltip("Display color for asynchronous transition hint lines.")]
    [SerializeField] private Color asyncHintColor = new Color(1f, 0.5f, 0f, 0.6f);

    [Tooltip("Reference to the LevelJsonLoader for reading asynchronous signal state arrays.")]
    [SerializeField] private LevelJsonLoader levelJsonLoader;

    [Header("Rendering Optimization")]
    [Tooltip("Number of off-screen clock intervals to buffer before and after the camera position.")]
    [SerializeField] private int bufferLines = 5;

    [Header("Gizmos")]
    [Tooltip("Whether to draw clock line gizmos in the Unity Editor Scene view.")]
    [SerializeField] private bool drawClockGizmos = true;

    [Tooltip("Whether to draw async transition gizmos in the Unity Editor Scene view.")]
    [SerializeField] private bool drawAsyncGizmos = true;

    [Tooltip("Interval spacing for numerical labels in Scene view gizmos (0 disables labels).")]
    [SerializeField] private int labelEveryN = 0;

    #endregion

    #region Private Fields

    /// <summary>
    /// Cached main camera transform.
    /// </summary>
    private Transform cameraTransform;

    /// <summary>
    /// Camera X position during the previous line regeneration update.
    /// </summary>
    private float lastCameraX;

    /// <summary>
    /// Active line parent GameObjects managed by this controller.
    /// </summary>
    private readonly List<GameObject> activeLines = new List<GameObject>();

    /// <summary>
    /// Tracked hint mode state to detect changes.
    /// </summary>
    private HintMode lastHintMode;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Initializes camera tracking, default hint mode based on async signals, and generates initial lines.
    /// Interacts with <see cref="Camera.main"/> and <see cref="LevelJsonLoader"/>.
    /// </summary>
    private void Start()
    {
        if (Camera.main != null)
            cameraTransform = Camera.main.transform;

        lastCameraX = cameraTransform != null ? cameraTransform.position.x : 0f;

        hintMode = HintMode.Off;

        lastHintMode = hintMode;
        GenerateVisibleLines();
    }

    /// <summary>
    /// Monitors camera movement and hint mode changes to trigger line regeneration when necessary.
    /// </summary>
    private void Update()
    {
        bool cameraMovedEnough = Mathf.Abs(cameraTransform.position.x - lastCameraX) >= clockStep;
        bool modeChanged = hintMode != lastHintMode;

        if (cameraMovedEnough) lastCameraX = cameraTransform.position.x;
        if (modeChanged) lastHintMode = hintMode;
        if (cameraMovedEnough || modeChanged) GenerateVisibleLines();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Cycles through hint modes: Off -> ClockOnly -> ClockAndAsync (if async signals exist) -> Off.
    /// Interacts with <see cref="LevelJsonLoader"/> via <see cref="HasAsyncSignals"/>.
    /// </summary>
    public void ToggleHintMode()
    {
        bool hasAsync = HasAsyncSignals();

        hintMode = hintMode switch
        {
            HintMode.Off => HintMode.ClockOnly,
            HintMode.ClockOnly => hasAsync ? HintMode.ClockAndAsync : HintMode.Off,
            HintMode.ClockAndAsync => HintMode.Off,
            _ => HintMode.ClockOnly
        };

        Debug.Log($"<color=cyan>[ClockLineHint] Hint mode: {hintMode}</color>");
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Clears and regenerates all visible hint lines based on the current hint mode and camera position.
    /// Interacts with <see cref="LevelManager.Instance"/> for level boundaries.
    /// </summary>
    private void GenerateVisibleLines()
    {
        ClearAllLines();
        if (hintMode == HintMode.Off) return;

        float cameraX = cameraTransform.position.x;
        float levelEndX = LevelManager.Instance != null ? LevelManager.Instance.levelEndX : 100f;

        GenerateClockLines(cameraX, levelEndX);

        if (hintMode == HintMode.ClockAndAsync)
            GenerateAsyncHintLines(cameraX, levelEndX);
    }

    /// <summary>
    /// Generates dashed vertical lines at each clock edge within the visible range.
    /// </summary>
    /// <param name="cameraX">Current horizontal camera position.</param>
    /// <param name="levelEndX">Right limit boundary of the current level.</param>
    private void GenerateClockLines(float cameraX, float levelEndX)
    {
        float startXOffset = levelJsonLoader != null ? levelJsonLoader.StartX : 0f;
        float halfSpan = bufferLines * clockStep;
        float leftEdge = cameraX - halfSpan;
        float rightEdge = Mathf.Min(cameraX + halfSpan, levelEndX);
        int startMultiple = Mathf.Max(1, Mathf.FloorToInt((leftEdge - startXOffset) / clockStep));
        int endMultiple = Mathf.FloorToInt((rightEdge - startXOffset) / clockStep);

        for (int i = startMultiple; i <= endMultiple; i++)
        {
            float xPos = startXOffset + (i * clockStep);
            if (xPos > levelEndX) break;
            CreateDashedLine(xPos, clockLineColor, $"ClockLine_{i}");
        }
    }

    /// <summary>
    /// Generates dashed vertical lines at async preset/clear transition positions within the visible range.
    /// Interacts with <see cref="LevelJsonLoader"/> to inspect signal transition indices.
    /// </summary>
    /// <param name="cameraX">Current horizontal camera position.</param>
    /// <param name="levelEndX">Right limit boundary of the current level.</param>
    private void GenerateAsyncHintLines(float cameraX, float levelEndX)
    {
        if (levelJsonLoader == null) return;

        var presetSignal = levelJsonLoader.ParsedPresetSignal;
        var clearSignal = levelJsonLoader.ParsedClearSignal;
        if (presetSignal == null && clearSignal == null) return;

        float startXOffset = levelJsonLoader.StartX;
        float halfSpan = bufferLines * clockStep;
        float leftEdge = cameraX - halfSpan;
        float rightEdge = Mathf.Min(cameraX + halfSpan, levelEndX);
        int maxLength = Mathf.Max(
            presetSignal != null ? presetSignal.Length : 0,
            clearSignal != null ? clearSignal.Length : 0);

        for (int i = 0; i < maxLength; i++)
        {
            bool presetTransition = presetSignal != null && i < presetSignal.Length &&
                                    presetSignal[i] != (i > 0 ? presetSignal[i - 1] : false);
            bool clearTransition = clearSignal != null && i < clearSignal.Length &&
                                    clearSignal[i] != (i > 0 ? clearSignal[i - 1] : false);

            if (!presetTransition && !clearTransition) continue;

            float xPos = startXOffset + i + 0.5f;
            if (xPos >= leftEdge && xPos <= rightEdge && xPos <= levelEndX)
            {
                string label = presetTransition && clearTransition ? "Preset+Clear"
                             : presetTransition ? "Preset" : "Clear";
                CreateDashedLine(xPos, asyncHintColor, $"AsyncHint_{i}_{label}");
            }
        }
    }

    /// <summary>
    /// Creates a dashed vertical line at the given X position by spawning multiple solid dash segments under a shared parent.
    /// </summary>
    /// <param name="xPos">World X position for line alignment.</param>
    /// <param name="color">Color assigned to line segment renderers.</param>
    /// <param name="name">Name string assigned to the created root GameObject.</param>
    private void CreateDashedLine(float xPos, Color color, string name)
    {
        GameObject lineRoot = new GameObject(name);
        lineRoot.transform.SetParent(transform);
        activeLines.Add(lineRoot);

        Vector3 start = new Vector3(xPos, -lineLength / 2f, 0);
        Vector3 end = new Vector3(xPos, lineLength / 2f, 0);

        float length = Vector3.Distance(start, end);
        Vector3 direction = (end - start).normalized;
        float traveled = 0f;
        float accumulator = 0f;
        bool isDrawingDash = true;

        while (traveled < length - 0.001f)
        {
            float target = isDrawingDash ? dashLength : dashGap;
            float step = Mathf.Min(target - accumulator, length - traveled);

            if (isDrawingDash)
            {
                Vector3 segStart = start + direction * traveled;
                Vector3 segEnd = segStart + direction * step;
                DrawDashSegment(segStart, segEnd, color, lineRoot.transform);
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

    /// <summary>
    /// Instantiates a single solid LineRenderer segment between two points.
    /// </summary>
    /// <param name="start">Start coordinate of the segment.</param>
    /// <param name="end">End coordinate of the segment.</param>
    /// <param name="color">Segment color.</param>
    /// <param name="parent">Parent transform container.</param>
    private void DrawDashSegment(Vector3 start, Vector3 end, Color color, Transform parent)
    {
        LineRenderer lr = Instantiate(linePrefab, parent);
        lr.positionCount = 2;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = color;
        lr.endColor = color;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }

    /// <summary>
    /// Destroys all currently active hint line GameObjects.
    /// </summary>
    private void ClearAllLines()
    {
        foreach (var line in activeLines)
            if (line != null) Destroy(line);
        activeLines.Clear();
    }

    /// <summary>
    /// Returns true if any preset or clear signal contains at least one transition.
    /// Interacts with <see cref="LevelJsonLoader"/>.
    /// </summary>
    /// <returns>True if transition events are present, false otherwise.</returns>
    private bool HasAsyncSignals()
    {
        if (levelJsonLoader == null) return false;

        var presetSignal = levelJsonLoader.ParsedPresetSignal;
        var clearSignal = levelJsonLoader.ParsedClearSignal;

        if (presetSignal != null)
            for (int i = 1; i < presetSignal.Length; i++)
                if (presetSignal[i] != presetSignal[i - 1]) return true;

        if (clearSignal != null)
            for (int i = 1; i < clearSignal.Length; i++)
                if (clearSignal[i] != clearSignal[i - 1]) return true;

        return false;
    }

    #endregion

    #region Gizmos

    /// <summary>
    /// Draws Scene view gizmos for visual debugging.
    /// </summary>
    private void OnDrawGizmos()
    {
        DrawClockLineGizmos();
        DrawAsyncHintGizmos();
    }

    /// <summary>
    /// Draws clock edge lines in the Scene view for level design reference.
    /// Interacts with <see cref="LevelManager"/>.
    /// </summary>
    private void DrawClockLineGizmos()
    {
        if (!drawClockGizmos) return;
        LevelManager lm = Application.isPlaying ? LevelManager.Instance : FindAnyObjectByType<LevelManager>();
        if (lm == null || clockStep <= 0f) return;

        float startXOffset = levelJsonLoader != null ? levelJsonLoader.StartX : 0f;
        int count = Mathf.FloorToInt((lm.levelEndX - startXOffset) / clockStep);
        Gizmos.color = clockLineColor;

        for (int i = 1; i <= count; i++)
        {
            float xPos = startXOffset + (i * clockStep);
            Vector3 p0 = new Vector3(xPos, -lineLength / 2f, 0f);
            Vector3 p1 = new Vector3(xPos, lineLength / 2f, 0f);
            Gizmos.DrawLine(p0, p1);
#if UNITY_EDITOR
            if (labelEveryN > 0 && i % labelEveryN == 0)
            {
                UnityEditor.Handles.color = clockLineColor;
                UnityEditor.Handles.Label(p1 + Vector3.up * 0.2f, i.ToString());
            }
#endif
        }
    }

    /// <summary>
    /// Draws async transition positions in the Scene view for level design reference.
    /// Interacts with <see cref="LevelJsonLoader"/>.
    /// </summary>
    private void DrawAsyncHintGizmos()
    {
        if (!drawAsyncGizmos || levelJsonLoader == null) return;

        var presetSignal = levelJsonLoader.ParsedPresetSignal;
        var clearSignal = levelJsonLoader.ParsedClearSignal;
        if (presetSignal == null && clearSignal == null) return;

        float startXOffset = levelJsonLoader.StartX;
        int maxLength = Mathf.Max(
            presetSignal != null ? presetSignal.Length : 0,
            clearSignal != null ? clearSignal.Length : 0);

        Gizmos.color = asyncHintColor;

        for (int i = 0; i < maxLength; i++)
        {
            bool presetTransition = presetSignal != null && i < presetSignal.Length &&
                                    presetSignal[i] != (i > 0 ? presetSignal[i - 1] : false);
            bool clearTransition = clearSignal != null && i < clearSignal.Length &&
                                    clearSignal[i] != (i > 0 ? clearSignal[i - 1] : false);

            if (!presetTransition && !clearTransition) continue;

            float xPos = startXOffset + i + 0.5f;
            Vector3 p0 = new Vector3(xPos, -lineLength / 2f, 0f);
            Vector3 p1 = new Vector3(xPos, lineLength / 2f, 0f);
            Gizmos.DrawLine(p0, p1);
#if UNITY_EDITOR
            if (labelEveryN > 0)
            {
                string label = presetTransition && clearTransition ? "P+C"
                             : presetTransition ? "P" : "C";
                UnityEditor.Handles.color = asyncHintColor;
                UnityEditor.Handles.Label(p1 + Vector3.up * 0.2f, $"{i}.5 {label}");
            }
#endif
        }
    }

    #endregion
}