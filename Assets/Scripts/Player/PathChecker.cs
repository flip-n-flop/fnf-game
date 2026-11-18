using System.Collections.Generic;
using FlipNflop.Commons.PhaseRules;
using UnityEngine;

#region Data Structures

/// <summary>
/// Immutable result structure produced by <see cref="PathChecker.Evaluate"/>.
/// Contains accuracy statistics, missed corner locations, and per-corner verification statuses.
/// </summary>
public readonly struct PathCheckResult
{
    /// <summary>
    /// True if all reference corners were successfully hit by the player's path.
    /// </summary>
    public readonly bool IsCorrect;

    /// <summary>
    /// Number of reference path segments covered by the player's path.
    /// </summary>
    public readonly int CoveredSegments;

    /// <summary>
    /// Total number of reference path segments (calculated as reference corner count minus 1).
    /// </summary>
    public readonly int GabaritoTotal;

    /// <summary>
    /// List of reference corner coordinates that were missed by the player.
    /// </summary>
    public readonly IReadOnlyList<Vector3> MissedCorners;

    /// <summary>
    /// Boolean verification result per reference corner (true = hit, false = missed).
    /// </summary>
    public readonly IReadOnlyList<bool> CornerChecks;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathCheckResult"/> struct.
    /// </summary>
    /// <param name="isCorrect">Whether all corners were hit.</param>
    /// <param name="coveredSegments">Count of covered reference segments.</param>
    /// <param name="gabaritoTotal">Total segment count in reference path.</param>
    /// <param name="missedCorners">Collection of missed corner points.</param>
    /// <param name="cornerChecks">Per-corner hit boolean flags.</param>
    public PathCheckResult(
        bool isCorrect,
        int coveredSegments,
        int gabaritoTotal,
        List<Vector3> missedCorners,
        List<bool> cornerChecks)
    {
        IsCorrect = isCorrect;
        CoveredSegments = coveredSegments;
        GabaritoTotal = gabaritoTotal;
        MissedCorners = missedCorners.AsReadOnly();
        CornerChecks = cornerChecks.AsReadOnly();
    }
}

#endregion

/// <summary>
/// Evaluates whether the player's recorded path (<see cref="SignalPath"/>) matches the expected reference corners (<see cref="GabaritoGenerator"/>).
/// Stateless evaluation module that produces a <see cref="PathCheckResult"/>, delegating to <see cref="CornerEvaluator"/> of the
/// shared library FlipNflopCommons, which evaluates the <see cref="AnswerKeyCorners"/> given to the constructor.
/// </summary>
public class PathChecker
{
    #region Fields

    /// <summary>
    /// Maximum world-unit distance allowed to consider a reference corner as hit.
    /// </summary>
    private readonly float cornerTolerance;

    /// <summary>
    /// Flag indicating whether detailed evaluation messages should be printed to the Unity console.
    /// </summary>
    private readonly bool enableDebugLogs;

