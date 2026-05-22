using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using Lambda.Core.Main.AssetBundleHandling;
using Lambda.Core.Main.UI;
using Lambda.Core.Patches.Tarkov.UI;
using Newtonsoft.Json;

namespace Lambda.Core.Main;

public struct PresetManagerSlotInfo
{
    public string defaultBsgId;
    public bool isRequired;
}

// this manager deals with capturing the equipment that the player brings into the raid for inventory resetting
// also it equips a random upper/lower whenever a new profile has been created
public class ClientEquipmentManager : Singleton<ClientEquipmentManager>, IDisposable
{
    private readonly string PresetDataPath = Path.Combine(Plugin.pathToConfigs, "DefaultEquipment.jsonc");

    private Dictionary<EquipmentSlot, PresetManagerSlotInfo> PresetInfoConfig = new(); // Hardcoded default preset

    public Dictionary<EquipmentSlot, Item> RecordedItems { get; private set; } = new(); // What is actually used

    public ClientEquipmentManager()
    {
        LoadItems(File.ReadAllText(PresetDataPath));
    }

    public void Dispose() { }

    private void LoadItems(string json)
    {
        PresetInfoConfig = JsonConvert.DeserializeObject<Dictionary<EquipmentSlot, PresetManagerSlotInfo>>(json);
    }

    // WARNING!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    // TacticalVest must always be evaluated first before Armor Vest to make sure that it's not armoured
    // WARNING!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    public void CapturePreset()
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

            RuntimeBundleLoader.Instance.AddToCache(equippedItem);
            RecordedItems[presetInfo.Key] = equippedItem;
        }
    }
}
