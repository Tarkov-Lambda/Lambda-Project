using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Comfort.Common;
using Cysharp.Threading.Tasks;
using EFT.InventoryLogic;
using Newtonsoft.Json;

namespace Lambda.Core.Main;

public class WeaponPresetManager : Singleton<WeaponPresetManager>, IDisposable
{
    private string _playerSelectionFilePath = Path.Combine(LambdaPlugin.pathToConfigs, "Presets");
    private readonly string _buildsDirectoryPath = Path.Combine(LambdaPlugin.pathToConfigs, "Presets", "WeaponBuilds");

    // template bsgId -> WeaponBuildClass.MongoID_0
    public Dictionary<string, string> SelectedGunPresetMap = new();

    // exported weapon builds keyed by the preset name
    private Dictionary<string, Weapon> _loadedWeaponBuilds = new();

    public WeaponPresetManager()
    {
        H.AfterApplicationLoaded += InitializeOnApplicationLoad;

        if (H.IsHeadless) return;
        // Hot-reload
        if (H.TarkovClientISession?.Profile_1?.Id != null) InitializeOnApplicationLoad();
    }

    public void Dispose()
    {
        H.AfterApplicationLoaded -= InitializeOnApplicationLoad;
        Release(this);
    }

    public async void InitializeOnApplicationLoad()
    {
        if (H.IsHeadless) return;

        _playerSelectionFilePath = Path.Combine(LambdaPlugin.pathToConfigs, "Presets", $"{H.TarkovClientISession.Profile_1.Id}.jsonc");

        // Wait for all custom presets to finish importing and saving
        await ImportExternalPresetsFromDisk();

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
    }

    public WeaponBuildClass GetPreferredBuildForTemplate(string bsgId)
    {
        if (SelectedGunPresetMap.TryGetValue(bsgId, out var mongoId))
        {
            var matchByMongo = FU.Presets.FirstOrDefault(b => b.MongoID_0 == mongoId);
            if (matchByMongo != null)
                return matchByMongo;
        }

        var userBuild = FU.Presets.FirstOrDefault(b => !b.FromPreset && b.Item?.TemplateId == bsgId);
        if (userBuild != null)
        {
            // var build = FU.SerializeItem(userBuild.Item);
            // ExportBuildToFile(build, userBuild.HandbookName);
            return userBuild;
        }

        return FU.Presets.FirstOrDefault(b => b.Item?.TemplateId == bsgId);
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

    public async UniTask ImportExternalPresetsFromDisk()
    {
        try
        {
            if (!Directory.Exists(_buildsDirectoryPath))
            {
                D.Log("Weapon builds directory does not exist.");
                return;
            }

            string[] files = Directory.GetFiles(_buildsDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);

            // Track all of our registration tasks
            List<UniTask> importTasks = new List<UniTask>();

            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);

                    if (string.IsNullOrWhiteSpace(json))
                        continue;

                    string name = Path.GetFileNameWithoutExtension(file);

                    // Add the task to our list instead of fire-and-forgetting
                    importTasks.Add(RegisterPresetToSystem(json, name));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load preset file {file}: {ex}");
                }
            }

            // Wait until ALL weapon presets are completely registered to the system
            await UniTask.WhenAll(importTasks);

            D.Log($"Loaded {_loadedWeaponBuilds.Count} weapon presets.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load weapon builds directory: {ex}");
        }
    }

    public async UniTask RegisterPresetToSystem(string json, string presetName)
    {
        try
        {
            Item item = FU.InstantiatePreset(json);

            if (item != null && item is Weapon weaponPreset)
            {
                _loadedWeaponBuilds[presetName] = weaponPreset;

                // Await the creation to ensure WeaponBuildsStorage is updated before we continue
                await FU.CreateAndSaveWeaponPreset(weaponPreset, presetName);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to register weapon preset '{presetName}': {ex}");
        }
    }
}