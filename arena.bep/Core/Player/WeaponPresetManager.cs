using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Utils;
using ifp.arena.bep.Core.UI;
using Newtonsoft.Json;

namespace ifp.arena.bep.Core;

public class WeaponPresetManager : Singleton<WeaponPresetManager>, IDisposable
{
    private string playerWeaponPresetDataPath = Path.Combine(Plugin.pathToConfigs, "WeaponPresets");
    private readonly string WeaponPresetDataPath = Path.Combine(Plugin.pathToConfigs, "WeaponPresets", "Builds");

    // template bsgId -> WeaponBuildClass.MongoID_0 (either points to a custom preset, or )
    public Dictionary<string, string> SelectedGunPreset = new();

    // exported weapon builds (hash collision warning) keyed by the json name (preset name)
    public Dictionary<string, Weapon> WeaponPresetBuilds = new();

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

        LoadExistingWeaponPresets();
    }

    public void Dispose()
    {
        Release(this);
    }

    // Fetch a build that exists in the user's gun builds
    // priority: explicitly selected -> any user made build -> bsg made
    // NOTE: During profile creation stage, if the player picks a profile with existing presets
    // those presets will be instantly chosen
    public WeaponBuildClass GetCustomTemplate(string bsgId)
    {
        if (SelectedGunPreset.TryGetValue(bsgId, out var mongoId))
        {
            var matchByMongo = FU.WeaponPresets.FirstOrDefault(b => b.MongoID_0 == mongoId);
            if (matchByMongo != null)
                return matchByMongo;
        }

        var userBuild = FU.WeaponPresets.FirstOrDefault(b => !b.FromPreset && b.Item?.TemplateId == bsgId);
        if (userBuild != null)
        {
            var serializedItem = FU.SerializeItem(userBuild.Item);
            SaveWeaponPreset(serializedItem, userBuild.HandbookName);
            return userBuild;
        }

        return FU.WeaponPresets.FirstOrDefault(b => b.Item?.TemplateId == bsgId);
    }

    private void InitializeData()
    {
        D.Log("Initializing Weapon Presets");

        List<string> allWeaponsTemplateIds = FU.GetAllWeaponTemplateIds();

        foreach (string weaponTemplateId in allWeaponsTemplateIds)
        {
            var customPreset = GetCustomTemplate(weaponTemplateId);

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

    // Serialize the item build and export it as a json file
    public void SaveWeaponPreset(string json, string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON content is empty");

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            Directory.CreateDirectory(WeaponPresetDataPath);
            var buildPath = Path.Combine(WeaponPresetDataPath, $"{name}.json");

            File.WriteAllText(buildPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save weapon presets: {ex}");
        }
    }

    public void LoadExistingWeaponPresets()
    {
        try
        {
            if (!Directory.Exists(WeaponPresetDataPath))
            {
                D.Log("Weapon preset directory does not exist.");
                return;
            }

            string[] files = Directory.GetFiles(WeaponPresetDataPath, "*.json", SearchOption.TopDirectoryOnly);

            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);

                    if (string.IsNullOrWhiteSpace(json))
                        continue;

                    string name = Path.GetFileNameWithoutExtension(file);

                    LoadWeaponPreset(json, name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load preset file {file}: {ex}");
                }
            }

            D.Log($"Loaded {WeaponPresetBuilds.Count} weapon presets.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load weapon presets directory: {ex}");
        }
    }

    public void LoadWeaponPreset(string json, string name)
    {
        try
        {
            Item item = FU.InstantiatePreset(json);

            if (item != null && item is Weapon weaponPreset)
            {
                WeaponPresetBuilds[name] = weaponPreset;
                FU.CreateAndSaveWeaponPreset(item, name).Forget();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save weapon presets: {ex}");
        }
    }
}