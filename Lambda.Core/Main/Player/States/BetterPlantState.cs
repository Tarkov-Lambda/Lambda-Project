using EFT;
using UnityEngine;

public class BetterPlantStateClass : PlantStateClass
{
    private readonly GClass777 Gclass777_0;

    public BetterPlantStateClass(MovementContext movementContext) : base(movementContext)
    {
        if (!MovementContext.IsAI)
        {
            Gclass777_0 = new GClass777();
            Gclass777_0.Init(movementContext);
        }
    }

    public override void Enter(bool isFromSameState)
    {
        base.Enter(isFromSameState);

        MovementContext.SetRotationLimit(Player.PlayerMovementConstantsClass.FULL_YAW_RANGE, Player.PlayerMovementConstantsClass.STAND_POSE_ROTATION_PITCH_RANGE);

        GClass777 gclass777_ = Gclass777_0;
        if (gclass777_ == null)
        {
            return;
        }
        gclass777_.Enter();
    }

    public override void SetStep(int step)
    {
        if (Mathf.Abs(step) > 0)
        {
            Vector3 vector = new(MovementContext.TransformForwardVector.z, 0f, -MovementContext.TransformForwardVector.x);
            if (MovementContext.OverlapOrHasNoGround(0.3f, new Vector3?(vector * Mathf.Sign((float)step)), 0.2f, 3f, 0f))
            {
                MovementContext.Step = 0;
                return;
            }
        }
        MovementContext.Step = step;
    }

    public override void ManualAnimatorMoveUpdate(float deltaTime)
    {
        base.ManualAnimatorMoveUpdate(deltaTime);
        Gclass777_0?.ProcessAnimatorStep(deltaTime, Type);
    }

    public override void Cancel()
    {
        // cancelling the freelook doesn't cancel planting
        if (Patch_GamePlayerOwner_TranslateCommand.IsRessetingFreelook) return;

        if (MovementContext.PlantAction == null)
        {
            return;
        }
        MovementContext.PlantAction(false);
        MovementContext.PlantAction = null;
        MovementContext.ExitOverridenState();
    }

    public override void BlendMotion(ref Vector3 motion, float deltaTime)
    {
        motion = Vector3.Lerp(motion, MovementContext.LastBlendMotionDelta * deltaTime, EFTHardSettings.Instance.IdleStateMotionPreservation);
    }
}