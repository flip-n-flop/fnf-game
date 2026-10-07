using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;
using Newtonsoft.Json;

/// <summary>
/// Main component responsible for loading level JSON data, parsing signals, configuring level settings,
/// rendering signal/terrain tilemaps via <see cref="TilemapRenderer"/>, spawning obstacles via <see cref="ObstacleSpawner"/>,
/// and simulating flip-flop logic using <see cref="FlipFlopSimulator"/>.
/// </summary>
public class LevelJsonLoader : MonoBehaviour
{
    #region Serialized Fields

    [Header("Tilemaps")]
    [SerializeField] private Tilemap inputTilemap;
    [SerializeField] private Tilemap terrainTilemap;
    [SerializeField] private Tilemap clockTilemap;

    [Header("Level JSON")]
    [SerializeField] private TextAsset levelFile;

    [Header("Tiles - Diagrams")]
    [Tooltip("Tiles for 3-bit neighborhood patterns (prev,curr,next) as binary index: 0=000..7=111")]
    [SerializeField] private TileBase[] diagramTiles = new TileBase[8];

    [Header("Tiles - Terrain")]
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase ceilingTile;
    [SerializeField] private bool flipCeilingY = true;
    [SerializeField] private TileBase wallTile;

    [Header("Placement Settings")]
    [SerializeField] private int startX = 0;
    [SerializeField] private int floorYRow = 0;
    [SerializeField] private int ceilingYRow = 12;

    [Header("Obstacles")]
    [Tooltip("If true, obstacle startTileY is relative to floorYRow; otherwise absolute tile Y.")]
    [SerializeField] private bool obstacleYRelativeToFloor = true;
    [SerializeField] private Transform obstaclesParent;
    [SerializeField] private List<ObstacleSpawner.ObstaclePrefabEntry> obstaclePrefabs = new List<ObstacleSpawner.ObstaclePrefabEntry>();

    [Header("Signal Rendering")]
    [Tooltip("Optional LineRenderer-based signal visualizer. When assigned, replaces tilemap rendering for diagram signals.")]
    [SerializeField] private SignalLineRenderer signalLineRenderer;

    [Header("HUD Labels")]
    [SerializeField] private SignalLabelRenderer signalLabelRenderer;

    [Header("Debug")]
    [SerializeField] private bool debugLogOutputVector = false;
    [SerializeField][TextArea] private string debugOutputVector;

    #endregion

    #region Components & Cache

    private TilemapRenderer tilemapRenderer;
    private ObstacleSpawner obstacleSpawner;

    /// <summary>
    /// Cached level length in tiles, used when reapplying signal colors via <see cref="ApplySignalColors"/>.
    /// </summary>
    private int _cachedLevelLength;

    /// <summary>
    /// Cached Y coordinate for J signal line.
    /// </summary>
    private int _cachedJ_Y;

    /// <summary>
    /// Cached Y coordinate for K signal line.
    /// </summary>
    private int _cachedK_Y;

    /// <summary>
    /// Cached Y coordinate for Clock signal line.
    /// </summary>
    private int _cachedClock_Y;

    /// <summary>
    /// Cached Y coordinate for Preset signal line.
    /// </summary>
    private int _cachedPresetY;

    /// <summary>
    /// Cached Y coordinate for Clear signal line.
    /// </summary>
    private int _cachedClearY;

    /// <summary>
    /// Flag indicating if Preset signal was provided in the loaded level data.
    /// </summary>
    private bool _hasPreset;

    /// <summary>
    /// Flag indicating if Clear signal was provided in the loaded level data.
    /// </summary>
    private bool _hasClear;

    #endregion

    #region Parsed Signals

    /// <summary>
    /// Gets the parsed boolean sequence for the J input signal.
    /// </summary>
    public bool[] ParsedJSignal { get; private set; }

    /// <summary>
    /// Gets the parsed boolean sequence for the K input signal.
    /// </summary>
    public bool[] ParsedKSignal { get; private set; }

