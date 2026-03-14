using System;
using EFT;
using UnityEngine;

namespace ifp.arena.bep.Core.MovementStates
{
    public class OldJumpState : JumpStateClass
    {
        public OldJumpState(MovementContext movementContext) : base(movementContext)
        {
            this.StickToGround = false;
        }

        public override void Enter(bool isFromSameState)
        {
            this.vector2_0 = Vector2.zero;
            this.vector3_2 = Vector3.zero;
            this.float_5 = this.MovementContext.SkillManager.StrengthBuffJumpHeightInc + 1f;
            this.MovementContext.HoldBreath(false);
            this.MovementContext.OnJump();
            this.float_2 = 0f;
            this.int_0 = 0;
            this.bool_0 = (this.MovementContext.PreviousState is OldSprintState);
            this.ejumpState_0 = JumpStateClass.EJumpState.PushingFromTheGround;
            Vector3 inputMotionBeforeLimit = this.MovementContext.InputMotionBeforeLimit;
            Vector3 vector = (this.MovementContext.PreviousState is OldRunState) ? (this.MovementContext.AbsoluteMovementDirection.normalized / 3f) : Vector3.zero;
            inputMotionBeforeLimit = new Vector3((Mathf.Abs(inputMotionBeforeLimit.x) > Mathf.Abs(vector.x)) ? inputMotionBeforeLimit.x : vector.x, 0f, (Mathf.Abs(inputMotionBeforeLimit.z) > Mathf.Abs(vector.z)) ? inputMotionBeforeLimit.z : vector.z);
            this.float_1 = inputMotionBeforeLimit.magnitude;
            this.float_0 = this.float_1;
            this.vector3_0 = ((this.float_1 > 0.1f) ? inputMotionBeforeLimit.normalized : this.MovementContext.TransformForwardVector);
            this.float_8 = this.MovementContext.TransformPosition.y;
            this.float_9 = 0f;
            float num = (!this.MovementContext.PhysicalConditionContainsAny(EPhysicalCondition.LeftLegDamaged | EPhysicalCondition.RightLegDamaged) || this.MovementContext.PhysicalConditionIs(EPhysicalCondition.OnPainkillers)) ? Mathf.Lerp(1f, 0.66f, this.MovementContext.Overweight) : 0.66f;
            this.vector3_1 = EFTHardSettings.Instance.LIFT_VELOCITY_BY_SPEED.Evaluate(this.float_1) * num * Vector3.up;
            this.float_3 = EFTHardSettings.Instance.JUMP_DELAY_BY_SPEED.Evaluate(this.float_1);
            this.MovementContext.PlayerAnimatorEnableLanding(false);
            base.Enter(isFromSameState);
            this.MovementContext.SetTilt(0f, false);
        }

        public override void Exit(bool toSameState)
        {
            base.Exit(toSameState);
            if (this.MovementContext.IsInPronePose)
            {
                this.MovementContext.IsInPronePose = this.MovementContext.CanProne;
            }
            this.MovementContext.SetTilt(0f, false);
            this.MovementContext.OnJumpEnd();
        }

        public override void ManualAnimatorMoveUpdate(float deltaTime)
        {
            this.MovementContext.PlayerAnimatorEnableInert(this.vector2_0.magnitude > 0.1f);
            if (this.ejumpState_0 == JumpStateClass.EJumpState.PushingFromTheGround)
            {
                if (!this.MovementContext.HeadBump(this.vector3_2.y * deltaTime) && this.float_2 <= 2f + this.float_3)
                {
                    if (!this.MovementContext.IsGrounded && this.float_2 > this.float_3)
                    {
                        this.ejumpState_0 = JumpStateClass.EJumpState.Jump;
                    }
                }
                else
                {
                    this.method_0();
                }
            }
            else if (this.ejumpState_0 == JumpStateClass.EJumpState.Jump)
            {
                if (this.float_9 > EFTHardSettings.Instance.JumpTimeDescendingForStateExit && this.MovementContext.IsGrounded)
                {
                    this.MovementContext.PlayerAnimatorEnableJump(false);
                    this.MovementContext.PlayerAnimatorEnableLanding(true);
                    if (this.vector2_0.sqrMagnitude > 0.1f && this.MovementContext.CanWalk)
                    {
                        this.MovementContext.EnableSprint(this.bool_0 && this.vector2_0.y > 0.1f);
                        this.MovementContext.PlayerAnimatorEnableSprint(this.MovementContext.IsSprintEnabled);
                        this.MovementContext.MovementDirection = this.vector2_0;
                        this.MovementContext.PlayerAnimatorEnableInert(true);
                    }
                    else
                    {
                        this.MovementContext.PlayerAnimatorEnableInert(false);
                    }
                }
                if (this.MovementContext.HeadBump(this.vector3_2.y * deltaTime))
                {
                    this.method_0();
                }
                if (this.bool_1)
                {
                    if (Mathf.Abs(this.MovementContext.TransformPosition.y - this.float_7) < 0.0001f)
                    {
                        this.int_0++;
                        base.FreefallTime = deltaTime;
                        if (this.int_0 > 3)
                        {
                            this.method_0();
                        }
                    }
                    else
                    {
                        this.int_0 = 0;
                    }
                }
            }
            if (!this.MovementContext.IsGrounded)
            {
                base.FreefallTime += deltaTime;
                this.float_7 = this.MovementContext.TransformPosition.y;
                this.bool_1 = true;
            }
            else
            {
                base.FreefallTime = deltaTime;
                this.bool_1 = false;
            }
            this.float_2 += deltaTime;
            this.ApplyMovementAndRotation(deltaTime);
        }

