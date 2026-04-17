using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using ifp.arena.bep.Core.UI;
using ifp.arena.bep.Patches.Tarkov.UI;
using Newtonsoft.Json;

namespace ifp.arena.bep.Core;

public struct PresetManagerSlotInfo
{
    public string defaultBsgId;
    public bool isRequired;
}

// this manager deals with capturing the equipment that the player brings into the raid for inventory resetting
// also it equips a random upper/lower whenever a new profile has been created
public class DefaultEquipmentManager : Singleton<DefaultEquipmentManager>, IDisposable
{
    private readonly string PresetDataPath = Path.Combine(Plugin.pathToConfigs, "DefaultEquipment.jsonc");

    private Dictionary<EquipmentSlot, PresetManagerSlotInfo> PresetInfoConfig = new(); // Hardcoded default preset

    public Dictionary<EquipmentSlot, Item> RecordedItems { get; private set; } = new(); // What is actually used

    public DefaultEquipmentManager()
    {
        LoadItems(File.ReadAllText(PresetDataPath));
        H.OnGameStarted += CapturePreset;
        H.AfterApplicationLoaded += AfterApplicationLoaded;

        if (H.IsInRaid()) CapturePreset(); // Hot-reload
    }


    public void Dispose()
    {
        H.OnGameStarted -= CapturePreset;
        H.AfterApplicationLoaded -= AfterApplicationLoaded;

        Release(this);
    }

    public void AfterApplicationLoaded()
    {
        if (Patch_LoginUI_Awake.IsNewProfile)
        {
            EquipRandomTacticalClothing();
        }
    }


    private void LoadItems(string json)
    {
        PresetInfoConfig = JsonConvert.DeserializeObject<Dictionary<EquipmentSlot, PresetManagerSlotInfo>>(json);
    }

    // WARNING!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    // TacticalVest must always be evaluated first before Armor Vest to make sure that it's not armoured
    // WARNING!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    private void CapturePreset()
    {
        if (H.IsHeadless) return;

        foreach (var presetInfo in PresetInfoConfig)
        { 
            Item equippedItem = null;

            // Whatever item the person brought in raid
            Item existingItem = H.MainPlayer.GetSlotItem(presetInfo.Key);

            if (existingItem != null)
            {
                if (presetInfo.Key is EquipmentSlot.TacticalVest or EquipmentSlot.ArmorVest)
                {
                    CompoundItem armor = existingItem as CompoundItem;
                    if (armor.CanFitPlates())
                    {
                        equippedItem = existingItem;
                    }
                }
                else equippedItem = existingItem;
            }

            if (equippedItem == null && presetInfo.Value.isRequired)
            {
                equippedItem = Singleton<PresetItemsCache>.Instance.GetPresetItem(presetInfo.Value.defaultBsgId);
            }

            // If the tactical rig is armoured, skip armor vest
            if (presetInfo.Key is EquipmentSlot.ArmorVest)
            {
                if (PU.IsTacRigArmored(RecordedItems[EquipmentSlot.TacticalVest] as VestItemClass))
                {
                    continue;
                }
            }

            RecordedItems[presetInfo.Key] = equippedItem;
        }
    }

    public void EquipRandomTacticalClothing()
    {
        var session = ItemUiContext.Instance?.Session;
        if (session == null)
        {
            D.LogError("Session not found. Ensure you are in the main menu.");
            return;
        }

        var profile = session.Profile;

        var ragman = session.Traders.FirstOrDefault(t => t.Settings.CustomizationSeller);
        if (ragman == null)
        {
            D.LogError("Could not find the Customization Trader (Ragman).");
            return;
        }

        // fetch all OWNED/UNLOCKED suites directly from the local profile memory
        var availableSuites = Singleton<CustomizationSolverClass>.Instance.GetAvailableSuites(profile.Side).ToList();

        List<GClass3682> availableUppers = new List<GClass3682>();
        List<GClass3682> availableLowers = new List<GClass3682>();

        foreach (var suite in availableSuites)
        {
            // GClass3683 is Upper Body, GClass3684 is Lower Body
            if (suite is GClass3683)
                availableUppers.Add(suite);
            else if (suite is GClass3684)
                availableLowers.Add(suite);
        }

        // pick random items
        System.Random random = new System.Random();
        List<GClass3682> suitesToEquip = new List<GClass3682>();

        if (availableUppers.Count > 0)
        {
            suitesToEquip.Add(availableUppers[random.Next(availableUppers.Count)]);
        }

        if (availableLowers.Count > 0)
        {
            suitesToEquip.Add(availableLowers[random.Next(availableLowers.Count)]);
        }

        // apply to the profile & backend
        if (suitesToEquip.Count > 0)
        {
            // This silently sends the equip request to the server
            ragman.ApplyWear(suitesToEquip.ToArray());
            D.Log($"Successfully equipped {suitesToEquip.Count} random clothing items.");
        }
    }
}
