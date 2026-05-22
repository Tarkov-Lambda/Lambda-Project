using System;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
using static EFT.Player;
using static EFT.PlayerAnimator;

// class full of bruteforce player manipulations 
public static class PlayerExtensions
{
    public static PlayerContext GetContext(this Player player)
    {
        return H.GetPlayerContext(player);
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

            // player.SetEmptyHands(new Callback<GInterface198>(result =>
            // {
            //     if (result.Failed)
            //     {
            //         D.LogError("failed to equip empty hands after reset: " + result.Error);
            //     }
            //     else
            //     {
            //         player.ForceUnlockInventory();
            //         D.Log("successfully reset to empty hands");
            //     }
            // }));
        }
        catch (Exception ex3)
        {
            D.LogError("error during hands resetting: " + ex3);
        }
    }

    // Lord forgive me
    public static void UpdateVisuals(this Player player, InventoryEquipment newEquipment)
    {
        UpdateObserver(player.NightVisionObserver, newEquipment.GetSlot(EquipmentSlot.Headwear));
        UpdateObserver(player.ThermalVisionObserver, newEquipment.GetSlot(EquipmentSlot.Headwear));
        UpdateObserver(player.FaceShieldObserver, newEquipment.GetSlot(EquipmentSlot.Headwear));
        UpdateObserver(player.FaceCoverObserver, newEquipment.GetSlot(EquipmentSlot.FaceCover));

        if (player.PlayerBody == null) return;

        player.PlayerBody.Equipment = newEquipment;

        var backpackSlot = newEquipment.GetSlot(EquipmentSlot.Backpack);
        var slotNames = (EquipmentSlot[])AccessTools.Field(typeof(PlayerBody), "SlotNames").GetValue(null);
        var slotViews = player.PlayerBody.SlotViews;

        var getByKeyMethod = AccessTools.Method(slotViews.GetType(), "GetByKey");
        var addOrReplaceMethod = AccessTools.Method(slotViews.GetType(), "AddOrReplace");
        var equipmentSlotClassType = typeof(PlayerBody).GetNestedType("EquipmentSlotClass", BindingFlags.Public | BindingFlags.NonPublic);
        var disposeMethod = AccessTools.Method(equipmentSlotClassType, "Dispose");

        foreach (EquipmentSlot slotName in slotNames)
        {
            var newSlot = newEquipment.GetSlot(slotName);
            var oldSlotView = getByKeyMethod.Invoke(slotViews, [slotName]);

            Transform bone = null;
            Transform altBone = null;

            if (oldSlotView != null)
            {
                bone = (Transform)AccessTools.Field(equipmentSlotClassType, "Transform_0").GetValue(oldSlotView);
                altBone = (Transform)AccessTools.Field(equipmentSlotClassType, "Transform_1").GetValue(oldSlotView);
            }
            else
            {
                bone = player.PlayerBody.GetSlotBone(slotName);
                altBone = player.PlayerBody.GetAlternativeHolsterBone(slotName);
            }

            var newSlotView = Activator.CreateInstance(
                equipmentSlotClassType,
                [player.PlayerBody, newSlot, bone, slotName, backpackSlot, altBone, false]
            );

            var replacedView = addOrReplaceMethod.Invoke(slotViews, [slotName, newSlotView]);
            if (replacedView != null)
            {
                disposeMethod.Invoke(replacedView, null);
            }
        }

        var disposeField = AccessTools.Field(typeof(PlayerBody), "_dispose");
        var compositeDisposable = disposeField?.GetValue(player.PlayerBody);

        if (compositeDisposable != null)
        {
            var addDisposableMethod = AccessTools.Method(compositeDisposable.GetType(), "AddDisposable", [typeof(Action)]);

            var headwearSlotView = getByKeyMethod.Invoke(slotViews, [EquipmentSlot.Headwear]);
            var faceCoverSlotView = getByKeyMethod.Invoke(slotViews, [EquipmentSlot.FaceCover]);

            var headwearParentedModel = AccessTools.Field(equipmentSlotClassType, "ParentedModel").GetValue(headwearSlotView);
            var faceCoverParentedModel = AccessTools.Field(equipmentSlotClassType, "ParentedModel").GetValue(faceCoverSlotView);

            var bindMethod = AccessTools.Method(headwearParentedModel.GetType(), "Bind");
            var method1Delegate = Delegate.CreateDelegate(typeof(Action<GameObject>), player.PlayerBody, "method_1");

            var hwDisposable = bindMethod.Invoke(headwearParentedModel, [method1Delegate]);
            var fcDisposable = bindMethod.Invoke(faceCoverParentedModel, [method1Delegate]);

            addDisposableMethod.Invoke(compositeDisposable, [hwDisposable]);
            addDisposableMethod.Invoke(compositeDisposable, [fcDisposable]);
        }

        AccessTools.Method(typeof(PlayerBody), "method_1").Invoke(player.PlayerBody, [null]);

        var method86Delegate = Delegate.CreateDelegate(typeof(Action<GameObject>), player, "method_86");
        player.BindSlotViewChangedAction(EquipmentSlot.Headwear, (Action<GameObject>)method86Delegate);
    }

    private static void UpdateObserver<T>(Player.GClass2059<T> observer, Slot newSlot) where T : class, IItemComponent
    {
        if (observer == null || newSlot == null)
            return;

        var type = typeof(Player.GClass2059<T>);

        AccessTools.Field(type, "Slot_0")?.SetValue(observer, newSlot);
        AccessTools.Method(type, "Update")?.Invoke(observer, null);
    }

    public static bool TryGetHandsResourceKey(this Player player, out ResourceKey resourceKey)
    {
        if (player.PlayerBody.BodyCustomization.TryGetValue(EBodyModelPart.Hands, out MongoID handsId))
        {
            resourceKey = H.CustomizationSolverClass.GetBundle(handsId);
            return true;
        }

        resourceKey = null;
        return false;
    }
}