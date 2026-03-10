using Comfort.Common;
using EFT;
using EFT.InputSystem;
using ifp.arena.bep.Patches.Tarkov.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ifp.arena.bep.Core.UI
{
    [RequireComponent(typeof(EftGamePlayerOwner))]
    public class InventoryHotkeyListener : MonoBehaviour
    {
        private EftGamePlayerOwner playerOwner;

        private float keyDownTime = 0f;
        private bool isKeyDown = false;

        private const float MAX_TAP_TIME = 0.250f;

        void Awake()
        {
            playerOwner = GetComponent<EftGamePlayerOwner>();
        }

        void Update()
        {
            if (playerOwner.Player.IsInventoryOpened)
            {
                keyDownTime = 0;
                isKeyDown = true;
                return;
            }

            bool isCurrentlyDown = CheckInventoryKeysDown();

            if (isCurrentlyDown && !isKeyDown)
            {
                isKeyDown = true;
                keyDownTime = Time.time;
            }
            else if (!isCurrentlyDown && isKeyDown)
            {
                isKeyDown = false;
                float heldTime = Time.time - keyDownTime;

                if (heldTime <= MAX_TAP_TIME)
                {
                    // tupa copy of bsg logic
                    if (playerOwner.Player.HealthController.IsAlive &&
                        !GamePlayerOwner.IgnoreInputInNPCDialog &&
                        !GamePlayerOwner.IgnoreInputWithKeepResetLook)
                    {
                        Patch_EftGamePlayerOwner_TranslateInventoryScreenInput.AllowOpenInventory = true;
                        playerOwner.TranslateInventoryScreenInput(ECommand.ToggleInventory);
                    }
                }
            }
        }

        private bool CheckInventoryKeysDown()
        {
            var settings = Singleton<SharedGameSettingsClass>.Instance?.Control?.Settings;
            var keyBindings = settings.UserKeyBindings?.Value;
            var invGroup = keyBindings.FirstOrDefault(x => x.keyName == EGameKey.Inventory);

            foreach (var variant in invGroup.variants)
            {
                if (variant.IsEmpty || variant.keyCode == null || variant.keyCode.Count == 0) continue;

                bool allKeysDown = true;

                foreach (var key in variant.keyCode)
                {
                    if (!Input.GetKey(key))
                    {
                        allKeysDown = false;
                        break;
                    }
                }

                if (allKeysDown) 
                    return true;
            }

            return false;
        }
    }
}