    /// <summary>
    /// Gets the parsed boolean sequence for the asynchronous Preset input signal.
    /// </summary>
    public bool[] ParsedPresetSignal { get; private set; }

    /// <summary>
    /// Gets the parsed boolean sequence for the asynchronous Clear input signal.
    /// </summary>
    public bool[] ParsedClearSignal { get; private set; }

    /// <summary>
    /// Gets the computed boolean output timeline for the level's flip-flop simulator.
    /// </summary>
    public bool[] OutputTimeline { get; private set; }

    /// <summary>
    /// Gets whether asynchronous signals are active high (true) or active low (false).
    /// </summary>
    public bool asyncActiveHigh { get; private set; }

    [SerializeField] private string[] outputOpsPerTile;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Subscribes to the <see cref="SignalColorManager.OnColorsChanged"/> event to update line colors in real time.
    /// </summary>
    private void OnEnable()
    {
        SignalColorManager.OnColorsChanged += ApplySignalColors;
    }

    /// <summary>
    /// Unsubscribes from the <see cref="SignalColorManager.OnColorsChanged"/> event.
    /// </summary>
    private void OnDisable()
    {
        SignalColorManager.OnColorsChanged -= ApplySignalColors;
    }

    /// <summary>
    /// Initializes level data source, parses JSON, configures <see cref="LevelManager"/>,
    /// simulates flip-flop operations via <see cref="FlipFlopSimulator"/>, and renders tilemaps and obstacles.
    /// </summary>
    private void Awake()
    {
        string json = ResolveJsonSource();
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogError("LevelJsonLoader: No JSON source available.");
            return;
        }

        LevelData data = ParseJson(json);
        if (data == null) return;

#if UNITY_EDITOR
        if (!ValidateAll(data)) return;
#endif