        private void method_0()
        {
            this.MovementContext.PlayerAnimatorEnableJump(false);
            this.MovementContext.PlayerAnimatorEnableLanding(true);
            this.ejumpState_0 = JumpStateClass.EJumpState.Bumbped;
        }

        protected virtual void ApplyMovementAndRotation(float deltaTime)
        {
            Quaternion rotation = Quaternion.Lerp(this.MovementContext.TransformRotation, Quaternion.AngleAxis(this.MovementContext.Yaw, Vector3.up), EFTHardSettings.Instance.TRANSFORM_ROTATION_LERP_SPEED * deltaTime);
            this.MovementContext.ApplyRotation(rotation);
            this.method_1(deltaTime);
            this.MovementContext.PlayerAnimatorSetAimAngle(this.MovementContext.Pitch);
        }

        public override void EnableSprint(bool enable, bool isToggle = false)
        {
            this.bool_0 &= (enable && this.MovementContext.CanSprint);
        }

        private void method_1(float deltaTime)
        {
            float d = this.float_2 - this.float_3;
            this.vector3_2 = ((this.float_2 < this.float_3) ? Vector3.zero : (this.vector3_1 * this.float_5 + Physics.gravity * d));
            Vector3 rhs = this.MovementContext.TransformVector(new Vector3(this.vector2_0.x, 0f, this.vector2_0.y).normalized);
            int num = (int)Vector3.Dot(this.vector3_0, rhs);
            if (this.MovementContext.TransformPosition.y < this.float_8)
            {
                this.vector3_2.y = this.vector3_2.y + (this.float_8 - this.MovementContext.TransformPosition.y) / deltaTime * 0.9f;
            }
            float b = this.float_0;
            switch (num)
            {
                case -1:
                    b = this.float_1 * 0.33f;
                    break;
                case 0:
                    b = this.float_1 * 0.8f;
                    break;
                case 1:
                    b = Mathf.Max(EFTHardSettings.Instance.AIR_MIN_SPEED, this.float_1 * 1.3f);
                    break;
            }
            this.float_0 = Mathf.Lerp(this.float_0, b, deltaTime * EFTHardSettings.Instance.AIR_LERP);
            Vector3 vector = (this.vector3_0 * this.float_0 + this.vector3_2) * deltaTime;
            if (this.ejumpState_0 == JumpStateClass.EJumpState.Bumbped)
            {
                vector.y = Mathf.Min(vector.y, 0f);
            }
            float y = this.MovementContext.TransformPosition.y;
            this.float_8 = this.MovementContext.TransformPosition.y + this.vector3_2.y * deltaTime;
            this.LimitMotion(ref vector, deltaTime);
            this.MovementContext.ApplyMotion(vector, deltaTime);
            if (this.MovementContext.TransformPosition.y - y < 0.001f || this.vector3_2.y < 0f)
            {
                this.float_9 += deltaTime;
            }
        }

        public override void Move(Vector2 direction)
        {
            this.vector2_0 = direction;
        }

        public override void SetTilt(float tilt)
        {
        }

        private Vector2 vector2_0;

        protected Vector3 vector3_0;

        private float float_0;

        private float float_1;

        protected Vector3 vector3_1;

        protected bool bool_0;

        private float float_2;

        private float float_3;

        protected JumpStateClass.EJumpState ejumpState_0;

        private float float_4;

        private float float_5 = 1f;

        private int int_0;

        private const float float_6 = 0.1f;

        private const int int_1 = 3;

        private LayerMask layerMask_0;

        private float float_7;

        private bool bool_1;

        private float float_8;

        private float float_9;

        private Vector3 vector3_2;

        protected enum EJumpState
        {
            PushingFromTheGround,
            Jump,
            Bumbped
        }
    }
}
