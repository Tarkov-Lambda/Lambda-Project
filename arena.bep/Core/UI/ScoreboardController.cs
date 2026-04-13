using arena.ui;
using Comfort.Common;
using EFT;
using ifp.arena.bep.Patches.Tarkov;
using ifp.arena.shared.Models;
using System;
using UnityEngine;
using System.Linq;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.networking;

namespace ifp.arena.bep.Core.UI
{
    internal class ScoreboardController : IDisposable
    {
        readonly ArenaMatchUI matchUI;

        InventoryHotkeyListener inventoryHotkeyListener;

        internal ScoreboardController(ArenaMatchUI matchUI)
        {
            EventBus.OnPlayerKill += OnPlayerKill;
            EventBus.OnEnter += OnMatchStateEnter;

            Patch_Gameworld_OnGameStarted.OnGameStarted += AddInventoryHotkeyInterceptor;
            if (Singleton<GameWorld>.Instantiated) 
                AddInventoryHotkeyInterceptor();

            matchUI.Scoreboard.gameObject.SetActive(false);
        }

        private void OnPlayerKill(PlayerKilledPacket packet) => Refresh();
        private void OnMatchStateEnter(MatchState state) => Refresh();

        private void AddInventoryHotkeyInterceptor()
        {
            inventoryHotkeyListener = H.MainPlayer.gameObject.AddComponent<InventoryHotkeyListener>();
            inventoryHotkeyListener.OnHoldBegin += () => matchUI.Scoreboard.gameObject.SetActive(true);
            inventoryHotkeyListener.OnHoldEnd += () => matchUI.Scoreboard.gameObject.SetActive(false);
        }

        void Refresh()
        {
            PlayerScoreInfo[] allPlayerStats = H.Scoreboard.Values.Select(p => p.Score).ToArray();
            matchUI.Scoreboard.SetPlayers(allPlayerStats, H.Session.factionWins, H.MainPlayerScore.Faction);
        }

        public void Dispose()
        {
            EventBus.OnPlayerKill -= OnPlayerKill;
            EventBus.OnEnter -= OnMatchStateEnter;

            Patch_Gameworld_OnGameStarted.OnGameStarted -= AddInventoryHotkeyInterceptor;

            if (inventoryHotkeyListener != null)
                Component.Destroy(inventoryHotkeyListener);
        }
    }
}
