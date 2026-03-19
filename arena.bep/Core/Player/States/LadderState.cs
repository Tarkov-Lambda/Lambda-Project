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
            if (wasOriginallyGrounded && !this.MovementContext.IsGrounded)
            {
                wasOriginallyGrounded = false;
            }

            this.MovementContext.ResetFlying();
            // Move up or down along the ladder's Y axis based on W/S input
            Vector3 motion = Vector3.up * _inputDirection.y * ClimbSpeed * deltaTime;
            this.MovementContext.ApplyMotion(motion, deltaTime);

            // Exit at the top or bottom of the ladder bounds
            Vector3 playerPos = _player.PlayerBody.transform.position;

            if (playerPos.y >= _ladder.TopPoint.y)
            {
                // Reached the top — nudge the player forward off the ladder
                _player.Teleport(playerPos + this.MovementContext.TransformForwardVector * 0.75f);
                ExitToIdle();
                return;
            }

            if (playerPos.y <= _ladder.BottomPoint.y || wasOriginallyGrounded)
            {
                ExitToIdle();
                return;
            }
        }

        public override void Jump()
        {
            // 1. Get the direction the camera is currently looking
            Vector3 lookDir = this.MovementContext.LookDirection;

            // 2. Flatten the direction (JumpStateClass forces Y to 0 anyway for horizontal momentum)
            Vector3 flatLookDir = new Vector3(lookDir.x, 0f, lookDir.z);

            // Fallback: If the player is looking absolutely perfectly straight up or down, 
            // we push them straight backward off the ladder instead so the vector doesn't break.
            if (flatLookDir.sqrMagnitude < 0.01f)
            {
                flatLookDir = -this.MovementContext.TransformForwardVector;
            }

            flatLookDir = flatLookDir.normalized;

            // 3. Define the strength of the leap (Tarkov max sprint speed is typically around 1.0f - 1.5f)
            float leapSpeed = 1.5f;

            // 4. Spoof the pre-jump momentum so JumpStateClass thinks we were sprinting in the look direction
            this.MovementContext.InputMotionBeforeLimit = flatLookDir * leapSpeed;

            // 5. Spoof the character's movement speed so the jump distance multiplier scales up
            this.MovementContext.SmoothedCharacterMovementSpeed = leapSpeed;
            this.MovementContext.CharacterMovementSpeed = leapSpeed;

            // 6. Rotate the player's body towards the leap direction so the jump animation faces the right way
            float yaw = Mathf.Atan2(flatLookDir.x, flatLookDir.z) * Mathf.Rad2Deg;
            this.MovementContext.Rotation = new Vector2(yaw, this.MovementContext.Pitch);

            // 7. Clean up your ladder logic here (e.g., reset FallSafeHeight if you modified it, re-enable collision, etc.)
            this.MovementContext.WeightRelatedValuesUpdated();

            // 8. Tell the animator to transition to the JumpStateClass
            // this.MovementContext.TryJump();

            this.MovementContext.method_2();
            this.MovementContext.PlayerAnimatorEnableJump(enabled: true);
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
            this.MovementContext.OverrideState(new JumpStateClass(this.MovementContext));
        }
    }
}
