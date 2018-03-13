using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace IpmiFanControl
{
    public class Ipmi
    {
        private readonly string _host;
        private readonly string _username;
        private readonly string _password;

        private static readonly Regex Regex = new Regex(@"\s(?<temp>\d+) degrees C$", RegexOptions.Compiled | RegexOptions.Multiline);

        public Ipmi(string host, string username, string password)
        {
            _host = host;
            _username = username;
            _password = password;
        }

        public void Authenticate()
        {
            GetTemperature();
        }

        public byte GetTemperature()
        {
            var command = $"-I lanplus -H {_host} -U {_username} -P {_password} sdr type temperature";

            var exitCode = RunIpmiCommand(command, out var output, out var _);
            if (exitCode != 0) {
                throw new Exception($"ipmitools exited with code {exitCode}");
            }

            return (byte) Regex.Matches(output)
                               .Where(m => m.Success)
                               .Select(m => m.Groups["temp"].Value)
                               .Select(int.Parse)
                               .Max();
        }

        public void SetFanSpeed(byte speed)
        {
            Console.WriteLine($"Setting fan speed to {speed}%");
            
            EngageManualControl();

            var command = $"-I lanplus -H {_host} -U {_username} -P {_password} raw 0x30 0x30 0x02 0xff 0x{speed:X2}";
            var exitCode = RunIpmiCommand(command, out var _, out var _);
            if (exitCode != 0) {
                throw new Exception($"ipmitools exited with code {exitCode}");
            }
        }

        public void EngageManualControl()
        {
            var command = $"-I lanplus -H {_host} -U {_username} -P {_password} raw 0x30 0x30 0x01 0x00";

            var exitCode = RunIpmiCommand(command, out var _, out var _);
            if (exitCode != 0) {
                throw new Exception($"ipmitools exited with code {exitCode}");
            }
        }

        public void ReleaseManualControl()
        {
            Console.WriteLine("Releasing fan speed control");

            var command = $"-I lanplus -H {_host} -U {_username} -P {_password} raw 0x30 0x30 0x01 0x01";

            var exitCode = RunIpmiCommand(command, out var _, out var _);
            if (exitCode != 0) {
                throw new Exception($"ipmitools exited with code {exitCode}");
            }
        }

        private int RunIpmiCommand(string command, out string output, out string error)
        {
            var process = Process.Start(new ProcessStartInfo("ipmitool") {
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            });

            output = process.StandardOutput.ReadToEnd();
            error = process.StandardError.ReadToEnd();

            Console.WriteLine(output.Trim());
            Console.WriteLine(error.Trim());

            process.WaitForExit();
            return process.ExitCode;
        }
    }
}