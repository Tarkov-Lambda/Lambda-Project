using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ifp.arena.bep.Core;
using UnityEngine;

namespace ifp.arena.bep
{
    /// <summary>
    /// Stores per-method call data collected by DynamicClassTracer.
    /// </summary>
    public class TracedMethodInfo
    {
        public string MethodName;
        public string TypeName;
        public long TotalCalls;

        // Rolling window: timestamps of recent calls (kept for the last 2 seconds)
        private readonly Queue<float> _recentCallTimes = new Queue<float>();
        private readonly object _lock = new object();

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

                // Prune anything older than 1 second
                while (_recentCallTimes.Count > 0 && now - _recentCallTimes.Peek() > 1f)
                    _recentCallTimes.Dequeue();

                CallsPerSecond = _recentCallTimes.Count;
            }
        }
    }

    /// <summary>
    /// Dynamically patches every method on a target type and records call
    /// statistics that can be visualised by TracerOverlay.
    /// </summary>
    public class DynamicClassTracer : IDisposable
    {
        // ── Static store ────────────────────────────────────────────────────────
        // All active tracers write here; TracerOverlay reads from here.
        public static readonly ConcurrentDictionary<string, TracedMethodInfo> TracedData
            = new ConcurrentDictionary<string, TracedMethodInfo>();

        // Human-readable label for each tracer (typeName → label used by GUI)
        public static readonly ConcurrentDictionary<string, string> TracerLabels
            = new ConcurrentDictionary<string, string>();

        // ── Instance ─────────────────────────────────────────────────────────
        private readonly Harmony _harmony;
        private readonly string _harmonyId;
        private readonly string _typeName;

        public DynamicClassTracer(Type targetType)
        {
            _typeName = targetType.Name;
            _harmonyId = $"com.ifp.respawn.tracer.{_typeName}";
            _harmony = new Harmony(_harmonyId);

            TracerLabels[_typeName] = _typeName;

            var prefixMethod = SymbolExtensions.GetMethodInfo(() => GenericPrefix(null));
            var harmonyPrefix = new HarmonyMethod(prefixMethod);

            foreach (var method in AccessTools.GetDeclaredMethods(targetType))
            {
                if (method.IsGenericMethodDefinition) continue;
                try
                {
                    _harmony.Patch(method, prefix: harmonyPrefix);
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

            // Clean up entries that belong to this tracer
            foreach (var key in TracedData.Keys)
            {
                if (key.StartsWith(_typeName + "."))
                    TracedData.TryRemove(key, out _);
            }

            TracerLabels.TryRemove(_typeName, out _);
            H.Log($"[TRACER] Unpatched all methods for {_harmonyId}");
        }

        // ── Harmony prefix (static — called for every patched method) ────────
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
    }
}
