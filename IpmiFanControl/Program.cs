using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommandLine;

namespace IpmiFanControl
{
    public class Program
    {
        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<Options>(args)
                         .MapResult(Run, error => 1);
        }

        public static Options ParseArgs(string[] args)
        {
            return Parser.Default.ParseArguments<Options>(args)
                         .MapResult(
                             x => x,
                             e => null);
        }

        private static int Run(Options options)
        {
            options.LoadDefaults();
            Console.WriteLine($"Starting temperature control with options: {{{options}}}");

            var ipmi = new Ipmi(options.Host, options.Username, options.Password);

            var mre = new ManualResetEventSlim();
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => mre.Set();

            try {
                var fan = new FanController(new FanCurve(options.IdleCurve.Select(Parse)),
                                            new FanCurve((options.SustainedLoadCurve ?? options.IdleCurve).Select(Parse)),
                                            options.OverheatTemperature,
                                            (byte)options.MaximumTemperature,
                                            TimeSpan.FromSeconds(options.MaximumAllowableOverheatTime.Value));

                ipmi.Authenticate();

                var timer = new System.Timers.Timer(options.UpdateInterval.Value * 1000) {
                    AutoReset = true
                };

                var errorCount = 0;

                timer.Elapsed += (s, _) => {
                    try {
                        var reading = ipmi.GetTemperature();
                        fan.PushReading(DateTime.Now, reading);

                        if (fan.Evaluate(out var desiredSpeed)) {
                            ipmi.SetFanSpeed(desiredSpeed);
                        }
                        else {
                            ipmi.ReleaseManualControl();
                        }

                        errorCount = 0;
                    }
                    catch (Exception e) {
                        errorCount++;
                        Console.WriteLine(e.Message);

                        if (errorCount > 2) {
                            ipmi.ReleaseManualControl();
                        }
                    }
                };

                timer.Start();
                mre.Wait();
            }
            finally {
                ipmi.ReleaseManualControl();
            }

            return 0;
        }

        public static (byte temp, float speed) Parse(string curvePoint)
        {
            var split = curvePoint.Split(',');
            return (byte.Parse(split[0]), float.Parse(split[1]));
        }
    }

    public class Options
    {
        [Option('h', "host", HelpText = "The IPMI host to connect to.")]
        public string Host { get; set; }

        [Option('u', "user", HelpText = "The authentication username used to connect to the host.")]
        public string Username { get; set; }

        [Option('p', "password", HelpText = "The authentication password used to connect to the host.")]
        public string Password { get; set; }

        [Option('i', "interval", HelpText = "The number of seconds between updates.")]
        public int? UpdateInterval { get; set; }

        [Option('o', "overheat-temperature", HelpText = "The temperature beyond which is considered unsafe for sustained periods, during which the sustained load fan curve may be activated.")]
        public int? OverheatTemperature { get; set; }

        [Option('m', "max-temperature", HelpText = "The maximum allowable temperature, at which the sustained load fan curve will be immediately activated.")]
        public int? MaximumTemperature { get; set; }

        [Option('a', "overheat-allowance", HelpText = "The maximum number of seconds for which the CPU is allowed to be at overheat temperatures before the susatined load fan curve is activated.")]
        public int? MaximumAllowableOverheatTime { get; set; }

        [Option('f', "idle-fans", Separator = ';', HelpText = "The fan curve to apply under normal load, in the form of (temp, speed%) pairs, separated by ';'. e.g. '30,5;40,10;50,20'.")]
        public IEnumerable<string> IdleCurve { get; set; }

        [Option('s', "sustained-fans", Separator = ';', HelpText = "The fan curve to apply under sustained load, in the form of (temp, speed%) pairs, separated by ';'. e.g. '30,5;40,10;50,25;70,100'.")]
        public IEnumerable<string> SustainedLoadCurve { get; set; }

        public void LoadDefaults()
        {
            Host = Host ?? Environment.GetEnvironmentVariable("IPMI_HOST") ?? "127.0.0.1";
            Username = Username ?? Environment.GetEnvironmentVariable("IPMI_USERNAME");
            Password = Password ?? Environment.GetEnvironmentVariable("IPMI_PASSWORD");

            if (UpdateInterval == null) {
                UpdateInterval = int.TryParse(Environment.GetEnvironmentVariable("UPDATE_INTERVAL"), out var interval) ? interval : 5;
            }

            if (MaximumTemperature == null) {
                MaximumTemperature = int.TryParse(Environment.GetEnvironmentVariable("MAXIMUM_TEMPERATURE"), out var max) ? max : 70;
            }

            if (MaximumAllowableOverheatTime == null) {
                MaximumAllowableOverheatTime = int.TryParse(Environment.GetEnvironmentVariable("OVERHEAT_TIME"), out var overheatTime) ? overheatTime : 900;
            }

            if (IdleCurve == null || !IdleCurve.Any()) {
                var env = Environment.GetEnvironmentVariable("FAN_CURVE");
                if (string.IsNullOrEmpty(env)) {
                   throw new Exception("No idle fan curve specified.");
                }

                IdleCurve = env.Split(';');
            }

            if (OverheatTemperature == null) {
                if (int.TryParse(Environment.GetEnvironmentVariable("OVERHEAT_TEMPERATURE"), out var overheat)) {
                    OverheatTemperature = overheat;
                }
                else {
                    var curveMax = IdleCurve.Select(Program.Parse).Max(x => x.temp);
                    OverheatTemperature = Math.Min(MaximumTemperature.Value, curveMax);
                }
            }

            if (SustainedLoadCurve == null || !SustainedLoadCurve.Any()) {
                var env = Environment.GetEnvironmentVariable("SUSTAINED_FAN_CURVE");
                if (string.IsNullOrEmpty(env)) {
                    SustainedLoadCurve = env.Split(';');
                }
                else {
                    SustainedLoadCurve = IdleCurve;
                }
            }
        }

        public override string ToString()
        {
            return $"{nameof(Host)}: {Host}, {nameof(UpdateInterval)}: {UpdateInterval}, {nameof(OverheatTemperature)}: {OverheatTemperature}, {nameof(MaximumTemperature)}: {MaximumTemperature}, {nameof(MaximumAllowableOverheatTime)}: {MaximumAllowableOverheatTime}, {nameof(IdleCurve)}: {IdleCurve.Aggregate((a,b) => $"{a};{b}")}, {nameof(SustainedLoadCurve)}: {SustainedLoadCurve.Aggregate((a, b) => $"{a};{b}")}";
        }
    }
}
