using System.Collections.Generic;
using FlipNflop.Commons.PhaseRules;
using UnityEngine;

/// <summary>
/// Converts between the Unity <see cref="Vector3"/> of the game and the <see cref="PathPoint"/> of the
/// shared library FlipNflopCommons, whose trace and answer key rules work in <c>float</c> on x and y.
/// The z of the game is always 0: it is dropped in one direction and set to 0 in the other.
/// Used by <see cref="GabaritoGenerator"/>, <see cref="PathChecker"/> and <see cref="PathVerifier"/>.
/// </summary>
public static class PathPointConversion
{
    #region Conversions

    /// <summary>
    /// Converts a world position into a commons point.
    /// </summary>
    /// <param name="position">The world position.</param>
    /// <returns>The point with the same x and y.</returns>
    public static PathPoint ToPathPoint(this Vector3 position)
    {
        return new PathPoint(position.x, position.y);
    }

    /// <summary>
    /// Converts a commons point into a world position with z = 0.
    /// </summary>
    /// <param name="point">The commons point.</param>
    /// <returns>The world position.</returns>
    public static Vector3 ToVector3(this PathPoint point)
    {
        return new Vector3(point.X, point.Y, 0f);
    }

    /// <summary>
    /// Converts a list of world positions, such as <see cref="SignalPath.PathPoints"/>, into commons points, in order.
    /// </summary>
    /// <param name="positions">The world positions.</param>
    /// <returns>A new list with one point per position.</returns>
    public static List<PathPoint> ToPathPoints(this IReadOnlyList<Vector3> positions)
    {
        var points = new List<PathPoint>(positions.Count);
        for (int i = 0; i < positions.Count; i++) points.Add(positions[i].ToPathPoint());
        return points;
    }

    /// <summary>
    /// Converts commons points, such as <see cref="AnswerKeyCorners.Corners"/>, into world positions, in order.
    /// </summary>
    /// <param name="points">The commons points.</param>
    /// <returns>A new list with one position per point.</returns>
    public static List<Vector3> ToVector3List(this IReadOnlyList<PathPoint> points)
    {
        var positions = new List<Vector3>(points.Count);
        for (int i = 0; i < points.Count; i++) positions.Add(points[i].ToVector3());
        return positions;
    }

    #endregion
}
