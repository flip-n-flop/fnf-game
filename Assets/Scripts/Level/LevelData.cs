using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

#region JSON Converters

/// <summary>
/// JSON converter for parsing string representations of binary sequences (e.g. "1011 00") into boolean arrays.
/// Used during level JSON deserialization in <see cref="LevelJsonLoader"/>.
/// </summary>
public class BinaryBoolArrayConverter : JsonConverter<bool[]>
{
    /// <summary>
    /// Reads and converts a binary string into a boolean array.
    /// </summary>
    public override bool[] ReadJson(JsonReader reader, Type objectType, bool[] existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        string data = (string)reader.Value;
        if (string.IsNullOrWhiteSpace(data)) return null;

        data = data.Replace(" ", "");
        bool[] result = new bool[data.Length];
        for (int i = 0; i < data.Length; i++) result[i] = data[i] == '1';
        return result;
    }

    /// <summary>
    /// Writing JSON is not implemented for this converter.
    /// </summary>
    public override void WriteJson(JsonWriter writer, bool[] value, JsonSerializer serializer) => throw new NotImplementedException();
}

/// <summary>
/// JSON converter for parsing hex color string values (e.g. "#FF0000") into Unity <see cref="Color"/> structures.
/// Used during level JSON deserialization in <see cref="LevelJsonLoader"/>.
/// </summary>
public class UnityColorConverter : JsonConverter<Color>
{
    /// <summary>
    /// Reads and converts a hex string into a Unity <see cref="Color"/>.
    /// </summary>
    public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        string hex = (string)reader.Value;
        if (string.IsNullOrWhiteSpace(hex)) return Color.white;

        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (ColorUtility.TryParseHtmlString(hex, out Color color)) return color;
        return Color.white;
    }

    /// <summary>
    /// Writing JSON is not implemented for this converter.
    /// </summary>
    public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer) => throw new NotImplementedException();
}

/// <summary>
/// JSON converter for parsing asynchronous active state settings ("high" / 1 vs "low" / 0).
/// Used during level JSON deserialization in <see cref="LevelJsonLoader"/>.
/// </summary>
public class AsyncActiveConverter : JsonConverter<int>
{
    /// <summary>
    /// Reads and converts string ("high"/"low") or integer values into an active mode integer (1=high, 0=low).
    /// </summary>
    public override int ReadJson(JsonReader reader, Type objectType, int existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            string val = ((string)reader.Value).ToLower().Trim();
            return (val == "high") ? 1 : 0;
        }
        if (reader.TokenType == JsonToken.Integer) return Convert.ToInt32(reader.Value);
        return 1;
    }

    /// <summary>
    /// Writing JSON is not implemented for this converter.
    /// </summary>
    public override void WriteJson(JsonWriter writer, int value, JsonSerializer serializer) => throw new NotImplementedException();
}

#endregion

#region Level Data DTO

/// <summary>
/// Data Transfer Object (DTO) representing the raw structure of a level loaded from JSON.
/// Contains signal sequences, colors, terrain bands, and obstacle parameters.
/// Deserialized by <see cref="LevelJsonLoader"/> and consumed by <see cref="FlipFlopSimulator"/>, <see cref="TilemapRenderer"/>, and <see cref="ObstacleSpawner"/>.
/// </summary>
[Serializable]
public class LevelData
{
    /// <summary>
    /// The display name of the level.
    /// </summary>
    [JsonProperty("levelName")]
    public string LevelName { get; set; }

    /// <summary>
    /// Total number of clock cycles present in the level.
    /// </summary>
    [JsonProperty("clockCycles")]
    public int ClockCycles { get; set; }

    /// <summary>
    /// Fallback setter property for legacy level JSON files containing the typo "clockCicles".
    /// </summary>
    [JsonProperty("clockCicles")]
    private int ClockCiclesLegacy { set => ClockCycles = ClockCycles > 0 ? ClockCycles : value; }

