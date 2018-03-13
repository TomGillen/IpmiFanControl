using System;
using IpmiFanControl;
using Xunit;

namespace Tests
{
    public class ProgramTests
    {
        [Fact]
        public void ParseFanCurves()
        {
            var args = new[] {
                "-h", "host.domain.com",
                "-u", "username",
                "-p", "password",
                "-f", "30,5;40,10;50,20",
                "-s", "30,5;40,10;50,20;70,100"
            };

            var options = Program.ParseArgs(args);

            Assert.NotNull(options);
        }
    }
}
