using HarmonyLib;
using ifp.arena.shared;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace ifp.arena.bep.Patches
{
    internal class Patch_AudioSource_spatialBlend : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.spatialBlend));

        [PatchTranspiler]
        public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = instructions.ToList();

            var setter = AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.spatialBlend));
            var hook = AccessTools.Method(typeof(Patch_AudioSource_spatialBlend), nameof(SpatialBlendHook));

            for (int i = 0; i < codes.Count; i++)
            {
                var instr = codes[i];

                // Match the callvirt to set_spatialBlend
                if (instr.Calls(setter))
                {
                    /*
                        Stack at this point:

                        AudioSource
                        float

                        We replace the call with our method:
                        static void SpatialBlendHook(AudioSource src, float value)
                    */

                    codes[i] = new CodeInstruction(OpCodes.Call, hook);

                    // (Optional) If you want to preserve exact stack behavior, nothing else needed
                    continue;
                }
            }

            return codes;
        }

        static void SpatialBlendHook(AudioSource src, float value)
        {
            var bridge = src.GetComponent<PhononDSPBridge>();
            if (bridge == null) return;

            bridge.spatialBlendOverride = Mathf.Clamp01(value);
        }
    }


}


