using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Displays world-space hint labels at the next clock edge to the right of the player position.
/// Interacts with <see cref="LevelManager"/> for clock step calculation, <see cref="LevelJsonLoader"/> for parsed signals,
/// and <see cref="Tilemap"/> for world position positioning.
/// </summary>
public class OperationHint : MonoBehaviour
{
    #region Serialized Fields

    [Header("References")]
    [Tooltip("Player transform used to determine current X position.")]
    [SerializeField] private Transform player;

    [Tooltip("LevelJsonLoader to access parsed signals and clock step.")]
    [SerializeField] private LevelJsonLoader levelJsonLoader;

    [Tooltip("Input tilemap used to convert tile rows to world Y positions.")]
    [SerializeField] private Tilemap inputTilemap;

    [Header("Tile Rows (must match LevelJsonLoader)")]
    [Tooltip("Tile row Y for the J signal line.")]
    [SerializeField] private int jTileRow = 12;

    [Tooltip("Tile row Y for Preset signal line.")]
    [SerializeField] private int presetTileRow = 8;

    [Tooltip("Tile row Y for Clear signal line.")]
    [SerializeField] private int clearTileRow = 6;

    [Header("Output Line Y (world space)")]
    [Tooltip("World Y where the operation label appears (fixed).")]
    [SerializeField] private float outputHighY = 1.25f;

    [Header("Label Appearance")]
    [Tooltip("Font asset for the world-space TMP labels.")]
    [SerializeField] private TMP_FontAsset labelFont;

    [Tooltip("Font size for signal labels (J, K, Preset, Clear).")]
    [SerializeField] private float signalFontSize = 6f;

    [Tooltip("Font size for the operation label.")]
    [SerializeField] private float operationFontSize = 8f;

    [Tooltip("Color for signal value labels.")]
    [SerializeField] private Color signalColor = new Color(0.9f, 0.9f, 0.2f);

    [Tooltip("Color for the operation label.")]
    [SerializeField] private Color operationColor = Color.white;

    [Tooltip("Horizontal offset to the right of the clock line.")]
    [SerializeField] private float xOffset = 10f;

    [Tooltip("Z position for the labels (should be in front of tilemap).")]
    [SerializeField] private float labelZ = -1f;

    [Header("Settings")]
    [Tooltip("How many seconds the hint stays visible before auto-hiding.")]
    [SerializeField] private float hintDuration = 3f;

    #endregion

    #region Private Fields

    /// <summary>
    /// Parent object containing all active TextMeshPro hint labels.
    /// </summary>
    private GameObject labelsRoot;

    /// <summary>
    /// Active coroutine handle for auto-hiding the operation hint.
    /// </summary>
    private Coroutine hideCoroutine;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Ensures active labels are destroyed when this component is destroyed.
    /// </summary>
    private void OnDestroy() => ClearLabels();

    #endregion

    #region Public Methods

