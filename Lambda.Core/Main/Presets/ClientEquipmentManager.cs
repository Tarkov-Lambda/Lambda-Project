using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Comfort.Common;
using EFT;
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
// at the moment there is no server side validation for this, and ultimately it would be best if capture preset would be split into validation as well
public class DefaultEquipmentManager : Singleton<DefaultEquipmentManager>, IDisposable
{
    private readonly string PresetDataPath = Path.Combine(LambdaPlugin.pathToConfigs, "DefaultEquipment.jsonc");

    private static Dictionary<EquipmentSlot, PresetManagerSlotInfo> PresetInfoConfig = new(); // Hardcoded default preset

    public DefaultEquipmentManager()
    {
        LoadItems(File.ReadAllText(PresetDataPath));
    }

    public void Dispose() { }

    private void LoadItems(string json)
    {
        PresetInfoConfig = JsonConvert.DeserializeObject<Dictionary<EquipmentSlot, PresetManagerSlotInfo>>(json);
    }

    public static Dictionary<EquipmentSlot, Item> CapturePreset(Player player)
    {
        Dictionary<EquipmentSlot, Item> RecordedItems = new();

        foreach (var presetInfo in PresetInfoConfig)
        {
            Item equippedItem = null;

            // Whatever item the person brought in raid
            Item existingItem = player.GetSlotItem(presetInfo.Key);

            if (existingItem != null)
            {
                if (presetInfo.Key is EquipmentSlot.TacticalVest or EquipmentSlot.ArmorVest)
                {
                    if (existingItem is CompoundItem armor && armor.CanFitPlates())
                    {
                        equippedItem = existingItem;
                    }
                }
                else if (presetInfo.Key is EquipmentSlot.FaceCover)
                {
                    if (existingItem is not ArmoredEquipmentItemClass)
                    {
                        equippedItem = existingItem;
                    }
                }
                else
                {
                    equippedItem = existingItem;
                }
            }

            if (equippedItem == null && presetInfo.Value.isRequired)
            {
                equippedItem = Singleton<PresetItemsCache>.Instance.GetPresetItem(presetInfo.Value.defaultBsgId);
            }

            if (presetInfo.Key is EquipmentSlot.ArmorVest)
            {
                if (RecordedItems.TryGetValue(EquipmentSlot.TacticalVest, out Item tacVestItem) && tacVestItem is VestItemClass vest && PU.IsTacRigArmored(vest))
                {
                    continue;
                }
            }

            if (equippedItem != null)
            {
                RuntimeBundleLoader.Instance.AddToCache(equippedItem);
                RecordedItems[presetInfo.Key] = equippedItem;
            }
        }

        return RecordedItems;
    }
}

