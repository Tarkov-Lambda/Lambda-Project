using System.Collections.Generic;
using EFT.Interactive;
using UnityEngine;

namespace Lambda.Core.Main;

public class MolotovPhysicsTrigger : DamageTrigger, IPhysicsTrigger, IPhysicsTriggerWithStay
{
    private static readonly HashSet<IPlayerOwner> s_damagedThisTick = new();
    private static int s_lastFrame = -1;

    public override string Description { get; } = "FlameDamageTrigger";

    public override bool IsStatic => true;

    public override void ProceedDamage(IPlayerOwner player, BodyPartCollider bodyPart)
    {
        // frame reset
        if (Time.frameCount != s_lastFrame)
        {
            s_lastFrame = Time.frameCount;
            s_damagedThisTick.Clear();
        }

        if (!s_damagedThisTick.Add(player))
            return;

        bodyPart.ProceedFlame();
    }

    public override void AddPenalty(IPlayerOwner player) { }
    public override void RemovePenalty(IPlayerOwner player) { }
    public override void PlaySound(bool useOcclusion = false) { }
}