using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Tilemaps;

/// <summary>
/// Positions pre-defined UI Toolkit HUD labels at screen positions matching tilemap signal lines (J, K, Preset, Clear, Clock).
/// Label elements are defined in <c>HUDLabels.uxml</c> and styled via <c>HUDLabels.uss</c>.
/// Active-low signals use dedicated overline elements matched to text width.
/// Invoked by <see cref="LevelJsonLoader"/> during level rendering.
/// </summary>
public class SignalLabelRenderer : MonoBehaviour
{
    #region Serialized Fields

    [Header("Label Sprites")]
    [SerializeField] private Sprite jLabelSprite;
    [SerializeField] private Sprite kLabelSprite;
    [SerializeField] private Sprite presetLabelSprite;
    [SerializeField] private Sprite clearLabelSprite;
    [SerializeField] private Sprite clockLabelSprite;

    [Header("Position Settings")]
    [Tooltip("X offset from left edge of the screen")]
    [SerializeField] private float xOffset = 10f;

    [Tooltip("Y offset applied to all labels (positive = down, negative = up)")]
    [SerializeField] private float yOffset = 0f;

    [Tooltip("Width and height of labels in pixels")]
    [SerializeField] private float labelSizePixels = 50f;

    [Tooltip("Scale multiplier for label sprites")]
    [SerializeField] private float labelScale = 1f;

    [Tooltip("Update positions in real-time (useful if camera moves vertically)")]
    [SerializeField] private bool updateInRealTime = false;

    [Header("References")]
    [SerializeField] private LevelJsonLoader levelJsonLoader;
    [SerializeField] private Tilemap inputTilemap;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private UIDocument uiDocument;

    #endregion

    #region Private State

    private VisualElement hudRoot;
    private VisualElement jLabel, kLabel, presetLabel, clearLabel, clockLabel;
    private VisualElement jIcon, kIcon, presetIcon, clearIcon, clockIcon;
    private VisualElement jOverline, kOverline, presetOverline, clearOverline, clockOverline;
    private Label jText, kText, presetText, clearText, clockText;

