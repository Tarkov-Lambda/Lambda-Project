using System;
using Comfort.Common;
using EFT;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.Core.MovementStates;
using ifp.arena.bep.networking;
using ifp.arena.shared;
using UnityEngine;

namespace ifp.arena.bep.Core
{
    public class InRaidPresetManager : Singleton<InRaidPresetManager>, IDisposable
    {

        public InRaidPresetManager()
        {
            // GameModeTicker.onUpdate += OnUpdate;
            // GameModeTicker.onLateUpdate += OnLateUpdate;
        }

        public void Dispose()
        {
            // Ladder.onPlayerEnterLadder -= OnTriggerEnter;
            // Ladder.onPlayerExitLadder -= OnTriggerExit;
            // GameModeTicker.onUpdate -= OnUpdate;
            // GameModeTicker.onLateUpdate -= OnLateUpdate;
            Release(this);
        }
    }
}