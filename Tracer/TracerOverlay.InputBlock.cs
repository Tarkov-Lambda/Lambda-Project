using HarmonyLib;
using UnityEngine;

namespace ifp.tracer
{
    // unity legacy input patcher 
    internal static class InputBlockPatches
    {
        internal static void Apply(Harmony harmony)
        {
            harmony.PatchAll(typeof(Patch_GetMouseButton));
            harmony.PatchAll(typeof(Patch_GetMouseButtonDown));
            harmony.PatchAll(typeof(Patch_GetMouseButtonUp));
            harmony.PatchAll(typeof(Patch_GetKey));
            harmony.PatchAll(typeof(Patch_GetKeyDown));
            harmony.PatchAll(typeof(Patch_GetKeyUp));
            harmony.PatchAll(typeof(Patch_GetAxis));
            harmony.PatchAll(typeof(Patch_GetAxisRaw));
            harmony.PatchAll(typeof(Patch_CursorVisible));
            harmony.PatchAll(typeof(Patch_CursorLockState));
        }

        private static bool IsMouseKey(KeyCode key) => key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6;

        [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButton))]
        static class Patch_GetMouseButton
        {
            static bool Prefix(ref bool __result)
            {
                if (!TracerOverlay.IsVisible) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonDown))]
        static class Patch_GetMouseButtonDown
        {
            static bool Prefix(ref bool __result)
            {
                if (!TracerOverlay.IsVisible) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonUp))]
        static class Patch_GetMouseButtonUp
        {
            static bool Prefix(ref bool __result)
            {
                if (!TracerOverlay.IsVisible) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetKey), typeof(KeyCode))]
        static class Patch_GetKey
        {
            static bool Prefix(KeyCode key, ref bool __result)
            {
                if (!TracerOverlay.IsVisible || !IsMouseKey(key)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
        static class Patch_GetKeyDown
        {
            static bool Prefix(KeyCode key, ref bool __result)
            {
                if (!TracerOverlay.IsVisible || !IsMouseKey(key)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyUp), typeof(KeyCode))]
        static class Patch_GetKeyUp
        {
            static bool Prefix(KeyCode key, ref bool __result)
            {
                if (!TracerOverlay.IsVisible || !IsMouseKey(key)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Cursor), "set_visible")]
        static class Patch_CursorVisible
        {
            static bool Prefix(bool value)
            {
                // Allow hiding only when the overlay is closed.
                if (TracerOverlay.IsVisible && !value) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(Cursor), "set_lockState")]
        static class Patch_CursorLockState
        {
            static bool Prefix(CursorLockMode value)
            {
                if (TracerOverlay.IsVisible && value != CursorLockMode.None) return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetAxis))]
        static class Patch_GetAxis
        {
            static bool Prefix(string axisName, ref float __result)
            {
                if (!TracerOverlay.IsVisible) return true;
                if (axisName == "Mouse X" || axisName == "Mouse Y" || axisName == "Mouse ScrollWheel")
                {
                    __result = 0f;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetAxisRaw))]
        static class Patch_GetAxisRaw
        {
            static bool Prefix(string axisName, ref float __result)
            {
                if (!TracerOverlay.IsVisible) return true;
                if (axisName == "Mouse X" || axisName == "Mouse Y" || axisName == "Mouse ScrollWheel")
                {
                    __result = 0f;
                    return false;
                }
                return true;
            }
        }
    }
}
