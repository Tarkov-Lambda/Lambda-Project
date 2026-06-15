using Audio.ReverbSubsystem;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Systems.Effects;
using UnityEngine;

namespace Lambda.Core.Patches.Tarkov;

internal class Patch_MagazineItemClass_GetAmmoCountByLevel : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MagazineItemClass), nameof(MagazineItemClass.GetAmmoCountByLevel));

    [PatchPrefix]
    static bool Prefix(ref bool @checked, ref int skill)
    {
        @checked = true;
        skill = 2;
        return true;
    }
}

internal class Patch_ReverbSuperSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSuperSource), nameof(ReverbSuperSource.Play));

    [PatchPrefix]
    static bool Prefix() => false;
}

internal class Patch_ReverbSuperSource_PlayScheduled : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(ReverbSuperSource), nameof(ReverbSuperSource.PlayScheduled));

    [PatchPrefix]
    static bool Prefix() => false;
}

internal class Patch_BallisticsCalculator_CreateShot : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(
            typeof(BallisticsCalculator),
            "Shoot_1",
            new Type[]
            {
                typeof(AmmoItemClass),
                typeof(Vector3),
                typeof(Vector3),
                typeof(string),
                typeof(Item),
                typeof(float),
                typeof(int)
            });

    static EftBulletClass savedBullet = null;

    [PatchPrefix]
    static bool Prefix(BallisticsCalculator __instance, ref EftBulletClass __result, ref int ___int_0, AmmoItemClass ammo, UnityEngine.Vector3 shotPosition, UnityEngine.Vector3 shotDirection, System.String playerProfileID, EFT.InventoryLogic.Item item, System.Single speedFactor, System.Int32 fragmentIndex)
    {
        int num = ___int_0;
        ___int_0 = num + 1;
        savedBullet ??= __instance.CreateShot(ammo, shotPosition, shotDirection, num, playerProfileID, item, speedFactor, fragmentIndex);
        EftBulletClass eftBulletClass = savedBullet;
        // __instance.Shoot(eftBulletClass);
        __result = eftBulletClass;
        return false;
    }
}

internal class Patch_Player_FirearmController_InitiateShot : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Player.FirearmController), nameof(Player.FirearmController.InitiateShot));



    [PatchPrefix]
    static bool Prefix(
        Player.FirearmController __instance,
        ISharedBallisticsCalculator ___BallisticsCalculator,
        Player ____player,
        // List<EftBulletClass> ___list_0,
        Action ___action_0,
        ref bool ___bool_6,
        WeaponManagerClass ___weaponManagerClass,
        // Player.FirearmController.UnderbarrelManagerClass ___underbarrelManagerClass,
        IWeapon weapon,
        AmmoItemClass ammo,
        Vector3 shotPosition,
        Vector3 shotDirection
        // Vector3 fireportPosition,
        // int chamberIndex,
        // float overheat
        )
    {
        // ____player.OnMakingShot(weapon, ____player.PlayerBones.WeaponRoot.position - shotPosition);
        if (ammo.InitialSpeed > 0f)
        {
            if (ammo.ProjectileCount == 1)
            {
                EftBulletClass eftBulletClass = ___BallisticsCalculator.Shoot(ammo, shotPosition, shotDirection, ____player.ProfileId, weapon.Item, weapon.SpeedFactor, 0);
                __instance.RegisterShot(weapon.Item, eftBulletClass);
            }
            // else
            // {
            //     ___list_0.Clear();
            //     ___BallisticsCalculator.ShotMultiProjectileShot(ammo, shotPosition, shotDirection, weapon.SpeedFactor, ___list_0, ____player.ProfileId, weapon.Item);
            //     foreach (EftBulletClass eftBulletClass2 in ___list_0)
            //     {
            //         __instance.RegisterShot(weapon.Item, eftBulletClass2);
            //     }
            //     ___list_0.Clear();
            // }
        }

        // ___action_0?.Invoke();

        // if (!____player.IsAI) ____player.OnStatisticsShot?.Invoke(weapon.Item, ammo);

        // ___bool_6 = true;
        // ___weaponManagerClass.PlayShotEffects(____player.IsVisible, ____player.SqrCameraDistance);
        return false;
    }
}

