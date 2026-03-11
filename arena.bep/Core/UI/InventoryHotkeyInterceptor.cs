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

        private float keyDownBeginTimestamp = 0f;
        private bool wasKeyDownPrevFrame = false;
        private bool waitingForKeyReleaseAfterInventoryClose = false;

        private const float MAX_TAP_TIME = 0.250f;

        private bool sentHoldBegin = false;
        public event Action OnHoldBegin;
        public event Action OnHoldEnd;

        void Awake()
        {
            playerOwner = GetComponent<EftGamePlayerOwner>();
        }

        void Update()
        {
            if (playerOwner.Player.IsInventoryOpened)
            {
                waitingForKeyReleaseAfterInventoryClose = true;
                sentHoldBegin = false;
                return;
            }

            bool isDownThisFrame = CheckInventoryKeysDown();

            if (waitingForKeyReleaseAfterInventoryClose)
            {
                if (isDownThisFrame)
                    return;

                waitingForKeyReleaseAfterInventoryClose = false;
                wasKeyDownPrevFrame = false;
                keyDownBeginTimestamp = 0f;
                sentHoldBegin = false;
                return;
            }

            if (isDownThisFrame)
            {
                if (!wasKeyDownPrevFrame) // just pressed down
                {
                    wasKeyDownPrevFrame = true;
                    keyDownBeginTimestamp = Time.time;
                }

                if (!sentHoldBegin)
                {
                    float heldTime = Time.time - keyDownBeginTimestamp;
                    if (heldTime > MAX_TAP_TIME)
                    {
                        OnHoldBegin?.Invoke();
                        sentHoldBegin = true;
                    }
                }
            }
            else
            {
                if (wasKeyDownPrevFrame)
                {
                    wasKeyDownPrevFrame = false;
                    float heldTime = Time.time - keyDownBeginTimestamp;

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
                    else if (sentHoldBegin)
                    {
                        OnHoldEnd?.Invoke();
                    }
                }

                sentHoldBegin = false;
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
