using UnityEngine;
using arena.ui.scoreboard;
using ifp.arena.shared;

#if EFT_RUNTIME
using EFT;
using EFT.InventoryLogic;
using Comfort.Common;
using ifp.arena.bep;
using ifp.arena.bep.Core;
using ifp.arena.bep.Core.UI;
using ifp.arena.bep.GameTypes;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.Patches.Tarkov.UI;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.bep.networking;
using ifp.arena.bep.Core.Economy;
#endif

namespace arena.ui
{
    public class ArenaMatchUI : MonoBehaviour
    {
        [SerializeField] private TopBar topBar;
        [SerializeField] private Scoreboard scoreboard;

#if EFT_RUNTIME

        InventoryHotkeyListener inventoryHotkeyListener;

        void Awake()
        {
            Patch_Gameworld_OnGameStarted.OnGameStarted += AddInventoryHotkeyInterceptor;

            if (Singleton<GameWorld>.Instantiated)
                AddInventoryHotkeyInterceptor(Singleton<GameWorld>.Instance);

            EventBus.OnEnter += OnMatchStateEnter;
            EventBus.OnPlayerKill += OnPlayerKill;

            H.Arena.OnUpdateTick += () => topBar.SetTime(H.Arena.StateTimer);
        }

        private void AddInventoryHotkeyInterceptor(GameWorld gameWorld)
        {
            inventoryHotkeyListener = gameWorld.MainPlayer.gameObject.AddComponent<InventoryHotkeyListener>();
            inventoryHotkeyListener.OnHoldBegin += () => scoreboard.gameObject.SetActive(true);
            inventoryHotkeyListener.OnHoldEnd += () => scoreboard.gameObject.SetActive(false);
        }

        void OnMatchStateEnter(MatchState matchState)
        {
            Refresh();
        }

        void OnPlayerKill(PlayerKilledPacket killPacket)
        {
            Refresh();
        }

        void Refresh()
        {
            int scoreCT = H.Session.factionWins[Faction.CT];
            int scoreT = H.Session.factionWins[Faction.T];

            topBar.SetScores(scoreCT, scoreT);

            scoreboard.SetPlayers(H.Session.scoreboard);
        }

        void OnDestroy()
        {
            Patch_Gameworld_OnGameStarted.OnGameStarted -= AddInventoryHotkeyInterceptor;

            EventBus.OnEnter -= OnMatchStateEnter;

            if (inventoryHotkeyListener != null)
                Component.Destroy(inventoryHotkeyListener);
        }
#endif
    }
}