    /// <summary>
    /// Commons answer key corners evaluated by <see cref="Evaluate"/>, with the tolerance of their geometry; null for the
    /// tolerance-only constructor, which cannot evaluate.
    /// </summary>
    private readonly AnswerKeyCorners answerKeyCorners;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PathChecker"/> class with tolerance and logging options.
    /// The shared rules evaluate only the corners of a phase, so an instance created this way has no answer key:
    /// use <see cref="PathChecker(AnswerKeyCorners, bool)"/> to evaluate.
    /// </summary>
    /// <param name="cornerTolerance">Maximum distance tolerance for hitting corners.</param>
    /// <param name="enableDebugLogs">Whether to log detailed verification info.</param>
    public PathChecker(float cornerTolerance, bool enableDebugLogs = false)
    {
        this.cornerTolerance = cornerTolerance;
        this.enableDebugLogs = enableDebugLogs;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PathChecker"/> class for the answer key corners of a phase,
    /// built by <see cref="GabaritoGenerator"/>; the tolerance is the one of their geometry.
    /// </summary>
    /// <param name="answerKeyCorners">Commons answer key corners of the loaded level.</param>
    /// <param name="enableDebugLogs">Whether to log detailed verification info.</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="answerKeyCorners"/> is null.</exception>
    public PathChecker(AnswerKeyCorners answerKeyCorners, bool enableDebugLogs = false)
    {
        this.answerKeyCorners = answerKeyCorners ?? throw new System.ArgumentNullException(nameof(answerKeyCorners));
        cornerTolerance = answerKeyCorners.Geometry.Tolerance;
        this.enableDebugLogs = enableDebugLogs;
    }

    #endregion

    #region Public Verification API

    /// <summary>
    /// Evaluates the player's path against the reference corners (answer key) with <see cref="CornerEvaluator"/>:
    /// a corner is hit when the path passes within the tolerance of it, the path is correct when every corner is hit,
    /// and a reference segment is covered when its midpoint is within the tolerance of the path.
    /// Receives corners generated by <see cref="GabaritoGenerator"/> and path points recorded by <see cref="SignalPath"/>.
    /// </summary>
    /// <param name="correctCorners">List of reference solution corners; must be the corners of the answer key given to the constructor.</param>
    /// <param name="playerPath">List of recorded player path coordinates.</param>
    /// <returns>A <see cref="PathCheckResult"/> containing detailed match stats.</returns>
    /// <exception cref="System.InvalidOperationException">The instance was created without answer key corners.</exception>
    /// <exception cref="System.ArgumentException"><paramref name="correctCorners"/> are not the corners of the answer key.</exception>
    public PathCheckResult Evaluate(List<Vector3> correctCorners, List<Vector3> playerPath)
    {
        RequireAnswerKeyCorners(correctCorners);

        if (enableDebugLogs)
        {
            Debug.Log("<color=cyan>[PathChecker] Starting path evaluation</color>");
            Debug.Log($"  Player points: {playerPath.Count}");
            Debug.Log($"  Reference corners: {correctCorners.Count}");
            Debug.Log($"  Tolerance: {cornerTolerance}");
        }

        CornerEvaluation evaluation = CornerEvaluator.Evaluate(answerKeyCorners, playerPath.ToPathPoints());

        var cornerChecks = new List<bool>(evaluation.CornerHits);
        var missedCorners = evaluation.MissedCorners.ToVector3List();

        if (enableDebugLogs)
        {
            for (int cornerIndex = 0; cornerIndex < cornerChecks.Count; cornerIndex++)
            {
                if (!cornerChecks[cornerIndex])
                    Debug.LogWarning($"  <color=red>✗ Corner #{cornerIndex} MISSED:</color> Pos={correctCorners[cornerIndex]}");
                else
                    Debug.Log($"  <color=green>✓ Corner #{cornerIndex} OK:</color> Pos={correctCorners[cornerIndex]}");
            }

            int correct = cornerChecks.FindAll(x => x).Count;
            Debug.Log($"<color=yellow>[PathChecker] Result: {correct}/{cornerChecks.Count} corners hit</color>");
        }

        return new PathCheckResult(evaluation.IsCorrect, evaluation.CoveredSegments, evaluation.SegmentCount, missedCorners, cornerChecks);
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Ensures that the instance has answer key corners and that the given corners are theirs (same count and same x and y).
    /// </summary>
    /// <param name="correctCorners">Corners given to <see cref="Evaluate"/>.</param>
    /// <exception cref="System.InvalidOperationException">The instance was created without answer key corners.</exception>
    /// <exception cref="System.ArgumentException">The corners differ from the answer key corners.</exception>
    private void RequireAnswerKeyCorners(List<Vector3> correctCorners)
    {
        if (answerKeyCorners == null)
        {
            throw new System.InvalidOperationException(
                "[PathChecker] No answer key corners: create the checker with the AnswerKeyCorners of the level (GabaritoGenerator.AnswerKeyCorners).");
        }

        IReadOnlyList<PathPoint> expected = answerKeyCorners.Corners;
        bool same = correctCorners != null && correctCorners.Count == expected.Count;
        for (int i = 0; same && i < expected.Count; i++)
            same = correctCorners[i].x == expected[i].X && correctCorners[i].y == expected[i].Y;

        if (!same)
        {
            throw new System.ArgumentException(
                "[PathChecker] The corners differ from the answer key corners the checker was created with.", nameof(correctCorners));
        }
    }

    #endregion
}
