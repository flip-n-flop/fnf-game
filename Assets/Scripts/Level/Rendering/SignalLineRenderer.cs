using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders digital waveform signals (J, K, Preset, Clear, Clock) as orthogonal line strips using <see cref="LineRenderer"/>
/// and manages their corresponding world-space badge hierarchies via <see cref="SignalLabelGroup"/>.
/// Automatically calculates uniform vertical spacing between all active signal lines in a scenario based on the total signal count (2 to 5).
/// Interacts with <see cref="LevelJsonLoader"/> for level data and <see cref="SignalColorManager"/> for color updates.
/// </summary>
[DefaultExecutionOrder(100)]
public class SignalLineRenderer : MonoBehaviour
{
    #region Serialized Fields - Dimensions

    [Header("Signal Dimensions")]
    [Tooltip("Height difference between Low (0) and High (1) logic levels in world units.")]
    [SerializeField] private float signalHeight = 0.9f;

    [Tooltip("Thickness of the rendered signal lines in world units.")]
    [SerializeField] private float lineWidth = 0.32f;

    [Tooltip("Material applied to active signal lines. If null, Sprites/Default is used.")]
    [SerializeField] private Material lineMaterial;

    #endregion

    #region Serialized Fields - Layout & Spacing

    [Header("Layout & Spacing")]
    [Tooltip("Base Y coordinate (logic level 0) for the Clock line at the bottom.")]
    [SerializeField] private float clockBaseY = 5.7f;

    [Tooltip("Uniform spacing between consecutive lines when level has 2 signals (Clock + D/T).")]
    [SerializeField] private float spacing2Signals = 3.5f;

    [Tooltip("Uniform spacing between consecutive lines when level has 3 signals (Clock + J + K).")]
    [SerializeField] private float spacing3Signals = 2.6f;

    [Tooltip("Uniform spacing between consecutive lines when level has 4 signals (Clock + J + K + Async).")]
    [SerializeField] private float spacing4Signals = 2.2f;

    [Tooltip("Uniform spacing between consecutive lines when level has 5 signals (Clock + J + K + PRE + CLR).")]
    [SerializeField] private float spacing5Signals = 1.75f;

    #endregion

    #region Serialized Fields - Scenario Labels

    [Header("Scenario Labels")]
    [Tooltip("Offset of the label groups relative to the camera X position and each line's vertical center.")]
    [SerializeField] private Vector2 labelOffset = new Vector2(-15.0f, 0f);

    [Tooltip("Uniform scale applied to each signal label badge hierarchy.")]
    [SerializeField] private float labelScale = 0.75f;

    [Tooltip("Optional horizontal offset relative to the label container position where the signal lines are clipped. Default 0 aligns with the badge center.")]
    [SerializeField] private float lineMaskOffset = 0f;

    [Tooltip("World-space hierarchy group for the J (or D/T) signal badge.")]
    [SerializeField] private SignalLabelGroup jLabel;

    [Tooltip("World-space hierarchy group for the K signal badge.")]
    [SerializeField] private SignalLabelGroup kLabel;

    [Tooltip("World-space hierarchy group for the Preset signal badge.")]
    [SerializeField] private SignalLabelGroup presetLabel;

    [Tooltip("World-space hierarchy group for the Clear signal badge.")]
    [SerializeField] private SignalLabelGroup clearLabel;

    [Tooltip("World-space hierarchy group for the Clock signal badge.")]
    [SerializeField] private SignalLabelGroup clockLabel;

    #endregion

    #region Serialized Fields - Drop Shadows

    [Header("Drop Shadows")]
    [Tooltip("Whether to render a subtle dark drop shadow beneath each signal waveform.")]
    [SerializeField] private bool enableShadow = true;

    [Tooltip("Positional offset for the shadow line relative to the main line.")]
    [SerializeField] private Vector2 shadowOffset = new Vector2(0.08f, -0.1f);

