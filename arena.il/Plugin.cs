using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using ifp.arena.il.Patches;
using SPT.Reflection.Patching;

namespace ifp.arena.il
{
    [BepInDependency("com.fika.core")]
    [BepInPlugin("com.ifp.arena.il", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static new ManualLogSource Logger;

        private readonly List<ModulePatch> _patches = new();

        private void RegisterPatch(ModulePatch patch)
        {
            patch.Enable();
            _patches.Add(patch);
        }


        void Start()
        {
            Logger = base.Logger;
            // RegisterPatch(new ObservedPlayer_CreateObservedPlayer_Transpiler());
        }


        void OnDestroy()
        {
            foreach (var patch in _patches)
                patch.Disable();

            _patches.Clear();
        }
    }
}