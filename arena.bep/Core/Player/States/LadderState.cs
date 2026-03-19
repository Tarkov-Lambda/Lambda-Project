using EFT;
using HarmonyLib;
using ifp.arena.shared;
using UnityEngine;

namespace ifp.arena.bep.Core.MovementStates
{
    public class LadderState : RunStateClass
    {
        private Ladder _ladder;
        private Vector2 _inputDirection;
        private Player _player;


        private bool wasOriginallyGrounded;

        private const float ClimbSpeed = 2.5f;

        public LadderState(MovementContext movementContext, Ladder ladder) : base(movementContext)
        {
            _ladder = ladder;
        }

        public override void Enter(bool isFromSameState)
        {
            wasOriginallyGrounded = this.MovementContext.IsGrounded;
            _player = AccessTools.Field(this.MovementContext.GetType(), "_player").GetValue(this.MovementContext) as Player;
            base.Enter(isFromSameState);

            _inputDirection = Vector2.zero;
            this.MovementContext.EnableSprint(false);

            // Snap player horizontally to the ladder center
            // Player player = AccessTools.Field(this.MovementContext.GetType(), "_player").GetValue(this.MovementContext) as Player;
            // if (player != null)
            // {
            //     Vector3 ladderCenter = _ladder.transform.position;
            //     Vector3 current = player.Transform.position;
            //     player.Teleport(new Vector3(ladderCenter.x, current.y, ladderCenter.z));
            // }
        }

        public override void Exit(bool toSameState)
        {
            base.Exit(toSameState);
            _inputDirection = Vector2.zero;
        }

        public override void Move(Vector2 direction)
        {
            _inputDirection = direction;
        }

        public override void ManualAnimatorMoveUpdate(float deltaTime)
        {
            this.MovementContext.ResetFlying();
            // Move up or down along the ladder's Y axis based on W/S input
            Vector3 motion = Vector3.up * _inputDirection.y * ClimbSpeed * deltaTime;
            this.MovementContext.ApplyMotion(motion, deltaTime);

            // Exit at the top or bottom of the ladder bounds
            float playerY = this.MovementContext.TransformPosition.y;

            if (playerY >= _ladder.TopPoint.y)
            {
                // Reached the top — nudge the player forward off the ladder
                Player player = AccessTools.Field(this.MovementContext.GetType(), "_player").GetValue(this.MovementContext) as Player;
                if (player != null)
                {
                    // Vector3 playerAnimatorDeltaPosition = this.MovementContext.TransformPosition + this.MovementContext.TransformForwardVector * 0.75f;
                    // this.ApplyMotion(ref playerAnimatorDeltaPosition, deltaTime);
                    // this.MovementContext.PlayerAnimatorEnableFallingDown(false);
                    player.Teleport(this.MovementContext.TransformPosition + this.MovementContext.TransformForwardVector * 0.75f);
                }
                ExitToIdle();
                return;
            }

            if (playerY <= _ladder.BottomPoint.y)
            {
                ExitToIdle();
                return;
            }
        }

        public override void Jump()
        {
            // Jump dismounts the ladder
            ExitToIdle();
        }

        public override void EnableSprint(bool enabled, bool isToggle = false)
        {
            // No sprinting on a ladder
        }

        public override void ChangePose(float poseDelta)
        {
            // No pose changes on a ladder
        }

        private void ExitToIdle()
        {
            this.MovementContext.OverrideState(new OldIdleState(this.MovementContext));
        }
    }
}
