using System;
using Comfort.Common;
using EFT;
using ifp.arena.bep.Core.Gamemode;
using ifp.arena.bep.Core.MovementStates;
using ifp.arena.bep.networking;
using ifp.arena.shared;
using UnityEngine;

namespace ifp.arena.bep.Core.Ladders
{
    public class LadderEventManager : Singleton<LadderEventManager>, IDisposable
    {
        public static bool isOnLadder;
        public static bool wasOriginallyGrounded;
        public static Collider ladderCollider;

        private float _climbSpeed = 4f;

        private float _ladderStepDistanceThreshold = 1.2f; 
        private float _ladderDistanceAccumulator = 0f;

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
            
            // Reset the audio accumulator when we grab the ladder
            _ladderDistanceAccumulator = 0f;
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
            _ladderDistanceAccumulator = 0f;

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

                Vector3 forwardMove = camTransform.forward * input.y;
                Vector3 sideMove = camTransform.right * input.x;

                Vector3 moveDirection = forwardMove + sideMove;
                Vector3 motionThisFrame = moveDirection * _climbSpeed * Time.deltaTime;
                
                H.MainPlayer.MovementContext.PlatformMotion = motionThisFrame;

                float distanceMovedThisFrame = motionThisFrame.magnitude;
                
                if (distanceMovedThisFrame > 0f)
                {
                    _ladderDistanceAccumulator += distanceMovedThisFrame;

                    if (_ladderDistanceAccumulator >= _ladderStepDistanceThreshold)
                    {
                        _ladderDistanceAccumulator %= _ladderStepDistanceThreshold;
                        Singleton<LadderNoisePacketHandler>.Instance.Send(); 
                    }
                }
            }
        }
    }
}