using System;
using System.Reflection;
using HarmonyLib;
using ifp.arena.bep.Core;

namespace ifp.arena.bep
{
    public class DynamicClassTracer : IDisposable
    {
        private readonly Harmony _harmony;
        private readonly string _harmonyId;

        public DynamicClassTracer(Type targetType)
        {
            _harmonyId = $"com.ifp.respawn.tracer.{targetType.Name}";
            _harmony = new Harmony(_harmonyId);

            var prefixMethod = SymbolExtensions.GetMethodInfo(() => GenericPrefix(null));
            var harmonyPrefix = new HarmonyMethod(prefixMethod);

            var allMethods = AccessTools.GetDeclaredMethods(targetType);

            foreach (var method in allMethods)
            {
                if (method.IsGenericMethodDefinition) continue;
                try
                {
                    _harmony.Patch(method, prefix: harmonyPrefix);
                    H.Log($"[TRACER] Successfully patched {targetType.Name}.{method.Name}");
                }
                catch (Exception ex)
                {
                    H.Log($"[TRACER] Failed to patch {targetType.Name}.{method.Name}: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            _harmony.UnpatchSelf();
            H.Log($"[TRACER] Successfully Unpatched all methods for {_harmonyId}");
        }

        private static void GenericPrefix(MethodBase __originalMethod)
        {
            H.Log($"[TRACE] Executing: {__originalMethod.DeclaringType.Name}.{__originalMethod.Name}");
        }
    }
}