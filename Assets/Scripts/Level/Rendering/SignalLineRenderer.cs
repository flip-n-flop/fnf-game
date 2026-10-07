using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders digital waveform signals (J, K, Preset, Clear, Clock) as orthogonal line strips using <see cref="LineRenderer"/>.
/// Automatically calculates uniform vertical spacing between all active signal lines in a scenario based on the total signal count (2 to 5).
/// Interacts with <see cref="LevelJsonLoader"/> for level data and <see cref="SignalLabelRenderer"/> for HUD badge alignment.
/// </summary>
public class SignalLineRenderer : MonoBehaviour
{
    #region Serialized Fields - Dimensions

    [Header("Signal Dimensions")]
    [Tooltip("Height difference between Low (0) and High (1) logic levels in world units.")]
    [SerializeField] private float signalHeight = 1.3f;

    [Tooltip("Thickness of the rendered signal lines in world units.")]
    [SerializeField] private float lineWidth = 0.12f;

    [Tooltip("Material applied to active signal lines. If null, Sprites/Default is used.")]
    [SerializeField] private Material lineMaterial;

    #endregion

    #region Serialized Fields - Layout & Spacing

    [Header("Layout & Spacing")]
    [Tooltip("Base Y coordinate (logic level 0) for the Clock line at the bottom.")]
    [SerializeField] private float clockBaseY = 5.0f;

    [Tooltip("Uniform spacing between consecutive lines when level has 2 signals (Clock + D/T).")]
    [SerializeField] private float spacing2Signals = 3.5f;

    [Tooltip("Uniform spacing between consecutive lines when level has 3 signals (Clock + J + K).")]
    [SerializeField] private float spacing3Signals = 2.6f;

    [Tooltip("Uniform spacing between consecutive lines when level has 4 signals (Clock + J + K + Async).")]
    [SerializeField] private float spacing4Signals = 2.2f;

    [Tooltip("Uniform spacing between consecutive lines when level has 5 signals (Clock + J + K + PRE + CLR).")]
    [SerializeField] private float spacing5Signals = 1.8f;

    #endregion

    #region Serialized Fields - Drop Shadows

    [Header("Drop Shadows")]
    [Tooltip("Whether to render a subtle dark drop shadow beneath each signal waveform.")]
    [SerializeField] private bool enableShadow = true;

    [Tooltip("Positional offset for the shadow line relative to the main line.")]
    [SerializeField] private Vector2 shadowOffset = new Vector2(0.04f, -0.06f);

    [Tooltip("Color tint applied to shadow LineRenderers.")]
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.45f);

    #endregion

    #region Configuration Constants

    private const float INPUT_X_OFFSET = 0.5f;
    private const float CLOCK_X_OFFSET = 0.0f;
    private const float LEAD_IN_LENGTH = 2.0f;
    private const float EXTEND_RIGHT_LENGTH = 4.0f;
    private const string SORTING_LAYER_NAME = "Default";
    private const int MAIN_SORTING_ORDER = 2;
    private const int SHADOW_SORTING_ORDER = 1;

    #endregion

    #region Private State

    private readonly Dictionary<string, LineRenderer> lines = new Dictionary<string, LineRenderer>();
    private static Material defaultSpriteMaterial;

    private float computedJY;
    private float computedKY;
    private float computedPresetY;
    private float computedClearY;
    private float computedClockY;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheExistingLineRenderers();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && lines != null && lines.Count > 0)
        {
            ApplyStyleSettingsToExistingLines();
        }
    }
