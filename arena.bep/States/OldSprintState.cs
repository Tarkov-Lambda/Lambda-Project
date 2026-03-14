using EFT;
using System;
using UnityEngine;

namespace OldTarkovMovement.MovementStates
{
    // Token: 0x02000EE9 RID: 3817
    public class OldSprintState : OldRunState
    {
        public OldSprintState(MovementContext movementContext) : base(movementContext)
        { }

        // Token: 0x06005C15 RID: 23573 RVA: 0x00286E60 File Offset: 0x00285060
        public override void Enter(bool isFromSameState)
        {
            base.Enter(isFromSameState);
            this.MovementContext.UpdateStateValues(ref this.StateSensitivity, ref this.RotationSpeedClamp);
            this.MovementContext.SetPoseLevel(1f, true);
            this.MovementContext.SetTilt(0f, false);
            this.MovementContext.SetPatrol(true);
        }

        // Token: 0x06005C16 RID: 23574 RVA: 0x00286EBC File Offset: 0x002850BC
        public override void Exit(bool toSameState)
        {
            base.Exit(toSameState);
            if (!toSameState)
            {
                this.MovementContext.EnableSprint(false);
                this.MovementContext.PlayerAnimatorEnableSprint(false);
            }
            this.MovementContext.SetPatrol(false);
            this.MovementContext.ResetSpeedAfterSprint();
            this.bool_2 = false;
        }

        public override void Vaulting()
        {
            this.MovementContext.TryVaulting();
        }

        // Token: 0x06005C17 RID: 23575 RVA: 0x00286F0C File Offset: 0x0028510C
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

        // Token: 0x06005C18 RID: 23576 RVA: 0x000DA1F5 File Offset: 0x000D83F5
        public override void EnableSprint(bool enabled, bool isToggle = false)
        {
            this.MovementContext.EnableSprint(enabled && this.MovementContext.CanSprint);
        }

        // Token: 0x06005C19 RID: 23577 RVA: 0x000A295B File Offset: 0x000A0B5B
        public override void SetTilt(float tilt)
        {
        }

        // Token: 0x06005C1A RID: 23578 RVA: 0x000DA213 File Offset: 0x000D8413
        public override void ChangePose(float poseDelta)
        {
            if (poseDelta < 0f)
            {
                this.bool_2 = true;
                this.MovementContext.PlayerAnimatorEnableInert(false);
                this.MovementContext.SetPoseLevel(0f, true);
            }
        }

        // Token: 0x06005C1B RID: 23579 RVA: 0x000A295B File Offset: 0x000A0B5B
        public override void ChangeSpeed(float speedDelta)
        {
        }

        // Token: 0x040057B9 RID: 22457
        private bool bool_2;

        // Token: 0x040057BA RID: 22458
        private int int_3;
    }
}