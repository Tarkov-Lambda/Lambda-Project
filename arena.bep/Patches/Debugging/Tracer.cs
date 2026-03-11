using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ifp.arena.bep.Core;
using UnityEngine;

namespace ifp.arena.bep
{
    // ── Per-invocation record ────────────────────────────────────────────────

    public struct TracedCallRecord
    {
        public float    Timestamp;   // Time.realtimeSinceStartup at call time
        public string[] Args;        // "paramName=value" for each argument
        public string   Result;      // return value string, or "(void)"
    }

    // ── Fixed-size ring buffer (thread-safe) ─────────────────────────────────

    public class CircularBuffer<T>
    {
        private readonly T[]    _buf;
        private readonly object _lock = new object();
        private int _head;   // index where NEXT write goes
        private int _count;  // how many valid entries

        public int Capacity => _buf.Length;
        public int Count    { get { lock (_lock) return _count; } }

        public CircularBuffer(int capacity) => _buf = new T[capacity];

        public void Add(T item)
        {
            lock (_lock)
            {
                _buf[_head] = item;
                _head = (_head + 1) % _buf.Length;
                if (_count < _buf.Length) _count++;
            }
        }

        /// Returns up to <paramref name="max"/> entries, most recent first.
        public T[] GetSnapshot(int max = int.MaxValue)
        {
            lock (_lock)
            {
                int n    = Math.Min(_count, max);
                var snap = new T[n];
                for (int i = 0; i < n; i++)
                {
                    int idx = ((_head - 1 - i) % _buf.Length + _buf.Length) % _buf.Length;
                    snap[i] = _buf[idx];
                }
                return snap;
            }
        }
    }

    // ── Per-method statistics + history ──────────────────────────────────────

    public class TracedMethodInfo
    {
        public string MethodName;
        public string TypeName;
        public long   TotalCalls;

        // Ring buffer: 20 slots is enough for both hot and cold display needs
        public readonly CircularBuffer<TracedCallRecord> History = new CircularBuffer<TracedCallRecord>(20);

        // Rolling 1-second window for calls/sec
        private readonly Queue<float> _recentCallTimes = new Queue<float>();
        private readonly object       _lock            = new object();
        private float _lastRecordTime = float.MinValue;

        public float LastCallTime    { get; private set; }
        public float CallsPerSecond  { get; private set; }

        // ── Call counting ────────────────────────────────────────────────────

        public void RecordCall()
        {
            float now = Time.realtimeSinceStartup;
            lock (_lock)
            {
                TotalCalls++;
                LastCallTime = now;

                _recentCallTimes.Enqueue(now);
                while (_recentCallTimes.Count > 0 && now - _recentCallTimes.Peek() > 1f)
                    _recentCallTimes.Dequeue();

                CallsPerSecond = _recentCallTimes.Count;
            }
        }

        // ── History recording ────────────────────────────────────────────────

        public void RecordHistory(object[] args, object result, MethodBase method)
        {
            float now = Time.realtimeSinceStartup;

            // Throttle high-frequency methods: record at most ~10 entries/sec
            // so the ring buffer covers a useful time window rather than filling instantly.
            float minInterval;
            lock (_lock)
            {
                minInterval = CallsPerSecond >= 5f ? 0.10f : 0f;
                if (now - _lastRecordTime < minInterval) return;
                _lastRecordTime = now;
            }

            // Build argument strings ("paramName=value")
            ParameterInfo[] parameters = method.GetParameters();
            int argCount = args?.Length ?? 0;
            string[] argStrings = new string[argCount];
            for (int i = 0; i < argCount; i++)
            {
                string pName = i < parameters.Length ? parameters[i].Name : $"arg{i}";
                argStrings[i] = $"{pName}={SafeStr(args[i])}";
            }

            // Result string
            bool   isVoid    = method is MethodInfo mi && mi.ReturnType == typeof(void);
            string resultStr = isVoid ? "(void)" : SafeStr(result);

            History.Add(new TracedCallRecord
            {
                Timestamp = now,
                Args      = argStrings,
                Result    = resultStr
            });
        }

