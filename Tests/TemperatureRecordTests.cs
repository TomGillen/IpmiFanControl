using System;
using IpmiFanControl;
using Xunit;

namespace Tests
{
    public class TemperatureRecordTests
    {
        [Fact]
        public void RecentWhenNewyReturnsZero()
        {
            var record = new TemperatureRecord(TimeSpan.FromMinutes(5));
            var recent = record.RecentMaximum(TimeSpan.FromSeconds(5));

            Assert.Equal(0, recent);
        }

        [Fact]
        public void RecentWhenOldReturnsLast()
        {
            var record = new TemperatureRecord(TimeSpan.FromMinutes(5));
            record.Push(DateTime.Now - TimeSpan.FromMinutes(30), 50);
            record.Push(DateTime.Now - TimeSpan.FromMinutes(25), 55);

            var recent = record.RecentMaximum(TimeSpan.FromSeconds(5));

            Assert.Equal(55, recent);
        }

        [Fact]
        public void RecentReturnsMostRecentOnly()
        {
            var now = DateTime.Now;
            var record = new TemperatureRecord(TimeSpan.FromMinutes(5));
            record.Push(now - TimeSpan.FromMinutes(30), 55);
            record.Push(now - TimeSpan.FromMinutes(25), 54);
            record.Push(now - TimeSpan.FromMinutes(10), 53);
            record.Push(now - TimeSpan.FromMinutes(4), 52);
            record.Push(now - TimeSpan.FromMinutes(3), 51);
            record.Push(now - TimeSpan.FromMinutes(2), 50);

            var recent = record.RecentMaximum(TimeSpan.FromMinutes(5));

            Assert.Equal(52, recent);
        }

        [Theory]
        [InlineData(40, 54, 0.2)]
        [InlineData(40, 50, 1)]
        [InlineData(15, 54, 0)]
        [InlineData(15, 51, 2/3f)]
        public void PercentOverThreshold(int minutes, byte threshold, float expected)
        {
            var now = DateTime.Now;
            var record = new TemperatureRecord(TimeSpan.FromMinutes(40));
            record.Push(now - TimeSpan.FromMinutes(30), 55);
            record.Push(now - TimeSpan.FromMinutes(25), 54);
            record.Push(now - TimeSpan.FromMinutes(10), 53);
            record.Push(now - TimeSpan.FromMinutes(4), 52);
            record.Push(now - TimeSpan.FromMinutes(3), 51);

            var percent = record.RecentPercentOverThreshold(TimeSpan.FromMinutes(minutes), threshold);

            Assert.Equal(expected, percent);
        }
    }
}
