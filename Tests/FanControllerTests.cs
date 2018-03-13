using System;
using System.Linq;
using IpmiFanControl;
using Xunit;

namespace Tests
{
    public class FanControllerTests
    {
        [Fact]
        public void IdleSpeedCurve()
        {
            var controller = new FanController(
                new FanCurve(new (byte, float)[] { (30, 5), (40, 10), (50, 15) }),
                new FanCurve(new(byte, float)[] { (30, 5), (50, 20), (70, 100) }),
                55,
                70,
                TimeSpan.FromMinutes(10));

            var now = DateTime.Now;
            var rand = new Random();
            var readings = Enumerable.Range(0, 500)
                                     .Select(i => (time: now - TimeSpan.FromSeconds(i * 3), temp: (byte) rand.Next(35, 45)))
                                     .Reverse();

            foreach (var reading in readings) {
                controller.PushReading(reading.time, reading.temp);
                controller.Evaluate(out var _);
            }

            controller.Evaluate(out var _);

            Assert.Equal(FanController.Mode.Idling, controller.CurrentMode);
        }

        [Fact]
        public void IdleSpeedCurveWithAllowableOverheat()
        {
            var controller = new FanController(
                new FanCurve(new(byte, float)[] { (30, 5), (40, 10), (50, 15) }),
                new FanCurve(new(byte, float)[] { (30, 5), (50, 20), (70, 100) }),
                55,
                70,
                TimeSpan.FromMinutes(10));

            var now = DateTime.Now;
            var rand = new Random();
            var readings = Enumerable.Range(0, 500)
                                     .Select(i => (time: now - TimeSpan.FromSeconds(i * 3) - TimeSpan.FromMinutes(5), temp: (byte) rand.Next(35, 45)))
                                     .Reverse();

            foreach (var reading in readings) {
                controller.PushReading(reading.time, reading.temp);
                controller.Evaluate(out var _);
            }

            var hotReadings = Enumerable.Range(0, 5 * 60 / 3)
                                     .Select(i => (time: now - TimeSpan.FromSeconds(i * 3), temp: (byte) rand.Next(56, 60)))
                                     .Reverse();

            foreach (var reading in hotReadings) {
                controller.PushReading(reading.time, reading.temp);
                controller.Evaluate(out var _);
            }

            controller.Evaluate(out var _);

            Assert.Equal(FanController.Mode.Idling, controller.CurrentMode);
        }

        [Fact]
        public void SustainedSpeedCurveWithOverheat()
        {
            var controller = new FanController(
                new FanCurve(new(byte, float)[] { (30, 5), (40, 10), (50, 15) }),
                new FanCurve(new(byte, float)[] { (30, 5), (50, 20), (70, 100) }),
                55,
                70,
                TimeSpan.FromMinutes(10));

            var now = DateTime.Now;
            var rand = new Random();
            var readings = Enumerable.Range(0, 500)
                                     .Select(i => (time: now - TimeSpan.FromSeconds(i * 3) - TimeSpan.FromMinutes(5), temp: (byte) rand.Next(35, 45)))
                                     .Reverse();

            foreach (var reading in readings) {
                controller.PushReading(reading.time, reading.temp);
                controller.Evaluate(out var _);
            }

            var hotReadings = Enumerable.Range(0, 5 * 60 / 3)
                                        .Select(i => (time: now - TimeSpan.FromSeconds(i * 3), temp: (byte) rand.Next(65, 70)))
                                        .Reverse();

            foreach (var reading in hotReadings) {
                controller.PushReading(reading.time, reading.temp);
                controller.Evaluate(out var _);
            }

            controller.Evaluate(out var _);

            Assert.Equal(FanController.Mode.SustainedLoad, controller.CurrentMode);
        }
    }
}
