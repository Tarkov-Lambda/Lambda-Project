using System;
using System.Collections.Generic;
using System.IO;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using ifp.arena.bep.Core.UI;
using Newtonsoft.Json;

namespace ifp.arena.bep.Core;

public class WeaponPresetManager : Singleton<WeaponPresetManager>, IDisposable
{
    private string playerWeaponPresetDataPath;

    // template bsgId -> WeaponBuildClass.MongoID_0 (either points to a custom preset, or )
    public Dictionary<string, string> SelectedGunPreset = new();

    public WeaponPresetManager()
    {
        playerWeaponPresetDataPath = Path.Combine(Plugin.pathToConfigs, "WeaponPresets", $"{H.TarkovISession.Profile_1.Id}.jsonc");

        if (File.Exists(playerWeaponPresetDataPath))
        {
            string existingData = File.ReadAllText(playerWeaponPresetDataPath);

            if (!string.IsNullOrWhiteSpace(existingData))
            {
                LoadExistingData(existingData);
            }
            else
            {
                InitializeData();
            }
        }
        else
        {
            InitializeData();
        }
    }

    public void Dispose()
    {
        Release(this);
    }

    private void InitializeData()
    {
        D.Log("Initializing Weapon Presets");

        List<string> allWeaponsTemplateIds = FU.GetAllWeaponTemplateIds();

        foreach (string weaponTemplateId in allWeaponsTemplateIds)
        {
            var customPreset = FU.GetCustomTemplate(weaponTemplateId);

            if (customPreset == null) continue;

            SelectedGunPreset[weaponTemplateId] = customPreset.MongoID_0;
        }

        SaveData();
    }

    private void LoadExistingData(string json)
    {
        D.Log("Loading Existing Weapon Presets");
        
        var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

        if (data != null)
            SelectedGunPreset = data;
        else
            InitializeData();
    }

    public void SetChosenWeaponPreset(string bsgId, string mongoId)
    {
        SelectedGunPreset[bsgId] = mongoId;
        SaveData();
    }

    private void SaveData()
    {
        try
        {
            string directory = Path.GetDirectoryName(playerWeaponPresetDataPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(SelectedGunPreset, Formatting.Indented);
            File.WriteAllText(playerWeaponPresetDataPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save weapon presets: {ex}");
        }
    }
}
