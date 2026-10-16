using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders digital waveform signals (J, K, Preset, Clear, Clock) as orthogonal line strips using <see cref="LineRenderer"/>
/// and manages their corresponding world-space badge hierarchies via <see cref="SignalLabelGroup"/>.
/// Automatically calculates uniform vertical spacing between all active signal lines in a scenario based on the total signal count (2 to 5).
/// Interacts with <see cref="LevelJsonLoader"/> for level data and <see cref="SignalColorManager"/> for color updates.
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

    #region Serialized Fields - Scenario Labels

    [Header("Scenario Labels")]
    [Tooltip("Offset of the label groups relative to the camera X position and each line's vertical center.")]
    [SerializeField] private Vector2 labelOffset = new Vector2(-15.2f, 0f);

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
    private Transform labelsContainer;
    private Camera mainCamera;

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
        CacheExistingLineRenderers();
        UpdateLabelsContainerPosition();
    }

    private void LateUpdate()
    {
        UpdateLabelsContainerPosition();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateLabelsContainerPosition();

        if (Application.isPlaying && lines != null && lines.Count > 0)
        {
            ApplyStyleSettingsToExistingLines();
        }
        else if (!Application.isPlaying)
        {
            PositionLabelsForPreview();
        }
    }

    [UnityEditor.Callbacks.DidReloadScripts]
    private static void ApplyLabelAdjustmentsInScene()
    {
        const string sdfFontPath = "Assets/TextMesh Pro/Fonts/FS Industrie Bold Italic SDF.asset";
        TMPro.TMP_FontAsset uiFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(sdfFontPath);

        if (uiFont == null)
        {
            Font sourceFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/FS Industrie Bold Italic.otf");
            if (sourceFont != null)
            {
                uiFont = TMPro.TMP_FontAsset.CreateFontAsset(sourceFont);
                if (uiFont != null)
                {
                    uiFont.name = "FS Industrie Bold Italic SDF";
                    UnityEditor.AssetDatabase.CreateAsset(uiFont, sdfFontPath);

                    if (uiFont.atlasTextures != null && uiFont.atlasTextures.Length > 0 && uiFont.atlasTextures[0] != null)
                    {
                        uiFont.atlasTextures[0].name = "FS Industrie Bold Italic Atlas";
                        UnityEditor.AssetDatabase.AddObjectToAsset(uiFont.atlasTextures[0], uiFont);
                    }

                    if (uiFont.material != null)
                    {
                        uiFont.material.name = "FS Industrie Bold Italic Material";
                        UnityEditor.AssetDatabase.AddObjectToAsset(uiFont.material, uiFont);
                    }

                    UnityEditor.EditorUtility.SetDirty(uiFont);
                    UnityEditor.AssetDatabase.SaveAssets();
                }
            }
        }

        if (uiFont == null)
        {
            uiFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Exo2-VariableFont_wght SDF.asset");
        }

        Sprite whiteBarSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/WhiteSqr.png");

        SignalLabelGroup[] groups = FindObjectsByType<SignalLabelGroup>(FindObjectsSortMode.None);
        for (int i = 0; i < groups.Length; i++)
        {
            SignalLabelGroup group = groups[i];
            if (group == null) continue;

            float bgWidth = 3.83f;
            float bgHeight = 1.88f;
            Transform bg = group.transform.Find("Background");
            if (bg != null)
            {
                SpriteRenderer bgSr = bg.GetComponent<SpriteRenderer>();
                if (bgSr != null && bgSr.sprite != null)
                {
                    bgWidth = bgSr.sprite.bounds.size.x * Mathf.Abs(bg.localScale.x);
                    bgHeight = bgSr.sprite.bounds.size.y * Mathf.Abs(bg.localScale.y);
                }

                for (int c = 0; c < bg.childCount; c++)
                {
                    Transform child = bg.GetChild(c);
                    if (child != null && child.name.StartsWith("BottomHalftone"))
                    {
                        child.name = "LabelHalftone";
                        UnityEditor.EditorUtility.SetDirty(child.gameObject);
                    }
                }
            }

            const float targetFontSize = 10f;

            TMPro.TMP_Text tmp = group.GetComponentInChildren<TMPro.TMP_Text>(true);
            Vector3 textPos = Vector3.zero;
            float textTopY = 0.34f;
            float textWidth = 1.15f;
            float textCenterX = 0f;

            if (tmp != null)
            {
                textPos = tmp.transform.localPosition;
                if (uiFont != null)
                {
                    tmp.font = uiFont;
                }

                tmp.fontSize = targetFontSize;
                tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

                RectTransform rect = tmp.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.sizeDelta = new Vector2(bgWidth, bgHeight);
                }

                tmp.ForceMeshUpdate(true);
                Bounds tb = tmp.textBounds;
                if (tb.size.y > 0.05f)
                {
                    textTopY = tb.max.y;
                    textWidth = tb.size.x * 0.95f;
                    textCenterX = tb.center.x;
                }

                UnityEditor.EditorUtility.SetDirty(tmp);
            }

            Transform overline = group.transform.Find("Overline");
            if (overline != null)
            {
                SpriteRenderer sr = overline.GetComponent<SpriteRenderer>();
                if (sr == null) sr = overline.gameObject.AddComponent<SpriteRenderer>();

                if (whiteBarSprite != null)
                {
                    sr.sprite = whiteBarSprite;
                }

                sr.color = Color.white;
                sr.sortingOrder = 7;

                const float barThickness = 0.055f;
                float barY = textPos.y + textTopY + 0.07f;
                overline.localPosition = new Vector3(textPos.x + textCenterX + 0.04f, barY, 0f);
                overline.localScale = new Vector3(textWidth, barThickness, 1f);

                bool isAsyncLabel = group.name.Contains("Preset") || group.name.Contains("Clear");
                overline.gameObject.SetActive(isAsyncLabel);
                UnityEditor.EditorUtility.SetDirty(overline.gameObject);
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
            clockLabel?.Configure("CLK", clockColor, !isClockRisingEdge, GetChannelCenterY(computedClockY));
        }
        else
        {
            HideChannel("Clock_Line");
            clockLabel?.Hide();
        }

        if (hasClear)
        {
            RenderChannel("Clear_Line", clearSignal, computedClearY, INPUT_X_OFFSET, baseXOffset, clearColor);
            clearLabel?.Configure("CLR", clearColor, !isAsyncActiveHigh, GetChannelCenterY(computedClearY));
        }
        else
        {
            HideChannel("Clear_Line");
            clearLabel?.Hide();
        }

        if (hasPreset)
        {
            RenderChannel("Preset_Line", presetSignal, computedPresetY, INPUT_X_OFFSET, baseXOffset, presetColor);
            presetLabel?.Configure("PRE", presetColor, !isAsyncActiveHigh, GetChannelCenterY(computedPresetY));
        }
        else
        {
            HideChannel("Preset_Line");
            presetLabel?.Hide();
        }

        if (hasK)
        {
            RenderChannel("K_Line", kSignal, computedKY, INPUT_X_OFFSET, baseXOffset, kColor);
            kLabel?.Configure("K", kColor, false, GetChannelCenterY(computedKY));
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
            jLabel?.Configure(mainText, jColor, false, GetChannelCenterY(computedJY));
        }
        else
        {
            HideChannel("J_Line");
            jLabel?.Hide();
        }
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

    private Vector3[] BuildSignalPoints(bool[] signal, float baseY, float height, float xOffset, float effectiveStartX)
    {
        var points = new List<Vector3>(signal.Length * 3);

        float originX = effectiveStartX + xOffset;
        float initialY = signal[0] ? (baseY + height) : baseY;

        if (LEAD_IN_LENGTH > 0f)
        {
            points.Add(new Vector3(effectiveStartX - LEAD_IN_LENGTH, initialY, 0f));
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
        clockLabel?.SetChannelY(GetChannelCenterY(clockBaseY + (0 * spacing5Signals)));
        clearLabel?.SetChannelY(GetChannelCenterY(clockBaseY + (1 * spacing5Signals)));
        presetLabel?.SetChannelY(GetChannelCenterY(clockBaseY + (2 * spacing5Signals)));
        kLabel?.SetChannelY(GetChannelCenterY(clockBaseY + (3 * spacing5Signals)));
        jLabel?.SetChannelY(GetChannelCenterY(clockBaseY + (4 * spacing5Signals)));
    }

    #endregion
}
