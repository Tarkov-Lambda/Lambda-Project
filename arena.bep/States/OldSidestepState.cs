using System;
using EFT;
using UnityEngine;

namespace OldTarkovMovement.MovementStates
{
    public class OldSidestepState : SideStepStateClass
    {
        public OldSidestepState(MovementContext movementContext) : base(movementContext)
        { }
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

        public override void Exit(bool toSameState)
        {
            if (!toSameState)
            {
                this.MovementContext.SetRotationLimit(Player.PlayerMovementConstantsClass.FULL_YAW_RANGE, Player.PlayerMovementConstantsClass.STAND_POSE_ROTATION_PITCH_RANGE);
                this.SetStep(0);
            }
            base.Exit(toSameState);
        }

        public override void SetTilt(float tilt)
        {
        }

        public override void Jump()
        {
            this.SetStep(0);
        }
    }
}