#endif

    #endregion

    #region Public Rendering API

    /// <summary>
    /// Computes uniform vertical spacing and renders all active digital waveforms and their drop shadows.
    /// Called by <see cref="LevelJsonLoader"/> during level construction.
    /// </summary>
    public void RenderSignals(
        bool[] jSignal,
        bool[] kSignal,
        bool[] presetSignal,
        bool[] clearSignal,
        int[] clockPattern,
        Color jColor,
        Color kColor,
        Color presetColor,
        Color clearColor,
        Color clockColor,
        float baseXOffset = 0f)
    {
        bool hasClock = clockPattern != null && clockPattern.Length > 0;
        bool hasClear = clearSignal != null && clearSignal.Length > 0;
        bool hasPreset = presetSignal != null && presetSignal.Length > 0;
        bool hasK = kSignal != null && kSignal.Length > 0;
        bool hasJ = jSignal != null && jSignal.Length > 0;

        int signalCount = (hasClock ? 1 : 0) +
                          (hasClear ? 1 : 0) +
                          (hasPreset ? 1 : 0) +
                          (hasK ? 1 : 0) +
                          (hasJ ? 1 : 0);

        float spacing = ResolveSpacingForCount(signalCount);

        int slotIndex = 0;

        if (hasClock)
        {
            computedClockY = clockBaseY + (slotIndex * spacing);
            slotIndex++;
        }

        if (hasClear)
        {
            computedClearY = clockBaseY + (slotIndex * spacing);
            slotIndex++;
        }

        if (hasPreset)
        {
            computedPresetY = clockBaseY + (slotIndex * spacing);
            slotIndex++;
        }

        if (hasK)
        {
            computedKY = clockBaseY + (slotIndex * spacing);
            slotIndex++;
        }

        if (hasJ)
        {
            computedJY = clockBaseY + (slotIndex * spacing);
            slotIndex++;
        }

        if (hasClock)
        {
            bool[] clockBool = ConvertPatternToBoolArray(clockPattern);
            RenderChannel("Clock_Line", clockBool, computedClockY, CLOCK_X_OFFSET, baseXOffset, clockColor);
        }
        else
        {
            HideChannel("Clock_Line");
        }

        if (hasClear)
            RenderChannel("Clear_Line", clearSignal, computedClearY, INPUT_X_OFFSET, baseXOffset, clearColor);
        else
            HideChannel("Clear_Line");

        if (hasPreset)
            RenderChannel("Preset_Line", presetSignal, computedPresetY, INPUT_X_OFFSET, baseXOffset, presetColor);
        else
            HideChannel("Preset_Line");

        if (hasK)
            RenderChannel("K_Line", kSignal, computedKY, INPUT_X_OFFSET, baseXOffset, kColor);
        else
            HideChannel("K_Line");

        if (hasJ)
            RenderChannel("J_Line", jSignal, computedJY, INPUT_X_OFFSET, baseXOffset, jColor);
        else
            HideChannel("J_Line");
    }

    /// <summary>
    /// Clears and hides all signal and shadow lines.
    /// </summary>
    public void ClearAllSignals()
    {
        foreach (var kvp in lines)
        {
            if (kvp.Value != null)
            {
                kvp.Value.positionCount = 0;
                kvp.Value.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Updates line colors in real time when modified by settings.
    /// Called by <see cref="LevelJsonLoader"/> responding to <see cref="SignalColorManager.OnColorsChanged"/>.
    /// </summary>
    public void UpdateColors(Color jColor, Color kColor, Color clockColor, Color presetColor, Color clearColor)
    {
        SetLineColor("J_Line", jColor);
        SetLineColor("K_Line", kColor);
        SetLineColor("Clock_Line", clockColor);
        SetLineColor("Preset_Line", presetColor);
        SetLineColor("Clear_Line", clearColor);
    }

    #endregion

    #region Channel Position Queries for HUD

    /// <summary>
    /// Computes world Y center for J (or single-input D/T) signal line.
    /// </summary>
    public float GetJWorldY() => computedJY + (signalHeight / 2f);

    /// <summary>
    /// Computes world Y center for K signal line.
    /// </summary>
    public float GetKWorldY() => computedKY + (signalHeight / 2f);

    /// <summary>
    /// Computes world Y center for Preset signal line.
    /// </summary>
    public float GetPresetWorldY() => computedPresetY + (signalHeight / 2f);

    /// <summary>
    /// Computes world Y center for Clear signal line.
    /// </summary>
    public float GetClearWorldY() => computedClearY + (signalHeight / 2f);

    /// <summary>
    /// Computes world Y center for Clock signal line.
    /// </summary>
    public float GetClockWorldY() => computedClockY + (signalHeight / 2f);

    #endregion

    #region Internal Rendering Logic

    /// <summary>
    /// Returns the configured uniform spacing corresponding to the active signal count in the level.
    /// </summary>
    private float ResolveSpacingForCount(int signalCount)
    {
        switch (signalCount)
        {
            case 2: return spacing2Signals;
            case 3: return spacing3Signals;
            case 4: return spacing4Signals;
            case 5: return spacing5Signals;
            default: return spacing5Signals;
        }
    }

    /// <summary>
    /// Generates orthogonal vertices and updates the target LineRenderer and its shadow.
    /// </summary>
    private void RenderChannel(string lineName, bool[] signal, float baseY, float xOffset, float effectiveStartX, Color color)
    {
        if (signal == null || signal.Length == 0)
        {
            HideChannel(lineName);
            return;
        }

        Vector3[] points = BuildSignalPoints(signal, baseY, signalHeight, xOffset, effectiveStartX);

        LineRenderer mainLine = GetOrCreateLine(lineName, color, MAIN_SORTING_ORDER);
        mainLine.gameObject.SetActive(true);
        mainLine.positionCount = points.Length;
        mainLine.SetPositions(points);
        mainLine.startColor = color;
        mainLine.endColor = color;

        string shadowName = lineName + "_Shadow";
        if (enableShadow)
        {
            Vector3[] shadowPoints = BuildShadowPoints(points);
            LineRenderer shadowLine = GetOrCreateLine(shadowName, shadowColor, SHADOW_SORTING_ORDER);
            shadowLine.gameObject.SetActive(true);
            shadowLine.positionCount = shadowPoints.Length;
            shadowLine.SetPositions(shadowPoints);
            shadowLine.startColor = shadowColor;
            shadowLine.endColor = shadowColor;
        }
        else
        {
            HideChannel(shadowName);
        }
    }

    /// <summary>
    /// Builds an array of orthogonal 2D coordinates representing a digital waveform.
    /// </summary>
    private Vector3[] BuildSignalPoints(bool[] signal, float baseY, float height, float xOffset, float effectiveStartX)
    {
        var points = new List<Vector3>(signal.Length * 3);

        float originX = effectiveStartX + xOffset;
        float initialY = signal[0] ? (baseY + height) : baseY;

        if (LEAD_IN_LENGTH > 0f)
        {
            points.Add(new Vector3(originX - LEAD_IN_LENGTH, initialY, 0f));
        }

        points.Add(new Vector3(originX, initialY, 0f));

        for (int i = 0; i < signal.Length; i++)
        {
            float currentY = signal[i] ? (baseY + height) : baseY;
            float stepStartX = originX + i;
            float stepEndX = originX + i + 1f;

            if (i > 0)
            {
                float prevY = signal[i - 1] ? (baseY + height) : baseY;
                if (Mathf.Abs(currentY - prevY) > 0.001f)
                {
                    points.Add(new Vector3(stepStartX, currentY, 0f));
                }
            }

            points.Add(new Vector3(stepEndX, currentY, 0f));
        }

        if (EXTEND_RIGHT_LENGTH > 0f)
        {
            float finalY = signal[signal.Length - 1] ? (baseY + height) : baseY;
            float finalX = originX + signal.Length + EXTEND_RIGHT_LENGTH;
            points.Add(new Vector3(finalX, finalY, 0f));
        }

        return points.ToArray();
    }

    /// <summary>
    /// Creates offset points for shadow lines placed slightly behind in Z.
    /// </summary>
    private Vector3[] BuildShadowPoints(Vector3[] originalPoints)
    {
        var shadowPoints = new Vector3[originalPoints.Length];
        for (int i = 0; i < originalPoints.Length; i++)
        {
            shadowPoints[i] = new Vector3(
                originalPoints[i].x + shadowOffset.x,
                originalPoints[i].y + shadowOffset.y,
                originalPoints[i].z + 0.05f
            );
        }
        return shadowPoints;
    }

    /// <summary>
    /// Converts an integer pattern array (0/1) to a boolean array.
    /// </summary>
    private bool[] ConvertPatternToBoolArray(int[] pattern)
    {
        var bools = new bool[pattern.Length];
        for (int i = 0; i < pattern.Length; i++)
        {
            bools[i] = pattern[i] != 0;
        }
        return bools;
    }

    /// <summary>
    /// Sets line color on a cached LineRenderer if active.
    /// </summary>
    private void SetLineColor(string lineName, Color color)
    {
        if (lines.TryGetValue(lineName, out LineRenderer line) && line != null)
        {
            line.startColor = color;
            line.endColor = color;
        }
    }

    /// <summary>
    /// Deactivates a channel line and its corresponding shadow line.
    /// </summary>
    private void HideChannel(string lineName)
    {
        if (lines.TryGetValue(lineName, out LineRenderer line) && line != null)
        {
            line.positionCount = 0;
            line.gameObject.SetActive(false);
        }

        string shadowName = lineName + "_Shadow";
        if (lines.TryGetValue(shadowName, out LineRenderer shadow) && shadow != null)
        {
            shadow.positionCount = 0;
            shadow.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Retrieves or dynamically instantiates a child LineRenderer component.
    /// </summary>
    private LineRenderer GetOrCreateLine(string lineName, Color color, int order)
    {
        if (lines.TryGetValue(lineName, out LineRenderer existingLine) && existingLine != null)
        {
            return existingLine;
        }

        Transform child = transform.Find(lineName);
        GameObject lineObj;
        LineRenderer lr;

        if (child != null)
        {
            lineObj = child.gameObject;
            lr = lineObj.GetComponent<LineRenderer>() ?? lineObj.AddComponent<LineRenderer>();
        }
        else
        {
            lineObj = new GameObject(lineName);
            lineObj.transform.SetParent(transform, false);
            lr = lineObj.AddComponent<LineRenderer>();
        }

        Material mat = lineMaterial != null ? lineMaterial : GetDefaultMaterial();
        lr.material = mat;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = color;
        lr.endColor = color;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.TransformZ;
        lr.sortingLayerName = SORTING_LAYER_NAME;
        lr.sortingOrder = order;
        lr.numCornerVertices = 0;
        lr.numCapVertices = 0;
        lr.textureMode = LineTextureMode.Tile;

        lines[lineName] = lr;
        return lr;
    }

    /// <summary>
    /// Caches all existing LineRenderer children on Awake.
    /// </summary>
    private void CacheExistingLineRenderers()
    {
        foreach (Transform child in transform)
        {
            var lr = child.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lines[child.name] = lr;
            }
        }
    }

    /// <summary>
    /// Updates width, materials, and sorting order on cached lines during Inspector edits.
    /// </summary>
    private void ApplyStyleSettingsToExistingLines()
    {
        Material mat = lineMaterial != null ? lineMaterial : GetDefaultMaterial();
        foreach (var kvp in lines)
        {
            if (kvp.Value == null) continue;
            bool isShadow = kvp.Key.EndsWith("_Shadow");

            kvp.Value.startWidth = lineWidth;
            kvp.Value.endWidth = lineWidth;
            kvp.Value.sortingLayerName = SORTING_LAYER_NAME;
            kvp.Value.sortingOrder = isShadow ? SHADOW_SORTING_ORDER : MAIN_SORTING_ORDER;
            kvp.Value.material = isShadow ? mat : (lineMaterial != null ? lineMaterial : mat);
        }
    }

    /// <summary>
    /// Lazily resolves a default unlit material using the Sprites/Default shader.
    /// </summary>
    private Material GetDefaultMaterial()
    {
        if (defaultSpriteMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                defaultSpriteMaterial = new Material(shader);
            }
        }
        return defaultSpriteMaterial;
    }

    #endregion
}
