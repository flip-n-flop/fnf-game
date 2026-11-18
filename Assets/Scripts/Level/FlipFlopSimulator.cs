using System;
using System.Collections.Generic;
using FlipNflop.Commons.PhaseRules;
using UnityEngine;

/// <summary>
/// Simulates various flip-flop logic types (JK, SR, D, T) with synchronous and asynchronous inputs.
/// Provides timeline computation and event generation for level path verification and tilemap rendering.
/// The JK simulation, the only one the levels use, delegates to <see cref="JkSimulator"/> of the shared library FlipNflopCommons.
/// Interacts with <see cref="LevelData"/> and is invoked by <see cref="LevelJsonLoader"/> and <see cref="PathVerifier"/>.
/// </summary>
public static class FlipFlopSimulator
{
    #region JK Flip-Flop Simulation

    /// <summary>
    /// Name given to the phase definitions built by <see cref="CreateJkPhase"/>, which receive only the signals.
    /// </summary>
    private const string SimulatedLevelName = "simulated_level";

    /// <summary>
    /// Computes JK flip-flop timeline with asynchronous preset/clear support.
    /// Returns doubled-resolution output: each input tile produces 2 output positions (X.0 and X.5).
    /// This allows both synchronous (at X.0) and asynchronous (at X.5) operations to coexist.
    /// Timeline length = (diagramLength+1) * 2, where even indices are sync, odd indices are async.
    /// Delegates to <see cref="JkSimulator"/> of the shared library FlipNflopCommons, with the phase built by <see cref="CreateJkPhase"/>.
    /// Used by <see cref="LevelJsonLoader"/> and <see cref="PathVerifier"/>.
    /// </summary>
    /// <param name="jSignal">Input array for J signal values.</param>
    /// <param name="kSignal">Input array for K signal values.</param>
    /// <param name="presetSignal">Input array for asynchronous Preset signal values.</param>
    /// <param name="clearSignal">Input array for asynchronous Clear signal values.</param>
    /// <param name="clockStep">Clock step interval in tile units; the shared rules support only 6.</param>
    /// <param name="diagramLength">Total logical length of the diagram; a multiple of the clock step.</param>
    /// <param name="ops">Output array containing operation tokens for each step.</param>
    /// <param name="events">Output list containing state change events.</param>
    /// <param name="asyncActiveHigh">If true, preset/clear active when signal=1; if false, active when signal=0.</param>
    /// <returns>Boolean array representing the output Q signal timeline over time, or null when <paramref name="diagramLength"/> is not positive.</returns>
    /// <exception cref="InvalidOperationException">Preset and Clear are active in the same tile, or the clock step or the length is not supported by the shared rules.</exception>
    public static bool[] SimulateJK(bool[] jSignal, bool[] kSignal, bool[] presetSignal, bool[] clearSignal,
        int clockStep, int diagramLength, out string[] ops, out List<SignalEvent> events, bool asyncActiveHigh = true)
    {
        ops = null;
        events = null;
        if (diagramLength <= 0) return null;

        PhaseDefinition phase = CreateJkPhase(jSignal, kSignal, presetSignal, clearSignal, clockStep, diagramLength, asyncActiveHigh);
        JkSimulation simulation = JkSimulator.Simulate(phase);

        var timeline = new bool[simulation.Output.Count];
        for (int i = 0; i < timeline.Length; i++) timeline[i] = simulation.Output[i];

        var opArr = new string[simulation.Tokens.Count];
        for (int i = 0; i < opArr.Length; i++) opArr[i] = simulation.Tokens[i];

        var eventList = new List<SignalEvent>(simulation.Events.Count);
        foreach (OutputEvent change in simulation.Events) eventList.Add(new SignalEvent(change.X, change.Value));

        ops = opArr;
        events = eventList;
        return timeline;
    }