    [Tooltip("Color tint applied to shadow LineRenderers.")]
    [SerializeField] private Color shadowColor = Color.white;

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
    private readonly Dictionary<string, Vector3[]> rawLinePoints = new Dictionary<string, Vector3[]>();
    private Vector3[] scratchBuffer = new Vector3[1024];
    private float lastCutoffX = float.MinValue;
    private static Material defaultSpriteMaterial;
    private static Material defaultHoloLineMaterial;
    private Transform labelsContainer;
    private Camera mainCamera;
    private Transform playerTransform;

    private float computedJY;
    private float computedKY;
    private float computedPresetY;
    private float computedClearY;
    private float computedClockY;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        mainCamera = Camera.main;
        ResolvePlayerTransform();
        CacheExistingLineRenderers();
        UpdateLabelsContainerPosition();
    }

    private void LateUpdate()
    {
        UpdateLabelsContainerPosition();
        UpdateLineClipping();
        UpdateLabelHoloStates();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateLabelsContainerPosition();

        if (Application.isPlaying && lines != null && lines.Count > 0)
        {
            ApplyStyleSettingsToExistingLines();
            UpdateLineClipping(force: true);
        }
        else if (!Application.isPlaying)
        {
            PositionLabelsForPreview();
            UpdateLineClipping(force: true);
        }
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void SyncSceneDefaults()
    {
        if (Application.isPlaying) return;

        SignalLineRenderer[] renderers = FindObjectsByType<SignalLineRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            SignalLineRenderer r = renderers[i];
            if (r == null) continue;

            if (r.labelScale <= 0.01f)
            {
                r.labelScale = 1.0f;
            }

            if (r.spacing5Signals < 1.75f)
            {
                r.spacing5Signals = 1.75f;
                r.clockBaseY = 5.7f;
            }

            if (r.lineMaterial == null)
            {
                r.lineMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SignalLineHolo.mat");
            }

            r.ApplyStyleSettingsToExistingLines();
            r.PositionLabelsForPreview();
            UnityEditor.EditorUtility.SetDirty(r);
        }

        LevelJsonLoader[] loaders = FindObjectsByType<LevelJsonLoader>();
        for (int i = 0; i < loaders.Length; i++)
        {
            LevelJsonLoader loader = loaders[i];
            if (loader == null) continue;

            UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(loader);
            UnityEditor.SerializedProperty startXProp = so.FindProperty("startX");
            if (startXProp != null && startXProp.intValue == 0)
            {
                startXProp.intValue = 4;
                so.ApplyModifiedPropertiesWithoutUndo();
                UnityEditor.EditorUtility.SetDirty(loader);
            }
        }
    }