    /// <summary>
    /// Cached world Y coordinates and active state configurations for real-time positioning updates.
    /// </summary>
    private float curJY, curKY, curPresetY, curClearY, curClockY;
    private bool curHasPreset, curHasClear, curHasK;
    private bool curAsyncActiveHigh, curClockActiveHigh;
    private bool labelsConfigured;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        CacheUIElements();
    }

    private void Update()
    {
        if (updateInRealTime && labelsConfigured)
        {
            PositionAllLabels();
        }
    }

    private void OnDestroy() => HideAllLabels();

    #endregion

    #region UI Element Caching

    /// <summary>
    /// Queries and caches all pre-defined label elements from the UIDocument.
    /// </summary>
    private void CacheUIElements()
    {
        if (uiDocument == null) return;

        VisualElement root = uiDocument.rootVisualElement;
        hudRoot = root.Q<VisualElement>("HudRoot");

        jLabel = root.Q<VisualElement>("J_Label");
        kLabel = root.Q<VisualElement>("K_Label");
        presetLabel = root.Q<VisualElement>("Preset_Label");
        clearLabel = root.Q<VisualElement>("Clear_Label");
        clockLabel = root.Q<VisualElement>("Clock_Label");

        jIcon = jLabel?.Q<VisualElement>(className: "signal-icon");
        kIcon = kLabel?.Q<VisualElement>(className: "signal-icon");
        presetIcon = presetLabel?.Q<VisualElement>(className: "signal-icon");
        clearIcon = clearLabel?.Q<VisualElement>(className: "signal-icon");
        clockIcon = clockLabel?.Q<VisualElement>(className: "signal-icon");

        jOverline = jLabel?.Q<VisualElement>(className: "signal-overline");
        kOverline = kLabel?.Q<VisualElement>(className: "signal-overline");
        presetOverline = presetLabel?.Q<VisualElement>(className: "signal-overline");
        clearOverline = clearLabel?.Q<VisualElement>(className: "signal-overline");
        clockOverline = clockLabel?.Q<VisualElement>(className: "signal-overline");

        jText = jLabel?.Q<Label>(className: "signal-text");
        kText = kLabel?.Q<Label>(className: "signal-text");
        presetText = presetLabel?.Q<Label>(className: "signal-text");
        clearText = clearLabel?.Q<Label>(className: "signal-text");
        clockText = clockLabel?.Q<Label>(className: "signal-text");
    }

    #endregion

    #region Public API

    /// <summary>
    /// Configures and positions all signal labels based on tilemap Y row coordinates.
    /// Invoked by <see cref="LevelJsonLoader.RenderLevel"/>.
    /// </summary>
    /// <param name="jY">Y tile row index for J signal.</param>
    /// <param name="kY">Y tile row index for K signal.</param>
    /// <param name="presetY">Y tile row index for Preset signal.</param>
    /// <param name="clearY">Y tile row index for Clear signal.</param>
    /// <param name="clockY">Y tile row index for Clock signal.</param>
    /// <param name="hasPreset">Whether Preset signal line exists in current level.</param>
    /// <param name="hasClear">Whether Clear signal line exists in current level.</param>
    /// <param name="isAsyncActiveHigh">True if asynchronous signals are active high; false if active low (overlined).</param>
    /// <param name="isClockActiveHigh">True if clock active edge is rising; false if falling (overlined).</param>
    public void GenerateLabels(int jY, int kY, int presetY, int clearY, int clockY, bool hasPreset, bool hasClear,
                               bool isAsyncActiveHigh = true, bool isClockActiveHigh = true)
    {
        float jWorld = TileToWorldY(jY);
        float kWorld = TileToWorldY(kY);
        float preWorld = TileToWorldY(presetY);
        float clrWorld = TileToWorldY(clearY);
        float clkWorld = TileToWorldY(clockY);

        GenerateLabels(jWorld, kWorld, preWorld, clrWorld, clkWorld,
            hasPreset, hasClear, isAsyncActiveHigh, isClockActiveHigh);
    }

    /// <summary>
    /// Generates and positions labels at specified world Y coordinates (e.g. from <see cref="SignalLineRenderer"/>).
    /// Interacts with <see cref="UIDocument"/> to project world positions onto screen panel space.
    /// </summary>
    /// <param name="jWorldY">World Y coordinate for J signal label.</param>
    /// <param name="kWorldY">World Y coordinate for K signal label.</param>
    /// <param name="presetWorldY">World Y coordinate for Preset signal label.</param>
    /// <param name="clearWorldY">World Y coordinate for Clear signal label.</param>
    /// <param name="clockWorldY">World Y coordinate for Clock signal label.</param>
    /// <param name="hasPreset">Whether Preset signal line exists in current level.</param>
    /// <param name="hasClear">Whether Clear signal line exists in current level.</param>
    /// <param name="isAsyncActiveHigh">True if asynchronous signals are active high; false if active low (overlined).</param>
    /// <param name="isClockActiveHigh">True if clock active edge is rising; false if falling (overlined).</param>
    public void GenerateLabels(float jWorldY, float kWorldY, float presetWorldY, float clearWorldY, float clockWorldY,
                               bool hasPreset, bool hasClear,
                               bool isAsyncActiveHigh = true, bool isClockActiveHigh = true,
                               bool hasK = true, string mainLabelText = "J")
    {
        curJY = jWorldY;
        curKY = kWorldY;
        curPresetY = presetWorldY;
        curClearY = clearWorldY;
        curClockY = clockWorldY;
        curHasPreset = hasPreset;
        curHasClear = hasClear;
        curHasK = hasK;
        curAsyncActiveHigh = isAsyncActiveHigh;
        curClockActiveHigh = isClockActiveHigh;

        if (mainCamera == null || uiDocument == null)
        {
            Debug.LogWarning("SignalLabelRenderer: Camera or UIDocument reference missing!");
            return;
        }

        if (hudRoot == null) CacheUIElements();

        SetSprite(jIcon, jLabelSprite);
        SetSprite(kIcon, kLabelSprite);
        SetSprite(presetIcon, presetLabelSprite);
        SetSprite(clearIcon, clearLabelSprite);
        SetSprite(clockIcon, clockLabelSprite);

        if (jText != null) jText.text = string.IsNullOrEmpty(mainLabelText) ? "J" : mainLabelText;
        if (kText != null) kText.text = "K";
        if (presetText != null) presetText.text = "PRE";
        if (clearText != null) clearText.text = "CLR";
        if (clockText != null) clockText.text = "CLK";

        SetOverline(presetOverline, !isAsyncActiveHigh);
        SetOverline(clearOverline, !isAsyncActiveHigh);
        SetOverline(clockOverline, !isClockActiveHigh);

        ShowLabel(jLabel, true);
        ShowLabel(kLabel, hasK);
        ShowLabel(clockLabel, true);
        ShowLabel(presetLabel, hasPreset);
        ShowLabel(clearLabel, hasClear);

        labelsConfigured = true;
        PositionAllLabels();
    }

    /// <summary>
    /// Converts an integer tile row index to its corresponding world Y center.
    /// </summary>
    private float TileToWorldY(int tileY)
    {
        if (inputTilemap != null)
        {
            return inputTilemap.CellToWorld(new Vector3Int(0, tileY, 0)).y + (inputTilemap.cellSize.y / 2f);
        }
        return tileY + 0.5f;
    }

    /// <summary>
    /// Hides the entire HUD overlay. Called by menu systems to prevent labels from appearing above menus.
    /// </summary>
    public void HideHUD()
    {
        if (hudRoot != null)
            hudRoot.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Shows the HUD overlay. Called when returning to gameplay.
    /// </summary>
    public void ShowHUD()
    {
        if (hudRoot != null)
            hudRoot.style.display = DisplayStyle.Flex;
    }

    #endregion

    #region Positioning

    /// <summary>
    /// Positions all visible labels at their corresponding world Y screen positions.
    /// </summary>
    private void PositionAllLabels()
    {
        PositionLabel(jLabel, curJY);
        if (curHasK) PositionLabel(kLabel, curKY);
        PositionLabel(clockLabel, curClockY);
        if (curHasPreset) PositionLabel(presetLabel, curPresetY);
        if (curHasClear) PositionLabel(clearLabel, curClearY);
    }

    /// <summary>
    /// Positions a single label element at the screen position corresponding to a world Y coordinate.
    /// </summary>
    private void PositionLabel(VisualElement label, float worldY)
    {
        if (label == null || mainCamera == null || uiDocument == null) return;

        Vector3 worldPos = new Vector3(0f, worldY, 0f);

        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
            uiDocument.rootVisualElement.panel,
            worldPos,
            mainCamera
        );

        float scaledSize = labelSizePixels * labelScale;
        label.style.width = scaledSize;
        label.style.height = scaledSize;
        label.style.left = xOffset;
        label.style.top = panelPos.y - (scaledSize / 2f) + yOffset;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Sets the background image of a signal icon element from a sprite.
    /// </summary>
    private void SetSprite(VisualElement icon, Sprite sprite)
    {
        if (icon == null || sprite == null) return;
        icon.style.backgroundImage = new StyleBackground(sprite);
    }

    /// <summary>
    /// Toggles the "active" USS class on an overline element for active-low signal indication.
    /// </summary>
    private void SetOverline(VisualElement overline, bool active)
    {
        if (overline == null) return;
        if (active)
            overline.AddToClassList("active");
        else
            overline.RemoveFromClassList("active");
    }

    /// <summary>
    /// Shows or hides a label element.
    /// </summary>
    private void ShowLabel(VisualElement label, bool show)
    {
        if (label == null) return;
        label.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Hides all label elements.
    /// </summary>
    private void HideAllLabels()
    {
        ShowLabel(jLabel, false);
        ShowLabel(kLabel, false);
        ShowLabel(presetLabel, false);
        ShowLabel(clearLabel, false);
        ShowLabel(clockLabel, false);
    }

    #endregion
}