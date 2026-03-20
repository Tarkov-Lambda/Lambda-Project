using System;
using System.Collections.Generic;
using System.IO;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Newtonsoft.Json;

namespace ifp.arena.bep.Core
{
    public struct PresetManagerSlotInfo
    {
        public string defaultBsgId;
        public bool isRequired;
    }

    public class PresetManager : Singleton<PresetManager>, IDisposable
    {

        private string PresetDataPath = Path.Combine(BepInEx.Paths.PluginPath, "ifp", "json", "PresetData.json");

        private Dictionary<EquipmentSlot, PresetManagerSlotInfo> PresetInfoConfig = new(); // Hardcoded default preset

        public Dictionary<EquipmentSlot, Item> RecordedItems { get; private set; } = new(); // What is actually used

        private PresetManager()
        {
            LoadItems(File.ReadAllText(PresetDataPath));
            H.OnGameStarted += CapturePreset;

            if (H.isInRaid()) CapturePreset(); // Hot-reload
        }

        public void Dispose()
        {
            H.OnGameStarted -= CapturePreset;
            Release(this);
        }

        private void LoadItems(string json)
        {
            PresetInfoConfig = JsonConvert.DeserializeObject<Dictionary<EquipmentSlot, PresetManagerSlotInfo>>(json);
        }

        private void CapturePreset(GameWorld gWorld = null)
        {
            foreach (var presetInfo in PresetInfoConfig)
            {
                // When a person enters the raid, their slots override defaults. if a required slot does not have an item, we use default.
                Item item = H.MainInventory.Equipment.GetSlot(presetInfo.Key).ContainedItem;
                if (item == null && presetInfo.Value.isRequired)
                {
                    item = ItemsUtils.CreateItemFromTemplateId(presetInfo.Value.defaultBsgId);
                }

                RecordedItems[presetInfo.Key] = item;
            }
        }
    }
}