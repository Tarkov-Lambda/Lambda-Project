using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ifp.arena.bep.Core;
using UnityEngine;

namespace ifp.arena.bep
{
    public struct TracedCallRecord
    {
        public float Timestamp;   // Time.realtimeSinceStartup at call time
        public string[] Args;        // "paramName=value" for each argument
        public string Result;      // return value string, or "(void)"
    }

    public class CircularBuffer<T>
    {
        private readonly T[] _buf;
        private readonly object _lock = new object();
        private int _head;   // index where NEXT write goes
        private int _count;  // how many valid entries

        public int Capacity => _buf.Length;
        public int Count { get { lock (_lock) return _count; } }

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

        // Returns up to the max, most recent first.
        public T[] GetSnapshot(int max = int.MaxValue)
        {
            lock (_lock)
            {
                int n = Math.Min(_count, max);
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


    public class TracedMethodInfo
    {
        public string MethodName;
        public string TypeName;
        public long TotalCalls;

        // Ring buffer, max history
        public readonly CircularBuffer<TracedCallRecord> History = new CircularBuffer<TracedCallRecord>(20);

        // Rolling 1-second window for calls/sec
        private readonly Queue<float> _recentCallTimes = new Queue<float>();
        private readonly object _lock = new object();
        private float _lastRecordTime = float.MinValue;

        public float LastCallTime { get; private set; }
        public float CallsPerSecond { get; private set; }

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

        public void RecordHistory(object[] args, object result, MethodBase method)
        {
            float now = Time.realtimeSinceStartup;

            // throttle high frequency methods. record at most 10 entries / sec
            float minInterval;
            lock (_lock)
            {
                minInterval = CallsPerSecond >= 5f ? 0.10f : 0f;
                if (now - _lastRecordTime < minInterval) return;
                _lastRecordTime = now;
            }

            // build argument strings ("paramName=value")
            ParameterInfo[] parameters = method.GetParameters();
            int argCount = args?.Length ?? 0;
            string[] argStrings = new string[argCount];
            for (int i = 0; i < argCount; i++)
            {
                string pName = i < parameters.Length ? parameters[i].Name : $"arg{i}";
                argStrings[i] = $"{pName}={SafeStr(args[i])}";
            }

            // Result string
            bool isVoid = method is MethodInfo mi && mi.ReturnType == typeof(void);
            string resultStr = isVoid ? "(void)" : SafeStr(result);

            History.Add(new TracedCallRecord
            {
                Timestamp = now,
                Args = argStrings,
                Result = resultStr
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

    public class DynamicClassTracer : IDisposable
    {
        public static readonly ConcurrentDictionary<string, TracedMethodInfo> TracedData = new ConcurrentDictionary<string, TracedMethodInfo>();

        public static readonly ConcurrentDictionary<string, string> TracerLabels = new ConcurrentDictionary<string, string>();

        private readonly Harmony _harmony;
        private readonly string _harmonyId;
        private readonly string _typeName;

        public DynamicClassTracer(Type targetType)
        {
            _typeName = targetType.Name;
            _harmonyId = $"com.ifp.respawn.tracer.{_typeName}";
            _harmony = new Harmony(_harmonyId);

            TracerLabels[_typeName] = _typeName;

            // standard path (no ref/out params) postfix uses __args safely
            var harmonyPrefix = new HarmonyMethod(AccessTools.Method(typeof(DynamicClassTracer), nameof(GenericPrefix)));
            var harmonyPostfix = new HarmonyMethod(AccessTools.Method(typeof(DynamicClassTracer), nameof(GenericPostfix)));
            var harmonyPostfixVoid = new HarmonyMethod(AccessTools.Method(typeof(DynamicClassTracer), nameof(GenericPostfixVoid)));

            // ref safe path (has ref/out params) args captured in prefix, postfix omits __args
            // to prevent Harmony's copy back from overwriting the ref results the original method wrote.
            var harmonyCapturePrefix = new HarmonyMethod(AccessTools.Method(typeof(DynamicClassTracer), nameof(GenericPrefixCapture)));
            var harmonyPostfixRefSafe = new HarmonyMethod(AccessTools.Method(typeof(DynamicClassTracer), nameof(GenericPostfixRefSafe)));
            var harmonyPostfixVoidRefSafe = new HarmonyMethod(AccessTools.Method(typeof(DynamicClassTracer), nameof(GenericPostfixVoidRefSafe)));

            foreach (var method in targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsGenericMethodDefinition) continue;
                try
                {
                    bool isVoid = method.ReturnType == typeof(void);
                    bool hasRefOut = System.Linq.Enumerable.Any(method.GetParameters(), p => p.ParameterType.IsByRef);

                    HarmonyMethod prefix = hasRefOut ? harmonyCapturePrefix : harmonyPrefix;
                    HarmonyMethod postfix = hasRefOut ? (isVoid ? harmonyPostfixVoidRefSafe : harmonyPostfixRefSafe)
                                                      : (isVoid ? harmonyPostfixVoid : harmonyPostfix);

                    _harmony.Patch(method, prefix: prefix, postfix: postfix);
                    D.Log($"[TRACER] Patched {_typeName}.{method.Name}{(hasRefOut ? " (ref-safe)" : "")}");
                }
                catch (Exception ex)
                {
                    D.Log($"[TRACER] Failed to patch {_typeName}.{method.Name}: {ex.Message}");
                }
            }
        }

        // methods that have ref/out parameters must NOT use object[] __args in their
        // postfix, harmony copies the __args array back over the ref parameters after
        // the postfix runs, which would overwrite any modifications the original method made
        // 
        // instead we capture a clone of the arguments in a second prefix and store it
        // on a per-thread stack so the ref safe postfix can read them without __args
        [ThreadStatic]
        private static Stack<object[]> _refArgStack;

        // prefix for methods that have ref/out params: records the call count AND
        // pushes a clone of the argument values onto the thread local stack.
        private static void GenericPrefixCapture(MethodBase __originalMethod, object[] __args)
        {
            string typeName = __originalMethod.DeclaringType?.Name ?? "Unknown";
            string key = $"{typeName}.{__originalMethod.Name}";

            var info = TracedData.GetOrAdd(key, _ => new TracedMethodInfo
            {
                TypeName = typeName,
                MethodName = __originalMethod.Name
            });
            info.RecordCall();

            // clone the arg values before the original method can mutate any ref params.
            if (_refArgStack == null) _refArgStack = new Stack<object[]>();
            int len = __args?.Length ?? 0;
            var snapshot = new object[len];
            for (int i = 0; i < len; i++)
                snapshot[i] = __args[i];
            _refArgStack.Push(snapshot);
        }

        // postfix for non-void methods with ref/out params.
        // deliberately omits object[] __args to prevent Harmony's copy-back.
        private static void GenericPostfixRefSafe(MethodBase __originalMethod, object __result)
        {
            string typeName = __originalMethod.DeclaringType?.Name ?? "Unknown";
            string key = $"{typeName}.{__originalMethod.Name}";

            object[] args = (_refArgStack != null && _refArgStack.Count > 0) ? _refArgStack.Pop() : null;
            if (TracedData.TryGetValue(key, out var info))
                info.RecordHistory(args, __result, __originalMethod);
        }

        // postfix for void methods with ref/out params.
        // deliberately omits object[] __args to prevent Harmony's copy-back.
        private static void GenericPostfixVoidRefSafe(MethodBase __originalMethod)
        {
            string typeName = __originalMethod.DeclaringType?.Name ?? "Unknown";
            string key = $"{typeName}.{__originalMethod.Name}";

            object[] args = (_refArgStack != null && _refArgStack.Count > 0) ? _refArgStack.Pop() : null;
            if (TracedData.TryGetValue(key, out var info))
                info.RecordHistory(args, null, __originalMethod);
        }

        public void Dispose()
        {
            _harmony.UnpatchSelf();
            foreach (var key in TracedData.Keys)
                if (key.StartsWith(_typeName + "."))
                    TracedData.TryRemove(key, out _);
            TracerLabels.TryRemove(_typeName, out _);
            D.Log($"[TRACER] Unpatched all methods for {_harmonyId}");
        }

        private static void GenericPrefix(MethodBase __originalMethod)
        {
            string typeName = __originalMethod.DeclaringType?.Name ?? "Unknown";
            string key = $"{typeName}.{__originalMethod.Name}";

            var info = TracedData.GetOrAdd(key, _ => new TracedMethodInfo
            {
                TypeName = typeName,
                MethodName = __originalMethod.Name
            });

            info.RecordCall();
        }

        // __args  — all method arguments as object[] (HarmonyLib injectable)
        // __result — return value, boxed (non-void methods only)
        private static void GenericPostfix(MethodBase __originalMethod, object[] __args, object __result)
        {
            string typeName = __originalMethod.DeclaringType?.Name ?? "Unknown";
            string key = $"{typeName}.{__originalMethod.Name}";

            if (TracedData.TryGetValue(key, out var info))
                info.RecordHistory(__args, __result, __originalMethod);
        }

        // separate postfix for void methods. omitting __result avoids harmony error
        private static void GenericPostfixVoid(MethodBase __originalMethod, object[] __args)
        {
            string typeName = __originalMethod.DeclaringType?.Name ?? "Unknown";
            string key = $"{typeName}.{__originalMethod.Name}";

            if (TracedData.TryGetValue(key, out var info))
                info.RecordHistory(__args, null, __originalMethod);
        }
    }
}