#endif

    #endregion

    #region Public Rendering API

    /// <summary>
    /// Computes uniform vertical spacing, renders all active digital waveforms and their drop shadows,
    /// and configures the corresponding world-space <see cref="SignalLabelGroup"/> badges.
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
        float baseXOffset = 0f,
        bool isAsyncActiveHigh = true,
        bool isClockRisingEdge = true)
    {
        UpdateLabelsContainerPosition();

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
            clockLabel?.Configure("CLK", clockColor, !isClockRisingEdge, GetChannelCenterY(computedClockY), labelScale);
        }
        else
        {
            HideChannel("Clock_Line");
            clockLabel?.Hide();
        }

        if (hasClear)
        {
            RenderChannel("Clear_Line", clearSignal, computedClearY, INPUT_X_OFFSET, baseXOffset, clearColor);
            clearLabel?.Configure("CLR", clearColor, !isAsyncActiveHigh, GetChannelCenterY(computedClearY), labelScale);
        }
        else
        {
            HideChannel("Clear_Line");
            clearLabel?.Hide();
        }

        if (hasPreset)
        {
            RenderChannel("Preset_Line", presetSignal, computedPresetY, INPUT_X_OFFSET, baseXOffset, presetColor);
            presetLabel?.Configure("PRE", presetColor, !isAsyncActiveHigh, GetChannelCenterY(computedPresetY), labelScale);
        }
        else
        {
            HideChannel("Preset_Line");
            presetLabel?.Hide();
        }

        if (hasK)
        {
            RenderChannel("K_Line", kSignal, computedKY, INPUT_X_OFFSET, baseXOffset, kColor);
            kLabel?.Configure("K", kColor, false, GetChannelCenterY(computedKY), labelScale);
        }
        else
        {
            HideChannel("K_Line");
            kLabel?.Hide();
        }

        if (hasJ)
        {
            RenderChannel("J_Line", jSignal, computedJY, INPUT_X_OFFSET, baseXOffset, jColor);
            string mainText = hasK ? "J" : "D";
            jLabel?.Configure(mainText, jColor, false, GetChannelCenterY(computedJY), labelScale);
        }
        else
        {
            HideChannel("J_Line");
            jLabel?.Hide();
        }

        UpdateLineClipping(force: true);
    }

    /// <summary>
    /// Clears and hides all signal and shadow lines.
    /// </summary>
    public void ClearAllSignals()
    {
        rawLinePoints.Clear();
        lastCutoffX = float.MinValue;
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
    /// Updates line and badge colors in real time when modified by settings.
    /// Called by <see cref="LevelJsonLoader"/> responding to <see cref="SignalColorManager.OnColorsChanged"/>.
    /// </summary>
    public void UpdateColors(Color jColor, Color kColor, Color clockColor, Color presetColor, Color clearColor)
    {
        SetLineColor("J_Line", jColor);
        SetLineColor("K_Line", kColor);
        SetLineColor("Clock_Line", clockColor);
        SetLineColor("Preset_Line", presetColor);
        SetLineColor("Clear_Line", clearColor);

        jLabel?.ApplyColor(jColor);
        kLabel?.ApplyColor(kColor);
        clockLabel?.ApplyColor(clockColor);
        presetLabel?.ApplyColor(presetColor);
        clearLabel?.ApplyColor(clearColor);
    }

    #endregion

    #region Channel Position Queries for HUD

    public float GetJWorldY() => computedJY + (signalHeight / 2f);
    public float GetKWorldY() => computedKY + (signalHeight / 2f);
    public float GetPresetWorldY() => computedPresetY + (signalHeight / 2f);
    public float GetClearWorldY() => computedClearY + (signalHeight / 2f);
    public float GetClockWorldY() => computedClockY + (signalHeight / 2f);

    #endregion

    #region Internal Rendering Logic

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

    private void RenderChannel(string lineName, bool[] signal, float baseY, float xOffset, float effectiveStartX, Color color)
    {
        if (signal == null || signal.Length == 0)
        {
            HideChannel(lineName);
            return;
        }

        Vector3[] points = BuildSignalPoints(signal, baseY, signalHeight, xOffset, effectiveStartX);
        rawLinePoints[lineName] = points;

        LineRenderer mainLine = GetOrCreateLine(lineName, color, MAIN_SORTING_ORDER);
        mainLine.gameObject.SetActive(true);
        mainLine.startColor = color;
        mainLine.endColor = color;

        string shadowName = lineName + "_Shadow";
        if (enableShadow)
        {
            Vector3[] shadowPoints = BuildShadowPoints(points);
            rawLinePoints[shadowName] = shadowPoints;

            LineRenderer shadowLine = GetOrCreateLine(shadowName, shadowColor, SHADOW_SORTING_ORDER);
            shadowLine.gameObject.SetActive(true);
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
    /// All lines (including Clock) share the exact same starting X coordinate (<c>effectiveStartX - LEAD_IN_LENGTH</c>)
    /// underneath the labels, while the Clock transitions remain shifted by <c>-0.5</c> relative to input transitions.
    /// </summary>
    private Vector3[] BuildSignalPoints(bool[] signal, float baseY, float height, float xOffset, float effectiveStartX)
    {
        var points = new List<Vector3>(signal.Length * 3);

        float lineStartX = effectiveStartX - LEAD_IN_LENGTH;
        float originX = effectiveStartX + xOffset;
        float initialY = signal[0] ? (baseY + height) : baseY;

        points.Add(new Vector3(lineStartX, initialY, 0f));

        if (originX > lineStartX)
        {
            points.Add(new Vector3(originX, initialY, 0f));
        }

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

    private bool[] ConvertPatternToBoolArray(int[] pattern)
    {
        var bools = new bool[pattern.Length];
        for (int i = 0; i < pattern.Length; i++)
        {
            bools[i] = pattern[i] != 0;
        }
        return bools;
    }

    private void SetLineColor(string lineName, Color color)
    {
        if (lines.TryGetValue(lineName, out LineRenderer line) && line != null)
        {
            line.startColor = color;
            line.endColor = color;
        }
    }

    private void HideChannel(string lineName)
    {
        rawLinePoints.Remove(lineName);
        if (lines.TryGetValue(lineName, out LineRenderer line) && line != null)
        {
            line.positionCount = 0;
            line.gameObject.SetActive(false);
        }

        string shadowName = lineName + "_Shadow";
        rawLinePoints.Remove(shadowName);
        if (lines.TryGetValue(shadowName, out LineRenderer shadow) && shadow != null)
        {
            shadow.positionCount = 0;
            shadow.gameObject.SetActive(false);
        }
    }

    private LineRenderer GetOrCreateLine(string lineName, Color color, int order)
    {
        bool isShadow = lineName.EndsWith("_Shadow");
        Material targetMat = isShadow ? GetDefaultMaterial() : GetMainLineMaterial();

        if (lines.TryGetValue(lineName, out LineRenderer existingLine) && existingLine != null)
        {
            existingLine.sharedMaterial = targetMat;
            existingLine.startWidth = lineWidth;
            existingLine.endWidth = lineWidth;
            existingLine.sortingOrder = order;
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

        lr.sharedMaterial = targetMat;
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

    private void CacheExistingLineRenderers()
    {
        foreach (Transform child in transform)
        {
            var lr = child.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lines[child.name] = lr;
                if (lr.positionCount > 0 && !rawLinePoints.ContainsKey(child.name))
                {
                    Vector3[] pts = new Vector3[lr.positionCount];
                    lr.GetPositions(pts);
                    rawLinePoints[child.name] = pts;
                }
            }
        }
        ApplyStyleSettingsToExistingLines();
    }

    private void ApplyStyleSettingsToExistingLines()
    {
        Material mainMat = GetMainLineMaterial();
        Material shadowMat = GetDefaultMaterial();
        foreach (var kvp in lines)
        {
            if (kvp.Value == null) continue;
            bool isShadow = kvp.Key.EndsWith("_Shadow");

            kvp.Value.startWidth = lineWidth;
            kvp.Value.endWidth = lineWidth;
            kvp.Value.sortingLayerName = SORTING_LAYER_NAME;
            kvp.Value.sortingOrder = isShadow ? SHADOW_SORTING_ORDER : MAIN_SORTING_ORDER;
            kvp.Value.sharedMaterial = isShadow ? shadowMat : mainMat;
        }
    }

    private Material GetMainLineMaterial()
    {
        if (lineMaterial != null)
        {
            return lineMaterial;
        }

#if UNITY_EDITOR
        lineMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SignalLineHolo.mat");
        if (lineMaterial != null)
        {
            return lineMaterial;
        }
#endif

        if (defaultHoloLineMaterial == null)
        {
            Shader holoShader = Shader.Find("Custom/Sprite_Holographic");
            if (holoShader != null)
            {
                defaultHoloLineMaterial = new Material(holoShader);
                defaultHoloLineMaterial.SetFloat("_UseWorldSpace", 1f);
            }
        }

        return defaultHoloLineMaterial != null ? defaultHoloLineMaterial : GetDefaultMaterial();
    }

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

    private void UpdateLabelsContainerPosition()
    {
        if (labelsContainer == null)
        {
            labelsContainer = transform.Find("Labels");
            if (labelsContainer == null) return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        float camX = mainCamera != null ? mainCamera.transform.position.x : 17f;
        labelsContainer.position = new Vector3(camX + labelOffset.x, labelOffset.y, 0f);
    }

    private float GetChannelCenterY(float channelBaseY)
    {
        return channelBaseY + (signalHeight * 0.5f);
    }

    private void PositionLabelsForPreview()
    {
        UpdateLabelsContainerPosition();
        clockLabel?.SetChannelY(GetChannelCenterY(clockBaseY + (0 * spacing5Signals)), labelScale);
        clearLabel?.SetChannelY(GetChannelCenterY(clockBaseY + (1 * spacing5Signals)), labelScale);
        presetLabel?.SetChannelY(GetChannelCenterY(clockBaseY + (2 * spacing5Signals)), labelScale);
        kLabel?.SetChannelY(GetChannelCenterY(clockBaseY + (3 * spacing5Signals)), labelScale);
        jLabel?.SetChannelY(GetChannelCenterY(clockBaseY + (4 * spacing5Signals)), labelScale);

        if (SignalColorManager.Instance != null)
        {
            clockLabel?.ApplyColor(SignalColorManager.Instance.ColorCLK);
            clearLabel?.ApplyColor(SignalColorManager.Instance.ColorClear);
            presetLabel?.ApplyColor(SignalColorManager.Instance.ColorPreset);
            kLabel?.ApplyColor(SignalColorManager.Instance.ColorK);
            jLabel?.ApplyColor(SignalColorManager.Instance.ColorJ);
        }

        if (labelsContainer != null)
        {
            float previewCutoffX = labelsContainer.position.x + lineMaskOffset;
            UpdateLabelBrightPositions(previewCutoffX, instant: true);
            UpdateLabelHoloStates(instant: true);
        }
    }

    #endregion

    #region Dynamic Line Masking & Trimming

    /// <summary>
    /// Updates line geometry so signals are masked/clipped to begin at the label position and not appear to the left of it.
    /// If the camera moves backwards, previous points are automatically revealed.
    /// </summary>
    public void UpdateLineClipping(bool force = false)
    {
        if (labelsContainer == null || rawLinePoints.Count == 0) return;

        float cutoffX = labelsContainer.position.x + lineMaskOffset;

        if (!force && Mathf.Approximately(cutoffX, lastCutoffX)) return;
        lastCutoffX = cutoffX;

        foreach (var kvp in rawLinePoints)
        {
            string lineName = kvp.Key;
            Vector3[] fullPoints = kvp.Value;
            if (fullPoints == null || fullPoints.Length == 0) continue;

            if (!lines.TryGetValue(lineName, out LineRenderer lr) || lr == null || !lr.gameObject.activeSelf)
                continue;

            bool isShadow = lineName.EndsWith("_Shadow");
            float effectiveCutoff = isShadow ? (cutoffX + shadowOffset.x) : cutoffX;

            TrimAndApplyPoints(lr, fullPoints, effectiveCutoff);
        }

        UpdateLabelBrightPositions(cutoffX, instant: force);
        if (force)
        {
            UpdateLabelHoloStates(instant: true);
        }
    }

    private float GetBaseYForLine(string lineName)
    {
        switch (lineName)
        {
            case "Clock_Line": return computedClockY;
            case "Clear_Line": return computedClearY;
            case "Preset_Line": return computedPresetY;
            case "K_Line": return computedKY;
            case "J_Line": return computedJY;
            default: return 0f;
        }
    }

    /// <summary>
    /// Evaluates the logic level (true = High, false = Low) of a signal line at the specified world X coordinate.
    /// </summary>
    public bool GetSignalStateAtX(string lineName, float worldX)
    {
        if (!rawLinePoints.TryGetValue(lineName, out Vector3[] points) || points == null || points.Length == 0)
        {
            return false;
        }

        float baseY = GetBaseYForLine(lineName);
        float centerY = GetChannelCenterY(baseY);

        if (worldX <= points[0].x)
        {
            return points[0].y > centerY;
        }

        if (worldX >= points[points.Length - 1].x)
        {
            return points[points.Length - 1].y > centerY;
        }

        int firstIndex = 1;
        while (firstIndex < points.Length && points[firstIndex].x < worldX)
        {
            firstIndex++;
        }

        return points[firstIndex].y > centerY;
    }

    private void UpdateLabelBrightPositions(float cutoffX, bool instant = false)
    {
        if (clockLabel != null) clockLabel.SetSignalState(GetSignalStateAtX("Clock_Line", cutoffX), instant);
        if (clearLabel != null) clearLabel.SetSignalState(GetSignalStateAtX("Clear_Line", cutoffX), instant);
        if (presetLabel != null) presetLabel.SetSignalState(GetSignalStateAtX("Preset_Line", cutoffX), instant);
        if (kLabel != null) kLabel.SetSignalState(GetSignalStateAtX("K_Line", cutoffX), instant);
        if (jLabel != null) jLabel.SetSignalState(GetSignalStateAtX("J_Line", cutoffX), instant);
    }

    private void ResolvePlayerTransform()
    {
        if (playerTransform != null) return;

        if (mainCamera != null && mainCamera.TryGetComponent(out CameraController camCtrl) && camCtrl.target != null)
        {
            playerTransform = camCtrl.target;
            return;
        }

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            playerTransform = pc.transform;
        }
    }

    private void UpdateLabelHoloStates(bool instant = false)
    {
        if (rawLinePoints.Count == 0) return;

        ResolvePlayerTransform();

        float evalX = playerTransform != null
            ? playerTransform.position.x
            : (labelsContainer != null ? labelsContainer.position.x + lineMaskOffset : 0f);

        if (clockLabel != null) clockLabel.SetHoloState(GetSignalStateAtX("Clock_Line", evalX), instant);
        if (clearLabel != null) clearLabel.SetHoloState(GetSignalStateAtX("Clear_Line", evalX), instant);
        if (presetLabel != null) presetLabel.SetHoloState(GetSignalStateAtX("Preset_Line", evalX), instant);
        if (kLabel != null) kLabel.SetHoloState(GetSignalStateAtX("K_Line", evalX), instant);
        if (jLabel != null) jLabel.SetHoloState(GetSignalStateAtX("J_Line", evalX), instant);
    }

    /// <summary>
    /// Trims an orthogonal signal waveform to begin precisely at <paramref name="cutoffX"/>,
    /// discarding any points to the left of <paramref name="cutoffX"/>.
    /// Uses pre-allocated scratch buffers to avoid per-frame GC allocations.
    /// </summary>
    private void TrimAndApplyPoints(LineRenderer lr, Vector3[] originalPoints, float cutoffX)
    {
        if (lr == null) return;

        if (originalPoints == null || originalPoints.Length == 0)
        {
            lr.positionCount = 0;
            return;
        }

        // If cutoff is at or before the very first point, show full line
        if (cutoffX <= originalPoints[0].x)
        {
            lr.positionCount = originalPoints.Length;
            lr.SetPositions(originalPoints);
            return;
        }

        // If cutoff is at or past the very last point, nothing is visible
        if (cutoffX >= originalPoints[originalPoints.Length - 1].x)
        {
            lr.positionCount = 0;
            return;
        }

        // Find the first point whose X coordinate is >= cutoffX.
        int firstIndex = 1;
        while (firstIndex < originalPoints.Length && originalPoints[firstIndex].x < cutoffX)
        {
            firstIndex++;
        }

        bool atExactX = Mathf.Approximately(originalPoints[firstIndex].x, cutoffX);
        int remainingOriginalCount = originalPoints.Length - firstIndex;
        int trimmedCount = atExactX ? remainingOriginalCount : (remainingOriginalCount + 1);

        EnsureScratchBufferSize(trimmedCount);

        int writeIndex = 0;
        if (!atExactX)
        {
            // The segment between (firstIndex - 1) and firstIndex is guaranteed to be horizontal.
            // Cut precisely at cutoffX using the segment's constant Y level.
            scratchBuffer[writeIndex++] = new Vector3(
                cutoffX,
                originalPoints[firstIndex].y,
                originalPoints[firstIndex].z
            );
        }

        for (int i = firstIndex; i < originalPoints.Length; i++)
        {
            scratchBuffer[writeIndex++] = originalPoints[i];
        }

        lr.positionCount = trimmedCount;
        lr.SetPositions(scratchBuffer);
    }

    private void EnsureScratchBufferSize(int requiredSize)
    {
        if (scratchBuffer == null || scratchBuffer.Length < requiredSize)
        {
            int newSize = Mathf.Max(requiredSize, (scratchBuffer?.Length ?? 512) * 2);
            scratchBuffer = new Vector3[newSize];
        }
    }

    #endregion
}
