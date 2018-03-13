using System;
using System.Collections.Generic;
using System.Linq;

namespace IpmiFanControl
{
    public static class Mathf
    {
        public static float Clamp(float value, float min, float max)
        {
            if (float.IsNegativeInfinity(value)) {
                return min;
            }

            if (float.IsNaN(value) || float.IsPositiveInfinity(value)) {
                return max;
            }

            return Math.Max(min, Math.Min(value, max));
        }

        public static float Lerp(float a, float b, float alpha)
        {
            return a * (1 - alpha) + b * alpha;
        }
    }

    public class FanCurve
    {
        private readonly (byte temp, float speed)[] _thresholds;

        public (byte temp, float speed)[] Thresholds => _thresholds;

        public FanCurve(IEnumerable<(byte temp, float speed)> curve)
        {
            _thresholds = curve.ToArray();

            if (!_thresholds.Any()) {
                throw new ArgumentException("curve must contain at least one temperature");
            }

            for (int i = 1; i < _thresholds.Length; i++) {
                if (_thresholds[i - 1].temp >= _thresholds[i].temp) {
                    throw new ArgumentException("temperature thresholds in curve must be in ascending order");
                }
            }
        }

        public float Evaluate(byte temperature)
        {
            for (int i = 1; i < _thresholds.Length; i++) {
                var (endTemp, endSpeed) = _thresholds[i];

                if (endTemp < temperature) {
                    continue;
                }

                var (startTemp, startSpeed) = _thresholds[i - 1];

                var progress = 1 - Mathf.Clamp((endTemp - temperature) / (float)(endTemp - startTemp), 0, 1);
                return Mathf.Lerp(startSpeed, endSpeed, progress);
            }

            return _thresholds[_thresholds.Length - 1].speed;
        }
    }

    public class TemperatureRecord
    {
        private readonly TimeSpan _window;
        private readonly List<(DateTime time, byte temp)> _readings;

        private (DateTime, byte) _lastReading;

        public TemperatureRecord(TimeSpan window)
        {
            _window = window;
            _readings = new List<(DateTime time, byte temp)>();
            _lastReading = (DateTime.Now, 0);
        }

        public void Push(DateTime time, byte temperature)
        {
            _readings.Add((time, temperature));
            _lastReading = (time, temperature);
        }

        public float RecentPercentOverThreshold(TimeSpan duration, byte threshold)
        {
            CullOldReaadings();

            var range = Recent(duration);
            return range.Count(r => r.temp > threshold) / (float)range.Count;
        }

        public byte RecentMaximum(TimeSpan duration)
        {
            var range = Recent(duration);
            return range.Max(r => r.temp);
        }

        private List<(DateTime TimeoutException, byte temp)> Recent(TimeSpan duration)
        {
            CullOldReaadings();

            var now = DateTime.Now;
            var results = _readings.Where(r => (now - r.time) <= duration).ToList();

            if (results.Count == 0) {
                results.Add(_lastReading);
            }

            return results;
        }

        private void CullOldReaadings()
        {
            var cutoff = DateTime.Now - _window;
            while (_readings.Any() && _readings.First().time < cutoff) {
                _readings.RemoveAt(0);
            }
        }
    }

    public class FanController
    {
        public enum Mode
        {
            Idling,
            SustainedLoad
        }

        private readonly FanCurve _quietProfile;
        private readonly FanCurve _sustainedProfile;
        private readonly byte _overheatThreshold;
        private readonly byte _maxTemperature;
        private readonly TimeSpan _maxOverheatAllowance;
        private readonly TemperatureRecord _temperature;

        private Mode _currentMode;

        public Mode CurrentMode => _currentMode;

        public FanController(FanCurve quietProfile, FanCurve sustainedProfile, int? overheatThreshold, byte maxTemperature, TimeSpan maxOverheatAllowance)
        {
            _quietProfile = quietProfile;
            _sustainedProfile = sustainedProfile;
            _overheatThreshold = (byte)(overheatThreshold ?? quietProfile.Thresholds.Max(t => t.temp));
            _maxTemperature = Math.Max(maxTemperature, _overheatThreshold);
            _maxOverheatAllowance = maxOverheatAllowance;
            _temperature = new TemperatureRecord(TimeSpan.FromSeconds(Math.Max(15, maxOverheatAllowance.TotalSeconds)));
            _currentMode = Mode.SustainedLoad;
        }

        public void PushReading(DateTime time, byte temperature)
        {
            Console.WriteLine($"Measuring {temperature}°C");
            _temperature.Push(time, temperature);
        }

        public bool Evaluate(out byte speed)
        {
            var temp = _temperature.RecentMaximum(TimeSpan.FromSeconds(15));

            if (_currentMode == Mode.Idling) {
                var overheatAlpha = Mathf.Clamp((_maxTemperature - temp) / (float)(_maxTemperature - _overheatThreshold), 0, 1);
                var overheatTimeAllowance = _maxOverheatAllowance * overheatAlpha;

                if (_temperature.RecentPercentOverThreshold(overheatTimeAllowance, _overheatThreshold) > 0.9) {
                    _currentMode = Mode.SustainedLoad;
                }
            }
            else {
                if (_temperature.RecentPercentOverThreshold(_maxOverheatAllowance, _overheatThreshold) < 0.1) {
                    _currentMode = Mode.Idling;
                }
            }

            if (_currentMode == Mode.Idling) {
                speed = (byte)_quietProfile.Evaluate(temp);
            }
            else {
                speed = (byte)_sustainedProfile.Evaluate(temp);
            }

            return temp <= _maxTemperature;
        }
    }
}