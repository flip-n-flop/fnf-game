using System;

namespace FlipNflop.Gla
{
    /// <summary>Splits a scaled frame between all traversed diagram columns, including the endpoints.</summary>
    public static class ColumnTiming
    {
        /// <summary>Maps a diagram-relative position to a column, including the approach and slack.</summary>
        public static int Column(double x, int count) => (int)Math.Max(0, Math.Min(count - 1, Math.Floor(x)));

        /// <summary>Reports each visited column and its time to the commons builder. Positions are relative to the diagram origin.</summary>
        public static void Distribute(double from, double to, double seconds, int count, Action<int, double> addTime)
        {
            double left = Math.Min(from, to);
            double right = Math.Max(from, to);
            int first = Column(left, count);
            int last = Column(right, count);
            if (first == last)
            {
                addTime(first, seconds);
                return;
            }

            double cursor = left;
            for (int column = first; column <= last; column++)
            {
                double end = column == last ? right : column + 1;
                addTime(column, seconds * ((end - cursor) / (right - left)));
                cursor = end;
            }
        }
    }
}
