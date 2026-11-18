using System;
using System.Globalization;
using System.Text;

/// <summary>
/// Result of the validation of the trace at the end of an attempt that reached the end of the diagram or ended by death,
/// published by <see cref="PathVerifier.ValidationCompleted"/> for the attempt collection.
/// It carries the fields of the attempt record that come from the validation: <c>conclusion</c>, <c>overall_accuracy</c>,
/// <c>performance</c>, and the per-column vectors <c>trace</c> and <c>correct</c>, computed by the shared library FlipNflopCommons
/// (<c>CornerEvaluator</c>, <c>ScoreRules</c> and <c>ColumnEvaluator</c>). Plain C#, without Unity dependencies.
/// </summary>
public sealed class TraceValidationResult
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TraceValidationResult"/> class. The vectors are copied.
    /// </summary>
    /// <param name="conclusion">Whether the traced diagram is correct (every answer key corner was hit).</param>
    /// <param name="overallAccuracy">Covered answer key segments divided by the total, from 0 to 1.</param>
    /// <param name="performance">Score shown on the result screen, from 0 to the maximum score.</param>
    /// <param name="trace">Traced level per column: 0, 1 or null where the column has no trace.</param>
    /// <param name="correct">Accuracy per column: 1, 0 or null where the column has no trace.</param>
    /// <exception cref="ArgumentNullException"><paramref name="trace"/> or <paramref name="correct"/> is null.</exception>
    /// <exception cref="ArgumentException">The two vectors have different lengths.</exception>
    public TraceValidationResult(bool conclusion, float overallAccuracy, int performance, int?[] trace, int?[] correct)
    {
        if (trace == null) throw new ArgumentNullException(nameof(trace));
        if (correct == null) throw new ArgumentNullException(nameof(correct));
        if (trace.Length != correct.Length)
            throw new ArgumentException($"trace has {trace.Length} items and correct has {correct.Length}.", nameof(correct));

        Conclusion = conclusion;
        OverallAccuracy = overallAccuracy;
        Performance = performance;
        Trace = (int?[])trace.Clone();
        Correct = (int?[])correct.Clone();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Whether the traced diagram is correct (<c>conclusion</c>): the criterion that wins the level and offers the next one.
    /// </summary>
    public bool Conclusion { get; }

    /// <summary>
    /// Overall accuracy (<c>overall_accuracy</c>): covered answer key segments divided by the total, as the score uses it.
    /// It is not the mean of <see cref="Correct"/>.
    /// </summary>
    public float OverallAccuracy { get; }

    /// <summary>
    /// Score (<c>performance</c>), the same value the result screen shows.
    /// </summary>
    public int Performance { get; }

    /// <summary>
    /// Traced level per column (<c>trace</c>): 0, 1 or null, with one item per column of the level.
    /// </summary>
    public int?[] Trace { get; }

    /// <summary>
    /// Accuracy per column (<c>correct</c>): 1 when the whole trace in the column is valid (green), 0 otherwise,
    /// null where <see cref="Trace"/> is null; one item per column of the level.
    /// </summary>
    public int?[] Correct { get; }

    /// <summary>
    /// Number of columns of the level (the length of the vectors).
    /// </summary>
    public int ColumnCount => Trace.Length;

    #endregion

    #region Factory & Formatting

    /// <summary>
    /// Result of a trace too short to be evaluated (fewer than 2 points): incorrect diagram, score 0, overall accuracy 0,
    /// and <paramref name="columnCount"/> nulls in both vectors.
    /// </summary>
    /// <param name="columnCount">Number of columns of the level.</param>
    /// <returns>The result of the short trace.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="columnCount"/> is negative.</exception>
    public static TraceValidationResult ForShortTrace(int columnCount)
    {
        if (columnCount < 0) throw new ArgumentOutOfRangeException(nameof(columnCount), columnCount, "The number of columns must be >= 0.");

        return new TraceValidationResult(false, 0f, 0, new int?[columnCount], new int?[columnCount]);
    }

    /// <summary>
    /// Formats the result with the transport names of the attempt record, for the Development Build log.
    /// </summary>
    /// <returns>A single line, such as <c>conclusion=false overall_accuracy=0.6666667 performance=620 trace=[0,1,...] correct=[1,0,...]</c>.</returns>
    public override string ToString()
    {
        var text = new StringBuilder();
        text.Append("conclusion=").Append(Conclusion ? "true" : "false");
        text.Append(" overall_accuracy=").Append(OverallAccuracy.ToString("R", CultureInfo.InvariantCulture));
        text.Append(" performance=").Append(Performance.ToString(CultureInfo.InvariantCulture));
        text.Append(" columns=").Append(ColumnCount.ToString(CultureInfo.InvariantCulture));
        AppendVector(text, " trace=", Trace);
        AppendVector(text, " correct=", Correct);
        return text.ToString();
    }

    /// <summary>
    /// Appends a per-column vector as <c>[a,b,null,...]</c>.
    /// </summary>
    /// <param name="text">Target builder.</param>
    /// <param name="label">Label written before the vector.</param>
    /// <param name="values">Vector to write.</param>
    private static void AppendVector(StringBuilder text, string label, int?[] values)
    {
        text.Append(label).Append('[');
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) text.Append(',');
            text.Append(values[i].HasValue ? values[i].Value.ToString(CultureInfo.InvariantCulture) : "null");
        }

        text.Append(']');
    }

    #endregion
}
