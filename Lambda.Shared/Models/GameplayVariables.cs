using MemoryPack;
using UnityEngine;

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

    // Molotov
    public int MaxNodes;
    public float SpreadRadius;
    public float FireRadius;
    public float TimeBetweenNodes;
    public float MaxStepHeight;
    public float MaxDropHeight;

    // public double KillTradeWindow;
}

[MemoryPackable]
public partial struct FireNode
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Radius;
    public float TimeOffset;
}
