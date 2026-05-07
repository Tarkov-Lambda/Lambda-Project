using System;
using System.Collections.Generic;
using System.Reflection;
using MemoryPack;

public static class GameplayVariables
{
    public static GameplayVariablesStruct vars = new()
    {
        LeanSpeed = 1.3f,
        AimSpeedPenaltyReduction = 0.15f,

        PistolADSMotionScale = 0.1f,
        PistolDisplacementStrScale = .25f,
        PistolZoomBoostScale = 0.1f,

        RifleADSMotionScale = 1f,
        RifleDisplacementStrScale = 1f,

        transmissionHigh = 0.25f,
        transmissionMid = 0.3f,
        transmissionLow = 0.4f,
    };

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

[MemoryPackable]
public partial struct GameplayVariablesStruct
{
    public float LeanSpeed;
    public float AimSpeedPenaltyReduction;

    public float PistolADSMotionScale;
    public float PistolDisplacementStrScale;
    public float PistolZoomBoostScale;

    public float RifleADSMotionScale;
    public float RifleDisplacementStrScale;

    public float transmissionHigh;
    public float transmissionMid;
    public float transmissionLow;
}