internal class Patch_EftBulletClass_smethod_0 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(EftBulletClass), nameof(EftBulletClass.smethod_0));

    [PatchPrefix]
    static bool Prefix(ref EftBulletClass __result)
    {
        if (EftBulletClass.Stack_0.Count > 0)
        {
            __result = EftBulletClass.Stack_0.Pop();
        }
        __result = new EftBulletClass();

        return false;
    }
}

public class Transpiler_EftBulletClass_method_1 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(EftBulletClass), nameof(EftBulletClass.method_1));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

internal class Patch_EftBulletClass_smethod_1 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(EftBulletClass), nameof(EftBulletClass.smethod_1));

    [PatchPrefix]
    static bool Prefix(EftBulletClass shot)
    {
        Singleton<GameWorld>.Instance.TrajectoryCalculatorPool.Return(shot.TrajectoryInfo);
        shot.TrajectoryInfo = null;
        if (EftBulletClass.Stack_0.Count < 200)
        {
            shot.method_0();
            EftBulletClass.Stack_0.Push(shot);
        }
        return false;
    }
}

public class Transpiler_EftBulletClass_method_2 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(EftBulletClass), nameof(EftBulletClass.method_2));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldc_I4_1);
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

internal class Patch_EftBulletClass_CalculateG1DragCoefficient : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(EftBulletClass), nameof(EftBulletClass.CalculateG1DragCoefficient));

    [PatchPrefix]
    static bool Prefix(float velocity, ref float __result)
    {
        int num = (int)Mathf.Floor(velocity / 17.15f); // 343 * 0.05
        if (num <= 0)
        {
            __result = 0f;
            return false;
        }
        var list = EftBulletClass.List_0;
        if (num >= list.Count)
        {
            __result = list[^1].ballist;
            return false;
        }

        float num2 = list[num - 1].mach * 343f;
        float num3 = list[num].mach * 343f;
        float ballist = list[num - 1].ballist;
        __result = (list[num].ballist - ballist) / (num3 - num2) * (velocity - num2) + ballist;
        return false;
    }
}

internal class Patch_Shell_Update : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(Shell), nameof(Shell.Update));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

internal class Patch_BallisticsCalculator_method_2 : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BallisticsCalculator), nameof(BallisticsCalculator.method_2));

    [PatchTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

internal class Patch_MuzzleManager_Play_Optimize : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(MuzzleManager), nameof(MuzzleManager.Play));

    [PatchPrefix]
    public static bool Prefix(EMuzzleParticlePivot pivot, Transform pTransform)
    {
        var instance = Singleton<Effects>.Instance;
        var commonSystems = instance.MuzzleEffect.CommonSystems;
        instance.TryAddToMBOITParticleManager(commonSystems);
        for (var i = 0; i < commonSystems.Length; i++)
        {
            var container = commonSystems[i];
            if (container.Pivot == pivot)
            {
                var rootParticleSystem = container.RootParticleSystem;
                var t = rootParticleSystem.transform;
                t.SetPositionAndRotation(pTransform.position, pTransform.rotation);
                rootParticleSystem.Stop(true);
                rootParticleSystem.Play(true);
                break;
            }
        }

        return false;
    }
}

internal class Patch_WeaponManager_PlayShotEffects_Optimize : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(WeaponManagerClass), nameof(WeaponManagerClass.PlayShotEffects));

    [PatchPrefix]
    static bool Prefix(WeaponManagerClass __instance, bool isVisible, float sqrCameraDistance)
    {
        if (__instance.Player != null && __instance.Player.IsYourPlayer)
        {
            return true; // Always play for the local player
        }

        if (sqrCameraDistance > 2500f)
        {
            return false;
        }

        return true;
    }
}

internal class Patch_BallisticsCalculator_Shoot : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(BallisticsCalculator), nameof(BallisticsCalculator.Shoot), new[] { typeof(EftBulletClass) });

    [PatchPrefix]
    static bool Prefix()
    {
        // D.Log(Environment.StackTrace);
        return false;
    }
}

internal class Patch_BetterSource_Play : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SimpleSource), nameof(SimpleSource.Play));

    [PatchPrefix]
    static bool Prefix() => false;
}

internal class Patch_BetterSource_PlayScheduled : ModulePatch
{
    protected override MethodBase GetTargetMethod() => AccessTools.Method(typeof(SimpleSource), nameof(SimpleSource.PlayScheduled));

    [PatchPrefix]
    static bool Prefix() => false;
}