        private static string SafeStr(object obj)
        {
            if (obj == null) return "null";
            try
            {
                string s = obj.ToString();
                return s.Length > 50 ? s.Substring(0, 47) + "…" : s;
            }
            catch
            {
                return $"<{obj.GetType().Name}>";
            }
        }
    }

    // ── Dynamic class tracer ─────────────────────────────────────────────────

    /// <summary>
    /// Dynamically patches every method on a target type and records call
    /// statistics + per-invocation history that TracerOverlay can display.
    /// </summary>
    public class DynamicClassTracer : IDisposable
    {
        // Static stores — read by TracerOverlay from any component
        public static readonly ConcurrentDictionary<string, TracedMethodInfo> TracedData
            = new ConcurrentDictionary<string, TracedMethodInfo>();

        public static readonly ConcurrentDictionary<string, string> TracerLabels
            = new ConcurrentDictionary<string, string>();

        private readonly Harmony _harmony;
        private readonly string  _harmonyId;
        private readonly string  _typeName;

        public DynamicClassTracer(Type targetType)
        {
            _typeName  = targetType.Name;
            _harmonyId = $"com.ifp.respawn.tracer.{_typeName}";
            _harmony   = new Harmony(_harmonyId);

            TracerLabels[_typeName] = _typeName;

            var harmonyPrefix  = new HarmonyMethod(AccessTools.Method(typeof(DynamicClassTracer), nameof(GenericPrefix)));
            var harmonyPostfix = new HarmonyMethod(AccessTools.Method(typeof(DynamicClassTracer), nameof(GenericPostfix)));

            var harmonyPostfixVoid = new HarmonyMethod(AccessTools.Method(typeof(DynamicClassTracer), nameof(GenericPostfixVoid)));

            foreach (var method in AccessTools.GetDeclaredMethods(targetType))
            {
                if (method.IsGenericMethodDefinition) continue;
                try
                {
                    bool isVoid = method.ReturnType == typeof(void);
                    _harmony.Patch(method, prefix: harmonyPrefix, postfix: isVoid ? harmonyPostfixVoid : harmonyPostfix);
                    H.Log($"[TRACER] Patched {_typeName}.{method.Name}");
                }
                catch (Exception ex)
                {
                    H.Log($"[TRACER] Failed to patch {_typeName}.{method.Name}: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            _harmony.UnpatchSelf();
            foreach (var key in TracedData.Keys)
                if (key.StartsWith(_typeName + "."))
                    TracedData.TryRemove(key, out _);
            TracerLabels.TryRemove(_typeName, out _);
            H.Log($"[TRACER] Unpatched all methods for {_harmonyId}");
        }

        // ── Harmony patches ───────────────────────────────────────────────────

        private static void GenericPrefix(MethodBase __originalMethod)
        {
            string typeName = __originalMethod.DeclaringType?.Name ?? "Unknown";
            string key      = $"{typeName}.{__originalMethod.Name}";

            var info = TracedData.GetOrAdd(key, _ => new TracedMethodInfo
            {
                TypeName   = typeName,
                MethodName = __originalMethod.Name
            });

            info.RecordCall();
        }

        // __args  — all method arguments as object[] (HarmonyLib injectable)
        // __result — return value, boxed (non-void methods only)
        private static void GenericPostfix(MethodBase __originalMethod, object[] __args, object __result)
        {
            string typeName = __originalMethod.DeclaringType?.Name ?? "Unknown";
            string key      = $"{typeName}.{__originalMethod.Name}";

            if (TracedData.TryGetValue(key, out var info))
                info.RecordHistory(__args, __result, __originalMethod);
        }

        // Separate postfix for void methods — omitting __result avoids the
        // "Cannot get result from void method" HarmonyX compile error.
        private static void GenericPostfixVoid(MethodBase __originalMethod, object[] __args)
        {
            string typeName = __originalMethod.DeclaringType?.Name ?? "Unknown";
            string key      = $"{typeName}.{__originalMethod.Name}";

            if (TracedData.TryGetValue(key, out var info))
                info.RecordHistory(__args, null, __originalMethod);
        }
    }
}