    /// <summary>
    /// Builds the phase definition of the shared library FlipNflopCommons (<see cref="PhaseDefinition"/>)
    /// from the signals of a JK level, as <see cref="SimulateJK"/> receives them.
    /// The flip-flop type is always JK, because <see cref="LevelData"/> does not read it, and the active
    /// clock edge is the game default (falling), because it does not change the simulation.
    /// Missing J or K signals read as 0, as in the game. Used by <see cref="SimulateJK"/> and <see cref="LevelJsonLoader"/>.
    /// </summary>
    /// <param name="jSignal">Input array for J signal values.</param>
    /// <param name="kSignal">Input array for K signal values.</param>
    /// <param name="presetSignal">Input array for asynchronous Preset signal values, or null.</param>
    /// <param name="clearSignal">Input array for asynchronous Clear signal values, or null.</param>
    /// <param name="clockStep">Clock step interval in tile units; must be <see cref="PhaseDefinition.ColumnsPerClockCycle"/>.</param>
    /// <param name="diagramLength">Total logical length of the diagram; a positive multiple of the clock step.</param>
    /// <param name="asyncActiveHigh">If true, preset/clear active when signal=1; if false, active when signal=0.</param>
    /// <returns>The phase definition, with <paramref name="diagramLength"/> / 6 clock cycles.</returns>
    /// <exception cref="InvalidOperationException">The clock step is not 6 or the length is not a positive multiple of it.</exception>
    public static PhaseDefinition CreateJkPhase(bool[] jSignal, bool[] kSignal, bool[] presetSignal, bool[] clearSignal,
        int clockStep, int diagramLength, bool asyncActiveHigh = true)
    {
        if (clockStep != PhaseDefinition.ColumnsPerClockCycle || diagramLength <= 0 || diagramLength % clockStep != 0)
        {
            throw new InvalidOperationException(
                $"Unsupported level geometry: the shared JK rules need a clock step of {PhaseDefinition.ColumnsPerClockCycle} tiles " +
                $"and a diagram length multiple of it (got step {clockStep}, length {diagramLength}).");
        }

        return new PhaseDefinition(
            SimulatedLevelName,
            diagramLength / clockStep,
            PhaseDefinition.JkFlipFlopType,
            ClockEdge.Falling,
            jSignal ?? Array.Empty<bool>(),
            kSignal ?? Array.Empty<bool>(),
            presetSignal,
            clearSignal,
            asyncActiveHigh);
    }

    #endregion

    #region SR Flip-Flop Simulation

    /// <summary>
    /// Computes SR flip-flop timeline with doubled-resolution output.
    /// Returns doubled-resolution output: each input tile produces 2 output positions (X.0 and X.5).
    /// Timeline length = (diagramLength+1) * 2, where even indices are sync, odd indices are async.
    /// S=1 sets Q, R=1 resets Q, S=R=1 is invalid.
    /// Interacts with <see cref="LevelData"/> during level simulation.
    /// </summary>
    /// <param name="sSignal">Input array for Set (S) signal values.</param>
    /// <param name="rSignal">Input array for Reset (R) signal values.</param>
    /// <param name="clockStep">Clock step interval in tile units.</param>
    /// <param name="diagramLength">Total logical length of the diagram.</param>
    /// <param name="ops">Output array containing operation tokens for each step.</param>
    /// <param name="events">Output list containing state change events.</param>
    /// <param name="invalidStateToZero">If true, handles invalid S=R=1 state by forcing output to false.</param>
    /// <returns>Boolean array representing the output Q signal timeline over time.</returns>
    public static bool[] SimulateSR(bool[] sSignal, bool[] rSignal, int clockStep, int diagramLength,
        out string[] ops, out List<SignalEvent> events, bool invalidStateToZero = true)
    {
        ops = null;
        events = null;
        if (diagramLength <= 0) return null;

        int inputLength = diagramLength + 1;
        int outputLength = inputLength * 2;
        var timeline = new bool[outputLength];
        var opArr = new string[outputLength];
        var eventList = new List<SignalEvent>();

        bool q = false;

        for (int i = 0; i < inputLength; i++)
        {
            int syncIndex = i * 2;
            int asyncIndex = i * 2 + 1;

            bool isEdge = (clockStep > 0 && i > 0 && (i % clockStep) == 0);

            bool qBeforeSync = q;
            string syncToken = null;

            if (isEdge)
            {
                bool s = GetAt(sSignal, i - 1);
                bool r = GetAt(rSignal, i - 1);

                if (s && r)
                {
                    throw new InvalidOperationException($"Invalid level inputs: Set (S) and Reset (R) cannot be active simultaneously at tile {i - 1}.");
                }
                else if (s)
                {
                    q = true;
                    syncToken = qBeforeSync != q ? "set_sync" : "hold_sync";
                }
                else if (r)
                {
                    q = false;
                    syncToken = qBeforeSync != q ? "reset_sync" : "hold_sync";
                }
                else
                {
                    syncToken = "hold_sync";
                }

                if (q != qBeforeSync)
                {
                    eventList.Add(new SignalEvent(i, q));
                }
            }

            timeline[syncIndex] = q;
            opArr[syncIndex] = syncToken ?? (qBeforeSync == q ? "keep" : (q ? "set_initial" : "reset_initial"));

            timeline[asyncIndex] = q;
            opArr[asyncIndex] = "keep";
        }

        ops = opArr;
        events = eventList;
        return timeline;
    }

