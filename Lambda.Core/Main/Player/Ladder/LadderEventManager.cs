using System;
using Comfort.Common;
using EFT;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using Lambda.Shared;
using UnityEngine;

namespace Lambda.Core.Main;

public class LadderManager : Singleton<LadderManager>, IDisposable
{
    public static bool isOnLadder;
    public static bool wasOriginallyGrounded;
    public static Collider ladderCollider;

    private float _climbSpeed = 3f;

    private float _ladderStepDistanceThreshold = 3f;
    private float _ladderDistanceAccumulator = 0f;
    private Ladder _currentLader;

    public LadderManager()
    {
        Ladder.OnPlayerEnterLadder += OnTriggerEnter;
        Ladder.OnPlayerExitLadder += OnTriggerExit;
        UnityTicker.OnUpdate += OnUpdate;
        UnityTicker.OnLateUpdate += OnLateUpdate;
    }

    public void Dispose()
    {
        Ladder.OnPlayerEnterLadder -= OnTriggerEnter;
        Ladder.OnPlayerExitLadder -= OnTriggerExit;
        UnityTicker.OnUpdate -= OnUpdate;
        UnityTicker.OnLateUpdate -= OnLateUpdate;
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
        _currentLader = ladderEvent.ladder;
        Singleton<LadderNoisePacketWarden>.Instance.Send(_currentLader.ladderMaterial);
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
        _currentLader = null;

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

            Vector3 ladderUp = _currentLader.transform.up;

            Vector3 sideMove = camTransform.right * input.x;

            Vector3 verticalMove = ladderUp * input.y;

            Vector3 moveDirection = verticalMove + sideMove;

            Vector3 motionThisFrame =
                moveDirection *
                _climbSpeed *
                H.MainPlayer.MovementContext.PoseLevel *
                Time.deltaTime;

            H.MainPlayer.MovementContext.PlatformMotion = motionThisFrame;

            float distanceMovedThisFrame = motionThisFrame.magnitude;

            if (distanceMovedThisFrame > 0f)
            {
                _ladderDistanceAccumulator += distanceMovedThisFrame;

                if (_ladderDistanceAccumulator >= _ladderStepDistanceThreshold)
                {
                    _ladderDistanceAccumulator %= _ladderStepDistanceThreshold;
                    Singleton<LadderNoisePacketWarden>.Instance.Send(_currentLader.ladderMaterial);
                }
            }
        }
    }
}