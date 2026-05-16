using System;
using EFT;
using UnityEngine;

public class BetterPlantStateClass(MovementContext movementContext) : PlantStateClass(movementContext)
{
    public override void Enter(bool isFromSameState)
    {
        base.Enter(isFromSameState);

        // override the rotation limit set by the base class
        this.MovementContext.SetRotationLimit(Player.PlayerMovementConstantsClass.FULL_YAW_RANGE, Player.PlayerMovementConstantsClass.STAND_POSE_ROTATION_PITCH_RANGE);

        this.Float_1 = this.MovementContext.PoseLevel;
        this.MovementContext.SetTilt(0f, false);
        if (!this.PlantMultitool)
        {
            this.MovementContext.SetPoseLevel(0f, false);
        }

        this.MovementContext.SetPatrol(true);
        this.Float_0 = Time.realtimeSinceStartup;
    }

    public override void Exit(bool toSameState)
    {
        base.Exit(toSameState);

        if (!this.PlantMultitool)
        {
            this.MovementContext.SetPoseLevel(this.Float_1, false);
        }
        this.MovementContext.SetPatrol(false);
    }

    public override void ManualAnimatorMoveUpdate(float deltaTime)
    {
        base.ManualAnimatorMoveUpdate(deltaTime);
        if (Time.realtimeSinceStartup - this.Float_0 <= this.PlantTime)
        {
            return;
        }
        if (this.MovementContext.PlantAction != null)
        {
            this.MovementContext.PlantAction(true);
            this.MovementContext.PlantAction = null;
        }
        this.MovementContext.ExitOverridenState();
    }

    public override void ChangePose(float poseDelta) { }

    public override void SetTilt(float tilt) { }

    public override void Pickup(bool enabled, Action action) { }

    public override void Examine(bool enabled, Action action) { }

    public override void Plant(bool enabled, bool multitool, float plantTime, Action<bool> action)
    {
        this.MovementContext.ExitOverridenState();
    }

    public override void Cancel()
    {
        // cancelling the freelook doesn't cancel planting
        if (Patch_GamePlayerOwner_TranslateCommand.IsRessetingFreelook) return;

        if (this.MovementContext.PlantAction == null)
        {
            return;
        }
        this.MovementContext.PlantAction(false);
        this.MovementContext.PlantAction = null;
        this.MovementContext.ExitOverridenState();
    }
}
