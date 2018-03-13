using IpmiFanControl;
using Xunit;

namespace Tests
{
    public class FanCurveTests
    {
        [Theory]
        [InlineData(20, 5)]
        [InlineData(30, 5)]
        [InlineData(40, 10)]
        [InlineData(45, 15)]
        [InlineData(50, 20)]
        [InlineData(60, 20)]
        public void EvaluateCurve(byte temp, float speed)
        {
            var fanCurve = new FanCurve(new (byte, float)[] {
                (30, 5),
                (40, 10),
                (50, 20)
            });

            var evaluatedSpeed = fanCurve.Evaluate(temp);

            Assert.Equal(speed, evaluatedSpeed);
        }
    }
}
