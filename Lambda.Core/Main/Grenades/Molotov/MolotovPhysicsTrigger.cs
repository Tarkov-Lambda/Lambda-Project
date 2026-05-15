using EFT.Interactive;

namespace Lambda.Core.Main;

public class MolotovPhysicsTrigger : DamageTrigger, IPhysicsTrigger, IPhysicsTriggerWithStay
{
    public override string Description { get; } = "FlameDamageTrigger";

    public override bool IsStatic
    {
        get
        {
            return true;
        }
    }

    public override void ProceedDamage(IPlayerOwner player, BodyPartCollider bodyPart)
    {
        bodyPart.ProceedFlame();
    }

    public override void AddPenalty(IPlayerOwner player)
    {
    }

    public override void RemovePenalty(IPlayerOwner player)
    {
    }

    public override void PlaySound(bool useOcclusion = false)
    {
    }

    private readonly string string_1;
}