    #endregion

    #region D Flip-Flop Simulation

    /// <summary>
    /// Computes D flip-flop timeline with doubled-resolution output.
    /// Returns doubled-resolution output: each input tile produces 2 output positions (X.0 and X.5).
    /// Timeline length = (diagramLength+1) * 2, where even indices are sync, odd indices are async.
    /// Q follows D input at each clock edge.
    /// Interacts with <see cref="LevelData"/> during level simulation.
    /// </summary>
    /// <param name="dSignal">Input array for D signal values.</param>
    /// <param name="presetSignal">Input array for asynchronous Preset signal values.</param>
    /// <param name="clearSignal">Input array for asynchronous Clear signal values.</param>
    /// <param name="clockStep">Clock step interval in tile units.</param>
    /// <param name="diagramLength">Total logical length of the diagram.</param>
    /// <param name="ops">Output array containing operation tokens for each step.</param>
    /// <param name="events">Output list containing state change events.</param>
    /// <param name="asyncActiveHigh">If true, preset/clear active when signal=1; if false, active when signal=0.</param>
    /// <returns>Boolean array representing the output Q signal timeline over time.</returns>
    public static bool[] SimulateD(bool[] dSignal, bool[] presetSignal, bool[] clearSignal,
        int clockStep, int diagramLength, out string[] ops, out List<SignalEvent> events, bool asyncActiveHigh = true)
    {
        ops = null;
        events = null;
        if (diagramLength <= 0) return null;

        int inputLength = diagramLength + 1;
        int outputLength = inputLength * 2;
        var timeline = new bool[outputLength];
        var opArr = new string[outputLength];
        var eventList = new List<SignalEvent>();

        bool q = false;
        bool asyncSincePrevEdge = false;

        for (int i = 0; i < inputLength; i++)
        {
            int syncIndex = i * 2;
            int asyncIndex = i * 2 + 1;

            bool isEdge = (clockStep > 0 && i > 0 && (i % clockStep) == 0);

            bool qBeforeSync = q;
            bool syncApplied = false;
            string syncToken = null;

            if (isEdge)
            {
                if (!asyncSincePrevEdge)
                {
                    bool d = GetAt(dSignal, i - 1);
                    q = d;

                    syncApplied = true;
                    syncToken = qBeforeSync != q ? (d ? "set_sync" : "reset_sync") : "hold_sync";

                    if (q != qBeforeSync)
                    {
                        eventList.Add(new SignalEvent(i, q));
                    }
                }
                else
                {
                    syncApplied = false;
                    syncToken = "sync_ignored";
                }

                asyncSincePrevEdge = false;
            }

            timeline[syncIndex] = q;
            opArr[syncIndex] = syncApplied && syncToken != null && syncToken != "hold_sync"
                ? syncToken
                : (syncToken == "sync_ignored" ? syncToken : (qBeforeSync == q ? "keep" : (q ? "set_initial" : "reset_initial")));

            bool qBeforeAsync = q;
            bool presetValue = GetAt(presetSignal, i);
            bool clearValue = GetAt(clearSignal, i);

            bool hasPreset = presetSignal != null && i < presetSignal.Length && (asyncActiveHigh ? presetValue : !presetValue);
            bool hasClear = clearSignal != null && i < clearSignal.Length && (asyncActiveHigh ? clearValue : !clearValue);

            string asyncToken;

            if (hasPreset || hasClear)
            {
                q = hasClear ? false : true;

                asyncToken = (q != qBeforeAsync)
                    ? (hasClear ? "clear_async" : "preset_async")
                    : (hasClear ? "clear_async_noop" : "preset_async_noop");

                if (q != qBeforeAsync)
                {
                    eventList.Add(new SignalEvent(i + 0.5f, q));
                }

                asyncSincePrevEdge = true;
            }
            else
            {
                asyncToken = (qBeforeAsync == q) ? "keep" : (q ? "set_initial" : "reset_initial");
            }

            timeline[asyncIndex] = q;
            opArr[asyncIndex] = asyncToken;
        }

        ops = opArr;
        events = eventList;
        return timeline;
    }

