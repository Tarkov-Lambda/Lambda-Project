using EFT;
using ifp.arena.bep.Core.MovementStates;
using ifp.arena.shared;
using UnityEngine;

namespace ifp.arena.bep.Core.Ladders
{
    [RequireComponent(typeof(Ladder))]
    public class LadderTrigger : MonoBehaviour
    {
        private Ladder _ladder;

        private void Awake()
        {
            _ladder = GetComponent<Ladder>();
        }

        private void OnTriggerEnter(Collider other)
        {
            H.Dump(other);
            Player player = other.GetComponentInParent<Player>();
            if (player == null || !player.IsYourPlayer) return;

            // Only enter the ladder state when the player is on foot (idle or running)
            BaseMovementState current = player.MovementContext.CurrentState;
            if (current is LadderState) return;

            player.MovementContext.OverrideState(new LadderState(player.MovementContext, _ladder));
        }

        private void OnTriggerExit(Collider other)
        {
            H.Dump(other);
            Player player = other.GetComponentInParent<Player>();
            if (player == null || !player.IsYourPlayer) return;

            // Safety net: if they somehow leave the collider while still in LadderState, return them to idle
            if (player.MovementContext.CurrentState is LadderState)
            {
                player.MovementContext.OverrideState(new OldIdleState(player.MovementContext));
            }
        }
    }
}
