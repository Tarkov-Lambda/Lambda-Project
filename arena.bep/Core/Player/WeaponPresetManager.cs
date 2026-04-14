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
    private string _playerSelectionFilePath = Path.Combine(Plugin.pathToConfigs, "WeaponPresets");
    private readonly string _buildsDirectoryPath = Path.Combine(Plugin.pathToConfigs, "WeaponPresets", "Builds");

    // template bsgId -> WeaponBuildClass.MongoID_0
    public Dictionary<string, string> SelectedGunPresetMap = new();

    // exported weapon builds keyed by the preset name
    private Dictionary<string, Weapon> _loadedWeaponBuilds = new();

    public WeaponPresetManager()
    {
        H.TarkovApp.AfterApplicationLoaded += InitializeOnApplicationLoad;

        // Hot-reload
        if (H.TarkovISession?.Profile_1?.Id != null) InitializeOnApplicationLoad();
    }

    public void Dispose()
    {
        H.TarkovApp.AfterApplicationLoaded -= InitializeOnApplicationLoad;
        Release(this);
    }

    public void InitializeOnApplicationLoad()
    {
        _playerSelectionFilePath = Path.Combine(Plugin.pathToConfigs, "WeaponPresets", $"{H.TarkovISession.Profile_1.Id}.jsonc");

        if (File.Exists(_playerSelectionFilePath))
        {
            string existingData = File.ReadAllText(_playerSelectionFilePath);

            if (!string.IsNullOrWhiteSpace(existingData))
            {
                DeserializePlayerSelections(existingData);
            }
            else
            {
                GenerateDefaultSelectionMap();
            }
        }
        else
        {
            GenerateDefaultSelectionMap();
        }

        ImportExternalPresetsFromDisk();
    }

    public WeaponBuildClass GetPreferredBuildForTemplate(string bsgId)
    {
        if (SelectedGunPresetMap.TryGetValue(bsgId, out var mongoId))
        {
            var matchByMongo = FU.WeaponPresets.FirstOrDefault(b => b.MongoID_0 == mongoId);
            if (matchByMongo != null)
                return matchByMongo;
        }

        var userBuild = FU.WeaponPresets.FirstOrDefault(b => !b.FromPreset && b.Item?.TemplateId == bsgId);
        if (userBuild != null)
        {
            return userBuild;
        }

        return FU.WeaponPresets.FirstOrDefault(b => b.Item?.TemplateId == bsgId);
    }

    private void GenerateDefaultSelectionMap()
    {
        D.Log("Initializing Weapon Presets Selection Map");

        List<string> allWeaponsTemplateIds = FU.GetAllWeaponTemplateIds();

        foreach (string weaponTemplateId in allWeaponsTemplateIds)
        {
            var preferredBuild = GetPreferredBuildForTemplate(weaponTemplateId);

            if (preferredBuild == null) continue;

            SelectedGunPresetMap[weaponTemplateId] = preferredBuild.MongoID_0;
        }

        PersistPlayerSelectionsToDisk();
    }

    private void DeserializePlayerSelections(string json)
    {
        D.Log("Loading Existing Weapon Selections");

        var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

        if (data != null)
            SelectedGunPresetMap = data;
        else
            GenerateDefaultSelectionMap();
    }

    public void UpdateSelectedPreset(string bsgId, string mongoId)
    {
        SelectedGunPresetMap[bsgId] = mongoId;
        PersistPlayerSelectionsToDisk();
    }

    private void PersistPlayerSelectionsToDisk()
    {
        try
        {
            string directory = Path.GetDirectoryName(_playerSelectionFilePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(SelectedGunPresetMap, Formatting.Indented);
            File.WriteAllText(_playerSelectionFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save weapon selections: {ex}");
        }
    }

    /// <summary>
    /// Exports a specific weapon build configuration to a JSON file.
    /// </summary>
    public void ExportBuildToFile(string json, string presetName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON content is empty");

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                presetName = presetName.Replace(c, '_');
            }

            Directory.CreateDirectory(_buildsDirectoryPath);
            var buildPath = Path.Combine(_buildsDirectoryPath, $"{presetName}.json");

            File.WriteAllText(buildPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to export weapon build: {ex}");
        }
    }

    /// <summary>
    /// Scans the builds directory and imports all found .json weapon presets.
    /// </summary>
    public void ImportExternalPresetsFromDisk()
    {
        try
        {
            if (!Directory.Exists(_buildsDirectoryPath))
            {
                D.Log("Weapon builds directory does not exist.");
                return;
            }

            string[] files = Directory.GetFiles(_buildsDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);

            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);

                    if (string.IsNullOrWhiteSpace(json))
                        continue;

                    string name = Path.GetFileNameWithoutExtension(file);

                    RegisterPresetToSystem(json, name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load preset file {file}: {ex}");
                }
            }

            D.Log($"Loaded {_loadedWeaponBuilds.Count} weapon presets.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load weapon builds directory: {ex}");
        }
    }

    /// <summary>
    /// Deserializes a weapon build JSON and registers it into the Tarkov preset system.
    /// </summary>
    public void RegisterPresetToSystem(string json, string presetName)
    {
        try
        {
            Item item = FU.DeserializeItem(json);
            Item clonedItem = item.CloneItem(H.MainPlayer.InventoryController);

            if (clonedItem != null && clonedItem is Weapon weaponPreset)
            {
                _loadedWeaponBuilds[presetName] = weaponPreset;
                FU.CreateAndSaveWeaponPreset(weaponPreset, presetName).Forget();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register weapon preset '{presetName}': {ex}");
        }
    }
}