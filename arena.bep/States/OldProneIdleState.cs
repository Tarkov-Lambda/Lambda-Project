using EFT;
using System;
using UnityEngine;

namespace OldTarkovMovement.MovementStates
{
    // Token: 0x02000EE2 RID: 3810
    public class OldProneIdleState : ProneIdleStateClass
    {
        public OldProneIdleState(MovementContext movementContext) : base(movementContext)
        {
            movementContext.IsInPronePose = false;
        }

        public override void Jump()
        {
            this.Prone();
        }

        public override void BlindFire(int b)
        {
            base.BlindFire(0);
        }

        public override void ChangePose(float poseDelta)
        {
            this.Prone();
            if (!this.MovementContext.IsInPronePose)
            {
                this.MovementContext.SetPoseLevel(0f, true);
            }
        }

        public override void SetTilt(float tilt)
        {
            if (!tilt.IsZero() && Math.Abs(this.MovementContext.Tilt - tilt) > 0.001f)
            {
                tilt = (this.MovementContext.CanProneTilt(Math.Sign(tilt)) ? tilt : 0f);
            }
            base.SetTilt(tilt);
        }

        public override void Enter(bool isFromSameState)
        {
            this.bool_2 = true;
            this.BlindFire(0);
            this.MovementContext.SetPOMCollider(PlayerOverlapManager.EExtrusionCollider.Prone);
        }

        public override void Exit(bool toSameState)
        {
            this.bool_2 = false;
            this.MovementContext.SetPOMCollider(PlayerOverlapManager.EExtrusionCollider.Default);
        }

        public override void ManualAnimatorMoveUpdate(float deltaTime)
        {
            this.ProcessAnimatorMovement(deltaTime);
        }

        public override void SetStep(int step)
        {
            this.MovementContext.Step = ((step == 0 || !this.MovementContext.CanRoll(step)) ? 0 : step);
        }

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

        public override void Move(Vector2 direction)
        {
            if (this.MovementContext.CanMoveInProne)
            {
                base.Move(direction);
            }
        }

        private float float_1;

        private bool bool_2;

        private Vector3 vector3_0;
    }
}