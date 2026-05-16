using System;
using EFT;
using UnityEngine;

namespace Lambda.Core.Main.MovementStates;

public class UnstaggeredRunState : RunStateClass
{
    public UnstaggeredRunState(MovementContext movementContext) : base(movementContext)
    {
        animationCurve_0 = EFTHardSettings.Instance.DIRECTION_CURVE;
    }

    public override void Enter(bool isFromSameState)
    {
        base.Enter(isFromSameState);
        if (MovementContext.IsHoldingBreath())
        {
            MovementContext.EnableSprint(true);
        }
        vector2_0 = MovementContext.MovementDirection;
        int_0 = 0;
        int_1 = 0;
        int_2 = 0;
        vector2_1 = Vector2.zero;
        float_4 = MovementContext.TransformRotation.eulerAngles.y;
        bool_0 = false;
    }

    public override void Exit(bool toSameState)
    {
        base.Exit(toSameState);
        bool_0 = true;
    }

    public override void ManualAnimatorMoveUpdate(float deltaTime)
    {
        if (bool_0)
        {
            return;
        }
        method_1(deltaTime);
        if (!HasNoInputForLongTime() && !(MovementContext.InteractionInfo.WorldInteractiveObject != null) && MovementContext.CanWalk)
        {
            vector2_0 = Vector2.zero;
            UpdateRotationAndPosition(deltaTime);
        }
        else
        {
            MovementContext.MovementDirection = MovementContext.MovementDirection.normalized;
            MovementContext.PlayerAnimatorEnableInert(false);
        }
        if (MovementContext.MovementDirection.y <= 0.1f)
        {
            return;
        }
        if (bool_1)
        {
            MovementContext.EnableSprint(true);
            bool_1 = false;
        }
        if (MovementContext.IsSprintEnabled && MovementContext.PoseLevel > 0.9f && MovementContext.SmoothedCharacterMovementSpeed >= 1f)
        {
            MovementContext.PlayerAnimatorEnableSprint(true);
        }
    }

    public override void Vaulting()
    {
        MovementContext.TryVaulting();
    }

    protected new virtual bool HasNoInputForLongTime()
    {
        return int_0 > 10 /**EFTHardSettings.Instance.MAX_FRAMES_WITHOUT_INPUT*/ || int_0 > int_1;
    }

    protected new virtual void UpdateRotationAndPosition(float deltaTime)
    {
        method_0(deltaTime);
        UpdatePosition(deltaTime);
    }

    private new void method_0(float deltaTime)
    {
        base.UpdateRotationSpeed(deltaTime);
        float f = Mathf.DeltaAngle(MovementContext.Yaw, float_4);
        float num = Mathf.InverseLerp(10f, 45f, Mathf.Abs(f)) + 1f;
        float_4 = Mathf.LerpAngle(float_4, MovementContext.Yaw, EFTHardSettings.Instance.TRANSFORM_ROTATION_LERP_SPEED * deltaTime * num);
        MovementContext.ApplyRotation(Quaternion.AngleAxis(float_4, Vector3.up) * MovementContext.AnimatorDeltaRotation);
    }

    protected new virtual void UpdatePosition(float deltaTime)
    {
        Vector3 playerAnimatorDeltaPosition = MovementContext.PlayerAnimatorDeltaPosition;
        MovementContext.ProjectMotionToSurface(ref playerAnimatorDeltaPosition);
        ApplyGravity(ref playerAnimatorDeltaPosition, deltaTime);
        LimitMotion(ref playerAnimatorDeltaPosition, deltaTime);
        MovementContext.ApplyMotion(playerAnimatorDeltaPosition, deltaTime);
        if (!MovementContext.IsGrounded)
        {
            MovementContext.PlayerAnimatorEnableFallingDown(true);
        }
    }

    private new void method_1(float deltaTime)
    {
        if (Math.Abs(vector2_0.y) < 1E-45f && Math.Abs(vector2_0.x) < 1E-45f)
        {
            int_0++;
            int_2 = 0;
            return;
        }
        int_1++;
        int_2++;
        int_0 = 0;

        if (vector2_0 != vector2_1)
        {
            float_0 = 0f;
            float_1 = animationCurve_0.Evaluate(MovementContext.SmoothedCharacterMovementSpeed);

            // Enforce a minimum transition time so the swap is never instantaneous.
            float_1 = Mathf.Max(float_1, DirectionSwapMinTime);

            vector2_2 = MovementContext.MovementDirection;
            vector2_1 = vector2_0;
        }

        float_0 += deltaTime;
        float t = 1f;
        if (float_1 > 0f)
        {
            t = Mathf.Clamp01(float_0 / float_1);
        }

        float curvedT = DirectionSwapCurve.Evaluate(t);

        MovementContext.MovementDirection = Vector2.Lerp(vector2_2, vector2_0, curvedT);

        method_2(vector2_0, MovementContext.MovementDirection);
    }

    protected void method_2(Vector2 inputDirection, Vector2 lerpedDirection)
    {
        EMovementDirection discreteDirection = GClass2076.ConvertToMovementDirection(inputDirection);
        MovementContext.PlayerAnimatorSetDiscreteDirection(discreteDirection);
    }

    public override void Move(Vector2 direction)
    {
        vector2_0 = direction;
    }

    public override void EnableSprint(bool enabled, bool isToggle = false)
    {
        if (!MovementContext.CanSprint)
        {
            return;
        }
        if (MovementContext.MovementDirection.y > 0.1f)
        {
            MovementContext.EnableSprint(enabled);
            if (!MovementContext.IsSprintEnabled || !MovementContext.SetPoseLevel(1f, false))
            {
                MovementContext.EnableSprint(false);
                return;
            }
        }
        else if (!isToggle)
        {
            bool_1 = enabled;
        }
    }

    public override void Jump()
    {
        if (MovementContext.PoseLevel > 0.6f && MovementContext.IsGrounded)
        {
            MovementContext.TryJump();
            return;
        }
        ChangePose(1f - MovementContext.PoseLevel);
    }

    public override void ChangePose(float poseDelta)
    {
        MovementContext.SetPoseLevel(MovementContext.PoseLevel + poseDelta, false);
        if (MovementContext.PoseLevel < 0.9f)
        {
            MovementContext.EnableSprint(false);
        }
    }

    protected Vector2 vector2_0;

    protected AnimationCurve animationCurve_0;

    public AnimationCurve DirectionSwapCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float DirectionSwapMinTime = 0.25f;

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