    /// <summary>
    /// Shows the operation hint by calculating the next clock edge, retrieving signal values,
    /// determining the flip-flop operation state, and spawning TextMeshPro labels.
    /// Interacts with <see cref="LevelManager.Instance"/>, <see cref="LevelJsonLoader"/>, and <see cref="Tilemap"/>.
    /// </summary>
    public void ShowHint()
    {
        if (player == null || levelJsonLoader == null || inputTilemap == null)
        {
            Debug.LogWarning("[OperationHint] Missing references (player, levelJsonLoader or inputTilemap).");
            return;
        }

        float clockStep = LevelManager.Instance != null ? LevelManager.Instance.clockStepX : 6f;
        float startXOffset = levelJsonLoader.StartX;
        float relativePlayerX = Mathf.Max(0f, player.position.x - startXOffset);
        int currentClock = Mathf.FloorToInt(relativePlayerX / clockStep);
        int nextClock = currentClock + 1;
        int signalIndex = nextClock * (int)clockStep - 1;
        float xPos = startXOffset + (currentClock + 1) * clockStep + xOffset;

        bool j = GetSignalAt(levelJsonLoader.ParsedJSignal, signalIndex);
        bool k = GetSignalAt(levelJsonLoader.ParsedKSignal, signalIndex);
        bool preset = GetSignalAt(levelJsonLoader.ParsedPresetSignal, signalIndex);
        bool clear = GetSignalAt(levelJsonLoader.ParsedClearSignal, signalIndex);

        Debug.Log($"currentClock:{currentClock} nextClock:{nextClock} signalIndex:{signalIndex} J:{j} K:{k}");

        bool hasPreset = levelJsonLoader.ParsedPresetSignal != null;
        bool hasClear = levelJsonLoader.ParsedClearSignal != null;
        bool hasAsync = hasPreset || hasClear;

        bool asyncActiveHigh = levelJsonLoader.asyncActiveHigh;
        string operation = ResolveOperation(j, k, preset, clear, hasPreset, hasClear, asyncActiveHigh);

        ClearLabels();
        labelsRoot = new GameObject("HintLabels");

        SpawnLabel(labelsRoot.transform, $"J = {B(j)}", xPos, TileRowToWorldY(jTileRow), signalFontSize, signalColor);

        int kRow = hasAsync ? 10 : 8;
        SpawnLabel(labelsRoot.transform, $"K = {B(k)}", xPos, TileRowToWorldY(kRow), signalFontSize, signalColor);

        if (hasPreset)
            SpawnLabel(labelsRoot.transform, $"Preset = {B(preset)}", xPos, TileRowToWorldY(presetTileRow), signalFontSize, signalColor);

        if (hasClear)
            SpawnLabel(labelsRoot.transform, $"Clear = {B(clear)}", xPos, TileRowToWorldY(clearTileRow), signalFontSize, signalColor);

        SpawnLabel(labelsRoot.transform, operation, xPos, outputHighY, operationFontSize, operationColor);

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(AutoHide());
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Spawns a single label with the specified text, position, font size, and color under the given parent transform.
    /// Interacts with <see cref="TextMeshPro"/>.
    /// </summary>
    /// <param name="parent">Parent transform for the spawned label GameObject.</param>
    /// <param name="text">Text string to display.</param>
    /// <param name="x">World X coordinate.</param>
    /// <param name="y">World Y coordinate.</param>
    /// <param name="fontSize">Font size for the label.</param>
    /// <param name="color">Text color for the label.</param>
    private void SpawnLabel(Transform parent, string text, float x, float y, float fontSize, Color color)
    {
        var obj = new GameObject($"Label_{text}");
        obj.transform.SetParent(parent);
        obj.transform.position = new Vector3(x, y, labelZ);

        var tmp = obj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Left;

        if (labelFont != null)
            tmp.font = labelFont;
    }

    /// <summary>
    /// Resolves the flip-flop operation string based on input signals and asynchronous control states.
    /// </summary>
    /// <param name="j">State of the J input signal.</param>
    /// <param name="k">State of the K input signal.</param>
    /// <param name="preset">State of the Preset signal.</param>
    /// <param name="clear">State of the Clear signal.</param>
    /// <param name="hasPreset">Whether Preset signal is defined in level data.</param>
    /// <param name="hasClear">Whether Clear signal is defined in level data.</param>
    /// <param name="asyncActiveHigh">True if async signals are active high, false if active low.</param>
    /// <returns>Descriptive string of the resolved operation state.</returns>
    private string ResolveOperation(bool j, bool k, bool preset, bool clear,
                                     bool hasPreset, bool hasClear, bool asyncActiveHigh)
    {
        bool presetActive = hasPreset && (asyncActiveHigh ? preset : !preset);
        bool clearActive = hasClear && (asyncActiveHigh ? clear : !clear);

        if (presetActive) return "Preset";
        if (clearActive) return "Clear";
        if (j && k) return "Comuta";
        if (j) return "Set";
        if (k) return "Reset";
        return "Mantém";
    }

    /// <summary>
    /// Converts a tile row index to a world Y position using the input tilemap's cell layout.
    /// Interacts with <see cref="Tilemap"/>.
    /// </summary>
    /// <param name="tileRow">Tilemap cell Y row index.</param>
    /// <returns>Centered world Y position for the row.</returns>
    private float TileRowToWorldY(int tileRow)
    {
        Vector3 worldPos = inputTilemap.CellToWorld(new Vector3Int(0, tileRow, 0));
        return worldPos.y + inputTilemap.cellSize.y / 2f;
    }

    /// <summary>
    /// Safely retrieves the boolean signal state at a given index.
    /// </summary>
    /// <param name="signal">Signal array to index into.</param>
    /// <param name="index">Signal array index.</param>
    /// <returns>True if high signal, false if low signal or index out of bounds.</returns>
    private static bool GetSignalAt(bool[] signal, int index)
    {
        if (signal == null || index < 0 || index >= signal.Length) return false;
        return signal[index];
    }

    /// <summary>
    /// Converts a boolean value to binary string representation ("1" or "0").
    /// </summary>
    /// <param name="v">Boolean value.</param>
    /// <returns>"1" for true, "0" for false.</returns>
    private static string B(bool v) => v ? "1" : "0";

    /// <summary>
    /// Destroys all active label GameObjects.
    /// </summary>
    private void ClearLabels()
    {
        if (labelsRoot != null) { Destroy(labelsRoot); labelsRoot = null; }
    }

    /// <summary>
    /// Coroutine that waits for <see cref="hintDuration"/> seconds before clearing active labels.
    /// </summary>
    private IEnumerator AutoHide()
    {
        yield return new WaitForSeconds(hintDuration);
        ClearLabels();
        hideCoroutine = null;
    }

    #endregion
}