using EFT.UI;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Patches.Tarkov;
using ifp.arena.ui.Nameplate;
using System;
using UnityEngine;

namespace Lambda.Core.Main.UI
{
    internal class NameplateController : IDisposable
    {
        private readonly GameObject nameplateObject;

        public NameplateController(CommonUI commonUI, AssetBundle uiBundle)
        {
            EventBus.OnEnter += OnMatchStateEnter;
            Patch_Gameworld_OnDispose.OnDispose += Patch_Gameworld_OnDispose_OnDispose;

            nameplateObject = new GameObject("Nameplate Renderer", typeof(RectTransform), typeof(NameplateRenderer));

            nameplateObject.transform.SetParent(commonUI.EftBattleUIScreen.transform, false);

            NameplateRenderer renderer = nameplateObject.GetComponent<NameplateRenderer>();
            GameObject prefabNameplate = uiBundle.LoadAsset<GameObject>("Packages/com.ifp.arena.ui/Nameplate/Nameplate.prefab");
            renderer.Init(commonUI, prefabNameplate.GetComponent<Nameplate>());
        }

        private void OnMatchStateEnter(MatchState state)
        {
            nameplateObject.gameObject.SetActive(true);
        }

        private void Patch_Gameworld_OnDispose_OnDispose()
        {
            nameplateObject.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            EventBus.OnEnter -= OnMatchStateEnter;
            Patch_Gameworld_OnDispose.OnDispose -= Patch_Gameworld_OnDispose_OnDispose;

            if (nameplateObject != null)
                GameObject.Destroy(nameplateObject);
        }
    }
}
