using System;
using System.Diagnostics;

namespace MzuApplication.Data
{
    /// <summary>
    /// Lightweight, in-process performance telemetry for database access. Every DB round
    /// trip reports its outcome and duration here, so the Settings screen can show real,
    /// observed metrics — query speed, success/error rate and session uptime — rather than
    /// fabricated figures. Thread-safe; cheap enough to wrap every call.
    /// </summary>
    internal static class DbMetrics
    {
        private static readonly object _lock = new object();
        private static readonly DateTime _startedUtc = DateTime.UtcNow;

        private static long _total;        // total DB operations attempted
        private static long _failures;     // operations that threw
        private static long _totalMs;      // cumulative duration of successful ops
        private static long _minMs = long.MaxValue;
        private static long _maxMs;
        private static long _lastMs;
        private static DateTime? _lastErrorUtc;
        private static string _lastError;

        /// <summary>
        /// Times an operation, recording its duration and whether it succeeded. Rethrows
        /// any exception after recording it, so callers behave exactly as before.
        /// </summary>
        public static T Measure<T>(Func<T> op)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                T result = op();
                sw.Stop();
                Record(true, sw.ElapsedMilliseconds, null);
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Record(false, sw.ElapsedMilliseconds, ex.Message);
                throw;
            }
        }

        public static void Measure(Action op)
        {
            Measure<object>(() => { op(); return null; });
        }

        private static void Record(bool ok, long ms, string error)
        {
            lock (_lock)
            {
                _total++;
                _lastMs = ms;
                if (ok)
                {
                    _totalMs += ms;
                    if (ms < _minMs) _minMs = ms;
                    if (ms > _maxMs) _maxMs = ms;
                }
                else
                {
                    _failures++;
                    _lastError = error;
                    _lastErrorUtc = DateTime.UtcNow;
                }
            }
        }

        /// <summary>A snapshot of the current metrics for display.</summary>
        public static DbMetricsSnapshot Snapshot()
        {
            lock (_lock)
            {
                long ok = _total - _failures;
                return new DbMetricsSnapshot
                {
                    TotalOps = _total,
                    Failures = _failures,
                    SuccessRatePercent = _total == 0 ? 100.0 : ok * 100.0 / _total,
                    ErrorRatePercent = _total == 0 ? 0.0 : _failures * 100.0 / _total,
                    AvgMs = ok == 0 ? 0 : (double)_totalMs / ok,
                    MinMs = _minMs == long.MaxValue ? 0 : _minMs,
                    MaxMs = _maxMs,
                    LastMs = _lastMs,
                    Uptime = DateTime.UtcNow - _startedUtc,
                    LastError = _lastError,
                    LastErrorUtc = _lastErrorUtc
                };
            }
        }

        /// <summary>Clears the collected metrics (session uptime keeps running).</summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _total = _failures = _totalMs = _maxMs = _lastMs = 0;
                _minMs = long.MaxValue;
                _lastError = null;
                _lastErrorUtc = null;
            }
        }
    }

    internal sealed class DbMetricsSnapshot
    {
        public long TotalOps;
        public long Failures;
        public double SuccessRatePercent;
        public double ErrorRatePercent;
        public double AvgMs;
        public long MinMs;
        public long MaxMs;
        public long LastMs;
        public TimeSpan Uptime;
        public string LastError;
        public DateTime? LastErrorUtc;
    }
}
