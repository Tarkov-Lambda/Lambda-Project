using System;
using Comfort.Common;
using EFT;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.Core.MovementStates;
using ifp.arena.shared;
using UnityEngine;

namespace ifp.arena.bep.Core.Ladders
{
    public class LadderEventManager : Singleton<LadderEventManager>, IDisposable
    {
        public static bool isOnLadder;
        public static bool wasOriginallyGrounded;
        public static Collider ladderCollider;

        // GoldSrc default climb speed was relatively fast (~200 units). Adjust to fit your scale.
        private float _climbSpeed = 4f;

        public LadderEventManager()
        {
            H.Log("Created");
            Ladder.onPlayerEnterLadder += OnTriggerEnter;
            Ladder.onPlayerExitLadder += OnTriggerExit;
            GameModeTicker.onUpdate += OnUpdate;
            GameModeTicker.onLateUpdate += OnLateUpdate;
        }

        public void Dispose()
        {
            Ladder.onPlayerEnterLadder -= OnTriggerEnter;
            Ladder.onPlayerExitLadder -= OnTriggerExit;
            GameModeTicker.onUpdate -= OnUpdate;
            GameModeTicker.onLateUpdate -= OnLateUpdate;
            Release(this);
        }

        private void OnTriggerEnter(LadderEventPayload ladderEvent)
        {
            Player player = ladderEvent.other.GetComponentInParent<Player>();
            if (player == null || !player.IsYourPlayer) return;
            if (player.MovementContext.CurrentState is SprintStateClass) return;

            isOnLadder = true;
            wasOriginallyGrounded = H.MainPlayer.MovementContext.IsGrounded;
            ladderCollider = ladderEvent.other;
        }

        private void OnTriggerExit(LadderEventPayload ladderEvent)
        {
            Player player = ladderEvent.other.GetComponentInParent<Player>();
            if (player == null || !player.IsYourPlayer || ladderCollider == null) return;

            Exit();
        }

        private void OnUpdate()
        {
            if (isOnLadder)
            {
                H.MainPlayer.MovementContext.IsGrounded = true;
                if (H.MainPlayer.PoseLevel == 0f)
                {
                    Exit();
                }
            }
        }

        private void Exit()
        {
            isOnLadder = false;
            ladderCollider = null;
            H.MainPlayer.MovementContext.IsGrounded = false; // idk if needed

            // Vector3 motion = CameraClass.Instance.Camera.transform.forward * 3;
            // H.MainPlayer.MovementContext.ApplyMotion(motion, 1f);
        }

        private void OnLateUpdate()
        {
            if (isOnLadder)
            {
                H.MainPlayer.MovementContext.ResetFlying();

                Vector2 input = H.MainPlayer.InputDirection; // x = A/D, y = W/S
                Transform camTransform = CameraClass.Instance.Camera.transform;

                // --- GOLDSRC LADDER MOVEMENT ---
                // W/S maps directly to Camera Forward. A/D maps directly to Camera Right.
                Vector3 forwardMove = camTransform.forward * input.y;
                Vector3 sideMove = camTransform.right * input.x;

                Vector3 moveDirection = forwardMove + sideMove;


                // --- COLLISION FALLBACK ---
                // In GoldSrc, the engine's built-in "SlideMove" physics stops the player from going
                // through the wall when their camera's forward vector points into it. 
                // If EFT's PlatformMotion simply translates the player without sweeping/sliding, 
                // looking straight into the wall and holding W will clip them out of bounds. 
                // If this happens, you can use the collider to project their movement onto the ladder's plane:
                // if (ladderCollider != null)
                // {
                //     // Find the ladder's outward-facing normal (assuming its local Z faces away from the wall)
                //     Vector3 ladderNormal = ladderCollider.transform.forward;

                //     // If the player is trying to move backward into the wall, flatten that movement
                //     if (Vector3.Dot(moveDirection, ladderNormal) < 0)
                //     {
                //         moveDirection = Vector3.ProjectOnPlane(moveDirection, ladderNormal);
                //     }
                // }

                // Apply final movement. 
                H.MainPlayer.MovementContext.PlatformMotion = moveDirection * _climbSpeed * Time.deltaTime;
            }
        }
    }
}