using EFT;
using System;
using UnityEngine;

namespace ifp.arena.bep.Core.MovementStates
{
    public class OldSprintStateModern : OldRunState
    {
        public OldSprintStateModern(MovementContext movementContext) : base(movementContext)
        { }

        public override void Enter(bool isFromSameState)
        {
            base.Enter(isFromSameState);
            this.MovementContext.UpdateStateValues(ref this.StateSensitivity, ref this.RotationSpeedClamp);
            this.MovementContext.SetPoseLevel(1f, true);
            this.MovementContext.SetTilt(0f, false);
            //this.MovementContext.SetPatrol(true);
        }

        public override void Exit(bool toSameState)
        {
            base.Exit(toSameState);
            if (!toSameState)
            {
                this.MovementContext.EnableSprint(false);
                this.MovementContext.PlayerAnimatorEnableSprint(false);
            }
            //this.MovementContext.SetPatrol(false);
            this.MovementContext.ResetSpeedAfterSprint();
            this.bool_2 = false;
        }

        public override void Vaulting()
        {
            this.MovementContext.TryVaulting();
        }

        public override void ManualAnimatorMoveUpdate(float deltaTime)
        {
            if (this.bool_0)
            {
                return;
            }
            if ((Math.Abs(this.vector2_0.y) < 1E-45f || this.bool_2 || !this.MovementContext.CanWalk) && Time.frameCount > this.int_3)
            {
                this.MovementContext.PlayerAnimatorEnableSprint(false);
                if (Mathf.Abs(this.vector2_0.x) < 1E-45f || this.bool_2)
                {
                    this.MovementContext.PlayerAnimatorEnableInert(false);
                }
            }
            else if (this.MovementContext.IsSprintEnabled)
            {
                this.MovementContext.MovementDirection = Vector2.Lerp(this.MovementContext.MovementDirection, this.vector2_0, deltaTime * EFTHardSettings.Instance.DIRECTION_LERP_SPEED);
                this.vector2_0 = Vector2.zero;
                this.int_3 = Time.frameCount;
                base.method_2(this.vector2_0, this.MovementContext.MovementDirection);
                this.MovementContext.SprintAcceleration(deltaTime);
                this.UpdateRotationAndPosition(deltaTime);
            }
            else
            {
                this.MovementContext.PlayerAnimatorEnableSprint(false);
            }
            if (!this.MovementContext.CanSprint)
            {
                this.MovementContext.PlayerAnimatorEnableSprint(false);
            }
        }

        public override void EnableSprint(bool enabled, bool isToggle = false)
        {
            this.MovementContext.EnableSprint(enabled && this.MovementContext.CanSprint);
        }

        public override void SetTilt(float tilt)
        {
        }

        public override void ChangePose(float poseDelta)
        {
            if (poseDelta < 0f)
            {
                this.bool_2 = true;
                this.MovementContext.PlayerAnimatorEnableInert(false);
                this.MovementContext.SetPoseLevel(0f, true);
            }
        }

        public override void ChangeSpeed(float speedDelta)
        {
        }

        private bool bool_2;

        private int int_3;
    }
}