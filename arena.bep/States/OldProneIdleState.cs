using EFT;
using System;
using UnityEngine;

namespace OldTarkovMovement.MovementStates
{
    // Token: 0x02000EE2 RID: 3810
    public class OldProneIdleState : ProneIdleStateClass
    {
        // Token: 0x06005BE8 RID: 23528 RVA: 0x000D9F49 File Offset: 0x000D8149
        public OldProneIdleState(MovementContext movementContext) : base(movementContext)
        {
            movementContext.IsInPronePose = false;
        }

        // Token: 0x06005BE9 RID: 23529 RVA: 0x000D9F57 File Offset: 0x000D8157
        public override void Jump()
        {
            this.Prone();
        }

        // Token: 0x06005BEA RID: 23530 RVA: 0x000D9FB1 File Offset: 0x000D81B1
        public override void BlindFire(int b)
        {
            base.BlindFire(0);
        }

        // Token: 0x06005BEB RID: 23531 RVA: 0x000D9FBA File Offset: 0x000D81BA
        public override void ChangePose(float poseDelta)
        {
            this.Prone();
            if (!this.MovementContext.IsInPronePose)
            {
                this.MovementContext.SetPoseLevel(0f, true);
            }
        }

        // Token: 0x06005BEC RID: 23532 RVA: 0x00286618 File Offset: 0x00284818
        public override void SetTilt(float tilt)
        {
            if (!tilt.IsZero() && Math.Abs(this.MovementContext.Tilt - tilt) > 0.001f)
            {
                tilt = (this.MovementContext.CanProneTilt(Math.Sign(tilt)) ? tilt : 0f);
            }
            base.SetTilt(tilt);
        }

        // Token: 0x06005BED RID: 23533 RVA: 0x000D9FE1 File Offset: 0x000D81E1
        public override void Enter(bool isFromSameState)
        {
            this.bool_2 = true;
            this.BlindFire(0);
            this.MovementContext.SetPOMCollider(PlayerOverlapManager.EExtrusionCollider.Prone);
        }

        // Token: 0x06005BEE RID: 23534 RVA: 0x000D9FFD File Offset: 0x000D81FD
        public override void Exit(bool toSameState)
        {
            this.bool_2 = false;
            this.MovementContext.SetPOMCollider(PlayerOverlapManager.EExtrusionCollider.Default);
        }

        // Token: 0x06005BEF RID: 23535 RVA: 0x000DA012 File Offset: 0x000D8212
        public override void ManualAnimatorMoveUpdate(float deltaTime)
        {
            this.ProcessAnimatorMovement(deltaTime);
        }

        // Token: 0x06005BF0 RID: 23536 RVA: 0x000DA01B File Offset: 0x000D821B
        public override void SetStep(int step)
        {
            this.MovementContext.Step = ((step == 0 || !this.MovementContext.CanRoll(step)) ? 0 : step);
        }

        // Token: 0x06005BF1 RID: 23537 RVA: 0x0028666C File Offset: 0x0028486C
        public override void ProcessAnimatorMovement(float deltaTime)
        {
            Vector3 playerAnimatorDeltaPosition = this.MovementContext.PlayerAnimatorDeltaPosition;
            this.ApplyGravity(ref playerAnimatorDeltaPosition, deltaTime);
            bool flag = false;
            if (Math.Abs(this.MovementContext.Yaw - this.MovementContext.PreviousYaw) > 1E-45f)
            {
                Quaternion animatorDeltaRotation = this.MovementContext.AnimatorDeltaRotation;
                Quaternion quaternion = animatorDeltaRotation * animatorDeltaRotation * animatorDeltaRotation;
                if (flag = (this.MovementContext.RotationOverlapPrediction(playerAnimatorDeltaPosition, quaternion, this.MovementContext.PlayerTransform.Original).sqrMagnitude < 1E-06f))
                {
                    Vector3 vector = this.MovementContext.TransformRotation * quaternion * Vector3.forward;
                    Vector3 normalized = Vector3.Cross(new Vector3(vector.z, 0f, -vector.x), this.MovementContext.SurfaceNormal).normalized;
                    if (flag &= this.MovementContext.HasGround(0.55f, new Vector3?(normalized), 0.15f))
                    {
                        flag &= this.MovementContext.HasGround(0.75f, new Vector3?(-normalized), 0.15f);
                    }
                }
            }
            this.MovementContext.LimitMotionXZ(ref playerAnimatorDeltaPosition, deltaTime, 0.0009f);
            this.vector3_0 = this.MovementContext.TransformPosition + playerAnimatorDeltaPosition;
            this.MovementContext.ApplyMotion(playerAnimatorDeltaPosition, deltaTime);
            this.MovementContext.UpdateDeltaAngle();
            if (!flag)
            {
                this.MovementContext.RotateFail(ECantRotate.NotGround);
            }
            if (flag && Mathf.Abs(this.MovementContext.HandsToBodyAngle) > this.MovementContext.TrunkRotationLimit)
            {
                this.ProcessUpperbodyRotation(deltaTime, this.MovementContext.IsAI);
            }
            if (this.bool_2)
            {
                this.MovementContext.SetYawLimit(new Vector2(this.MovementContext.Rotation.x - this.MovementContext.HandsToBodyAngle - 35f, this.MovementContext.Rotation.x - this.MovementContext.HandsToBodyAngle + 35f));
            }
            this.MovementContext.AlignToSurface(deltaTime, null);
        }

        // Token: 0x06005BF2 RID: 23538 RVA: 0x000DA03D File Offset: 0x000D823D
        public override void Move(Vector2 direction)
        {
            if (this.MovementContext.CanMoveInProne)
            {
                base.Move(direction);
            }
        }

        // Token: 0x040057A3 RID: 22435
        private float float_1;

        // Token: 0x040057A4 RID: 22436
        private bool bool_2;

        // Token: 0x040057A5 RID: 22437
        private Vector3 vector3_0;
    }
}