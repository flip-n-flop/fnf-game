using NUnit.Framework;

namespace FlipNflop.Gla.Tests
{
    /// <summary>Verifies movement, pause and final sampling on the GLA time axis.</summary>
    public sealed class ColumnTimingTests
    {
        [TestCase(20.5, 22.5)]
        [TestCase(22.5, 20.5)]
        public void CrossedColumnsShareFrameTime(double from, double to)
        {
            double?[] times = new double?[42];
            ColumnTiming.Distribute(from, to, 4, times.Length, (i, value) => times[i] = value);
            Assert.That(times[20], Is.EqualTo(1));
            Assert.That(times[21], Is.EqualTo(2));
            Assert.That(times[22], Is.EqualTo(1));
            Assert.That(times[19], Is.Null);
        }

        [Test]
        public void ApproachSlackAndPauseConserveTime()
        {
            double[] times = new double[3];
            void Add(int i, double seconds) => times[i] += seconds;
            ColumnTiming.Distribute(-2, -2, 8, 3, Add);
            ColumnTiming.Distribute(-2, 4, 6, 3, Add);
            ColumnTiming.Distribute(1, 1, 0, 3, Add);
            Assert.That(times, Is.EqualTo(new[] { 11d, 1d, 2d }));
        }

        [Test]
        public void FinalSampleMarksNewColumnEvenWithZeroRemainingTime()
        {
            double?[] times = new double?[42];
            ColumnTiming.Distribute(20.8, 21, 0, times.Length, (i, value) => times[i] = value);
            Assert.That(times[21], Is.EqualTo(0));
            Assert.That(times[22], Is.Null);
        }
    }
}
