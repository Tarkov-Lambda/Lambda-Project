using System;
using EFT;
using UnityEngine;

namespace OldTarkovMovement.MovementStates
{
    // Token: 0x02000EDC RID: 3804
    public class OldRunState : RunStateClass
    {
        // Token: 0x06005B94 RID: 23444 RVA: 0x000D9AC6 File Offset: 0x000D7CC6
        public OldRunState(MovementContext movementContext) : base(movementContext)
        {
            this.animationCurve_0 = EFTHardSettings.Instance.DIRECTION_CURVE;
        }

        // Token: 0x06005B95 RID: 23445 RVA: 0x002858F4 File Offset: 0x00283AF4
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

        // Token: 0x06005B96 RID: 23446 RVA: 0x000D9ADF File Offset: 0x000D7CDF
        public override void Exit(bool toSameState)
        {
            base.Exit(toSameState);
            this.bool_0 = true;
        }

        // Token: 0x06005B97 RID: 23447 RVA: 0x00285978 File Offset: 0x00283B78
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

        // Token: 0x06005B98 RID: 23448 RVA: 0x000D9AEF File Offset: 0x000D7CEF
        protected virtual bool HasNoInputForLongTime()
        {
            return this.int_0 > 10 /**EFTHardSettings.Instance.MAX_FRAMES_WITHOUT_INPUT*/ || this.int_0 > this.int_1;
        }

        // Token: 0x06005B99 RID: 23449 RVA: 0x000D9B13 File Offset: 0x000D7D13
        protected virtual void UpdateRotationAndPosition(float deltaTime)
        {
            this.method_0(deltaTime);
            this.UpdatePosition(deltaTime);
        }

        // Token: 0x06005B9A RID: 23450 RVA: 0x00285A70 File Offset: 0x00283C70
        private void method_0(float deltaTime)
        {
            base.UpdateRotationSpeed(deltaTime);
            float f = Mathf.DeltaAngle(this.MovementContext.Yaw, this.float_4);
            float num = Mathf.InverseLerp(10f, 45f, Mathf.Abs(f)) + 1f;
            this.float_4 = Mathf.LerpAngle(this.float_4, this.MovementContext.Yaw, EFTHardSettings.Instance.TRANSFORM_ROTATION_LERP_SPEED * deltaTime * num);
            this.MovementContext.ApplyRotation(Quaternion.AngleAxis(this.float_4, Vector3.up) * this.MovementContext.AnimatorDeltaRotation);
        }

        // Token: 0x06005B9B RID: 23451 RVA: 0x00285B0C File Offset: 0x00283D0C
        protected virtual void UpdatePosition(float deltaTime)
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

        // Token: 0x06005B9C RID: 23452 RVA: 0x00285B6C File Offset: 0x00283D6C
        private void method_1(float deltaTime)
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

        // Token: 0x06005B9D RID: 23453 RVA: 0x00285C9C File Offset: 0x00283E9C
        protected void method_2(Vector2 inputDirection, Vector2 lerpedDirection)
        {
            EMovementDirection discreteDirection = GClass2076.ConvertToMovementDirection(inputDirection);
            this.MovementContext.PlayerAnimatorSetDiscreteDirection(discreteDirection);
        }

        // Token: 0x06005B9E RID: 23454 RVA: 0x000D881F File Offset: 0x000D6A1F
        //public override void BlindFire(int b)
        //{
        //    this.MovementContext.SetBlindFire(b);
        //}

        // Token: 0x06005B9F RID: 23455 RVA: 0x000D9B23 File Offset: 0x000D7D23
        public override void Move(Vector2 direction)
        {
            this.vector2_0 = direction;
        }

        // Token: 0x06005BA0 RID: 23456 RVA: 0x00285CBC File Offset: 0x00283EBC
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

        // Token: 0x06005BA1 RID: 23457 RVA: 0x00280EAC File Offset: 0x0027F0AC
        public override void Jump()
        {
            if (this.MovementContext.PoseLevel > 0.6f && this.MovementContext.IsGrounded)
            {
                this.MovementContext.TryJump();
                return;
            }
            this.ChangePose(1f - this.MovementContext.PoseLevel);
        }

        // Token: 0x06005BA2 RID: 23458 RVA: 0x000D9B2C File Offset: 0x000D7D2C
        public override void ChangePose(float poseDelta)
        {
            this.MovementContext.SetPoseLevel(this.MovementContext.PoseLevel + poseDelta, false);
            if (this.MovementContext.PoseLevel < 0.9f)
            {
                this.MovementContext.EnableSprint(false);
            }
        }

        // Token: 0x04005750 RID: 22352
        protected Vector2 vector2_0;

        // Token: 0x04005751 RID: 22353
        protected AnimationCurve animationCurve_0;

        // Token: 0x04005752 RID: 22354
        protected bool bool_0;

        // Token: 0x04005753 RID: 22355
        private Vector2 vector2_1;

        // Token: 0x04005754 RID: 22356
        private Vector2 vector2_2;

        // Token: 0x04005755 RID: 22357
        private float float_0;

        // Token: 0x04005756 RID: 22358
        private float float_1;

        // Token: 0x04005757 RID: 22359
        private float float_2;

        // Token: 0x04005758 RID: 22360
        private float float_3;

        // Token: 0x04005759 RID: 22361
        private int int_0;

        // Token: 0x0400575A RID: 22362
        private int int_1;

        // Token: 0x0400575B RID: 22363
        private int int_2;

        // Token: 0x0400575C RID: 22364
        private float float_4;

        // Token: 0x0400575D RID: 22365
        private bool bool_1;

        // Token: 0x0400575E RID: 22366
        private const float float_5 = 0.9f;
    }
}