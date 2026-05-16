using Comfort.Common;
using EFT.UI;
using EFT.UI.Screens;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using System;
using UnityEngine;

namespace Lambda.Core.Main.UI
{
    internal class FactionSelectionController : IDisposable
    {
        FactionSelectionEftScreen factionSelectionScreen;

        internal FactionSelectionController(CommonUI commonUI, AssetBundle uibundle)
        {
            UnityTicker.OnUpdate += OnUpdate;
            EventBus.OnSelfFactionChanged += OnSelfFactionChanged;
            EventBus.OnEnter += OnMatchStateEnter;

            GameObject prefabFactionSelection = uibundle.LoadAsset<GameObject>("Packages/com.lambda.ui/FactionSelection/FactionSelection.prefab");
            factionSelectionScreen = GameObject.Instantiate(prefabFactionSelection, commonUI.transform.GetChild(0)).AddComponent<FactionSelectionEftScreen>();
            factionSelectionScreen.transform.SetSiblingIndex(1);

            EftScreenManager.Instance.RegisterScreen(FactionSelectionEftScreen.FAKETYPE, factionSelectionScreen);
            factionSelectionScreen.Close();
        }

        void OnMatchStateEnter(MatchState matchState)
        {
            if (matchState == MatchState.Warmup && H.Gamemode is IGMTeam)
            {
                ShowFactionSelectionEftScreen();
            }
        }

        void OnSelfFactionChanged(Faction faction)
        {
            if (factionSelectionScreen.gameObject.activeSelf)
                factionSelectionScreen.Cancel();
        }

        public void ShowFactionSelectionEftScreen()
        {
            FactionSelectionEftScreen.FactionSelectionEftScreenController screenController = new(Singleton<FactionChangePacketWarden>.Instance.Send);
            screenController.ShowScreen(EScreenState.Temporary);
        }

        private void OnUpdate()
        {
            if (!H.IsInRaid())
                return;

            if (UIEventSystem.Instance.EventSystem_0.IsActive())
                return;

            if (Input.GetKeyDown(KeyCode.M))
            {
                ShowFactionSelectionEftScreen();
            }
        }

        public void Dispose()
        {
            UnityTicker.OnUpdate -= OnUpdate;
            EventBus.OnSelfFactionChanged -= OnSelfFactionChanged;
            EventBus.OnEnter -= OnMatchStateEnter;

            if (factionSelectionScreen != null)
            {
                if (EftScreenManager.Instance.CurrentBaseScreenController is FactionSelectionEftScreen.FactionSelectionEftScreenController)
                    EftScreenManager.Instance.CloseCurrentScreenForced();
                EftScreenManager.Instance.ReleaseScreen(FactionSelectionEftScreen.FAKETYPE, factionSelectionScreen);
                GameObject.Destroy(factionSelectionScreen.gameObject);
            }
        }
    }
}
