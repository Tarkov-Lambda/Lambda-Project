using Comfort.Common;
using EFT;
using HarmonyLib;
using ifp.arena.bep.networking;
using SPT.Reflection.Patching;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov
{
    public class Patch_Grenade_InvokeBlowUpEvent : ModulePatch
    {
        private const string _smokeTemplateId = "617aa4dd8166f034d57de9c5";
        private const string _molotovTemplateId = "617fd91e5539a84ec44ce155"; // Zarya

        protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Grenade), nameof(Grenade.InvokeBlowUpEvent));

        [PatchPrefix]
        static bool Prefix(Grenade __instance)
        {
            if (__instance.WeaponSource.StringTemplateId is _molotovTemplateId)
            {
                Singleton<CustomGrenadeExplosionPacketHandler>.Instance.Send(__instance.transform.position, CustomGrenadeType.Molotov);
                UnityEngine.Object.DestroyImmediate(__instance.gameObject);
                return false;
            }

            return true;
        }
    }
}