        InitComponents();
        ApplyLevelConfig(data);
        LoadSignals(data);
        RenderLevel(data);
    }

    #endregion

    #region Initialization Helpers

    /// <summary>
    /// Resolves the raw JSON string source from <see cref="UploadedLevelJson"/>, <see cref="MenuManager"/>, or default <see cref="levelFile"/>.
    /// </summary>
    /// <returns>Raw JSON string or null if unassigned.</returns>
    private string ResolveJsonSource()
    {
        if (!string.IsNullOrEmpty(UploadedLevelJson.Content))
        {
            string content = UploadedLevelJson.Content;
            UploadedLevelJson.Content = null;
            return content;
        }

        if (!string.IsNullOrEmpty(MenuManager.LevelToLoadJSON))
        {
            var asset = Resources.Load<TextAsset>("Levels/" + MenuManager.LevelToLoadJSON);
            if (asset != null) return asset.text;
            Debug.LogError($"LevelJsonLoader: File '{MenuManager.LevelToLoadJSON}' not found in Resources/Levels/.");
        }

        if (levelFile != null && !string.IsNullOrWhiteSpace(levelFile.text))
            return levelFile.text;

        return null;
    }

    /// <summary>
    /// Deserializes a raw JSON string into a <see cref="LevelData"/> object.
    /// </summary>
    /// <param name="json">Raw JSON string.</param>
    /// <returns>Deserialized <see cref="LevelData"/> object, or null on error.</returns>
    private LevelData ParseJson(string json)
    {
        try
        {
            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
            };
            return JsonConvert.DeserializeObject<LevelData>(json, settings);
        }
        catch (JsonException ex)
        {
            Debug.LogError($"LevelJsonLoader: Error parsing JSON — {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Instantiates helper components <see cref="TilemapRenderer"/> and <see cref="ObstacleSpawner"/>.
    /// </summary>
    private void InitComponents()
    {
        tilemapRenderer = new TilemapRenderer(
            inputTilemap, terrainTilemap, clockTilemap,
            diagramTiles, floorTile, ceilingTile, wallTile,
            flipCeilingY, startX);

        obstacleSpawner = new ObstacleSpawner(
            obstaclesParent, obstaclePrefabs, terrainTilemap,
            startX, floorYRow, obstacleYRelativeToFloor);

        if (signalLineRenderer == null)
        {
            signalLineRenderer = GetComponent<SignalLineRenderer>() ??
                                 GetComponentInChildren<SignalLineRenderer>() ??
                                 FindFirstObjectByType<SignalLineRenderer>();

            if (signalLineRenderer == null)
            {
                var lineObj = new GameObject("SignalLineRenderer");
                lineObj.transform.SetParent(transform, false);
                signalLineRenderer = lineObj.AddComponent<SignalLineRenderer>();
            }
        }
    }

    /// <summary>
    /// Loads input signal arrays from <see cref="LevelData"/> and invokes <see cref="FlipFlopSimulator.SimulateJK"/> to compute output.
    /// </summary>
    /// <param name="data">Loaded level data structure.</param>
    private void LoadSignals(LevelData data)
    {
        ParsedJSignal = data.JSignal;
        ParsedKSignal = data.KSignal;
        ParsedPresetSignal = data.PresetSignal;
        ParsedClearSignal = data.ClearSignal;

        asyncActiveHigh = data.AsyncActive != 0;

        GetClockSamplingParameters(out int clockStep, out _);
        int diagramLen = GetDiagramLength();

        try
        {
            OutputTimeline = FlipFlopSimulator.SimulateJK(
                ParsedJSignal, ParsedKSignal, ParsedPresetSignal, ParsedClearSignal,
                clockStep, diagramLen, out outputOpsPerTile, out _, asyncActiveHigh);
        }
        catch (InvalidOperationException ex)
        {
            Debug.LogError($"LevelJsonLoader: Error simulating flip-flop — {ex.Message}");
            OutputTimeline = null;
            outputOpsPerTile = null;
            return;
        }

        UpdateDebugOutputVectorString();

        if (debugLogOutputVector)
            Debug.Log($"LevelJsonLoader: Output timeline ({OutputTimeline?.Length ?? 0}): {debugOutputVector}");
    }

    /// <summary>
    /// Renders diagrams, clock lines, terrain tilemaps via <see cref="TilemapRenderer"/>,
    /// spawns obstacles via <see cref="ObstacleSpawner"/>, and configures HUD labels via <see cref="SignalLabelRenderer"/>.
    /// </summary>
    /// <param name="data">Loaded level data structure.</param>
    private void RenderLevel(LevelData data)
    {
        tilemapRenderer.ClearAllTilemaps();

        bool hasAsync = ParsedPresetSignal != null || ParsedClearSignal != null;
        int jY = 13;
        int kY = hasAsync ? 11 : 9;
        int presetY = 9;
        int clearY = 7;
        int clockY = 5;

        _hasPreset = ParsedPresetSignal != null;
        _hasClear = ParsedClearSignal != null;
        _cachedPresetY = presetY;
        _cachedClearY = clearY;

        Color jColor = SignalColorManager.Instance != null ? SignalColorManager.Instance.ColorJ : data.JSignalColor;
        Color kColor = SignalColorManager.Instance != null ? SignalColorManager.Instance.ColorK : data.KSignalColor;
        Color presetColor = SignalColorManager.Instance != null ? SignalColorManager.Instance.ColorPreset : data.PresetSignalColor;
        Color clearColor = SignalColorManager.Instance != null ? SignalColorManager.Instance.ColorClear : data.ClearSignalColor;
        Color clockColor = SignalColorManager.Instance != null ? SignalColorManager.Instance.ColorCLK : data.ClockSignalColor;

        GetClockSamplingParameters(out int clockStep, out _);
        int levelLength = LevelManager.Instance != null
            ? Mathf.RoundToInt(LevelManager.Instance.levelEndX)
            : 6 * data.ClockCycles;

        _cachedLevelLength = levelLength + clockStep;
        _cachedJ_Y = jY;
        _cachedK_Y = kY;
        _cachedClock_Y = clockY;

        bool risingEdge = string.Equals(data.ActiveClockEdge, "rising", StringComparison.OrdinalIgnoreCase);
        var clockPattern = BuildClockPattern(levelLength, clockStep, risingEdge);

        if (signalLineRenderer != null)
        {
            signalLineRenderer.ClearAllSignals();
            signalLineRenderer.RenderSignals(
                ParsedJSignal, ParsedKSignal, ParsedPresetSignal, ParsedClearSignal,
                clockPattern,
                jColor, kColor, presetColor, clearColor, clockColor,
                startX);
        }
        else
        {
            tilemapRenderer.RenderDiagram(ParsedJSignal, jY, jColor);
            tilemapRenderer.RenderDiagram(ParsedKSignal, kY, kColor);
            if (ParsedPresetSignal != null) tilemapRenderer.RenderDiagram(ParsedPresetSignal, presetY, presetColor);
            if (ParsedClearSignal != null) tilemapRenderer.RenderDiagram(ParsedClearSignal, clearY, clearColor);
            tilemapRenderer.RenderClock(clockPattern, clockY, clockColor);
        }

        tilemapRenderer.RenderTerrain(data.Floor, data.Ceiling, floorYRow, ceilingYRow, 3);
        tilemapRenderer.CompleteStaticScenery(floorYRow, ceilingYRow);

        if (data.Obstacles?.Count > 0)
            obstacleSpawner.SpawnObstacles(data.Obstacles);

        if (signalLabelRenderer != null)
        {
            bool asyncIsHigh = data.AsyncActive == 1;

            if (signalLineRenderer != null)
            {
                bool hasK = ParsedKSignal != null && ParsedKSignal.Length > 0;
                string mainLabel = hasK ? "J" : "D";

                signalLabelRenderer.GenerateLabels(
                    signalLineRenderer.GetJWorldY(),
                    signalLineRenderer.GetKWorldY(),
                    signalLineRenderer.GetPresetWorldY(),
                    signalLineRenderer.GetClearWorldY(),
                    signalLineRenderer.GetClockWorldY(),
                    ParsedPresetSignal != null,
                    ParsedClearSignal != null,
                    asyncIsHigh,
                    risingEdge,
                    hasK,
                    mainLabel
                );
            }
            else
            {
                signalLabelRenderer.GenerateLabels(
                    jY, kY, presetY, clearY, clockY,
                    ParsedPresetSignal != null,
                    ParsedClearSignal != null,
                    asyncIsHigh,
                    risingEdge
                );
            }
        }
    }

    #endregion

    #region Real-time Color Updates

    /// <summary>
    /// Reapplies signal line colors to LineRenderers or tilemaps when custom user colors are modified in settings.
    /// Responds to the <see cref="SignalColorManager.OnColorsChanged"/> event.
    /// </summary>
    private void ApplySignalColors()
    {
        if (SignalColorManager.Instance == null) return;

        Color jColor = SignalColorManager.Instance.ColorJ;
        Color kColor = SignalColorManager.Instance.ColorK;
        Color clkColor = SignalColorManager.Instance.ColorCLK;
        Color presetColor = _hasPreset ? SignalColorManager.Instance.ColorPreset : Color.white;
        Color clearColor = _hasClear ? SignalColorManager.Instance.ColorClear : Color.white;

        if (signalLineRenderer != null)
        {
            signalLineRenderer.UpdateColors(jColor, kColor, clkColor, presetColor, clearColor);
            return;
        }

        if (tilemapRenderer == null || _cachedLevelLength <= 0) return;

        ColorRow(inputTilemap, _cachedLevelLength, _cachedJ_Y, startX, jColor);
        ColorRow(inputTilemap, _cachedLevelLength, _cachedK_Y, startX, kColor);
        ColorRow(clockTilemap, _cachedLevelLength, _cachedClock_Y, startX, clkColor);

        if (_hasPreset)
        {
            ColorRow(inputTilemap, _cachedLevelLength, _cachedPresetY, startX, presetColor);
        }

        if (_hasClear)
        {
            ColorRow(inputTilemap, _cachedLevelLength, _cachedClearY, startX, clearColor);
        }
    }

    /// <summary>
    /// Recolors a specified horizontal row of tiles in a tilemap.
    /// </summary>
    /// <param name="map">Target tilemap.</param>
    /// <param name="length">Length of the tile row to color.</param>
    /// <param name="yRow">Y tile coordinate row.</param>
    /// <param name="baseX">Starting X tile coordinate.</param>
    /// <param name="color">New color to apply.</param>
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

    #endregion

    #region Public API

    /// <summary>
    /// Computes and returns output state transition events based on parsed signals.
    /// Invokes <see cref="FlipFlopSimulator.SimulateJK"/> and converts output events for <see cref="PathVerifier"/>.
    /// </summary>
    /// <returns>List of signal transition events for path verification.</returns>
    public List<PathVerifier.SignalEvent> ComputeOutputEventsFromParsedSignals()
    {
        int diagramLen = GetDiagramLength();
        if (diagramLen <= 0) return null;

        GetClockSamplingParameters(out int step, out _);
        FlipFlopSimulator.SimulateJK(
            ParsedJSignal, ParsedKSignal, ParsedPresetSignal, ParsedClearSignal,
            step, diagramLen, out _, out var events, asyncActiveHigh);

        var pathEvents = new List<PathVerifier.SignalEvent>();
        if (events != null)
            foreach (var evt in events)
                pathEvents.Add(new PathVerifier.SignalEvent(evt.x, evt.value));

        return pathEvents;
    }

    #endregion

    #region Helpers & Validations

    /// <summary>
    /// Gets clock step and sampling offset parameters based on <see cref="LevelManager.Instance"/>.
    /// </summary>
    private void GetClockSamplingParameters(out int step, out int startOffset)
    {
        float stepF = (LevelManager.Instance != null && LevelManager.Instance.clockStepX > 0f)
            ? LevelManager.Instance.clockStepX
            : 1f;
        step = Mathf.Max(1, Mathf.RoundToInt(stepF));
        startOffset = step;
    }

    /// <summary>
    /// Calculates the diagram length from <see cref="LevelManager.Instance"/> or input signal arrays.
    /// </summary>
    private int GetDiagramLength()
    {
        int len = LevelManager.Instance != null
            ? Mathf.RoundToInt(LevelManager.Instance.diagramEndX)
            : 0;
        return len > 0
            ? len
            : FlipFlopSimulator.MaxLen(ParsedJSignal, ParsedKSignal, ParsedPresetSignal, ParsedClearSignal);
    }

    /// <summary>
    /// Applies level boundaries and parameters from <see cref="LevelData"/> into <see cref="LevelManager.Instance"/>.
    /// </summary>
    private void ApplyLevelConfig(LevelData data)
    {
        if (LevelManager.Instance == null) return;

        if (data.ClockCycles <= 0)
        {
            Debug.LogWarning($"LevelJsonLoader: clockCycles={data.ClockCycles} invalid. Defaulting to 10.");
            data.ClockCycles = 10;
        }

        const int step = 6;
        LevelManager.Instance.clockStepX = step;
        LevelManager.Instance.diagramEndX = data.ClockCycles * step;
        LevelManager.Instance.phaseEndX = LevelManager.Instance.diagramEndX + LevelManager.Instance.phaseSlackTiles;
        LevelManager.Instance.levelEndX = LevelManager.Instance.diagramEndX;
    }

    /// <summary>
    /// Validates all required inspector fields and level data before rendering.
    /// </summary>
    private bool ValidateAll(LevelData data)
    {
        if (data == null)
        { Debug.LogError("LevelJsonLoader: Null LevelData (JSON parse failure)."); return false; }

        if (inputTilemap == null || terrainTilemap == null || clockTilemap == null)
        { Debug.LogError("LevelJsonLoader: One or more Tilemap references missing."); return false; }

        if (diagramTiles == null || diagramTiles.Length < 8)
        { Debug.LogError("LevelJsonLoader: diagramTiles array requires 8 elements (0..7)."); return false; }

        for (int i = 0; i < 8; i++)
            if (diagramTiles[i] == null)
            { Debug.LogError($"LevelJsonLoader: diagramTiles[{i}] is unassigned."); return false; }

        if (floorTile == null || ceilingTile == null)
        { Debug.LogError("LevelJsonLoader: floorTile or ceilingTile unassigned."); return false; }

        return true;
    }

    /// <summary>
    /// Updates the debug string representation of the output binary timeline.
    /// </summary>
    private void UpdateDebugOutputVectorString()
    {
        if (OutputTimeline == null) { debugOutputVector = "(null)"; return; }
        var sb = new StringBuilder(OutputTimeline.Length);
        foreach (bool b in OutputTimeline) sb.Append(b ? '1' : '0');
        debugOutputVector = sb.ToString();
    }

    /// <summary>
    /// Builds the repetitive clock signal pattern array.
    /// Falling edge: 000111 (transition 1->0).
    /// Rising edge: 111000 (transition 0->1).
    /// </summary>
    /// <param name="totalLength">Total totalLength in tiles.</param>
    /// <param name="step">Clock step width in tiles.</param>
    /// <param name="risingEdge">Whether clock active edge is rising or falling.</param>
    /// <returns>Integer pattern array for clock rendering.</returns>
    public static int[] BuildClockPattern(int totalLength, int step, bool risingEdge = false)
    {
        if (totalLength <= 0 || step <= 0) return null;

        int half = step / 2;
        int a = risingEdge ? 1 : 0;
        int b = risingEdge ? 0 : 1;
        var period = new int[step];

        for (int i = 0; i < half; i++) period[i] = a;
        for (int i = half; i < step; i++) period[i] = b;

        var arr = new int[totalLength + step];
        for (int i = 0; i < arr.Length; i++) arr[i] = period[i % step];
        return arr;
    }

    #endregion

    #region Context Menu Debug

    /// <summary>
    /// Context menu option to log the binary output vector string in the Unity Console.
    /// </summary>
    [ContextMenu("Log Output Vector (0/1)")]
    private void LogOutputVectorContext()
    {
        if (OutputTimeline == null)
        {
            GetClockSamplingParameters(out int clockStep, out _);
            OutputTimeline = FlipFlopSimulator.SimulateJK(
                ParsedJSignal, ParsedKSignal, ParsedPresetSignal, ParsedClearSignal,
                clockStep, GetDiagramLength(), out outputOpsPerTile, out _, asyncActiveHigh);
            UpdateDebugOutputVectorString();
        }
        Debug.Log($"LevelJsonLoader: Output timeline ({OutputTimeline?.Length ?? 0}): {debugOutputVector}");
    }

    /// <summary>
    /// Context menu option to log flip-flop operation tokens in the Unity Console.
    /// </summary>
    [ContextMenu("Log Output Ops")]
    private void LogOutputOpsContext()
    {
        if (outputOpsPerTile == null)
        {
            GetClockSamplingParameters(out int clockStep, out _);
            OutputTimeline = FlipFlopSimulator.SimulateJK(
                ParsedJSignal, ParsedKSignal, ParsedPresetSignal, ParsedClearSignal,
                clockStep, GetDiagramLength(), out outputOpsPerTile, out _, asyncActiveHigh);
        }
        var sb = new StringBuilder(outputOpsPerTile.Length * 6);
        for (int i = 0; i < outputOpsPerTile.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(outputOpsPerTile[i]);
        }
        Debug.Log($"LevelJsonLoader: Ops per tile ({outputOpsPerTile.Length}): {sb}");
    }

    #endregion
}