using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace ifp.arena.bep.Patches.Tarkov.UI
{
    internal class Patch_PreloaderUI_RefreshCornerLabel : ModulePatch
    {
        static readonly FieldInfo Field_PreloaderUI___alphaVersionLabel = AccessTools.Field(typeof(PreloaderUI), "_alphaVersionLabel");

        protected override MethodBase GetTargetMethod() 
            => AccessTools.Method(typeof(PreloaderUI), nameof(PreloaderUI.method_6));

        [PatchPostfix]
        static void Postfix(PreloaderUI __instance)
        {
            LocalizedText text = Field_PreloaderUI___alphaVersionLabel.GetValue(__instance) as LocalizedText;
            if (text != null)
            {
                text.LocalizationKey = GetWatermark();
            }
        }

        static string GetWatermark()
        {
            Assembly assembly = Assembly.GetAssembly(typeof(Plugin));
            Version version = assembly.GetName().Version;

            string product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "";

            string versionString = $"{version.Major}.{version.Minor}.{version.Build}";
            string description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";

            return $"{product} {versionString} | {description}";
        }
    }
}
