using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

/// <summary>
/// Scene-independent data manager for signal colors and accessibility palettes.
/// Encapsulates preset swatches, colorblind/contrast palettes, custom RGB colors, PlayerPrefs persistence, and change events.
/// Interacted with by <see cref="ColorSettingsTab"/> for UI, and consumed by <see cref="LevelJsonLoader"/> and <see cref="PathVerifier"/>.
/// </summary>
public class SignalColorManager
{
    #region Singleton

    private static SignalColorManager _instance;

    /// <summary>
    /// Gets the singleton instance, auto-initializing on first access.
    /// </summary>
    public static SignalColorManager Instance
    {
        get
        {
            if (_instance == null)
                Initialize();
            return _instance;
        }
    }

    /// <summary>
    /// Creates the singleton instance and loads color preferences from <see cref="PlayerPrefs"/>.
    /// </summary>
    public static void Initialize()
    {
        if (_instance == null)
        {
            _instance = new SignalColorManager();
            _instance.Load();
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Event invoked whenever any signal color configuration is changed.
    /// Listened to by <see cref="LevelJsonLoader"/> and <see cref="PathVerifier"/>.
    /// </summary>
    public static event Action OnColorsChanged;

    #endregion

    #region Constants & Defaults

    public const int CUSTOM_INDEX = -1;

    private const int DEFAULT_J_IDX = 0;
    private const int DEFAULT_K_IDX = 0;
    private const int DEFAULT_CLK_IDX = 6;
    private const int DEFAULT_PRESET_IDX = 0;
    private const int DEFAULT_CLEAR_IDX = 0;
    private const int DEFAULT_FEEDBACK_SUC_IDX = 4;
    private const int DEFAULT_FEEDBACK_FAIL_IDX = 7;

    private const string KEY_J_IDX = "SignalJ_Index";
    private const string KEY_K_IDX = "SignalK_Index";
    private const string KEY_CLK_IDX = "SignalCLK_Index";
    private const string KEY_PRESET_IDX = "SignalPreset_Index";
    private const string KEY_CLEAR_IDX = "SignalClear_Index";
    private const string KEY_FEEDBACK_SUC_IDX = "SignalFeedbackSuccess_Index";
    private const string KEY_FEEDBACK_FAIL_IDX = "SignalFeedbackFailure_Index";

    private const string KEY_J_R = "SignalJ_R";
    private const string KEY_J_G = "SignalJ_G";
    private const string KEY_J_B = "SignalJ_B";
    private const string KEY_K_R = "SignalK_R";
    private const string KEY_K_G = "SignalK_G";
    private const string KEY_K_B = "SignalK_B";
    private const string KEY_CLK_R = "SignalCLK_R";
    private const string KEY_CLK_G = "SignalCLK_G";
    private const string KEY_CLK_B = "SignalCLK_B";
    private const string KEY_PRESET_R = "SignalPreset_R";
    private const string KEY_PRESET_G = "SignalPreset_G";
    private const string KEY_PRESET_B = "SignalPreset_B";
    private const string KEY_CLEAR_R = "SignalClear_R";
    private const string KEY_CLEAR_G = "SignalClear_G";
    private const string KEY_CLEAR_B = "SignalClear_B";
    private const string KEY_FSUC_R = "SignalFSuc_R";
    private const string KEY_FSUC_G = "SignalFSuc_G";
    private const string KEY_FSUC_B = "SignalFSuc_B";
    private const string KEY_FFAIL_R = "SignalFFail_R";
    private const string KEY_FFAIL_G = "SignalFFail_G";
    private const string KEY_FFAIL_B = "SignalFFail_B";

    #endregion

    #region Preset Colors & Palettes

    /// <summary>
    /// Array of predefined preset colors available for signal swatch selection.
    /// Index order: 0 White (#bdc0ce), 1 Yellow, 2 Cyan, 3 Orange, 4 Green, 5 Magenta, 6 Blue, 7 Red, 8 Pink, 9 Purple.
    /// </summary>
    public static readonly Color[] PresetColors = new Color[]
    {
        new Color(0xBD / 255f, 0xC0 / 255f, 0xCE / 255f), // 0 White (#bdc0ce)
        new Color(1.00f, 0.93f, 0.00f),         // 1 Yellow
        new Color(0.00f, 1.00f, 1.00f),         // 2 Cyan
        new Color(1.00f, 0.55f, 0.00f),         // 3 Orange
        new Color(0.27f, 1.00f, 0.27f),         // 4 Green
        new Color(1.00f, 0.13f, 1.00f),         // 5 Magenta
        new Color(0.11f, 0.56f, 1.00f),         // 6 Blue
        new Color(1.00f, 0.20f, 0.20f),         // 7 Red
        new Color(1.00f, 0.53f, 0.80f),         // 8 Pink
        new Color(0.73f, 0.27f, 1.00f),         // 9 Purple
    };

    /// <summary>
    /// Array of accessibility color palettes mapping preset color indices for each signal.
    /// Order per palette: [J, K, Preset, Clear, CLK, FeedbackSuccess, FeedbackFailure].
    /// [0] Default, [1] Colorblindness, [2] High Contrast.
    /// </summary>
    public static readonly int[][] Palettes = new int[][]
    {
        new[] { 0, 0, 3, 3, 6, 4, 7 }, // Default
        new[] { 1, 9, 2, 3, 6, 7, 5 }, // Colorblind
        new[] { 1, 5, 3, 2, 6, 4, 7 }, // High Contrast
    };

    #endregion

    #region Properties

    public Color ColorJ { get; set; }
    public Color ColorK { get; set; }
    public Color ColorCLK { get; set; }
    public Color ColorPreset { get; set; }
    public Color ColorClear { get; set; }
    public Color ColorFeedbackSuccess { get; set; }
    public Color ColorFeedbackFailure { get; set; }

    public int IndexJ { get; set; }
    public int IndexK { get; set; }
    public int IndexCLK { get; set; }
    public int IndexPreset { get; set; }
    public int IndexClear { get; set; }
    public int IndexFeedbackSuccess { get; set; }
    public int IndexFeedbackFailure { get; set; }

    #endregion

    #region Load & Save

    /// <summary>
    /// Loads stored signal preset indices and custom RGB color values from <see cref="PlayerPrefs"/>.
    /// </summary>
    public void Load()
    {
        IndexJ = PlayerPrefs.GetInt(KEY_J_IDX, DEFAULT_J_IDX);
        IndexK = PlayerPrefs.GetInt(KEY_K_IDX, DEFAULT_K_IDX);
        IndexCLK = PlayerPrefs.GetInt(KEY_CLK_IDX, DEFAULT_CLK_IDX);
        IndexPreset = PlayerPrefs.GetInt(KEY_PRESET_IDX, DEFAULT_PRESET_IDX);
        IndexClear = PlayerPrefs.GetInt(KEY_CLEAR_IDX, DEFAULT_CLEAR_IDX);
        IndexFeedbackSuccess = PlayerPrefs.GetInt(KEY_FEEDBACK_SUC_IDX, DEFAULT_FEEDBACK_SUC_IDX);
        IndexFeedbackFailure = PlayerPrefs.GetInt(KEY_FEEDBACK_FAIL_IDX, DEFAULT_FEEDBACK_FAIL_IDX);

        ColorJ = LoadColorForSignal("J", IndexJ, DEFAULT_J_IDX);
        ColorK = LoadColorForSignal("K", IndexK, DEFAULT_K_IDX);
        ColorCLK = LoadColorForSignal("CLK", IndexCLK, DEFAULT_CLK_IDX);
        ColorPreset = LoadColorForSignal("Preset", IndexPreset, DEFAULT_PRESET_IDX);
        ColorClear = LoadColorForSignal("Clear", IndexClear, DEFAULT_CLEAR_IDX);
        ColorFeedbackSuccess = LoadColorForSignal("FeedbackSuccess", IndexFeedbackSuccess, DEFAULT_FEEDBACK_SUC_IDX);
        ColorFeedbackFailure = LoadColorForSignal("FeedbackFailure", IndexFeedbackFailure, DEFAULT_FEEDBACK_FAIL_IDX);
    }

    /// <summary>
    /// Saves all changes to <see cref="PlayerPrefs"/> and invokes <see cref="OnColorsChanged"/>.
    /// </summary>
    public void NotifyColorsChanged()
    {
        PlayerPrefs.Save();
        OnColorsChanged?.Invoke();
    }

    #endregion

    #region Public Color API

    /// <summary>
    /// Applies a preset color palette by index across all signals and saves choices to <see cref="PlayerPrefs"/>.
    /// Invokes <see cref="OnColorsChanged"/>.
    /// </summary>
    /// <param name="paletteIndex">Index of palette to apply from <see cref="Palettes"/>.</param>
    public void ApplyPalette(int paletteIndex)
    {
        if (paletteIndex < 0 || paletteIndex >= Palettes.Length)
            return;

        int[] palette = Palettes[paletteIndex];

        SetColorByIndex("J", palette[0]);
        SetColorByIndex("K", palette[1]);
        SetColorByIndex("Preset", palette[2]);
        SetColorByIndex("Clear", palette[3]);
        SetColorByIndex("CLK", palette[4]);
        SetColorByIndex("FeedbackSuccess", palette[5]);
        SetColorByIndex("FeedbackFailure", palette[6]);

        NotifyColorsChanged();
    }

    /// <summary>
    /// Sets a signal color by preset index and saves to <see cref="PlayerPrefs"/> without firing notifications.
    /// </summary>
    /// <param name="signal">Signal name string ("J", "K", "CLK", "Preset", "Clear", "FeedbackSuccess", "FeedbackFailure").</param>
    /// <param name="colorIndex">Index from <see cref="PresetColors"/>.</param>
    public void SetColorByIndex(string signal, int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= PresetColors.Length) return;
        ApplyToSignal(signal, colorIndex, PresetColors[colorIndex]);
        SaveIndex(signal, colorIndex);
    }

    /// <summary>
    /// Sets a signal color by preset index, saves to <see cref="PlayerPrefs"/>, and triggers <see cref="OnColorsChanged"/>.
    /// </summary>
    /// <param name="signal">Signal name string.</param>
    /// <param name="colorIndex">Index from <see cref="PresetColors"/>.</param>
    public void SetAndNotify(string signal, int colorIndex)
    {
        SetColorByIndex(signal, colorIndex);
        NotifyColorsChanged();
    }

    /// <summary>
    /// Sets a custom RGB color for a signal, updates index to <see cref="CUSTOM_INDEX"/>, saves to <see cref="PlayerPrefs"/>, and triggers <see cref="OnColorsChanged"/>.
    /// </summary>
    /// <param name="signal">Signal name string.</param>
    /// <param name="color">Custom RGBA <see cref="Color"/> value.</param>
    public void SetCustomColor(string signal, Color color)
    {
        ApplyToSignal(signal, CUSTOM_INDEX, color);
        SaveCustomColor(signal, color);
        SaveIndex(signal, CUSTOM_INDEX);
        NotifyColorsChanged();
    }

    /// <summary>
    /// Restores all signal colors to default preset indices, saves to <see cref="PlayerPrefs"/>, and triggers <see cref="OnColorsChanged"/>.
    /// </summary>
    public void RestoreDefaultColors()
    {
        SetColorByIndex("J", DEFAULT_J_IDX);
        SetColorByIndex("K", DEFAULT_K_IDX);
        SetColorByIndex("CLK", DEFAULT_CLK_IDX);
        SetColorByIndex("Preset", DEFAULT_PRESET_IDX);
        SetColorByIndex("Clear", DEFAULT_CLEAR_IDX);
        SetColorByIndex("FeedbackSuccess", DEFAULT_FEEDBACK_SUC_IDX);
        SetColorByIndex("FeedbackFailure", DEFAULT_FEEDBACK_FAIL_IDX);
        NotifyColorsChanged();
    }

    /// <summary>
    /// Evaluates current active signal indices to determine matching preset palette index from <see cref="Palettes"/>, or returns <see cref="CUSTOM_INDEX"/>.
    /// </summary>
    /// <returns>Palette index (0, 1, 2) or <see cref="CUSTOM_INDEX"/> (-1).</returns>
    public int GetCurrentPaletteIndex()
    {
        for (int i = 0; i < Palettes.Length; i++)
        {
            int[] palette = Palettes[i];

            if (palette[0] == IndexJ &&
                palette[1] == IndexK &&
                palette[2] == IndexPreset &&
                palette[3] == IndexClear &&
                palette[4] == IndexCLK &&
                palette[5] == IndexFeedbackSuccess &&
                palette[6] == IndexFeedbackFailure)
            {
                return i;
            }
        }

        return CUSTOM_INDEX;
    }

    /// <summary>
    /// Retrieves localized palette name string from Unity Localization table ("ConfigMenu").
    /// Interacts with <see cref="LocalizationSettings.StringDatabase"/>.
    /// </summary>
    /// <param name="index">Palette index (0: Default, 1: Colorblind, 2: High Contrast, else Custom).</param>
    /// <returns>Localized palette string.</returns>
    public static string GetLocalizedPaletteName(int index)
    {
        string tableName = "ConfigMenu";

        string key = index switch
        {
            0 => "palette_default",
            1 => "palette_colorblind",
            2 => "palette_highcontrast",
            _ => "palette_custom"
        };

        try
        {
            if (LocalizationSettings.InitializationOperation.IsDone && LocalizationSettings.StringDatabase != null)
            {
                string localized = LocalizationSettings.StringDatabase.GetLocalizedString(tableName, key);
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
        }
        catch
        {
            // Fallback for cold start before Localization system finishes async initialization
        }

        return index switch
        {
            0 => "Padrão",
            1 => "Daltonismo",
            2 => "Alto Contraste",
            _ => "Personalizado"
        };
    }

    #endregion

    #region Private Helpers

    private void ApplyToSignal(string signal, int index, Color color)
    {
        switch (signal)
        {
            case "J": IndexJ = index; ColorJ = color; break;
            case "K": IndexK = index; ColorK = color; break;
            case "CLK": IndexCLK = index; ColorCLK = color; break;
            case "Preset": IndexPreset = index; ColorPreset = color; break;
            case "Clear": IndexClear = index; ColorClear = color; break;
            case "FeedbackSuccess": IndexFeedbackSuccess = index; ColorFeedbackSuccess = color; break;
            case "FeedbackFailure": IndexFeedbackFailure = index; ColorFeedbackFailure = color; break;
        }
    }

    private void SaveIndex(string signal, int index)
    {
        switch (signal)
        {
            case "J": PlayerPrefs.SetInt(KEY_J_IDX, index); break;
            case "K": PlayerPrefs.SetInt(KEY_K_IDX, index); break;
            case "CLK": PlayerPrefs.SetInt(KEY_CLK_IDX, index); break;
            case "Preset": PlayerPrefs.SetInt(KEY_PRESET_IDX, index); break;
            case "Clear": PlayerPrefs.SetInt(KEY_CLEAR_IDX, index); break;
            case "FeedbackSuccess": PlayerPrefs.SetInt(KEY_FEEDBACK_SUC_IDX, index); break;
            case "FeedbackFailure": PlayerPrefs.SetInt(KEY_FEEDBACK_FAIL_IDX, index); break;
        }
    }

    private void SaveCustomColor(string signal, Color c)
    {
        switch (signal)
        {
            case "J":
                PlayerPrefs.SetFloat(KEY_J_R, c.r); PlayerPrefs.SetFloat(KEY_J_G, c.g); PlayerPrefs.SetFloat(KEY_J_B, c.b);
                break;
            case "K":
                PlayerPrefs.SetFloat(KEY_K_R, c.r); PlayerPrefs.SetFloat(KEY_K_G, c.g); PlayerPrefs.SetFloat(KEY_K_B, c.b);
                break;
            case "CLK":
                PlayerPrefs.SetFloat(KEY_CLK_R, c.r); PlayerPrefs.SetFloat(KEY_CLK_G, c.g); PlayerPrefs.SetFloat(KEY_CLK_B, c.b);
                break;
            case "Preset":
                PlayerPrefs.SetFloat(KEY_PRESET_R, c.r); PlayerPrefs.SetFloat(KEY_PRESET_G, c.g); PlayerPrefs.SetFloat(KEY_PRESET_B, c.b);
                break;
            case "Clear":
                PlayerPrefs.SetFloat(KEY_CLEAR_R, c.r); PlayerPrefs.SetFloat(KEY_CLEAR_G, c.g); PlayerPrefs.SetFloat(KEY_CLEAR_B, c.b);
                break;
            case "FeedbackSuccess":
                PlayerPrefs.SetFloat(KEY_FSUC_R, c.r); PlayerPrefs.SetFloat(KEY_FSUC_G, c.g); PlayerPrefs.SetFloat(KEY_FSUC_B, c.b);
                break;
            case "FeedbackFailure":
                PlayerPrefs.SetFloat(KEY_FFAIL_R, c.r); PlayerPrefs.SetFloat(KEY_FFAIL_G, c.g); PlayerPrefs.SetFloat(KEY_FFAIL_B, c.b);
                break;
        }
    }

    private Color LoadColorForSignal(string signal, int index, int defaultIndex)
    {
        if (index == CUSTOM_INDEX)
        {
            Color def = PresetColors[0];
            switch (signal)
            {
                case "J": return new Color(PlayerPrefs.GetFloat(KEY_J_R, def.r), PlayerPrefs.GetFloat(KEY_J_G, def.g), PlayerPrefs.GetFloat(KEY_J_B, def.b));
                case "K": return new Color(PlayerPrefs.GetFloat(KEY_K_R, def.r), PlayerPrefs.GetFloat(KEY_K_G, def.g), PlayerPrefs.GetFloat(KEY_K_B, def.b));
                case "CLK": return new Color(PlayerPrefs.GetFloat(KEY_CLK_R, def.r), PlayerPrefs.GetFloat(KEY_CLK_G, def.g), PlayerPrefs.GetFloat(KEY_CLK_B, def.b));
                case "Preset": return new Color(PlayerPrefs.GetFloat(KEY_PRESET_R, def.r), PlayerPrefs.GetFloat(KEY_PRESET_G, def.g), PlayerPrefs.GetFloat(KEY_PRESET_B, def.b));
                case "Clear": return new Color(PlayerPrefs.GetFloat(KEY_CLEAR_R, def.r), PlayerPrefs.GetFloat(KEY_CLEAR_G, def.g), PlayerPrefs.GetFloat(KEY_CLEAR_B, def.b));
                case "FeedbackSuccess": return new Color(PlayerPrefs.GetFloat(KEY_FSUC_R, def.r), PlayerPrefs.GetFloat(KEY_FSUC_G, def.g), PlayerPrefs.GetFloat(KEY_FSUC_B, def.b));
                case "FeedbackFailure": return new Color(PlayerPrefs.GetFloat(KEY_FFAIL_R, def.r), PlayerPrefs.GetFloat(KEY_FFAIL_G, def.g), PlayerPrefs.GetFloat(KEY_FFAIL_B, def.b));
            }
        }

        int clamped = Mathf.Clamp(index, 0, PresetColors.Length - 1);
        return PresetColors[clamped];
    }

    #endregion
}