    #endregion

    #region T Flip-Flop Simulation

    /// <summary>
    /// Computes T flip-flop timeline with doubled-resolution output.
    /// Returns doubled-resolution output: each input tile produces 2 output positions (X.0 and X.5).
    /// Timeline length = (diagramLength+1) * 2, where even indices are sync, odd indices are async.
    /// Q toggles when T=1 at clock edge, holds when T=0.
    /// Interacts with <see cref="LevelData"/> during level simulation.
    /// </summary>
    /// <param name="tSignal">Input array for T signal values.</param>
    /// <param name="presetSignal">Input array for asynchronous Preset signal values.</param>
    /// <param name="clearSignal">Input array for asynchronous Clear signal values.</param>
    /// <param name="clockStep">Clock step interval in tile units.</param>
    /// <param name="diagramLength">Total logical length of the diagram.</param>
    /// <param name="ops">Output array containing operation tokens for each step.</param>
    /// <param name="events">Output list containing state change events.</param>
    /// <param name="asyncActiveHigh">If true, preset/clear active when signal=1; if false, active when signal=0.</param>
    /// <returns>Boolean array representing the output Q signal timeline over time.</returns>
    public static bool[] SimulateT(bool[] tSignal, bool[] presetSignal, bool[] clearSignal,
        int clockStep, int diagramLength, out string[] ops, out List<SignalEvent> events, bool asyncActiveHigh = true)
    {
        ops = null;
        events = null;
        if (diagramLength <= 0) return null;

        int inputLength = diagramLength + 1;
        int outputLength = inputLength * 2;
        var timeline = new bool[outputLength];
        var opArr = new string[outputLength];
        var eventList = new List<SignalEvent>();

        bool q = false;
        bool asyncSincePrevEdge = false;

        for (int i = 0; i < inputLength; i++)
        {
            int syncIndex = i * 2;
            int asyncIndex = i * 2 + 1;

            bool isEdge = (clockStep > 0 && i > 0 && (i % clockStep) == 0);

            bool qBeforeSync = q;
            bool syncApplied = false;
            string syncToken = null;

            if (isEdge)
            {
                if (!asyncSincePrevEdge)
                {
                    bool t = GetAt(tSignal, i - 1);
                    if (t) q = !q;

                    syncApplied = true;
                    syncToken = t ? (qBeforeSync != q ? "toggle_sync" : "hold_sync") : "hold_sync";

                    if (q != qBeforeSync)
                    {
                        eventList.Add(new SignalEvent(i, q));
                    }
                }
                else
                {
                    syncApplied = false;
                    syncToken = "sync_ignored";
                }

                asyncSincePrevEdge = false;
            }

            timeline[syncIndex] = q;
            opArr[syncIndex] = syncApplied && syncToken != null && syncToken != "hold_sync"
                ? syncToken
                : (syncToken == "sync_ignored" ? syncToken : (qBeforeSync == q ? "keep" : (q ? "set_initial" : "reset_initial")));

            bool qBeforeAsync = q;
            bool presetValue = GetAt(presetSignal, i);
            bool clearValue = GetAt(clearSignal, i);

            bool hasPreset = presetSignal != null && i < presetSignal.Length && (asyncActiveHigh ? presetValue : !presetValue);
            bool hasClear = clearSignal != null && i < clearSignal.Length && (asyncActiveHigh ? clearValue : !clearValue);

            string asyncToken;

            if (hasPreset || hasClear)
            {
                q = hasClear ? false : true;

                asyncToken = (q != qBeforeAsync)
                    ? (hasClear ? "clear_async" : "preset_async")
                    : (hasClear ? "clear_async_noop" : "preset_async_noop");

                if (q != qBeforeAsync)
                {
                    eventList.Add(new SignalEvent(i + 0.5f, q));
                }

                asyncSincePrevEdge = true;
            }
            else
            {
                asyncToken = (qBeforeAsync == q) ? "keep" : (q ? "set_initial" : "reset_initial");
            }

            timeline[asyncIndex] = q;
            opArr[asyncIndex] = asyncToken;
        }

        ops = opArr;
        events = eventList;
        return timeline;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Safely gets value from a boolean array at the specified index.
    /// Returns false if out of bounds or if the array is null.
    /// </summary>
    /// <param name="arr">Target boolean array.</param>
    /// <param name="idx">Index to sample.</param>
    /// <returns>Boolean value at index or false if invalid.</returns>
    private static bool GetAt(bool[] arr, int idx)
    {
        return arr != null && idx >= 0 && idx < arr.Length && arr[idx];
    }

    /// <summary>
    /// Returns the maximum length among provided boolean arrays.
    /// </summary>
    /// <param name="arrays">Array of boolean arrays to compare.</param>
    /// <returns>Maximum length found, or 0 if null/empty.</returns>
    public static int MaxLen(params bool[][] arrays)
    {
        int max = 0;
        if (arrays == null) return 0;
        for (int i = 0; i < arrays.Length; i++)
        {
            if (arrays[i] != null && arrays[i].Length > max) max = arrays[i].Length;
        }
        return max;
    }

    /// <summary>
    /// Inverts all bits in a boolean array (active-high to active-low conversion).
    /// </summary>
    /// <param name="arr">Source boolean array.</param>
    /// <returns>New array containing inverted boolean values.</returns>
    public static bool[] InvertBits(bool[] arr)
    {
        if (arr == null) return null;
        var res = new bool[arr.Length];
        for (int i = 0; i < arr.Length; i++) res[i] = !arr[i];
        return res;
    }

    #endregion

    #region Data Structures

    /// <summary>
    /// Represents a signal transition event at a specific X position.
    /// Interacts with simulation outcomes processed by <see cref="LevelJsonLoader"/> and <see cref="PathVerifier"/>.
    /// </summary>
    public struct SignalEvent
    {
        /// <summary>
        /// X coordinate position of the event in world/tile space.
        /// </summary>
        public float x;

        /// <summary>
        /// New boolean state of the signal at this event.
        /// </summary>
        public bool value;

        /// <summary>
        /// Initializes a new instance of the <see cref="SignalEvent"/> struct.
        /// </summary>
        /// <param name="x">X position of event.</param>
        /// <param name="value">Boolean value at event.</param>
        public SignalEvent(float x, bool value)
        {
            this.x = x;
            this.value = value;
        }
    }

    #endregion
}