    /// <summary>
    /// Active state mode for asynchronous inputs (1 = active high, 0 = active low).
    /// </summary>
    [JsonProperty("asyncActive")]
    [JsonConverter(typeof(AsyncActiveConverter))]
    public int AsyncActive { get; set; } = 1;

    /// <summary>
    /// Active edge type for the clock signal ("rising" or "falling").
    /// </summary>
    [JsonProperty("activeClockEdge")]
    public string ActiveClockEdge { get; set; } = "falling";

    /// <summary>
    /// Boolean signal sequence for the J input line.
    /// </summary>
    [JsonProperty("jSignal")]
    [JsonConverter(typeof(BinaryBoolArrayConverter))]
    public bool[] JSignal { get; set; }

    /// <summary>
    /// Boolean signal sequence for the K input line.
    /// </summary>
    [JsonProperty("kSignal")]
    [JsonConverter(typeof(BinaryBoolArrayConverter))]
    public bool[] KSignal { get; set; }

    /// <summary>
    /// Boolean signal sequence for the asynchronous Preset input line.
    /// </summary>
    [JsonProperty("presetSignal")]
    [JsonConverter(typeof(BinaryBoolArrayConverter))]
    public bool[] PresetSignal { get; set; }

    /// <summary>
    /// Boolean signal sequence for the asynchronous Clear input line.
    /// </summary>
    [JsonProperty("clearSignal")]
    [JsonConverter(typeof(BinaryBoolArrayConverter))]
    public bool[] ClearSignal { get; set; }

    /// <summary>
    /// Custom rendering color for the J signal line.
    /// </summary>
    [JsonProperty("jSignalColor")]
    [JsonConverter(typeof(UnityColorConverter))]
    public Color JSignalColor { get; set; } = new Color(0xBD / 255f, 0xC0 / 255f, 0xCE / 255f);

    /// <summary>
    /// Custom rendering color for the K signal line.
    /// </summary>
    [JsonProperty("kSignalColor")]
    [JsonConverter(typeof(UnityColorConverter))]
    public Color KSignalColor { get; set; } = new Color(0xBD / 255f, 0xC0 / 255f, 0xCE / 255f);

    /// <summary>
    /// Custom rendering color for the Preset signal line.
    /// </summary>
    [JsonProperty("presetSignalColor")]
    [JsonConverter(typeof(UnityColorConverter))]
    public Color PresetSignalColor { get; set; } = new Color(0xBD / 255f, 0xC0 / 255f, 0xCE / 255f);

    /// <summary>
    /// Custom rendering color for the Clear signal line.
    /// </summary>
    [JsonProperty("clearSignalColor")]
    [JsonConverter(typeof(UnityColorConverter))]
    public Color ClearSignalColor { get; set; } = new Color(0xBD / 255f, 0xC0 / 255f, 0xCE / 255f);

    /// <summary>
    /// Custom rendering color for the Clock signal line.
    /// </summary>
    [JsonProperty("clockSignalColor")]
    [JsonConverter(typeof(UnityColorConverter))]
    public Color ClockSignalColor { get; set; } = new Color(0xBD / 255f, 0xC0 / 255f, 0xCE / 255f);

    /// <summary>
    /// Boolean band layout array for the floor terrain.
    /// </summary>
    [JsonProperty("floor")]
    [JsonConverter(typeof(BinaryBoolArrayConverter))]
    public bool[] Floor { get; set; }

    /// <summary>
    /// Boolean band layout array for the ceiling terrain.
    /// </summary>
    [JsonProperty("ceiling")]
    [JsonConverter(typeof(BinaryBoolArrayConverter))]
    public bool[] Ceiling { get; set; }

    /// <summary>
    /// List of obstacle definitions contained in the level.
    /// </summary>
    [JsonProperty("obstacles")]
    public List<ObstacleSpawner.ObstacleData> Obstacles { get; set; }
}

#endregion