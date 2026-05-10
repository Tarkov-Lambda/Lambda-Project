using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ifp.arena.bep;
using MemoryPack;
using Newtonsoft.Json;

public static class GameplayVariables
{
    private readonly static string DefaultGameplayVariablesPath = Path.Combine(Plugin.pathToConfigs, "GameplayVariables.jsonc");

    public static GameplayVariablesStruct vars = JsonConvert.DeserializeObject<GameplayVariablesStruct>(File.ReadAllText(DefaultGameplayVariablesPath));

    public static List<string> GetAllFieldStrings()
    {
        var result = new List<string>();

        var fields = typeof(GameplayVariablesStruct).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            object value = field.GetValue(vars);
            result.Add($"{field.Name}: {value}");
        }

        return result;
    }

    public static bool SetFieldValue(string fieldName, object value)
    {
        var field = typeof(GameplayVariablesStruct).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);

        if (field == null) return false;

        object boxed = vars;

        try
        {
            object convertedValue = Convert.ChangeType(value, field.FieldType);
            field.SetValue(boxed, convertedValue);

            vars = (GameplayVariablesStruct)boxed;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}