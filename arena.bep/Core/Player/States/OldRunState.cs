using System;
using EFT;
using UnityEngine;

namespace ifp.arena.bep.Core.MovementStates
{
    public class OldRunState : RunStateClass
    {
        public OldRunState(MovementContext movementContext) : base(movementContext)
        {
            this.animationCurve_0 = EFTHardSettings.Instance.DIRECTION_CURVE;
        }

        public override void Enter(bool isFromSameState)
        {
            base.Enter(isFromSameState);
            if (this.MovementContext.IsHoldingBreath())
            {
                this.MovementContext.EnableSprint(true);
            }
            this.vector2_0 = this.MovementContext.MovementDirection;
            this.int_0 = 0;
            this.int_1 = 0;
            this.int_2 = 0;
            this.vector2_1 = Vector2.zero;
            this.float_4 = this.MovementContext.TransformRotation.eulerAngles.y;
            this.bool_0 = false;
        }

        public override void Exit(bool toSameState)
        {
            base.Exit(toSameState);
            this.bool_0 = true;
        }

        public override void ManualAnimatorMoveUpdate(float deltaTime)
        {
            if (this.bool_0)
            {
                return;
            }
            this.method_1(deltaTime);
            if (!this.HasNoInputForLongTime() && !(this.MovementContext.InteractionInfo.WorldInteractiveObject != null) && this.MovementContext.CanWalk)
            {
                this.vector2_0 = Vector2.zero;
                this.UpdateRotationAndPosition(deltaTime);
            }
            else
            {
                this.MovementContext.MovementDirection = this.MovementContext.MovementDirection.normalized;
                this.MovementContext.PlayerAnimatorEnableInert(false);
            }
            if (this.MovementContext.MovementDirection.y <= 0.1f)
            {
                return;
            }
            if (this.bool_1)
            {
                this.MovementContext.EnableSprint(true);
                this.bool_1 = false;
            }
            if (this.MovementContext.IsSprintEnabled && this.MovementContext.PoseLevel > 0.9f && this.MovementContext.SmoothedCharacterMovementSpeed >= 1f)
            {
                this.MovementContext.PlayerAnimatorEnableSprint(true);
            }
        }

        public override void Vaulting()
        {
            this.MovementContext.TryVaulting();
        }

        protected new virtual bool HasNoInputForLongTime()
        {
            return this.int_0 > 10 /**EFTHardSettings.Instance.MAX_FRAMES_WITHOUT_INPUT*/ || this.int_0 > this.int_1;
        }

        protected new virtual void UpdateRotationAndPosition(float deltaTime)
        {
            this.method_0(deltaTime);
            this.UpdatePosition(deltaTime);
        }

        private new void method_0(float deltaTime)
        {
            base.UpdateRotationSpeed(deltaTime);
            float f = Mathf.DeltaAngle(this.MovementContext.Yaw, this.float_4);
            float num = Mathf.InverseLerp(10f, 45f, Mathf.Abs(f)) + 1f;
            this.float_4 = Mathf.LerpAngle(this.float_4, this.MovementContext.Yaw, EFTHardSettings.Instance.TRANSFORM_ROTATION_LERP_SPEED * deltaTime * num);
            this.MovementContext.ApplyRotation(Quaternion.AngleAxis(this.float_4, Vector3.up) * this.MovementContext.AnimatorDeltaRotation);
        }

        protected new virtual void UpdatePosition(float deltaTime)
        {
            Vector3 playerAnimatorDeltaPosition = this.MovementContext.PlayerAnimatorDeltaPosition;
            this.MovementContext.ProjectMotionToSurface(ref playerAnimatorDeltaPosition);
            this.ApplyGravity(ref playerAnimatorDeltaPosition, deltaTime);
            this.LimitMotion(ref playerAnimatorDeltaPosition, deltaTime);
            this.MovementContext.ApplyMotion(playerAnimatorDeltaPosition, deltaTime);
            if (!this.MovementContext.IsGrounded)
            {
                this.MovementContext.PlayerAnimatorEnableFallingDown(true);
            }
        }

        private new void method_1(float deltaTime)
        {
            if (Math.Abs(this.vector2_0.y) < 1E-45f && Math.Abs(this.vector2_0.x) < 1E-45f)
            {
                this.int_0++;
                this.int_2 = 0;
                return;
            }
            this.int_1++;
            this.int_2++;
            this.int_0 = 0;
            if (this.vector2_0 != this.vector2_1)
            {
                this.float_0 = 0f;
                this.float_1 = this.animationCurve_0.Evaluate(this.MovementContext.SmoothedCharacterMovementSpeed);
                this.vector2_2 = this.MovementContext.MovementDirection;
                this.vector2_1 = this.vector2_0;
            }
            this.float_0 += deltaTime;
            float t = 1f;
            if (this.float_1 > 0f)
            {
                t = this.float_0 / this.float_1;
            }
            this.MovementContext.MovementDirection = Vector2.Lerp(this.vector2_2, this.vector2_0, t);
            this.method_2(this.vector2_0, this.MovementContext.MovementDirection);
        }

        protected void method_2(Vector2 inputDirection, Vector2 lerpedDirection)
        {
            EMovementDirection discreteDirection = GClass2076.ConvertToMovementDirection(inputDirection);
            this.MovementContext.PlayerAnimatorSetDiscreteDirection(discreteDirection);
        }

        //public override void BlindFire(int b)
        //{
        //    this.MovementContext.SetBlindFire(b);
        //}

        public override void Move(Vector2 direction)
        {
            this.vector2_0 = direction;
        }

        public override void EnableSprint(bool enabled, bool isToggle = false)
        {
            if (!this.MovementContext.CanSprint)
            {
                return;
            }
            if (this.MovementContext.MovementDirection.y > 0.1f)
            {
                this.MovementContext.EnableSprint(enabled);
                if (!this.MovementContext.IsSprintEnabled || !this.MovementContext.SetPoseLevel(1f, false))
                {
                    this.MovementContext.EnableSprint(false);
                    return;
                }
            }
            else if (!isToggle)
            {
                this.bool_1 = enabled;
            }
        }

        public override void Jump()
        {
            if (this.MovementContext.PoseLevel > 0.6f && this.MovementContext.IsGrounded)
            {
                this.MovementContext.TryJump();
                return;
            }
            this.ChangePose(1f - this.MovementContext.PoseLevel);
        }

        public override void ChangePose(float poseDelta)
        {
            this.MovementContext.SetPoseLevel(this.MovementContext.PoseLevel + poseDelta, false);
            if (this.MovementContext.PoseLevel < 0.9f)
            {
                this.MovementContext.EnableSprint(false);
            }
        }

        protected Vector2 vector2_0;

        protected AnimationCurve animationCurve_0;

        protected bool bool_0;

        private Vector2 vector2_1;

        private Vector2 vector2_2;

        private float float_0;

        private float float_1;

        private float float_2;

        private float float_3;

        private int int_0;

        private int int_1;

        private int int_2;

        private float float_4;

        private bool bool_1;

        private const float float_5 = 0.9f;
    }
}