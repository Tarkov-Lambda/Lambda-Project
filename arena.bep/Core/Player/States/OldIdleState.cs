using System;
using EFT;
using JetBrains.Annotations;
using UnityEngine;

// Token: 0x02000ECF RID: 3791
namespace ifp.arena.bep.Core.MovementStates
{
    public class OldIdleState : IdleStateClass
    {
        public OldIdleState(MovementContext movementContext) : base(movementContext)
        {
            if (!this.MovementContext.IsAI)
            {
                this.GClass777_0 = new GClass777();
                this.GClass777_0.Init(movementContext);
            }
        }

        private static bool smethod_0(Vector2 direction)
        {
            return direction.x > 1E-05f || direction.y > 1E-05f || direction.x < -1E-05f || direction.y < -1E-05f;
        }

        public override void Exit(bool toSameState)
        {
            base.Exit(toSameState);
            this.MovementContext.HoldBreath(false);
            this.bool_1 = false;
            this.bool_0 = false;
        }

        public override void Enter(bool isFromSameState)
        {
            base.Enter(isFromSameState);
            this.MovementContext.EnableSprint(false);
            this.MovementContext.LeftStanceController.SetAnimatorLeftStanceToCacheFromBodyAction(false);
            GClass777 gclass = this.GClass777_0;
            if (gclass == null)
            {
                return;
            }
            gclass.Enter();
        }

        public override void BlindFire(int b)
        {
            this.MovementContext.SetBlindFire(b);
        }
        public override void BlendMotion(ref Vector3 motion, float deltaTime)
        {
            motion = Vector3.Lerp(motion, this.MovementContext.LastBlendMotionDelta * deltaTime, EFTHardSettings.Instance.IdleStateMotionPreservation);
        }
        public override void Vaulting()
        {
            this.MovementContext.TryVaulting();
        }

        public override void Pickup(bool enabled, [CanBeNull] Action action)
        {
            this.MovementContext.OverrideState(this.MovementContext.PickUpState);
            this.MovementContext.PickupAction = action;
        }

        public override void Plant(bool enabled, bool multitool, float plantTime, Action<bool> action)
        {
            PlantStateClass gclass;
            if ((gclass = (this.MovementContext.PlantState as PlantStateClass)) != null)
            {
                gclass.PlantMultitool = multitool;
                gclass.PlantTime = plantTime;
            }
            this.MovementContext.OverrideState(this.MovementContext.PlantState);
            this.MovementContext.PlantAction = action;
        }

        public override void Examine(bool enabled, [CanBeNull] Action action)
        {
            this.MovementContext.OverrideState(this.MovementContext.PickUpState);
            this.MovementContext.PickupAction = action;
        }

        public override void Move(Vector2 direction)
        {
            if (OldIdleState.smethod_0(direction) && this.MovementContext.CanWalk)
            {
                direction.x = (float)Math.Sign(direction.x);
                direction.y = (float)Math.Sign(direction.y);
                this.MovementContext.MovementDirection = direction;
                this.MovementContext.EnableSprint(this.bool_0 && direction.y > 0.1f);
                if (this.MovementContext.IsSprintEnabled)
                {
                    this.MovementContext.SetPoseLevel(1f, false);
                    if (this.MovementContext.PoseLevel > 0.9f && this.MovementContext.SmoothedCharacterMovementSpeed >= 1f)
                    {
                        this.MovementContext.PlayerAnimatorEnableSprint(true);
                    }
                }
                this.MovementContext.PlayerAnimatorEnableInert(true);
            }
            else
            {
                this.MovementContext.MovementDirection = Vector2.zero;
            }
        }

        public override void ManualAnimatorMoveUpdate(float deltaTime)
        {
            base.ManualAnimatorMoveUpdate(deltaTime);
            return;
            // this.ProcessUpperbodyRotation(deltaTime);
            // this.NoJitteryRotation(deltaTime);
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

        public override void EnableSprint(bool enable, bool isToggle = false)
        {
            if (!isToggle)
            {
                this.bool_0 = (enable && this.MovementContext.CanSprint);
            }
        }

        public override void EnableBreath(bool enable)
        {
            this.MovementContext.HoldBreath(enable);
        }

        public override void Kick()
        {
            this.MovementContext.PlayerAnimatorEnableKick(true);
        }

        public override void SetStep(int step)
        {
            if (Mathf.Abs(step) > 0)
            {
                Vector3 a = new Vector3(this.MovementContext.TransformForwardVector.z, 0f, -this.MovementContext.TransformForwardVector.x);
                if (this.MovementContext.OverlapOrHasNoGround(0.3f, new Vector3?(a * Mathf.Sign((float)step)), 0.2f, 3f, 0f))
                {
                    this.MovementContext.Step = 0;
                    return;
                }
            }
            this.MovementContext.Step = step;
        }

        public void NoJitteryRotation(float deltaTime)
        {
            if (this.MovementContext.IsInMountedState)
            {
                return;
            }
            base.UpdateRotationSpeed(deltaTime);

            Quaternion targetRotation = Quaternion.AngleAxis(this.MovementContext.Yaw, Vector3.up) * this.MovementContext.AnimatorDeltaRotation;
            Vector3 eulerRotation = targetRotation.eulerAngles;

            // I was getting motion sick because of this motherfucker
            eulerRotation.z = 0;

            // Apply corrected rotation
            this.MovementContext.ApplyRotation(Quaternion.Euler(eulerRotation));
        }


        public void method_0(float deltaTime)
        {
            GClass777 gclass777_ = this.Gclass777_0;
            if (gclass777_ == null)
            {
                return;
            }
            gclass777_.ProcessAnimatorStep(deltaTime, this.Type);
        }

        private bool bool_0;

        private bool bool_1;

        private const float float_0 = 1E-05f;

        private GClass777 GClass777_0;
    }
}
