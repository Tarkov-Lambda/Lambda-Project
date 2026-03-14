using System;
using EFT;
using UnityEngine;

namespace OldTarkovMovement.MovementStates
{
    // Token: 0x0200050F RID: 1295
    public class OldSidestepState : SideStepStateClass
    {
        public OldSidestepState(MovementContext movementContext) : base(movementContext)
        { }
        // Token: 0x0600210D RID: 8461 RVA: 0x0019DD68 File Offset: 0x0019BF68
        public override void Enter(bool isFromSameState)
        {
            base.Enter(isFromSameState);
            if (!isFromSameState)
            {
                float num = this.MovementContext.Yaw - this.MovementContext.HandsToBodyAngle;
                Vector2 yawLimit = new Vector2(num - this.MovementContext.TrunkRotationLimit + 1f, num + this.MovementContext.TrunkRotationLimit - 1f);
                this.MovementContext.SetRotationLimit(yawLimit, Player.PlayerMovementConstantsClass.STAND_POSE_ROTATION_PITCH_RANGE);
            }
            this.MovementContext.SetTilt(0f, false);
        }

        // Token: 0x0600210E RID: 8462 RVA: 0x000B4A10 File Offset: 0x000B2C10
        public override void Exit(bool toSameState)
        {
            if (!toSameState)
            {
                this.MovementContext.SetRotationLimit(Player.PlayerMovementConstantsClass.FULL_YAW_RANGE, Player.PlayerMovementConstantsClass.STAND_POSE_ROTATION_PITCH_RANGE);
                this.SetStep(0);
            }
            base.Exit(toSameState);
        }

        // Token: 0x0600210F RID: 8463 RVA: 0x000A295B File Offset: 0x000A0B5B
        public override void SetTilt(float tilt)
        {
        }

        // Token: 0x06002110 RID: 8464 RVA: 0x000B4A38 File Offset: 0x000B2C38
        public override void Jump()
        {
            this.SetStep(0);
        }
    }
}