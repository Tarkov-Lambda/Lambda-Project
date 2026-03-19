using System;
using Comfort.Common;
using EFT;
using ifp.arena.bep.Core.MovementStates;
using ifp.arena.shared;
using UnityEngine;

namespace ifp.arena.bep.Core.Ladders
{
    public class LadderEventManager : Singleton<LadderEventManager>, IDisposable
    {
        public LadderEventManager()
        {
            Ladder.onPlayerEnterLadder += OnTriggerEnter;
            Ladder.onPlayerExitLadder += OnTriggerExit;
        }

        public void Dispose()
        {
            Ladder.onPlayerEnterLadder -= OnTriggerEnter;
            Ladder.onPlayerExitLadder -= OnTriggerExit;
            Release(this);
        }


        private void OnTriggerEnter(LadderEventPayload ladderEvent)
        {
            H.Dump(ladderEvent);
            Player player = ladderEvent.collider.GetComponentInParent<Player>();
            if (player == null || !player.IsYourPlayer) return;

            // Only enter the ladder state when the player is on foot (idle or running)
            BaseMovementState current = player.MovementContext.CurrentState;
            if (current is LadderState) return;

            player.MovementContext.OverrideState(new LadderState(player.MovementContext, ladderEvent.ladder));
        }

        private void OnTriggerExit(LadderEventPayload ladderEvent)
        {
            H.Dump(ladderEvent);
            Player player = ladderEvent.collider.GetComponentInParent<Player>();
            if (player == null || !player.IsYourPlayer) return;

            if (player.MovementContext.CurrentState is LadderState)
            {
                H.MainPlayer.MovementContext.ExitOverridenState();
            }
        }
    }
}
