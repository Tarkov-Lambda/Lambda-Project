using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using ifp.arena.bep.Core.Gamemode;
using UnityEngine;

namespace ifp.arena.bep.Core;

public static class PlayerUtilities
{
    public static bool IsTacRigArmored(VestItemClass tacRig)
    {
        var tacRigTemplate = tacRig?.Template as VestTemplateClass;
        if (tacRigTemplate.BlocksArmorVest) return true;
        return false;
    }

    public static bool CanArmorFitPlates(VestItemClass tacRig)
    {
        var tacRigTemplate = tacRig?.Template as VestTemplateClass;
        if (tacRigTemplate.BlocksArmorVest) return true;
        return false;
    }

    public static async Task CloseEyes(bool playDeathAudio = true, bool openAfter = true, int closeDelay = 750, int openDelay = 4500)
    {
        DeathFade deathFade = CameraClass.Instance.Camera.GetComponent<DeathFade>();
        deathFade.enabled = true;

        await Task.Delay(closeDelay);
        deathFade.EnableEffect();

        if (playDeathAudio)
        {
            var resourceRequest = Resources.LoadAsync<UISoundsWrapper>("Audio/UISoundsWrapper");
            var soundsWrapper = (UISoundsWrapper)resourceRequest.asset;
            var uIClip = soundsWrapper.GetUIClip(EUISoundType.PlayerIsDead);

            H.EFTGUISounds.PlaySound(uIClip, false, true);
            H.EFTGUISounds.PlayUISound(EUISoundType.PlayerIsDead);
        }

        if (openAfter)
        {
            await Task.Delay(openDelay);


            OpenEyes();
        }
    }

    public static void OpenEyes()
    {
        DeathFade deathFade = CameraClass.Instance.Camera.GetComponent<DeathFade>();
        deathFade.enabled = true;
        deathFade.DisableEffect();
    }

    // Waits for the player to stop moving before performing inventory operations.
    // MUST be called before any operation that locks the inventory controller.
    public static async UniTask WaitUntilStationary(Player player)
    {
        await UniTask.WaitUntil(() => !player.MovementContext.CanWalk || player.MovementContext.Velocity.sqrMagnitude == 0f);
        // await UniTask.Delay(200);
    }
}