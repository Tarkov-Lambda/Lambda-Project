using System;
using Comfort.Common;
using EFT;
using HarmonyLib;
using ifp.arena.bep.Core;
using static EFT.Player;
using static EFT.PlayerAnimator;

public static class PlayerExtensions
{
    public static PlayerScore GetScore(this Player player)
    {
        return H.GetPlayerScore(player);
    }

    // full retard nuclear hand resetting
    // this needs to go ASAP
    public static void UnfuckHands(this Player player)
    {
        D.Log("unfucking hands controller");
        try
        {
            player.ProcessStatus = Player.EProcessStatus.None;

            if (player.AbstractProcess_0 != null)
            {
                try
                {
                    player.AbstractProcess_0.AbortAfterCompletion();
                }
                catch (Exception ex)
                {
                    D.LogError("failed to abort AbstractProcess_0: " + ex);
                }
                player.AbstractProcess_0 = null;
            }

            var firearmController = player.HandsController as FirearmController;

            if (player.HandsController != null)
            {
                if (firearmController != null)
                {
                    if (player.MovementContext != null)
                    {
                        player.MovementContext.OnStateChanged -= firearmController.method_17;
                    }
                    if (player.Physical != null)
                    {
                        player.Physical.OnSprintStateChangedEvent -= firearmController.method_16;
                    }

                    try
                    {
                        firearmController.RemoveBallisticCalculator();
                    }
                    catch (Exception ex)
                    {
                        D.LogError("failed to RemoveBallisticCalculator: " + ex);
                    }
                }

                try
                {
                    player.DestroyController();
                }
                catch (Exception ex2)
                {
                    D.LogError("failed to neatly destroy HandsController: " + ex2);

                    if (player.HandsController != null)
                    {
                        UnityEngine.Object.Destroy(player.HandsController);
                        player.HandsController = null;
                    }
                }
            }

            player.RemoveLeftHandItem(1f);

            if (player.ProceduralWeaponAnimation != null)
            {
                player.ProceduralWeaponAnimation.ClearPreviousWeapon();
            }

            player.SetInventoryOpened(false);
            if (player.MovementContext != null)
            {
                player.MovementContext.SetBlindFire(0);
                player.MovementContext.PlayerAnimatorSetWeaponId(EWeaponAnimationType.EmptyHands);
            }

            player.SetEmptyHands(new Callback<GInterface198>(result =>
            {
                if (result.Failed)
                {
                    D.LogError("failed to equip empty hands after reset: " + result.Error);
                }
                else
                {
                    player.ForceUnlockInventory();
                    D.Log("successfully reset to empty hands");
                }
            }));
        }
        catch (Exception ex3)
        {
            D.LogError("error during hands resetting: " + ex3);
        }
    }

    public static void EquipSomething(this Player player)
    {
        var firearmController = player.HandsController as FirearmController;
        if (player.LastEquippedWeaponOrKnifeItem != null)
        {
            InteractionsHandlerClass.Discard(player.LastEquippedWeaponOrKnifeItem, H.MainPlayer.InventoryController, true);

            player.ProcessStatus = EProcessStatus.None;
            player.TrySetLastEquippedWeapon();
        }
        else
        {
            player.ProcessStatus = EProcessStatus.None;
            player.SetFirstAvailableItem((result) => { });
        }

        if (firearmController != null && firearmController.Weapon != null)
        {
            Traverse.Create(player.ProceduralWeaponAnimation).Field("_firearmAnimationData").SetValue(firearmController);
        }
    }
}